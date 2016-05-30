using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;
using ExtensionMethods;

namespace HttpDrPush
{
    class ApplicationController
    {
        private bool isIamPolling = false;
        private long lastDrId = 0;
        public ApplicationController()
        {
            this.LoadConfig();
        }
        public void Start()
        {
            SharedClass.IsServiceCleaned = false;
            this.DeserializePendingRequests();
            System.Threading.Thread pollThread = new System.Threading.Thread(new System.Threading.ThreadStart(StartDbPoll));
            pollThread.Name = "DbPoller";
            pollThread.Start();
            System.Threading.Thread houseKeepingThread = new System.Threading.Thread(new System.Threading.ThreadStart(SharedClass.HouseKeeping));
            houseKeepingThread.Name = "HouseKeeping";
            houseKeepingThread.Start();
        }
        public void Stop()
        {   
            while (this.isIamPolling)
                System.Threading.Thread.Sleep(100);
            SharedClass.StopActiveAccountProcessors();
            while (SharedClass.ActiveAccountProcessorsCount > 0)
            {
                System.Threading.Thread.Sleep(1000);
                SharedClass.Logger.Info("Still " + SharedClass.ActiveAccountProcessorsCount.ToString() + " AccountProcessors are active");
            }
            SharedClass.SerializePendingQueue();
            SharedClass.IsServiceCleaned = true;
        }
        private void StartDbPoll()
        {
            SharedClass.Logger.Info("Started");
            SqlConnection sqlCon = new SqlConnection(SharedClass.ConnectionString);
            SqlCommand sqlCmd = new SqlCommand("DR_Get_PendingPushRequests", sqlCon);
            sqlCmd.CommandType = CommandType.StoredProcedure;
            SqlDataAdapter da = null;
            DataSet ds = null;
            int accountId = 0;
            Direction direction = Direction.OUTBOUND;
            while (!SharedClass.HasStopSignal)
            {
                try
                {
                    isIamPolling = true;
                    sqlCmd.Parameters.Clear();
                    sqlCmd.Parameters.Add("@LastId", SqlDbType.BigInt).Value = this.lastDrId;
                    da = new SqlDataAdapter(sqlCmd);                    
                    da.Fill(ds = new DataSet());
                    if (ds.Tables.Count > 0)
                    {
                        if (ds.Tables[0].Rows.Count > 0)
                        {
                            AccountProcessor accountProcessor = null;                            
                            foreach (DataRow row in ds.Tables[0].Rows)
                            {
                                try
                                { 
                                    PushRequest pushRequest = new PushRequest();
                                this.lastDrId = Convert.ToInt32(row["Id"]);
                                pushRequest.Id = Convert.ToInt64(row["Id"]);
                                accountId = Convert.ToInt32(row["AccountId"]);
                                switch (Convert.ToByte(row["ServiceId"]))
                                {
                                    case 1:
                                        direction = Direction.INBOUND;
                                        break;
                                    case 2:
                                        direction = Direction.OUTBOUND;
                                        break;
                                    default:
                                        direction = Direction.OUTBOUND;
                                        break;
                                }
                                pushRequest.MobileNumber = row["MobileNumber"].ToString();
                                pushRequest.UUID = row["UUID"].ToString();
                                if(!row["Text"].IsDBNull())
                                    pushRequest.Text = row["Text"].ToString();
                                pushRequest.SmsStatusCode = Convert.ToByte(row["SmsStateCode"]);
                                pushRequest.SmsStatusTime = DateTime.Parse(row["SmsStateTime"].ToString()).ToUnixTimeStamp();
                                if (!row["SenderName"].IsDBNull())
                                    pushRequest.SenderName = row["SenderName"].ToString();                                
                                if (!row["Cost"].IsDBNull())
                                    pushRequest.Cost = float.Parse(row["Cost"].ToString());

                                pushRequest.AttemptsMade = Convert.ToByte(row["AttemptsMade"]);
                                SharedClass.GetAccountProcessor(accountId, out accountProcessor);
                                if (accountProcessor == null)
                                {
                                    accountProcessor = new AccountProcessor(accountId);
                                    System.Threading.Thread accountProcessorThread = new System.Threading.Thread(accountProcessor.Start);
                                    accountProcessorThread.Name = "Account_" + accountId.ToString();
                                    accountProcessorThread.Start();
                                }
                                accountProcessor.EnQueue(pushRequest, direction);
                                }
                                catch (Exception e)
                                {
                                    SharedClass.Logger.Error("Exception While Parsing PushRequest In ApplicationPoller, Reason : " + e.ToString());
                                }                                
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    SharedClass.Logger.Error("Exception in PushRequests Polling, Reason : " + e.ToString());
                }
                finally
                {
                    this.isIamPolling = false;
                    System.Threading.Thread.Sleep(5000);
                }
            }
        }        
        private void DeserializePendingRequests()
        {
            try
            {
                if (System.IO.File.Exists(SharedClass.PendingQueueFileName))
                {
                    System.Runtime.Serialization.Formatters.Binary.BinaryFormatter formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    System.IO.Stream stream = new System.IO.FileStream(SharedClass.PendingQueueFileName, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read);
                    List<AccountPendingRequests> pendingPushRequests = null;
                    AccountPendingRequests apr = new AccountPendingRequests();
                    pendingPushRequests = formatter.Deserialize(stream) as List<AccountPendingRequests>;
                    stream.Close();
                    if (pendingPushRequests != null && pendingPushRequests.Count > 0)
                    {
                        SharedClass.Logger.Info("DeSerializing " + pendingPushRequests.Count.ToString() + " AcocuntPendingRequests");
                        while (pendingPushRequests.Count > 0)
                        {
                            apr = pendingPushRequests.First();
                            AccountProcessor accountProcessor = new AccountProcessor(apr.AccountId);
                            foreach (PushRequest pushRequest in apr.OutboundRequests)
                            {
                                accountProcessor.EnQueue(pushRequest, Direction.OUTBOUND);
                            }
                            foreach (PushRequest pushRequest in apr.InboundRequests)
                            {
                                accountProcessor.EnQueue(pushRequest, Direction.INBOUND);
                            }
                            System.Threading.Thread accountProcessorThread = new System.Threading.Thread(accountProcessor.Start);
                            accountProcessorThread.Name = "Account_" + apr.AccountId.ToString();
                            accountProcessorThread.Start();
                            pendingPushRequests.Remove(apr);
                        }
                    }
                    try
                    {
                        System.IO.File.Delete(SharedClass.PendingQueueFileName);
                    }
                    catch (Exception e)
                    {
                        SharedClass.Logger.Error("Error Deleting Serialized File, Reason : " + e.ToString());
                    }
                }
            }
            catch (Exception e)
            {
                SharedClass.Logger.Error("Exception while Deserializing Queue : " + e.ToString());
            }
        }
        private void LoadConfig()
        {
            SharedClass.InitiaLizeLogger();
        }
    }
}
