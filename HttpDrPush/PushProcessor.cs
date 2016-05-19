using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ExtensionMethods;
using System.Net;
using System.Xml;
using Newtonsoft.Json.Linq;

namespace HttpDrPush
{
    public class PushProcessor
    {
        private byte id = 0;
        private Direction direction;
        private AccountProcessor accountProcessor = null;
        private Queue<PushRequest> pushRequestsQueue = new Queue<PushRequest>();
        private Mutex queueMutex = null;
        private bool shouldIRun = true;
        private XmlDocument xmlDoc = null;
        private XmlElement rootElement = null;
        private XmlElement childElement = null;
        private JObject jObj = new JObject();
        private JArray jArray = new JArray();
        public PushProcessor(byte _id, Direction _direction, AccountProcessor _accountProcessor)
        {
            this.id = _id;
            this.accountProcessor = _accountProcessor;
            this.direction = _direction;
            switch (direction)
            { 
                case Direction.OUTBOUND:
                    switch (this.accountProcessor.OutboundConfig.DataFormat)
                    { 
                        case DataFormat.XML:
                            this.xmlDoc = new XmlDocument();
                            this.rootElement = xmlDoc.CreateElement(this.accountProcessor.OutboundConfig.RootElementName);
                            break;
                        case DataFormat.JSON:
                            this.jObj = new JObject();
                            break;
                        default:
                            this.jObj = new JObject();
                            break;
                    }
                    break;
                case Direction.INBOUND:
                    switch (this.accountProcessor.InboundConfig.DataFormat)
                    { 
                        case DataFormat.XML:
                            this.xmlDoc = new XmlDocument();
                            this.rootElement = xmlDoc.CreateElement(this.accountProcessor.InboundConfig.RootElementName);
                            break;
                        case DataFormat.JSON:
                            this.jObj = new JObject();
                            break;
                        default:
                            this.jObj = new JObject();
                            break;
                    }
                    break;
                default:
                    break;
            }
        }
        public void Start()
        {
            SharedClass.Logger.Info("Started");
            this.accountProcessor.IncreaseConcurrency(direction);
            PushRequest pushRequest = new PushRequest();
            while (this.shouldIRun)
            {
                if (this.QueueCount() > 0)
                {
                    pushRequest = this.DeQueue();
                    if (pushRequest != null)
                    {
                        if (OutboundPush(pushRequest))
                        { }
                        if (direction == Direction.OUTBOUND)
                            this.accountProcessor.LastProcessedTimeOutbound = DateTime.Now.ToUnixTimeStamp();
                        else
                            this.accountProcessor.LastProcessedTimeInbound = DateTime.Now.ToUnixTimeStamp();
                    }
                }
                else
                {
                    Thread.Sleep(2000);
                }
            }
            SharedClass.Logger.Info("Dead");
            this.accountProcessor.DecreaseConcurrency(direction);
        }
        public void Stop()
        {
            this.shouldIRun = false;            
        }
        private bool OutboundPush(PushRequest pushRequest)
        {
            HttpWebRequest request = null;
            HttpWebResponse response = null;
            string payload = string.Empty;
            bool isPushSuccess = false;
            try
            {
                request = WebRequest.Create(this.accountProcessor.OutboundConfig.Url) as HttpWebRequest;
                switch (this.accountProcessor.OutboundConfig.DataFormat)
                {
                    case DataFormat.JSON:
                        jObj.RemoveAll();
                        jObj.Add(new JProperty(this.accountProcessor.OutboundConfig.MobileNumberParameterName, pushRequest.MobileNumber));
                        jObj.Add(new JProperty(this.accountProcessor.OutboundConfig.UUIDParameterName, pushRequest.UUID));
                        jObj.Add(new JProperty(this.accountProcessor.OutboundConfig.SmsStatusCodeParameterName, pushRequest.SmsStatusCode));
                        jObj.Add(new JProperty(this.accountProcessor.OutboundConfig.SmsStatusParameterName, ""));
                        jObj.Add(new JProperty(this.accountProcessor.OutboundConfig.SmsStatusTimeParameterName, pushRequest.SmsStatusTime));
                        jObj.Add(new JProperty(this.accountProcessor.OutboundConfig.SenderNameParameterName, pushRequest.SenderName));
                        jObj.Add(new JProperty(this.accountProcessor.OutboundConfig.CostParameterName, pushRequest.Cost));
                        if (this.accountProcessor.OutboundConfig.IsSmsObjectAsArray)
                        {
                            jArray.RemoveAll();
                            jArray.Add(jObj);
                            jObj = new JObject(new JProperty(this.accountProcessor.OutboundConfig.RootElementName, jArray));
                        }
                        else {
                            jObj.Add(new JProperty(this.accountProcessor.OutboundConfig.RootElementName, jObj));
                        }
                        break;
                    case DataFormat.XML:
                        if (this.accountProcessor.OutboundConfig.IsSmsPropertiesAsAttributes)
                        {
                            rootElement.SetAttribute(this.accountProcessor.OutboundConfig.MobileNumberParameterName, pushRequest.MobileNumber);
                            rootElement.SetAttribute(this.accountProcessor.OutboundConfig.UUIDParameterName, pushRequest.UUID);
                            rootElement.SetAttribute(this.accountProcessor.OutboundConfig.SmsStatusCodeParameterName, pushRequest.SmsStatusCode.ToString());
                            rootElement.SetAttribute(this.accountProcessor.OutboundConfig.SmsStatusParameterName, "");
                            rootElement.SetAttribute(this.accountProcessor.OutboundConfig.SmsStatusTimeParameterName, pushRequest.SmsStatusTime.ToString());
                            rootElement.SetAttribute(this.accountProcessor.OutboundConfig.SenderNameParameterName, pushRequest.SenderName);
                            rootElement.SetAttribute(this.accountProcessor.OutboundConfig.CostParameterName, pushRequest.Cost.ToString());
                        }
                        else
                        {   
                            childElement = xmlDoc.CreateElement(this.accountProcessor.OutboundConfig.MobileNumberParameterName);
                            childElement.InnerText = pushRequest.MobileNumber;
                            rootElement.AppendChild(childElement);
                            
                            childElement = xmlDoc.CreateElement(this.accountProcessor.OutboundConfig.UUIDParameterName);
                            childElement.InnerText = pushRequest.UUID;
                            rootElement.AppendChild(childElement);

                            childElement = xmlDoc.CreateElement(this.accountProcessor.OutboundConfig.SmsStatusCodeParameterName);
                            childElement.InnerText = pushRequest.SmsStatusCode.ToString();
                            rootElement.AppendChild(childElement);

                            childElement = xmlDoc.CreateElement(this.accountProcessor.OutboundConfig.SmsStatusParameterName);
                            childElement.InnerText = "";
                            rootElement.AppendChild(childElement);

                            childElement = xmlDoc.CreateElement(this.accountProcessor.OutboundConfig.SmsStatusTimeParameterName);
                            childElement.InnerText = pushRequest.SmsStatusTime.ToString();
                            rootElement.AppendChild(childElement);

                            childElement = xmlDoc.CreateElement(this.accountProcessor.OutboundConfig.SenderNameParameterName);
                            childElement.InnerText = pushRequest.SenderName;
                            rootElement.AppendChild(childElement);

                            childElement = xmlDoc.CreateElement(this.accountProcessor.OutboundConfig.CostParameterName);
                            childElement.InnerText = pushRequest.Cost.ToString();
                            rootElement.AppendChild(childElement);
                        }
                        request.ContentType = "application/xml";
                        break;
                    case DataFormat.PLAIN:
                        payload = this.accountProcessor.OutboundConfig.MobileNumberParameterName + "=" + pushRequest.MobileNumber;
                        payload += "&" + this.accountProcessor.OutboundConfig.UUIDParameterName + "=" + pushRequest.UUID;
                        payload += "&" + this.accountProcessor.OutboundConfig.SmsStatusCodeParameterName + "=" + pushRequest.SmsStatusCode;
                        payload += "&" + this.accountProcessor.OutboundConfig.SmsStatusParameterName + "=";
                        payload += "&" + this.accountProcessor.OutboundConfig.SmsStatusTimeParameterName + "=" + pushRequest.SmsStatusTime.ToString();
                        payload += "&" + this.accountProcessor.OutboundConfig.SenderNameParameterName + "=" + pushRequest.SenderName;
                        payload += "&" + this.accountProcessor.OutboundConfig.CostParameterName + "=" + pushRequest.Cost.ToString();
                        if (this.accountProcessor.OutboundConfig.HttpMethod == HttpMethod.POST)
                            request.ContentType = "application/x-www-form-urlencoded";
                        break;                    
                    default:
                        break;
                }
                switch (this.accountProcessor.OutboundConfig.HttpMethod)
                { 
                    case HttpMethod.GET:
                        break;
                    case HttpMethod.POST:
                        break;
                }
            }
            catch (Exception e)
            {
                SharedClass.Logger.Error("Error Pushing PushId " + pushRequest.Id + ", Reason : " + e.ToString());
            }
            finally
            {
                if(response != null)
                    response.Close();
                request = null;
                response = null;
            }
            return isPushSuccess;
        }
        public void EnQueue(PushRequest pushRequest)
        {
            try
            {
                while (!this.queueMutex.WaitOne())
                {
                    Thread.Sleep(10);
                }
                this.pushRequestsQueue.Enqueue(pushRequest);
            }
            catch (Exception e)
            {
                SharedClass.Logger.Error("Exception while enqueuing PushRequest : " + pushRequest.Id + ", Reason : " + e.ToString());
            }
            finally
            {
                this.queueMutex.ReleaseMutex();
            }
        }
        public PushRequest DeQueue()
        {
            PushRequest pushRequest = null;
            try
            {
                while (!this.queueMutex.WaitOne())
                {
                    Thread.Sleep(10);
                }
                pushRequest = this.pushRequestsQueue.Dequeue();
            }
            catch (Exception e)
            {
                SharedClass.Logger.Error("Exception while dequeuing PushRequest, direction : " + direction + ", Reason : " + e.ToString());
            }
            finally
            {
                this.queueMutex.ReleaseMutex();
            }
            return pushRequest;
        }
        public int QueueCount()
        {
            int queueCount = 0;
            try
            {
                while (!this.queueMutex.WaitOne())
                {
                    Thread.Sleep(10);
                }
                queueCount = this.pushRequestsQueue.Count();
            }
            catch (Exception e)
            {
                SharedClass.Logger.Error("Exception while getting queue count, direction : " + direction + ", Reason : " + e.ToString());
            }
            finally
            {
                this.queueMutex.ReleaseMutex();
            }
            return queueCount;
        }
        #region PROPERTIES
        public byte Id { get { return id; } set { id = value; } }
        #endregion
    }
}
