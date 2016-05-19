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
            System.Threading.Thread pollThread = new System.Threading.Thread(new System.Threading.ThreadStart(StartDbPoll));
            pollThread.Name = "DbPoller";
            pollThread.Start();
        }
        public void Stop()
        {
            while (this.isIamPolling)
                System.Threading.Thread.Sleep(100);
        }
        private void StartDbPoll()
        {
            SharedClass.Logger.Info("Started");
            SqlConnection sqlCon = new SqlConnection(SharedClass.ConnectionString);
            SqlCommand sqlCmd = new SqlCommand("Get_Pending_PushRequests", sqlCon);
            SqlDataAdapter da = null;
            DataSet ds = null;
            long accountId = 0;
            Direction direction = Direction.OUTBOUND;
            while (!SharedClass.HasStopSignal)
            {
                try
                {
                    isIamPolling = true;
                    sqlCmd.Parameters.Clear();
                    sqlCmd.Parameters.Add("@LastId", SqlDbType.BigInt).Value = this.lastDrId;
                    da = new SqlDataAdapter();
                    da.Fill(ds = new DataSet());
                    if (ds.Tables.Count > 0)
                    {
                        if (ds.Tables[0].Rows.Count > 0)
                        {
                            AccountProcessor accountProcessor = null;
                            PushRequest pushRequest = new PushRequest();
                            foreach (DataRow row in ds.Tables[0].Rows)
                            {
                                pushRequest.Id = Convert.ToInt64(row["Id"]);
                                accountId = Convert.ToInt64(row["AccountId"]);
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
                                pushRequest.Text = row["Text"].ToString();
                                pushRequest.SmsStatusCode = Convert.ToByte(row["SmsStatusCode"]);
                                pushRequest.SmsStatusTime = DateTime.Parse(row["SmsStatusTime"].ToString()).ToUnixTimeStamp();
                                pushRequest.SenderName = row["SenderName"].ToString();
                                pushRequest.Cost = float.Parse(row["Cost"].ToString());
                                pushRequest.AttemptsMade = Convert.ToByte(row["AttemptsMade"]);
                                SharedClass.GetAccountProcessor(accountId, out accountProcessor);
                                if (accountProcessor == null)
                                {
                                    accountProcessor = new AccountProcessor(accountId);
                                }
                                accountProcessor.EnQueue(pushRequest, direction);
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
        private void HouseKeeping()
        {
            while (!SharedClass.HasStopSignal)
            {   
                if (SharedClass.ActiveAccountProcessorsCount > 0)
                {
                    AccountProcessor accountProcessor = null;                    
                    for (int i = 0; i < SharedClass.ActiveAccountProcessorsCount; i++)
                    {
                        if (!accountProcessor.IsNecessary)
                            accountProcessor.Stop();
                    }
                }
                System.Threading.Thread.Sleep(SharedClass.HouseKeepingThreadSleepTime * 1000);
            }
        }
        private void LoadConfig()
        { 

        }
    }
}
