using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HttpDrPush
{
    public class OutboundConfig
    {
        private string url = null;
        private HttpMethod httpMethod = HttpMethod.POST;
        private byte maxFailedAttempts = 0;
        private short retryDelayInSeconds = 20;
        private byte retryStrategy = 1;
        private byte concurrentConnections = 1;
        private byte currentConnections = 0;
        private string uuidParameterName = "UUID";
        private string mobileNumberParameterName = "Number";
        private string smsStatusParameterName = "Status";
        private string smsStatusCodeParameterName = "StatusCode";
        private string smsStatusTimeParameterName = "StatusTime";
        private string textParameterName = "Text";
        private string senderNameParameterName = "SenderId";
        private string costParameterName = "Cost";
        private Dictionary<string, string> requestHeaders = null;
        private DataFormat dataFormat = DataFormat.JSON;
        private string rootElementName = "Message";
        private bool isSmsPropertiesAsAttributes = true;
        private bool isSmsObjectAsArray = false;

        #region "PROPERTIES"
        public string Url { get { return url; } set { url = value; } }
        public HttpMethod HttpMethod { get { return httpMethod; } set { httpMethod = value; } }
        public byte MaxFailedAttempts { get { return maxFailedAttempts; } set { maxFailedAttempts = value; } }
        public short RetryDelayInSeconds { get { return retryDelayInSeconds; } set { retryDelayInSeconds = value; } }
        public byte RetryStrategy { get { return retryStrategy; } set { retryStrategy = value; } }
        public byte ConcurrentConnections { get { return concurrentConnections; } set { concurrentConnections = value; } }
        public byte CurrentConnections { get { return currentConnections; } set { currentConnections = value; } }
        public string UUIDParameterName { get { return uuidParameterName; } set { uuidParameterName = value; } }
        public string MobileNumberParameterName { get { return mobileNumberParameterName; } set { mobileNumberParameterName = value; } }
        public string SmsStatusParameterName { get { return smsStatusParameterName; } set { smsStatusParameterName = value; } }
        public string SmsStatusCodeParameterName { get { return smsStatusCodeParameterName; } set { smsStatusCodeParameterName = value; } }
        public string SmsStatusTimeParameterName { get { return smsStatusTimeParameterName; } set { smsStatusTimeParameterName = value; } }
        public string TextParameterName { get { return textParameterName; } set { textParameterName = value; } }
        public string SenderNameParameterName { get { return senderNameParameterName; } set { senderNameParameterName = value; } }
        public string CostParameterName { get { return costParameterName; } set { costParameterName = value; } }
        public Dictionary<string, string> RequestHeaders { get { return requestHeaders; } set { requestHeaders = value; } }
        public DataFormat DataFormat { get { return dataFormat; } set { dataFormat = value; } }
        public string RootElementName { get { return rootElementName; } set { rootElementName = value; } }
        public bool IsSmsPropertiesAsAttributes { get { return true; } set { isSmsPropertiesAsAttributes = value; } }
        public bool IsSmsObjectAsArray { get { return isSmsObjectAsArray; } set { isSmsObjectAsArray = value; } }
        #endregion
    }
}
