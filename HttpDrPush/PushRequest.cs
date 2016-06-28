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
        #region PRIVATE_VARIABLES
        private long _id = 0;
        private string _mobileNumber = string.Empty;
        private string _uuid = string.Empty;
        private string _text = string.Empty;
        private byte _smsStatusCode = 0;
        private long _smsStatusTime = 0;
        private string _senderName = string.Empty;
        private float _cost = 0;
        private byte _attemptsMade = 0;
        private int _responseStatusCode = 0;
        private int _timeTaken = 0;
        private bool _isSuccess = false;
        private string _url = string.Empty;
        private Dictionary<string, string> _extraParameters = null;
        #endregion        
        public void ReEnQueue()
        {
            System.Data.SqlClient.SqlConnection sqlCon = null;
            System.Data.SqlClient.SqlCommand sqlCmd = null;
            try
            {
                sqlCon = new System.Data.SqlClient.SqlConnection(SharedClass.ConnectionString);
                sqlCmd = new System.Data.SqlClient.SqlCommand("DR_ReEnQueue_PushRequest", sqlCon);
                sqlCmd.Parameters.Add("@Id", System.Data.SqlDbType.BigInt).Value = this._id;
                sqlCmd.Parameters.Add("@AttemptsMade", System.Data.SqlDbType.TinyInt).Value = this._attemptsMade;
                sqlCon.Open();
                sqlCmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                SharedClass.Logger.Error("Error ReEnQueuing PushRequest Id : " + this._id + ", Reason : " + e.ToString());
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
        public long Id { 
            get 
            { 
                return _id; 
            } 
            set 
            { 
                _id = value; 
            } 
        }
        public string Url 
        {
            get
            {
                if(this._url.Contains("?"))
                {
                    foreach(string paramandvalue in this._url.Split('&'))
                    {
                        this.ExtraParameters.Add(paramandvalue.Split('=').First(), paramandvalue.Split('=').Last());
                    }
                    this._url = this._url.Substring(0, this._url.IndexOf("?"));
                }
                return this._url;
            }
            set
            {
                this._url = value;
            }
        }
        public string MobileNumber 
        { 
            get 
            { 
                return _mobileNumber; 
            } 
            set 
            { 
                _mobileNumber = value; 
            } 
        }
        public string UUID 
        { 
            get 
            { 
                return _uuid; 
            } 
            set 
            { 
                _uuid = value; 
            } 
        }
        public string Text 
        { 
            get 
            { 
                return _text; 
            } 
            set 
            { 
                _text = value; 
            } 
        }
        public byte SmsStatusCode 
        { 
            get 
            { 
                return _smsStatusCode; 
            } 
            set 
            { 
                _smsStatusCode = value; 
            } 
        }
        public long SmsStatusTime 
        { 
            get 
            { 
                return _smsStatusTime; 
            } 
            set 
            { 
                _smsStatusTime = value; 
            } 
        }
        public string SenderName 
        { 
            get 
            { 
                return _senderName; 
            } 
            set 
            { 
                _senderName = value; 
            } 
        }
        public float Cost 
        { 
            get 
            { 
                return _cost; 
            } 
            set 
            { 
                _cost = value; 
            } 
        }
        public byte AttemptsMade 
        { 
            get 
            { 
                return _attemptsMade; 
            } 
            set 
            { 
                _attemptsMade = value; 
            } 
        }
        public int ResponseStatusCode 
        { 
            get 
            { 
                return _responseStatusCode; 
            } 
            set 
            { 
                _responseStatusCode = value; 
            } 
        }
        public int TimeTaken 
        { 
            get 
            { 
                return _timeTaken; 
            } 
            set 
            { 
                _timeTaken = value; 
            } 
        }
        public bool IsSuccess 
        { 
            get 
            { 
                return _isSuccess; 
            } 
            set 
            { 
                _isSuccess = value; 
            } 
        }
        public Dictionary<string, string> ExtraParameters
        {
            get
            {
                if (this._extraParameters == null)
                    this._extraParameters = new Dictionary<string, string>();
                return this._extraParameters;
            }
            set
            {
                this._extraParameters = value;
            }
        }
        #endregion
    }
}
