using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Data;
using System.Data.SqlClient;
using ExtensionMethods;

namespace HttpDrPush
{
    public class AccountProcessor
    {
        #region PRIVATE_VARIABLES
        private int _accountId = 0;
        private Queue<PushRequest> _outboundPushRequestsQueue = new Queue<PushRequest>();
        private Queue<PushRequest> _inboundPushRequestsQueue = new Queue<PushRequest>();
        private List<PushProcessor> _outboundPushProcessors = null;
        private List<PushProcessor> _inboundPushProcessors = null;
        private Mutex _concurrencyMutex = new Mutex();
        private OutboundConfig _outboundConfig = null;
        private InboundConfig _inboundConfig = null;
        private byte _lastProcessorIndexOutbound = 0;
        private byte _lastProcessorIndexInbound = 0;
        private long _lastProcessedTimeOutbound = 0;
        private long _lastProcessedTimeInbound = 0;
        private PendingPushRequests _accountPendingRequests = null;

        private SqlConnection _sqlCon = null;
        private SqlCommand _configSqlCmd = null;
        private SqlCommand _updateSqlCmd = null;
        #endregion        
        public AccountProcessor(int accountId) {
            this._accountId = accountId;
            _sqlCon = new SqlConnection(SharedClass.ConnectionString);
            _updateSqlCmd = new SqlCommand("DR_Update_PushRequest", _sqlCon);
            _updateSqlCmd.CommandType = CommandType.StoredProcedure;
            this.SetConfig();
        }
        public void IncreaseConcurrency(Direction direction)
        {
            while (!this._concurrencyMutex.WaitOne())
                Thread.Sleep(10);
            switch (direction)
            { 
                case Direction.OUTBOUND:
                    ++this._outboundConfig.CurrentConnections;
                    break;
                case Direction.INBOUND:
                    ++this._inboundConfig.CurrentConnections;
                    break;
                default:
                    break;
            }
            this._concurrencyMutex.ReleaseMutex();
        }
        public void DecreaseConcurrency(Direction direction)
        {
            while (!this._concurrencyMutex.WaitOne())
                Thread.Sleep(10);
            switch (direction)
            {
                case Direction.OUTBOUND:
                    --this._outboundConfig.CurrentConnections;
                    break;
                case Direction.INBOUND:
                    --this._inboundConfig.CurrentConnections;
                    break;
                default:
                    break;
            }
            this._concurrencyMutex.ReleaseMutex();
        }
        public void AddPendingRequest( PushRequest pushRequest, Direction direction)
        {   
            if (_accountPendingRequests == null)
            {
                _accountPendingRequests = new PendingPushRequests();
                _accountPendingRequests.AccountId = this._accountId;
            }
            lock (_accountPendingRequests)
            {
                if (direction == Direction.OUTBOUND)
                    _accountPendingRequests.OutboundRequests.Add(pushRequest);
                else
                    _accountPendingRequests.InboundRequests.Add(pushRequest);
            }
        }
        public void Start()
        {   
            SharedClass.Logger.Info("Started");
            this._lastProcessedTimeInbound = DateTime.Now.ToUnixTimeStamp();
            this._lastProcessedTimeOutbound = DateTime.Now.ToUnixTimeStamp();
            SharedClass.AddAccountProcessor(this._accountId, this);            
        }
        public void Stop()
        {   
            SharedClass.Logger.Info("Stopping AccountId : " + this._accountId + " Processor");
            if (this._outboundPushProcessors != null)
            {
                for (byte i = 0; i < this._outboundPushProcessors.Count; i++)
                {
                    this._outboundPushProcessors[i].Stop();
                }
            }
            if (this._inboundPushProcessors != null)
            {
                for (byte i = 0; i < this._inboundPushProcessors.Count; i++)
                {
                    this._inboundPushProcessors[i].Stop();
                }
            }
            if (this._outboundConfig != null)
            {
                while (this._outboundConfig.CurrentConnections > 0)
                {
                    System.Threading.Thread.Sleep(2000);
                    SharedClass.Logger.Info("Still " + this._outboundConfig.CurrentConnections + " Active Outbound PushProcessors");
                }
            }
            if (this._inboundConfig != null)
            {
                while (this._inboundConfig.CurrentConnections > 0)
                {
                    System.Threading.Thread.Sleep(2000);
                    SharedClass.Logger.Info("Still " + this._inboundConfig.CurrentConnections + " Active Inbound PushProcessors");
                }
            }
            if (_accountPendingRequests != null)
                SharedClass.AddAccountPendingRequests(_accountPendingRequests);
            SharedClass.ReleaseAccountProcessor(this._accountId);
        }        
        public void EnQueue(PushRequest pushRequest, Direction direction)
        {
            SharedClass.Logger.Info("EnQueuing PushId : " + pushRequest.Id.ToString() + ", Direction : " + direction.ToString());
            switch (direction)
            { 
                case Direction.OUTBOUND:
                    if (this._outboundConfig == null)
                    {
                        throw new InvalidOperationException("Outbound Config not set for account " + this._accountId.ToString());                        
                    }
                    if (this._outboundPushProcessors == null || this._outboundPushProcessors.Count == 0)
                        InitPushProcessors(direction);
                    if (_lastProcessorIndexOutbound >= this._outboundPushProcessors.Count)
                        _lastProcessorIndexOutbound = 0;
                    this._outboundPushProcessors[_lastProcessorIndexOutbound].EnQueue(pushRequest);
                    ++_lastProcessorIndexOutbound;
                    break;
                case Direction.INBOUND:
                    if (this._inboundConfig == null)
                    {
                        throw new InvalidOperationException("Inbound Config not set for account " + this._accountId.ToString());
                    }
                    if (this._inboundPushProcessors == null && this._inboundPushProcessors.Count == 0)
                        InitPushProcessors(direction);
                    if (_lastProcessorIndexInbound >= this._inboundPushProcessors.Count)
                        _lastProcessorIndexInbound = 0;
                    this._inboundPushProcessors[_lastProcessorIndexInbound].EnQueue(pushRequest);
                    ++_lastProcessorIndexInbound;
                    break;
                default:
                    break;
            }
        }
        public void UpdatePushRequest(PushRequest pushRequest, Direction direction)
        {
            try
            {
                _updateSqlCmd.Parameters.Clear();
                _updateSqlCmd.Parameters.Add("@PushId", SqlDbType.BigInt).Value = pushRequest.Id;
                _updateSqlCmd.Parameters.Add("@AttemptsMade", SqlDbType.TinyInt).Value = pushRequest.AttemptsMade;
                _updateSqlCmd.Parameters.Add("@TimeTaken", SqlDbType.Int).Value = pushRequest.TimeTaken;
                _updateSqlCmd.Parameters.Add("@ResponseStatusCode", SqlDbType.Int).Value = pushRequest.ResponseStatusCode;
                if (_sqlCon.State != ConnectionState.Open)
                    _sqlCon.Open();
                _updateSqlCmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                SharedClass.Logger.Error("Error Updating PushId : " + pushRequest.Id + ", Reason : " + e.ToString());
            }
        }
        private void SetConfig()
        {   
            SqlDataAdapter da = null;
            DataSet ds = null;
            try
            {   
                _configSqlCmd = new SqlCommand("DR_Get_AccountHttpPushConfig", _sqlCon);
                _configSqlCmd.CommandType = CommandType.StoredProcedure;
                _configSqlCmd.Parameters.Add("@AccountId", SqlDbType.Int).Value = this._accountId;
                da = new SqlDataAdapter(_configSqlCmd);
                da.Fill(ds = new DataSet());

                if (ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                    this.SetOutboundConfig(ds.Tables[0].Rows[0]);
                else
                    this.SetOutboundConfig(null);
            }
            catch (Exception e)
            {
                SharedClass.Logger.Error(e.ToString());
            }
        }
        private void SetOutboundConfig(DataRow outboundConfigRecord)
        {   
            this._outboundConfig = new OutboundConfig();
            this._outboundConfig.HttpMethod = HttpMethod.POST;
            if (outboundConfigRecord != null)
            {
                if (!outboundConfigRecord["Url"].IsDBNull() && outboundConfigRecord["Url"].ToString() != string.Empty)
                    this._outboundConfig.Url = outboundConfigRecord["Url"].ToString();
                switch (Convert.ToByte(outboundConfigRecord["HttpMethod"]))
                {
                    case 1:
                        this._outboundConfig.HttpMethod = HttpMethod.POST;
                        break;
                    case 2:
                        this._outboundConfig.HttpMethod = HttpMethod.GET;
                        break;
                    default:
                        break;
                }
                if (!outboundConfigRecord["MaxFailedAttempts"].IsDBNull())
                {
                    byte tempValue = this._outboundConfig.MaxFailedAttempts;
                    byte.TryParse(outboundConfigRecord["MaxFailedAttemts"].ToString(), out tempValue);
                    this._outboundConfig.MaxFailedAttempts = tempValue;
                }
                if (!outboundConfigRecord["RetryDelayInSeconds"].IsDBNull())
                {
                    byte tempValue = this._outboundConfig.RetryDelayInSeconds;
                    byte.TryParse(outboundConfigRecord["RetryDelayInSeconds"].ToString(), out tempValue);
                    this._outboundConfig.RetryDelayInSeconds = tempValue;
                }
                if (!outboundConfigRecord["RetryStrategy"].IsDBNull())
                {
                    byte tempValue = this._outboundConfig.RetryStrategy;
                    byte.TryParse(outboundConfigRecord["RetryStrategy"].ToString(), out tempValue);
                    this._outboundConfig.RetryStrategy = tempValue;
                }
                if (!outboundConfigRecord["ConcurrentConnections"].IsDBNull())
                {
                    byte tempValue = this._outboundConfig.ConcurrentConnections;
                    byte.TryParse(outboundConfigRecord["ConcurrentConnections"].ToString(), out tempValue);
                    this._outboundConfig.ConcurrentConnections = tempValue;
                }
                if (!outboundConfigRecord["UUIDParameterName"].IsDBNull())
                {
                    this._outboundConfig.UUIDParameterName = outboundConfigRecord["UUIDParameterName"].ToString();
                }
                if (!outboundConfigRecord["MobileNumberParameterName"].IsDBNull())
                {
                    this._outboundConfig.MobileNumberParameterName = outboundConfigRecord["MobileNumberParameterName"].ToString();
                }
                if (!outboundConfigRecord["SmsStatusParameterName"].IsDBNull())
                {
                    this._outboundConfig.SmsStatusParameterName = outboundConfigRecord["SmsStatusParameterName"].ToString();
                }
                if (!outboundConfigRecord["SmsStatusCodeParameterName"].IsDBNull())
                {
                    this._outboundConfig.SmsStatusCodeParameterName = outboundConfigRecord["SmsStatusCodeParameterName"].ToString();
                }
                if (!outboundConfigRecord["SmsStatusTimeParameterName"].IsDBNull())
                {
                    this._outboundConfig.SmsStatusTimeParameterName = outboundConfigRecord["SmsStatusTimeParameterName"].ToString();
                }
                if (!outboundConfigRecord["TextParameterName"].IsDBNull())
                {
                    this._outboundConfig.TextParameterName = outboundConfigRecord["TextParameterName"].ToString();
                }
                if (!outboundConfigRecord["SenderNameParameterName"].IsDBNull())
                {
                    this._outboundConfig.SenderNameParameterName = outboundConfigRecord["SenderNameParameterName"].ToString();
                }
                if (!outboundConfigRecord["CostParameterName"].IsDBNull())
                {
                    this._outboundConfig.CostParameterName = outboundConfigRecord["CostParameterName"].ToString();
                }
                if (!outboundConfigRecord["RequestHeaders"].IsDBNull())
                {
                    // HeaderName1:HeaderValue1_@_HeaderName2:HeaderValue2_@_HeaderName3:HeaderValue3
                    foreach (string header in outboundConfigRecord["RequestHeaders"].ToString().Split(new string[] { "_@_" }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        this._outboundConfig.RequestHeaders.Add(header.Split(new char[] { ':' }).First().ReplaceWhiteSpaces(), header.Split(new char[] { ':' }).Last().ReplaceWhiteSpaces());
                    }

                }
                if (!outboundConfigRecord["DataFormat"].IsDBNull())
                {
                    switch (Convert.ToByte(outboundConfigRecord["DataFormat"]))
                    {
                        case 2:
                            this._outboundConfig.DataFormat = PayloadFormat.XML;
                            break;
                        case 3:
                            this.OutboundConfig.DataFormat = PayloadFormat.PLAIN;
                            break;
                        default:
                            this._outboundConfig.DataFormat = PayloadFormat.JSON;
                            break;
                    }
                }
                if (!outboundConfigRecord["RootElementName"].IsDBNull())
                {
                    this._outboundConfig.RootElementName = outboundConfigRecord["RootElementName"].ToString();
                }
            }            
            InitPushProcessors(Direction.OUTBOUND);            
        }
        private void InitPushProcessors(Direction direction)
        {
            SharedClass.Logger.Info("Initializing Processors. AccountId : " + this._accountId.ToString() + ", Direction : " + direction.ToString());
            switch (direction)
            {
                case Direction.OUTBOUND:
                    this._outboundPushProcessors = new List<PushProcessor>();
                    for (byte i = 1; i <= this._outboundConfig.ConcurrentConnections; i++)
                    {
                        this._outboundPushProcessors.Add(new PushProcessor(i, direction, this));
                    }
                    this._lastProcessorIndexOutbound = 0;
                    break;
                case Direction.INBOUND:
                    this._inboundPushProcessors = new List<PushProcessor>();
                    for (byte i = 1; i <= this._inboundConfig.ConcurrentConnections; i++)
                    {
                        this._inboundPushProcessors.Add(new PushProcessor(i, direction, this));
                    }
                    this._lastProcessorIndexInbound = 0;
                    break;
                default:
                    break;
            }
            if (this._outboundPushProcessors != null && this._outboundPushProcessors.Count > 0)
            {
                foreach (PushProcessor pushProcessor in this._outboundPushProcessors)
                {
                    Thread pushThread = new Thread(new ThreadStart(pushProcessor.Start));
                    pushThread.Name = "Account_" + this._accountId.ToString() + "_Push_" + pushProcessor.Id.ToString() + "_O";
                    pushThread.Start();
                }
            }
            if (this._inboundPushProcessors != null && this._inboundPushProcessors.Count > 0)
            {
                foreach (PushProcessor pushProcessor in this._inboundPushProcessors)
                {
                    Thread pushThread = new Thread(new ThreadStart(pushProcessor.Start));
                    pushThread.Name = "Account_" + this._accountId.ToString() + "_Push_" + pushProcessor.Id.ToString() + "_I";
                    pushThread.Start();
                }
            }
        }
        #region PROPERTIES
        public int AccountId 
        { 
            get 
            { 
                return this._accountId; 
            } 
        }
        public long LastProcessedTimeInbound 
        { 
            get 
            { 
                return this._lastProcessorIndexInbound;
            } 
            set 
            { 
                this._lastProcessedTimeInbound = value; 
            } 
        }
        public long LastProcessedTimeOutbound 
        { 
            get 
            { 
                return this._lastProcessorIndexOutbound; 
            } 
            set 
            { 
                this._lastProcessedTimeOutbound = value; 
            } 
        }
        public OutboundConfig OutboundConfig 
        { 
            get 
            {
                return this._outboundConfig;
            }
        }
        public InboundConfig InboundConfig 
        { 
            get 
            {
                return this._inboundConfig;
            }
        }
        public bool IsNecessary
        {
            get
            {
                bool necessary = false;
                if (this._outboundPushProcessors != null && this._outboundPushProcessors.Count > 0)
                {
                    foreach (PushProcessor pushProcessor in this._outboundPushProcessors)
                    {
                        if (pushProcessor.QueueCount() > 0 || pushProcessor.IsRunning)
                        {
                            necessary = true;
                            break;
                        }
                    }
                }                
                if (!necessary) {
                    if (this._inboundPushProcessors != null && this._inboundPushProcessors.Count > 0)
                    {
                        foreach (PushProcessor pushProcessor in this._inboundPushProcessors)
                        {
                            if (pushProcessor.QueueCount() > 0 || pushProcessor.IsRunning)
                            {
                                necessary = true;
                                break;
                            }
                        }
                    }
                }
                if (!necessary)
                    necessary = (DateTime.Now.ToUnixTimeStamp() - this._lastProcessedTimeOutbound) < SharedClass.MaxInactivity;
                if (!necessary)
                    necessary = (DateTime.Now.ToUnixTimeStamp() - this._lastProcessedTimeInbound) < SharedClass.MaxInactivity;                
                return necessary;
            }
        }
        #endregion
    }
}
