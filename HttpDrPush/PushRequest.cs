using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HttpDrPush
{
    [Serializable]
    public class PushRequest
    {
        private long id = 0;
        private string mobileNumber = string.Empty;
        private string uuid = string.Empty;
        private string text = string.Empty;
        private byte smsStatusCode = 0;
        private long smsStatusTime = 0;
        private string senderName = string.Empty;
        private float cost = 0;
        private byte attemptsMade = 0;
        private int responseStatusCode = 0;
        private int timeTaken = 0;
        private bool isSuccess = false;
        public void ReEnQueue()
        {
            System.Data.SqlClient.SqlConnection sqlCon = null;
            System.Data.SqlClient.SqlCommand sqlCmd = null;
            try
            {
                sqlCon = new System.Data.SqlClient.SqlConnection(SharedClass.ConnectionString);
                sqlCmd = new System.Data.SqlClient.SqlCommand("DR_ReEnQueue_PushRequest", sqlCon);
                sqlCmd.Parameters.Add("@Id", System.Data.SqlDbType.BigInt).Value = this.id;
                sqlCmd.Parameters.Add("@AttemptsMade", System.Data.SqlDbType.TinyInt).Value = this.attemptsMade;
                sqlCon.Open();
                sqlCmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                SharedClass.Logger.Error("Error ReEnQueuing PushRequest Id : " + this.id + ", Reason : " + e.ToString());
            }
            finally
            {
                if (sqlCon.State == System.Data.ConnectionState.Open)
                {
                    sqlCon.Close();
                }
                sqlCon = null;
                sqlCmd = null;
            }
        }

        #region "PROPERTIES"
        public long Id { get { return id; } set { id = value; } }
        public string MobileNumber { get { return mobileNumber; } set { mobileNumber = value; } }
        public string UUID { get { return uuid; } set { uuid = value; } }
        public string Text { get { return text; } set { text = value; } }
        public byte SmsStatusCode { get { return smsStatusCode; } set { smsStatusCode = value; } }
        public long SmsStatusTime { get { return smsStatusTime; } set { smsStatusTime = value; } }
        public string SenderName { get { return senderName; } set { senderName = value; } }
        public float Cost { get { return cost; } set { cost = value; } }
        public byte AttemptsMade { get { return attemptsMade; } set { attemptsMade = value; } }
        public int ResponseStatusCode { get { return responseStatusCode; } set { responseStatusCode = value; } }
        public int TimeTaken { get { return timeTaken; } set { timeTaken = value; } }
        public bool IsSuccess { get { return isSuccess; } set { isSuccess = value; } }
        #endregion
    }
}
