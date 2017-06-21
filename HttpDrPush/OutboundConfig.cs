using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HttpDrPush
{
    public class OutboundConfig
    {
        #region PRIVATE_VARIABLES
        private string _url = string.Empty;
        private HttpMethod _httpMethod = HttpMethod.POST;
        private byte _maxFailedAttempts = SharedClass.MAX_FAILED_ATTEMPTS;
        private byte _retryDelayInSeconds = SharedClass.RETRY_DELAY_IN_SECONDS;
        private byte _retryStrategy = SharedClass.RETRY_STRATEGY;
        private byte _concurrentConnections = SharedClass.CONCURRENT_CONNECTIONS;
        private byte _currentConnections = 0;
        private string _uuidParameterName = SharedClass.UUID_PARAMETER_NAME;
        private string _mobileNumberParameterName = SharedClass.MOBILE_NUMBER_PARAMETER_NAME;
        private string _smsStatusParameterName = SharedClass.STATUS_PARAMETER_NAME;
        private string _smsStatusCodeParameterName = SharedClass.STATUS_CODE_PARAMETER_NAME;
        private string _smsStatusTimeParameterName = SharedClass.STATUS_TIME_PARAMETER_NAME;
        private string _textParameterName = SharedClass.TEXT_PARAMETER_NAME;
        private string _senderNameParameterName = SharedClass.SENDER_NAME_PARAMETER_NAME;
        private string _costParameterName = SharedClass.COST_PARAMETER_NAME;
        private Dictionary<string, string> _requestHeaders = null;
        private Dictionary<string, string> _extraParameters = null;
        private PayloadFormat _dataFormat = PayloadFormat.JSON;
        private string _rootElementName = SharedClass.ROOT_ELEMENT_NAME;
        private bool _isSmsPropertiesAsAttributes = true;
        private bool _isSmsObjectAsArray = false;        
        #endregion        

        #region "PROPERTIES"
        public string Url 
        { 
            get 
            {
                return this._url; 
            } 
            set 
            {
                this._url = value; 
            } 
        }
        public HttpMethod HttpMethod 
        { 
            get 
            {
                return this._httpMethod; 
            } 
            set 
            {
                this._httpMethod = value; 
            } 
        }
        public byte MaxFailedAttempts 
        { 
            get 
            {
                return this._maxFailedAttempts; 
            } 
            set 
            {
                this._maxFailedAttempts = value; 
            } 
        }
        public byte RetryDelayInSeconds 
        { 
            get 
            {
                return this._retryDelayInSeconds; 
            } 
            set 
            {
                this._retryDelayInSeconds = value; 
            } 
        }
        public byte RetryStrategy 
        { 
            get 
            {
                return this._retryStrategy; 
            } 
            set 
            {
                this._retryStrategy = value; 
            } 
        }
        public byte ConcurrentConnections 
        { 
            get 
            {
                return this._concurrentConnections; 
            } 
            set 
            {
                this._concurrentConnections = value; 
            } 
        }
        public byte CurrentConnections 
        { 
            get 
            {
                return this._currentConnections; 
            } 
            set 
            {
                this._currentConnections = value; 
            } 
        }
        public string UUIDParameterName
        {
            get
            {
                return this._uuidParameterName;
            }
            set
            {
                this._uuidParameterName = value;
            }
        }
        public string MobileNumberParameterName
        {
            get
            {
                return this._mobileNumberParameterName;
            }
            set
            {
                this._mobileNumberParameterName = value;
            }
        }
        public string SmsStatusParameterName 
        { 
            get 
            {
                return this._smsStatusParameterName; 
            } 
            set 
            {
                this._smsStatusParameterName = value; 
            } 
        }
        public string SmsStatusCodeParameterName 
        { 
            get 
            {
                return this._smsStatusCodeParameterName; 
            } 
            set 
            {
                this._smsStatusCodeParameterName = value; 
            } 
        }
        public string SmsStatusTimeParameterName 
        { 
            get 
            {
                return this._smsStatusTimeParameterName; 
            } 
            set 
            {
                this._smsStatusTimeParameterName = value; 
            } 
        }
        public string TextParameterName 
        { 
            get 
            {
                return this._textParameterName; 
            } 
            set 
            {
                this._textParameterName = value; 
            } 
        }
        public string SenderNameParameterName 
        { 
            get 
            { 
                return this._senderNameParameterName; 
            } 
            set 
            { 
                this._senderNameParameterName = value; 
            } 
        }
        public string CostParameterName 
        { 
            get 
            { 
                return _costParameterName; 
            } 
            set 
            { 
                this._costParameterName = value; 
            } 
        }
        public Dictionary<string, string> RequestHeaders 
        { 
            get 
            {
                if (this._requestHeaders == null)
                    this._requestHeaders = new Dictionary<string, string>();
                return this._requestHeaders;
            } 
            set 
            { 
                this._requestHeaders = value; 
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
        public PayloadFormat DataFormat 
        { 
            get 
            { 
                return this._dataFormat; 
            } 
            set 
            { 
                this._dataFormat = value; 
            } 
        }
        public string RootElementName 
        { 
            get 
            { 
                return this._rootElementName; 
            } 
            set 
            { 
                this._rootElementName = value; 
            } 
        }
        public bool IsSmsPropertiesAsAttributes 
        { 
            get 
            { 
                return this._isSmsPropertiesAsAttributes; 
            } 
            set 
            { 
                this._isSmsPropertiesAsAttributes = value; 
            } 
        }
        public bool IsSmsObjectAsArray
        {
            get
            {
                return this._isSmsObjectAsArray;
            }
            set
            {
                this._isSmsObjectAsArray = value;
            }
        }
        #endregion
    }
}
