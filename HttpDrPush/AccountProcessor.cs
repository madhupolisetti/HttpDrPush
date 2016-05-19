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
        private long accountId = 0;
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
        public AccountProcessor(long id) {
            this.accountId = id;
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
        public void Start()
        {   
            SharedClass.Logger.Info("Started");
            SharedClass.AddAccountProcessor(this.accountId, this);
            if (this.outboundPushProcessors != null && this.outboundPushProcessors.Count > 0)
            {
                foreach (PushProcessor pushProcessor in this.outboundPushProcessors)
                {
                    Thread pushThread = new Thread(new ThreadStart(pushProcessor.Start));
                    pushThread.Name = "Push_O_" + this.accountId.ToString() + "_" + pushProcessor.Id;
                    pushThread.Start();
                }
            }
            if (this.inboundPushProcessors != null && this.inboundPushProcessors.Count > 0)
            {
                foreach (PushProcessor pushProcessor in this.inboundPushProcessors)
                {
                    Thread pushThread = new Thread(new ThreadStart(pushProcessor.Start));
                    pushThread.Name = "Push_I_" + this.accountId.ToString() + "_" + pushProcessor.Id;
                    pushThread.Start();
                }
            }
        }
        public void Stop()
        {   
            SharedClass.Logger.Info("Stopping AccountId : " + this.accountId + " Processor");
            for (byte i = 0; i < this.outboundPushProcessors.Count; i++)
            {
                while (this.outboundPushProcessors[i].QueueCount() > 0)
                    this.outboundPushProcessors[i].DeQueue().ReEnQueue();
                
            }
            for (byte i = 0; i < this.inboundPushProcessors.Count; i++)
            {
                while (this.inboundPushProcessors[i].QueueCount() > 0)
                    this.inboundPushProcessors[i].DeQueue().ReEnQueue();
            }
            while (this.outboundConfig.CurrentConnections > 0)
            {
                System.Threading.Thread.Sleep(2000);
                SharedClass.Logger.Info("Still " + this.outboundConfig.CurrentConnections + " Active Outbound PushProcessors");
            }
            while (this.inboundConfig.CurrentConnections > 0)
            {
                System.Threading.Thread.Sleep(2000);
                SharedClass.Logger.Info("Still " + this.inboundConfig.CurrentConnections + " Active Inbound PushProcessors");
            }
        }        
        public void EnQueue(PushRequest pushRequest, Direction direction)
        {
            switch (direction)
            { 
                case Direction.OUTBOUND:
                    if (this.outboundPushProcessors.Count == 0)
                        InitPushProcessors(direction);
                    if (lastProcessorIndexOutbound >= this.outboundPushProcessors.Count)
                        lastProcessorIndexOutbound = 0;
                    this.outboundPushProcessors[lastProcessorIndexOutbound].EnQueue(pushRequest);
                    ++lastProcessorIndexOutbound;
                    break;
                case Direction.INBOUND:
                    if (this.inboundPushProcessors.Count == 0)
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
        private void SetConfig()
        {
            SqlConnection sqlCon = null;
            SqlCommand sqlCmd = null;
            SqlDataAdapter da = null;
            DataSet ds = null;
            try
            {
                sqlCon = new SqlConnection(SharedClass.ConnectionString);
                sqlCmd = new SqlCommand("DR_Get_AccountHttpPushConfig", sqlCon);
                sqlCmd.Parameters.Add("@AccountId", SqlDbType.BigInt).Value = this.accountId;
                da = new SqlDataAdapter();
                da.Fill(ds = new DataSet());
                
                if (ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                    this.SetOutboundConfig(ds.Tables[0].Rows[0]);

                if (ds.Tables.Count > 1 && ds.Tables[1].Rows.Count > 0)
                    this.SetOutboundConfig(ds.Tables[1].Rows[0]);
            }
            catch (Exception e)
            {
                SharedClass.Logger.Error(e.ToString());
            }
        }
        private void SetOutboundConfig(DataRow outboundConfigRecord)
        {   
            this.outboundConfig = new OutboundConfig();
            this.outboundPushProcessors = new List<PushProcessor>();
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
                InitPushProcessors(Direction.OUTBOUND);
            }
            if (!outboundConfigRecord["UUIDParameter"].IsDBNull())
            {
                this.outboundConfig.UUIDParameterName = outboundConfigRecord["UUIDParameterName"].ToString();
            }
            if (!outboundConfigRecord["MobileNumberParameter"].IsDBNull())
            {
                this.outboundConfig.MobileNumberParameterName = outboundConfigRecord["MobileNumberParameter"].ToString();
            }
            if (!outboundConfigRecord["SmsStatusParameter"].IsDBNull())
            {
                this.outboundConfig.SmsStatusParameterName = outboundConfigRecord["SmsStatusParameter"].ToString();
            }
            if (!outboundConfigRecord["SmsStatusCodeParameter"].IsDBNull())
            {
                this.outboundConfig.SmsStatusCodeParameterName = outboundConfigRecord["SmsStatusCodeParameter"].ToString();
            }
            if (!outboundConfigRecord["SmsStatusTimeParameter"].IsDBNull())
            {
                this.outboundConfig.SmsStatusTimeParameterName = outboundConfigRecord["SmsStatusTimeParameter"].ToString();
            }
            if (!outboundConfigRecord["TextParameter"].IsDBNull())
            {
                this.outboundConfig.TextParameterName = outboundConfigRecord["TextParameter"].ToString();
            }
            if (!outboundConfigRecord["SenderNameParameter"].IsDBNull())
            {
                this.outboundConfig.SenderNameParameterName = outboundConfigRecord["SenderNameParameter"].ToString();
            }
            if (!outboundConfigRecord["CostParameter"].IsDBNull())
            {
                this.outboundConfig.CostParameterName = outboundConfigRecord["CostParameter"].ToString();
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
        }
        #region PROPERTIES
        public long AccountId { get { return accountId; } }
        public long LastProcessedTimeInbound { get { return this.lastProcessorIndexInbound; } set { this.lastProcessedTimeInbound = value; } }
        public long LastProcessedTimeOutbound { get { return this.lastProcessorIndexOutbound; } set { this.lastProcessedTimeOutbound = value; } }
        public OutboundConfig OutboundConfig { get { return outboundConfig; } }
        public InboundConfig InboundConfig { get { return inboundConfig; } }
        public bool IsNecessary
        {
            get
            {
                bool isNecessary = false;
                foreach (PushProcessor pushProcessor in this.outboundPushProcessors)
                {
                    if (pushProcessor.QueueCount() > 0)
                    {
                        isNecessary = true;
                        break;
                    }
                }
                if (!isNecessary)
                {
                    for (byte i = 0; i < this.outboundPushProcessors.Count; i++)
                    {
                        this.outboundPushProcessors[i].Stop();
                    }
                }
                if (!isNecessary) {
                    foreach (PushProcessor pushProcessor in this.inboundPushProcessors)
                    {
                        if (pushProcessor.QueueCount() > 0)
                        {
                            isNecessary = true;
                            break;
                        }
                    }
                }
                if (!isNecessary)
                {
                    for (byte i = 0; i < this.inboundPushProcessors.Count; i++)
                    {
                        this.inboundPushProcessors[i].Stop();
                    }
                }
                return isNecessary ? true : ((DateTime.Now.ToUnixTimeStamp() - this.lastProcessedTimeOutbound) > SharedClass.MaxInactivity ? true : false);
            }
        }
        #endregion
    }
}
