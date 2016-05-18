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
        private short accountType = 1;
        private Queue<PushRequest> outboundPushRequestsQueue = new Queue<PushRequest>();
        private Queue<PushRequest> inboundPushRequestsQueue = new Queue<PushRequest>();
        private Mutex outboundQueueMutex = null;
        private Mutex inboundQueueMutex = null;
        private Mutex concurrencyMutex = new Mutex();
        private bool shouldIProcess = false;
        private OutboundConfig outboundConfig = null;
        private InboundConfig inboundConfig = null;
        public AccountProcessor(long id) {
            this.accountId = id;
            this.SetConfig();
        }
        private void SetConfig() {
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
                if (ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0) {
                    this.SetOutboundConfig(ds.Tables[0].Rows[0]);
                }
            }
            catch (Exception e) {
                SharedClass.Logger.Error(e.ToString());
            }
        }
        private void SetOutboundConfig(DataRow outboundConfigRecord)
        {
            this.outboundQueueMutex = new Mutex();
            this.outboundConfig = new OutboundConfig();
            this.outboundConfig.Url = outboundConfigRecord["Url"].ToString();
            this.outboundConfig.HttpMethod = Convert.ToByte(outboundConfigRecord["HttpMethod"]);
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
        public void Start()
        {
            SharedClass.Logger.Info("Started");            

        }
        private void StartOutboundPush()
        {
            while (!this.concurrencyMutex.WaitOne())
            {
                Thread.Sleep(10);
            }
            ++this.outboundConfig.CurrentConnections;
            this.concurrencyMutex.ReleaseMutex();
            PushRequest pushRequest = null;
            while (this.shouldIProcess && !SharedClass.HasStopSignal && this.QueueCount(Direction.OUTBOUND) > 0)
            {
                pushRequest = this.DeQueue(Direction.OUTBOUND);                
                if (pushRequest != null)
                {

                }
            }
            Thread.Sleep(2000);
            //if (this.outboundConfig.CurrentConnections == 0 && this.QueueCount(Direction.OUTBOUND) == 0)
            //{
            //    break;
            //}
            while (!this.concurrencyMutex.WaitOne())
            {
                Thread.Sleep(10);
            }
            --this.outboundConfig.CurrentConnections;
            this.concurrencyMutex.ReleaseMutex();
        }
        public void Stop()
        {
            SharedClass.Logger.Info("Stopping AccountId : " + this.accountId + " Processor");
            PushRequest pushRequest = null;
            while (this.QueueCount(Direction.OUTBOUND) > 0)
            {
                pushRequest = this.DeQueue(Direction.OUTBOUND);
                if (pushRequest != null)
                    pushRequest.ReEnQueue();
            }
            while (this.QueueCount(Direction.INBOUND) > 0)
            {
                pushRequest = this.DeQueue(Direction.INBOUND);
                if (pushRequest != null)
                    pushRequest.ReEnQueue();
            }
        }
        public void EnQueue(PushRequest pushRequest, Direction direction) {
            switch (direction)
            { 
                case Direction.OUTBOUND:
                    try
                    {
                        while (!this.outboundQueueMutex.WaitOne())
                        {
                            Thread.Sleep(10);
                        }
                        this.outboundPushRequestsQueue.Enqueue(pushRequest);                        
                    }
                    catch (Exception e)
                    {
                        SharedClass.Logger.Error("Exception while enqueuing PushRequest : " + pushRequest.Id + ", Reason : " + e.ToString());
                    }
                    finally
                    {
                        this.outboundQueueMutex.ReleaseMutex();
                    }
                    break;
                case Direction.INBOUND:
                    try
                    {
                        while (!this.inboundQueueMutex.WaitOne())
                        {
                            Thread.Sleep(10);
                        }                        
                        this.inboundPushRequestsQueue.Enqueue(pushRequest);
                    }
                    catch (Exception e)
                    {
                        SharedClass.Logger.Error("Exception while enqueuing PushRequest : " + pushRequest.Id + ", Reason : " + e.ToString());
                    }
                    finally
                    {
                        this.inboundQueueMutex.ReleaseMutex();
                    }
                    break;
                default:
                    break;
            }
        }
        public PushRequest DeQueue(Direction direction)
        {
            PushRequest pushRequest = null;
            switch (direction)
            { 
                case Direction.OUTBOUND:
                    try
                    {
                        while (!this.outboundQueueMutex.WaitOne())
                        {
                            Thread.Sleep(10);
                        }
                        pushRequest = this.outboundPushRequestsQueue.Dequeue();
                    }
                    catch (Exception e)
                    {
                        SharedClass.Logger.Error("Exception while dequeuing PushRequest, direction : " + direction + ", Reason : " + e.ToString());
                    }
                    finally
                    {
                        this.outboundQueueMutex.ReleaseMutex();
                    }
                    break;
                case Direction.INBOUND:
                    try
                    {
                        while (!this.inboundQueueMutex.WaitOne())
                        {
                            Thread.Sleep(10);
                        }
                        pushRequest = this.inboundPushRequestsQueue.Dequeue();
                    }
                    catch (Exception e)
                    {
                        SharedClass.Logger.Error("Exception while dequeuing PushRequest, direction : " + direction + ", Reason : " + e.ToString());
                    }
                    finally
                    {
                        this.inboundQueueMutex.ReleaseMutex();
                    }
                    break;
                default:
                    break;
            }
            return pushRequest;
        }
        public int QueueCount(Direction direction)
        {
            int queueCount = 0;
            switch (direction)
            { 
                case Direction.OUTBOUND:
                    try
                    {
                        while (!this.outboundQueueMutex.WaitOne())
                        {
                            Thread.Sleep(10);
                        }
                        queueCount = this.outboundPushRequestsQueue.Count();
                    }
                    catch (Exception e)
                    {
                        SharedClass.Logger.Error("Exception while getting queue count, direction : " + direction + ", Reason : " + e.ToString());
                    }
                    finally
                    {
                        this.outboundQueueMutex.ReleaseMutex();
                    }
                    break;
                case Direction.INBOUND:
                    try
                    {
                        while (!this.inboundQueueMutex.WaitOne())
                        {
                            Thread.Sleep(10);
                        }
                        queueCount = this.inboundPushRequestsQueue.Count();
                    }
                    catch (Exception e)
                    {
                        SharedClass.Logger.Error("Exception while getting queue count, direction : " + direction + ", Reason : " + e.ToString());
                    }
                    finally
                    {
                        this.inboundQueueMutex.ReleaseMutex();
                    }
                    break;
                default:
                    break;
            }
            return queueCount;
        }
        #region "PROPERTIES"
        public long AccountId { get { return accountId; } }        
        #endregion
    }
}
