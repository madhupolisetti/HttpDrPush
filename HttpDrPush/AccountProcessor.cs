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
        private int accountId = 0;
        //private short accountType = 1;
        private Queue<PushRequest> outboundPushRequestsQueue = new Queue<PushRequest>();
        private Queue<PushRequest> inboundPushRequestsQueue = new Queue<PushRequest>();
        private List<PushProcessor> outboundPushProcessors = null;
        private List<PushProcessor> inboundPushProcessors = null;
        private Mutex concurrencyMutex = new Mutex();        
        private OutboundConfig outboundConfig = null;
        private InboundConfig inboundConfig = null;
        private byte lastProcessorIndexOutbound = 0;
        private byte lastProcessorIndexInbound = 0;
        private long lastProcessedTimeOutbound = 0;
        private long lastProcessedTimeInbound = 0;
        private AccountPendingRequests accountPendingRequests = null;

        private SqlConnection sqlCon = null;
        private SqlCommand configSqlCmd = null;
        private SqlCommand updateSqlCmd = null;
        public AccountProcessor(int id) {
            this.accountId = id;
            sqlCon = new SqlConnection(SharedClass.ConnectionString);
            updateSqlCmd = new SqlCommand("DR_Update_PushRequest", sqlCon);
            updateSqlCmd.CommandType = CommandType.StoredProcedure;
            this.SetConfig();
        }
        public void IncreaseConcurrency(Direction direction)
        {
            while (!this.concurrencyMutex.WaitOne())
                Thread.Sleep(10);
            switch (direction)
            { 
                case Direction.OUTBOUND:
                    ++this.outboundConfig.CurrentConnections;
                    break;
                case Direction.INBOUND:
                    ++this.inboundConfig.CurrentConnections;
                    break;
                default:
                    break;
            }
            this.concurrencyMutex.ReleaseMutex();
        }
        public void DecreaseConcurrency(Direction direction)
        {
            while (!this.concurrencyMutex.WaitOne())
                Thread.Sleep(10);
            switch (direction)
            {
                case Direction.OUTBOUND:
                    --this.outboundConfig.CurrentConnections;
                    break;
                case Direction.INBOUND:
                    --this.inboundConfig.CurrentConnections;
                    break;
                default:
                    break;
            }
            this.concurrencyMutex.ReleaseMutex();
        }
        public void AddPendingRequest( PushRequest pushRequest, Direction direction)
        {   
            if (accountPendingRequests == null)
            {
                accountPendingRequests = new AccountPendingRequests();
                accountPendingRequests.AccountId = this.accountId;
            }
            lock (accountPendingRequests)
            {
                if (direction == Direction.OUTBOUND)
                    accountPendingRequests.OutboundRequests.Add(pushRequest);
                else
                    accountPendingRequests.InboundRequests.Add(pushRequest);
            }
        }
        public void Start()
        {   
            SharedClass.Logger.Info("Started");
            this.lastProcessedTimeInbound = DateTime.Now.ToUnixTimeStamp();
            this.lastProcessedTimeOutbound = DateTime.Now.ToUnixTimeStamp();
            SharedClass.AddAccountProcessor(this.accountId, this);            
        }
        public void Stop()
        {   
            SharedClass.Logger.Info("Stopping AccountId : " + this.accountId + " Processor");
            if (this.outboundPushProcessors != null)
            {
                for (byte i = 0; i < this.outboundPushProcessors.Count; i++)
                {
                    this.outboundPushProcessors[i].Stop();
                }
            }
            if (this.inboundPushProcessors != null)
            {
                for (byte i = 0; i < this.inboundPushProcessors.Count; i++)
                {
                    this.inboundPushProcessors[i].Stop();
                }
            }
            if (this.outboundConfig != null)
            {
                while (this.outboundConfig.CurrentConnections > 0)
                {
                    System.Threading.Thread.Sleep(2000);
                    SharedClass.Logger.Info("Still " + this.outboundConfig.CurrentConnections + " Active Outbound PushProcessors");
                }
            }
            if (this.inboundConfig != null)
            {
                while (this.inboundConfig.CurrentConnections > 0)
                {
                    System.Threading.Thread.Sleep(2000);
                    SharedClass.Logger.Info("Still " + this.inboundConfig.CurrentConnections + " Active Inbound PushProcessors");
                }
            }
            if (accountPendingRequests != null)
                SharedClass.AddAccountPendingRequests(accountPendingRequests);
            SharedClass.ReleaseAccountProcessor(this.accountId);
        }        
        public void EnQueue(PushRequest pushRequest, Direction direction)
        {
            SharedClass.Logger.Info("EnQueuing PushId : " + pushRequest.Id.ToString() + ", Direction : " + direction.ToString());
            switch (direction)
            { 
                case Direction.OUTBOUND:
                    if (this.outboundConfig == null)
                    {
                        throw new InvalidOperationException("Outbound Config not set for account " + this.accountId.ToString());                        
                    }
                    if (this.outboundPushProcessors == null || this.outboundPushProcessors.Count == 0)
                        InitPushProcessors(direction);
                    if (lastProcessorIndexOutbound >= this.outboundPushProcessors.Count)
                        lastProcessorIndexOutbound = 0;
                    this.outboundPushProcessors[lastProcessorIndexOutbound].EnQueue(pushRequest);
                    ++lastProcessorIndexOutbound;
                    break;
                case Direction.INBOUND:
                    if (this.inboundConfig == null)
                    {
                        throw new InvalidOperationException("Inbound Config not set for account " + this.accountId.ToString());
                    }
                    if (this.inboundPushProcessors == null && this.inboundPushProcessors.Count == 0)
                        InitPushProcessors(direction);
                    if (lastProcessorIndexInbound >= this.inboundPushProcessors.Count)
                        lastProcessorIndexInbound = 0;
                    this.inboundPushProcessors[lastProcessorIndexInbound].EnQueue(pushRequest);
                    ++lastProcessorIndexInbound;
                    break;
                default:
                    break;
            }
        }
        public void UpdatePushRequest(PushRequest pushRequest, Direction direction)
        {
            try
            {
                updateSqlCmd.Parameters.Clear();
                updateSqlCmd.Parameters.Add("@PushId", SqlDbType.BigInt).Value = pushRequest.Id;
                updateSqlCmd.Parameters.Add("@AttemptsMade", SqlDbType.TinyInt).Value = pushRequest.AttemptsMade;
                updateSqlCmd.Parameters.Add("@TimeTaken", SqlDbType.Int).Value = pushRequest.TimeTaken;
                updateSqlCmd.Parameters.Add("@ResponseStatusCode", SqlDbType.Int).Value = pushRequest.ResponseStatusCode;
                if (sqlCon.State != ConnectionState.Open)
                    sqlCon.Open();
                updateSqlCmd.ExecuteNonQuery();
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
                configSqlCmd = new SqlCommand("DR_Get_AccountHttpPushConfig", sqlCon);
                configSqlCmd.CommandType = CommandType.StoredProcedure;
                configSqlCmd.Parameters.Add("@AccountId", SqlDbType.Int).Value = this.accountId;
                da = new SqlDataAdapter(configSqlCmd);
                da.Fill(ds = new DataSet());
                
                if (ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                    this.SetOutboundConfig(ds.Tables[0].Rows[0]);

                //if (ds.Tables.Count > 1 && ds.Tables[1].Rows.Count > 0)
                //    this.SetOutboundConfig(ds.Tables[1].Rows[0]);
            }
            catch (Exception e)
            {
                SharedClass.Logger.Error(e.ToString());
            }
        }
        private void SetOutboundConfig(DataRow outboundConfigRecord)
        {   
            //foreach (DataColumn column in outboundConfigRecord.Table.Columns)
            //{
            //    if (outboundConfigRecord[column.ColumnName].IsDBNull())
            //        SharedClass.Logger.Info(column.ColumnName + " : NULL");
            //    else
            //        SharedClass.Logger.Info(column.ColumnName + " : " + outboundConfigRecord[column.ColumnName]);
            //}
            this.outboundConfig = new OutboundConfig();
            //this.outboundPushProcessors = new List<PushProcessor>();
            this.outboundConfig.Url = outboundConfigRecord["Url"].ToString();
            switch (Convert.ToByte(outboundConfigRecord["HttpMethod"]))
            {
                case 1:
                    this.outboundConfig.HttpMethod = HttpMethod.POST;
                    break;
                case 2:
                    this.outboundConfig.HttpMethod = HttpMethod.GET;
                    break;
                default:
                    break;
            }
            if (!outboundConfigRecord["MaxFailedAttempts"].IsDBNull())
            {   
                this.outboundConfig.MaxFailedAttempts = Convert.ToByte(outboundConfigRecord["MaxFailedAttempts"]);
            }
            if (!outboundConfigRecord["RetryDelayInSeconds"].IsDBNull())
            {
                this.outboundConfig.RetryDelayInSeconds = Convert.ToSByte(outboundConfigRecord["RetryDelayInSeconds"]);
            }
            if (!outboundConfigRecord["RetryStrategy"].IsDBNull())
            {
                this.outboundConfig.RetryStrategy = Convert.ToByte(outboundConfigRecord["RetryStrategy"]);
            }
            if (!outboundConfigRecord["ConcurrentConnections"].IsDBNull())
            {
                this.outboundConfig.ConcurrentConnections = Convert.ToByte(outboundConfigRecord["ConcurrentConnections"]);                
            }
            InitPushProcessors(Direction.OUTBOUND);
            if (!outboundConfigRecord["UUIDParameterName"].IsDBNull())
            {
                this.outboundConfig.UUIDParameterName = outboundConfigRecord["UUIDParameterName"].ToString();
            }
            if (!outboundConfigRecord["MobileNumberParameterName"].IsDBNull())
            {
                this.outboundConfig.MobileNumberParameterName = outboundConfigRecord["MobileNumberParameterName"].ToString();
            }
            if (!outboundConfigRecord["SmsStatusParameterName"].IsDBNull())
            {
                this.outboundConfig.SmsStatusParameterName = outboundConfigRecord["SmsStatusParameterName"].ToString();
            }
            if (!outboundConfigRecord["SmsStatusCodeParameterName"].IsDBNull())
            {
                this.outboundConfig.SmsStatusCodeParameterName = outboundConfigRecord["SmsStatusCodeParameterName"].ToString();
            }
            if (!outboundConfigRecord["SmsStatusTimeParameterName"].IsDBNull())
            {
                this.outboundConfig.SmsStatusTimeParameterName = outboundConfigRecord["SmsStatusTimeParameterName"].ToString();
            }
            if (!outboundConfigRecord["TextParameterName"].IsDBNull())
            {
                this.outboundConfig.TextParameterName = outboundConfigRecord["TextParameterName"].ToString();
            }
            if (!outboundConfigRecord["SenderNameParameterName"].IsDBNull())
            {
                this.outboundConfig.SenderNameParameterName = outboundConfigRecord["SenderNameParameterName"].ToString();
            }
            if (!outboundConfigRecord["CostParameterName"].IsDBNull())
            {
                this.outboundConfig.CostParameterName = outboundConfigRecord["CostParameterName"].ToString();
            }
            if (!outboundConfigRecord["RequestHeaders"].IsDBNull())
            {
                // HeaderName1:HeaderValue1_@_HeaderName2:HeaderValue2_@_HeaderName3:HeaderValue3
                foreach (string header in outboundConfigRecord["RequestHeaders"].ToString().Split(new string[] { "_@_" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    this.outboundConfig.RequestHeaders.Add(header.Split(new char[] { ':' }).First().ReplaceWhiteSpaces(), header.Split(new char[] { ':' }).Last().ReplaceWhiteSpaces());
                }

            }
            if (!outboundConfigRecord["DataFormat"].IsDBNull())
            {
                switch (Convert.ToByte(outboundConfigRecord["DataFormat"]))
                {
                    case 2:
                        this.outboundConfig.DataFormat = DataFormat.XML;
                        break;
                    case 3:
                        this.OutboundConfig.DataFormat = DataFormat.PLAIN;
                            break;
                    default:
                        this.outboundConfig.DataFormat = DataFormat.JSON;
                        break;
                }
            }
            if (!outboundConfigRecord["RootElementName"].IsDBNull())
            {
                this.outboundConfig.RootElementName = outboundConfigRecord["RootElementName"].ToString();
            }
        }
        private void InitPushProcessors(Direction direction)
        {
            SharedClass.Logger.Info("Initializing Processors. AccountId : " + this.accountId.ToString() + ", Direction : " + direction.ToString());
            switch (direction)
            {
                case Direction.OUTBOUND:
                    this.outboundPushProcessors = new List<PushProcessor>();
                    for (byte i = 1; i <= this.outboundConfig.ConcurrentConnections; i++)
                    {
                        this.outboundPushProcessors.Add(new PushProcessor(i, direction, this));
                    }
                    this.lastProcessorIndexOutbound = 0;
                    break;
                case Direction.INBOUND:
                    this.inboundPushProcessors = new List<PushProcessor>();
                    for (byte i = 1; i <= this.inboundConfig.ConcurrentConnections; i++)
                    {
                        this.inboundPushProcessors.Add(new PushProcessor(i, direction, this));
                    }
                    this.lastProcessorIndexInbound = 0;
                    break;
                default:
                    break;
            }
            if (this.outboundPushProcessors != null && this.outboundPushProcessors.Count > 0)
            {
                foreach (PushProcessor pushProcessor in this.outboundPushProcessors)
                {
                    Thread pushThread = new Thread(new ThreadStart(pushProcessor.Start));
                    pushThread.Name = "Account_" + this.accountId.ToString() + "_Push_" + pushProcessor.Id.ToString() + "_O";
                    pushThread.Start();
                }
            }
            if (this.inboundPushProcessors != null && this.inboundPushProcessors.Count > 0)
            {
                foreach (PushProcessor pushProcessor in this.inboundPushProcessors)
                {
                    Thread pushThread = new Thread(new ThreadStart(pushProcessor.Start));
                    pushThread.Name = "Account_" + this.accountId.ToString() + "_Push_" + pushProcessor.Id.ToString() + "_I";
                    pushThread.Start();
                }
            }
        }
        #region PROPERTIES
        public int AccountId { get { return accountId; } }
        public long LastProcessedTimeInbound { get { return this.lastProcessorIndexInbound; } set { this.lastProcessedTimeInbound = value; } }
        public long LastProcessedTimeOutbound { get { return this.lastProcessorIndexOutbound; } set { this.lastProcessedTimeOutbound = value; } }
        public OutboundConfig OutboundConfig { get { return outboundConfig; } }
        public InboundConfig InboundConfig { get { return inboundConfig; } }
        public bool IsNecessary
        {
            get
            {
                bool isNecessary = false;
                if (this.outboundPushProcessors != null && this.outboundPushProcessors.Count > 0)
                {
                    foreach (PushProcessor pushProcessor in this.outboundPushProcessors)
                    {
                        if (pushProcessor.QueueCount() > 0 || pushProcessor.IsRunning)
                        {
                            isNecessary = true;
                            break;
                        }
                    }
                }
                //if (!isNecessary)
                //{
                //    for (byte i = 0; i < this.outboundPushProcessors.Count; i++)
                //    {
                //        this.outboundPushProcessors[i].Stop();
                //    }
                //}
                if (!isNecessary) {
                    if (this.inboundPushProcessors != null && this.inboundPushProcessors.Count > 0)
                    {
                        foreach (PushProcessor pushProcessor in this.inboundPushProcessors)
                        {
                            if (pushProcessor.QueueCount() > 0)
                            {
                                isNecessary = true;
                                break;
                            }
                        }
                    }
                }
                //if (!isNecessary)
                //{
                //    for (byte i = 0; i < this.inboundPushProcessors.Count; i++)
                //    {
                //        this.inboundPushProcessors[i].Stop();
                //    }
                //}
                return isNecessary ? true : ((DateTime.Now.ToUnixTimeStamp() - this.lastProcessedTimeOutbound) > SharedClass.MaxInactivity ? false : true);
            }
        }
        #endregion
    }
}
