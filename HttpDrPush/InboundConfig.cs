using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HttpDrPush
{
    public class InboundConfig
    {
        private string url = null;
        private HttpMethod httpMethod = HttpMethod.POST;
        private byte maxFailedAttempts = 0;
        private short retryDelayInSeconds = 20;
        private byte retryStrategy = 1;
        private byte concurrentConnections = 1;
        private byte currentConnections = 0;
        private string mobileNumberParameterName = "Number";
        private string textParameterName = "Text";
        private Dictionary<string, string> requestHeaders = null;
        private DataFormat dataFormat = DataFormat.JSON;
        private string rootElementName = "Message";
        #region PROPERTIES
        public string Url { get { return url; } set { url = value; } }
        public HttpMethod HttpMethod { get { return httpMethod; } set { httpMethod = value; } }
        public byte MaxFailedAttempts { get { return maxFailedAttempts; } set { maxFailedAttempts = value; } }
        public short RetryDelayInSeconds { get { return retryDelayInSeconds; } set { retryDelayInSeconds = value; } }
        public byte RetryStrategy { get { return retryStrategy; } set { retryStrategy = value; } }
        public byte ConcurrentConnections { get { return concurrentConnections; } set { concurrentConnections = value; } }
        public byte CurrentConnections { get { return currentConnections; } set { currentConnections = value; } }
        public string MobileNumberParameterName { get { return mobileNumberParameterName; } set { mobileNumberParameterName = value; } }
        public string TextParameterName { get { return textParameterName; } set { textParameterName = value; } }
        public Dictionary<string, string> RequestHeaders { get { return requestHeaders; } set { requestHeaders = value; } }
        public DataFormat DataFormat { get { return dataFormat; } set { dataFormat = value; } }
        public string RootElementName { get { return rootElementName; } set { rootElementName = value; } }
        #endregion
    }
}
