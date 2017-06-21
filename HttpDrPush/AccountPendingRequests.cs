using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HttpDrPush
{
    [Serializable]
    public class PendingPushRequests
    {
        private int accountId = 0;
        private List<PushRequest> inboundRequests = new List<PushRequest>();
        private List<PushRequest> outboundRequests = new List<PushRequest>();
        public int AccountId { get { return this.accountId; } set { this.accountId = value; } }
        public List<PushRequest> InboundRequests { get { return this.inboundRequests; } set { this.inboundRequests = value; } }
        public List<PushRequest> OutboundRequests { get { return this.outboundRequests; } set { this.outboundRequests = value; } }
    }
}
