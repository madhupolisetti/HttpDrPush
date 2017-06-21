using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ExtensionMethods;
using System.Net;
using System.Xml;
using System.IO;
using Newtonsoft.Json.Linq;

namespace HttpDrPush
{
    public class PushProcessor
    {
        private byte id = 0;
        private Direction direction;
        private AccountProcessor accountProcessor = null;
        private Queue<PushRequest> pushRequestsQueue = new Queue<PushRequest>();
        private Mutex queueMutex = new Mutex();
        private bool shouldIRun = true;
        private bool isIamRunning = false;
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
                        case PayloadFormat.XML:
                            this.xmlDoc = new XmlDocument();
                            this.rootElement = xmlDoc.CreateElement(this.accountProcessor.OutboundConfig.RootElementName);
                            break;
                        case PayloadFormat.JSON:
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
                        case PayloadFormat.XML:
                            this.xmlDoc = new XmlDocument();
                            this.rootElement = xmlDoc.CreateElement(this.accountProcessor.InboundConfig.RootElementName);
                            break;
                        case PayloadFormat.JSON:
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
            byte successCount = 0;
            byte errorCount = 0;
            byte sleepCount = 0;
            this.accountProcessor.IncreaseConcurrency(direction);
            PushRequest pushRequest = new PushRequest();
            while (this.shouldIRun)
            {
                if (this.QueueCount() > 0)
                {
                    pushRequest = null;
                    pushRequest = this.DeQueue();
                    if (pushRequest != null)
                    {
                        if (successCount >= 100)
                            successCount = 0;
                        sleepCount = 0;
                        ++successCount;
                        isIamRunning = true;
                        OutboundPush(ref pushRequest);
                        if (!pushRequest.IsSuccess)
                            SharedClass.Logger.Error("Push Failed => PushId : " + pushRequest.Id.ToString() + ", AttemptsMade : " + pushRequest.AttemptsMade + ", LastStatusCode : " + pushRequest.ResponseStatusCode.ToString());
                        this.accountProcessor.UpdatePushRequest(pushRequest, direction);
                        if (direction == Direction.OUTBOUND)
                            this.accountProcessor.LastProcessedTimeOutbound = DateTime.Now.ToUnixTimeStamp();
                        else
                            this.accountProcessor.LastProcessedTimeInbound = DateTime.Now.ToUnixTimeStamp();
                        isIamRunning = false;
                    }
                }
                else
                {
                    Thread.Sleep(2000);
                    ++sleepCount;
                }
                if (sleepCount == 10)
                {
                    SharedClass.Logger.Info("Not DeQueued anything since 10 iterations");
                }
                if (successCount == 60)
                {
                    SharedClass.Logger.Info("Processed 60 Pushes");
                }
                if (errorCount == 10)
                {
                    SharedClass.Logger.Info("Error Count : 10");
                    errorCount = 0;
                }
            }
            SharedClass.Logger.Info("Dead");
            this.accountProcessor.DecreaseConcurrency(direction);
        }
        public void Stop()
        {
            this.shouldIRun = false;
            while (this.isIamRunning)
                Thread.Sleep(200);
            while (this.QueueCount() > 0)
                this.accountProcessor.AddPendingRequest(this.DeQueue(), direction);
        }
        private void OutboundPush(ref PushRequest pushRequest)
        {   
            HttpWebRequest request = null;
            HttpWebResponse response = null;
            string payload = string.Empty;            
            StreamWriter streamWriter = null;
            int startTime = System.DateTime.Now.ToUnixTimeStamp();
            bool isPushSuccess = false;
            payload = GetPayload(pushRequest);
            while (!isPushSuccess && pushRequest.AttemptsMade < this.accountProcessor.OutboundConfig.MaxFailedAttempts && this.shouldIRun)
            {
                try
                {   
                    if(this.accountProcessor.OutboundConfig.HttpMethod == HttpMethod.GET)
                        request = WebRequest.Create(pushRequest.Url + "?" + payload) as HttpWebRequest;
                    else
                        request = WebRequest.Create(pushRequest.Url) as HttpWebRequest;
                    if (this.accountProcessor.OutboundConfig.RequestHeaders.Count > 0)
                    {
                        foreach (KeyValuePair<string, string> header in this.accountProcessor.OutboundConfig.RequestHeaders)
                        {
                            request.Headers.Add(header.Key, header.Value);
                        }
                    }
                    request.UserAgent = "Smsc DR Publisher - O";
                    switch (this.accountProcessor.OutboundConfig.DataFormat)
                    {
                        case PayloadFormat.JSON:
                            request.ContentType = "application/json";
                            break;
                        case PayloadFormat.XML:
                            request.ContentType = "application/xml";
                            break;
                        case PayloadFormat.PLAIN:
                            if (this.accountProcessor.OutboundConfig.HttpMethod == HttpMethod.POST)
                                request.ContentType = "application/form-url-encoded";
                            break;
                        default:
                            break;
                    }
                    switch (this.accountProcessor.OutboundConfig.HttpMethod)
                    {
                        case HttpMethod.GET:
                            request.Method = HttpMethod.GET.ToString();
                            break;
                        case HttpMethod.POST:
                            request.Method = HttpMethod.POST.ToString();
                            streamWriter = new StreamWriter(request.GetRequestStream());                            
                            streamWriter.Write(payload);
                            streamWriter.Flush();
                            streamWriter.Close();
                            break;
                    }
                    response = (HttpWebResponse)request.GetResponse();
                    pushRequest.ResponseStatusCode = GetNumericStatusCode(response.StatusCode);                    
                    isPushSuccess = true;
                }
                catch (WebException e)
                {
                    SharedClass.Logger.Error("WebException : PushId " + pushRequest.Id.ToString() + ", Url : " + this.accountProcessor.OutboundConfig.Url + ", Method :  " + this.accountProcessor.OutboundConfig.HttpMethod.ToString() + ", AttemptsMade : " + pushRequest.AttemptsMade + ", Payload : " + payload + ", Reason " + e.ToString());
                    if(e.Response != null)
                        pushRequest.ResponseStatusCode = GetNumericStatusCode(((HttpWebResponse)e.Response).StatusCode);
                    ++pushRequest.AttemptsMade;                    
                }
                catch (Exception e)
                {
                    SharedClass.Logger.Error("Exception : PushId " + pushRequest.Id.ToString() + ", Url : " + this.accountProcessor.OutboundConfig.Url + ", Method :  " + this.accountProcessor.OutboundConfig.HttpMethod.ToString() + ", AttemptsMade : " + pushRequest.AttemptsMade + ", Payload : " + payload + ", Reason " + e.ToString());
                    ++pushRequest.AttemptsMade;
                }
                finally
                {
                    if (response != null)
                        response.Close();
                    request = null;
                    response = null;
                    pushRequest.TimeTaken = DateTime.Now.ToUnixTimeStamp() - startTime;
                }
                if (!pushRequest.IsSuccess && pushRequest.AttemptsMade < this.accountProcessor.OutboundConfig.MaxFailedAttempts)
                {   
                    if (this.accountProcessor.OutboundConfig.RetryStrategy == 1)
                        Thread.Sleep(this.accountProcessor.OutboundConfig.RetryDelayInSeconds * 1000);
                    else
                        Thread.Sleep(this.accountProcessor.OutboundConfig.RetryDelayInSeconds * 1000 * pushRequest.AttemptsMade);
                }
            }
            pushRequest.IsSuccess = isPushSuccess;            
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
                SharedClass.Logger.Info("DeQueued PushId : " + pushRequest.Id.ToString() + " From Queue");
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
        private string GetPayload(PushRequest pushRequest)
        {   
            string payload = string.Empty;                        
            try
            {   
                switch (this.accountProcessor.OutboundConfig.DataFormat)
                {       
                    case PayloadFormat.JSON:
                        jObj.RemoveAll();
                        jObj.Add(new JProperty(this.accountProcessor.OutboundConfig.MobileNumberParameterName, pushRequest.MobileNumber));
                        jObj.Add(new JProperty(this.accountProcessor.OutboundConfig.UUIDParameterName, pushRequest.UUID));
                        jObj.Add(new JProperty(this.accountProcessor.OutboundConfig.SmsStatusCodeParameterName, pushRequest.SmsStatusCode));
                        jObj.Add(new JProperty(this.accountProcessor.OutboundConfig.SmsStatusParameterName, ""));
                        jObj.Add(new JProperty(this.accountProcessor.OutboundConfig.SmsStatusTimeParameterName, pushRequest.SmsStatusTime));
                        jObj.Add(new JProperty(this.accountProcessor.OutboundConfig.SenderNameParameterName, pushRequest.SenderName));
                        jObj.Add(new JProperty(this.accountProcessor.OutboundConfig.CostParameterName, pushRequest.Cost));
                        if (pushRequest.ExtraParameters.Count > 0)
                        {
                            foreach (KeyValuePair<string, string> paramandvalue in pushRequest.ExtraParameters)
                            {
                                try
                                {
                                    jObj.Add(new JProperty(paramandvalue.Key, paramandvalue.Value));
                                }
                                catch (System.ArgumentException e)
                                {

                                }
                            }   
                        }
                        if (this.accountProcessor.OutboundConfig.ExtraParameters.Count > 0)
                        {
                            foreach (KeyValuePair<string, string> paramandvalue in this.accountProcessor.OutboundConfig.ExtraParameters)
                            {
                                try
                                {
                                    jObj.Add(new JProperty(paramandvalue.Key, paramandvalue.Value));
                                }
                                catch (System.ArgumentException e)
                                {

                                }
                            }   
                        }
                        if (this.accountProcessor.OutboundConfig.IsSmsObjectAsArray)
                        {   
                            jArray.RemoveAll();
                            jArray.Add(jObj);
                            jObj = new JObject(new JProperty(this.accountProcessor.OutboundConfig.RootElementName, jArray));
                        }
                        else
                        {
                            jObj = new JObject(new JProperty(this.accountProcessor.OutboundConfig.RootElementName, jObj));
                        }
                        payload = jObj.ToString();
                        break;
                    case PayloadFormat.XML:
                        rootElement.RemoveAll();
                        rootElement.RemoveAllAttributes();
                        if (this.accountProcessor.OutboundConfig.IsSmsPropertiesAsAttributes)
                        {
                            rootElement.SetAttribute(this.accountProcessor.OutboundConfig.MobileNumberParameterName, pushRequest.MobileNumber);
                            rootElement.SetAttribute(this.accountProcessor.OutboundConfig.UUIDParameterName, pushRequest.UUID);
                            rootElement.SetAttribute(this.accountProcessor.OutboundConfig.SmsStatusCodeParameterName, pushRequest.SmsStatusCode.ToString());
                            rootElement.SetAttribute(this.accountProcessor.OutboundConfig.SmsStatusParameterName, "");
                            rootElement.SetAttribute(this.accountProcessor.OutboundConfig.SmsStatusTimeParameterName, pushRequest.SmsStatusTime.ToString());
                            rootElement.SetAttribute(this.accountProcessor.OutboundConfig.SenderNameParameterName, pushRequest.SenderName);
                            rootElement.SetAttribute(this.accountProcessor.OutboundConfig.CostParameterName, pushRequest.Cost.ToString());
                            if (pushRequest.ExtraParameters.Count > 0)
                            {
                                foreach (KeyValuePair<string, string> paramandvalue in pushRequest.ExtraParameters)
                                    rootElement.SetAttribute(paramandvalue.Key, paramandvalue.Value);
                            }
                            if (this.accountProcessor.OutboundConfig.ExtraParameters.Count > 0)
                                foreach (KeyValuePair<string, string> paramandvalue in this.accountProcessor.OutboundConfig.ExtraParameters)
                                    rootElement.SetAttribute(paramandvalue.Key, paramandvalue.Value);
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
                            if(pushRequest.ExtraParameters.Count > 0)
                            { 
                                foreach (KeyValuePair<string, string> paramandvalue in pushRequest.ExtraParameters)
                                {
                                    childElement = xmlDoc.CreateElement(paramandvalue.Key);
                                    childElement.InnerText = paramandvalue.Value;
                                    rootElement.AppendChild(childElement);
                                }
                            }
                            if (this.accountProcessor.OutboundConfig.ExtraParameters.Count > 0)
                            {
                                foreach (KeyValuePair<string, string> paramandvalue in this.accountProcessor.OutboundConfig.ExtraParameters)
                                {
                                    childElement = xmlDoc.CreateElement(paramandvalue.Key);
                                    childElement.InnerText = paramandvalue.Value;
                                    rootElement.AppendChild(childElement);
                                }
                            }
                        }
                        payload = xmlDoc.InnerXml;
                        break;
                    case PayloadFormat.PLAIN:
                        payload = this.accountProcessor.OutboundConfig.MobileNumberParameterName + "=" + System.Web.HttpUtility.UrlEncode(pushRequest.MobileNumber);
                        payload += "&" + this.accountProcessor.OutboundConfig.UUIDParameterName + "=" + System.Web.HttpUtility.UrlEncode(pushRequest.UUID);
                        payload += "&" + this.accountProcessor.OutboundConfig.SmsStatusCodeParameterName + "=" + System.Web.HttpUtility.UrlEncode(pushRequest.SmsStatusCode.ToString());
                        payload += "&" + this.accountProcessor.OutboundConfig.SmsStatusParameterName + "=";
                        payload += "&" + this.accountProcessor.OutboundConfig.SmsStatusTimeParameterName + "=" + System.Web.HttpUtility.UrlEncode(pushRequest.SmsStatusTime.ToString());
                        payload += "&" + this.accountProcessor.OutboundConfig.SenderNameParameterName + "=" + System.Web.HttpUtility.UrlEncode(pushRequest.SenderName);
                        payload += "&" + this.accountProcessor.OutboundConfig.CostParameterName + "=" + System.Web.HttpUtility.UrlEncode(pushRequest.Cost.ToString());
                        if (pushRequest.ExtraParameters.Count > 0)
                            foreach (KeyValuePair<string, string> paramandvalue in pushRequest.ExtraParameters)
                                payload += "&" + paramandvalue.Key + "=" + System.Web.HttpUtility.UrlEncode(paramandvalue.Value);
                        if (this.accountProcessor.OutboundConfig.ExtraParameters.Count > 0)
                            foreach (KeyValuePair<string, string> paramandvalue in this.accountProcessor.OutboundConfig.ExtraParameters)
                                payload += "&" + paramandvalue.Key + "=" + System.Web.HttpUtility.UrlEncode(paramandvalue.Value);
                        break;
                    default:
                        break;
                }
            }
            catch (Exception e)
            {
                SharedClass.Logger.Error("Error Generating Payload, Reason : " + e.ToString());
            }
            return payload;
        }
        private int GetNumericStatusCode(HttpStatusCode statusCode)
        {
            int outputStatusCode = 0;
            switch (statusCode)
            { 
                case HttpStatusCode.Continue:
                    outputStatusCode = 100;
                    break;
                case HttpStatusCode.SwitchingProtocols:
                    outputStatusCode = 101;
                    break;
                case HttpStatusCode.OK:
                    outputStatusCode = 200;
                    break;
                case HttpStatusCode.Created:
                    outputStatusCode = 201;
                    break;
                case HttpStatusCode.Accepted:
                    outputStatusCode = 202;
                    break;
                case HttpStatusCode.NonAuthoritativeInformation:
                    outputStatusCode = 203;
                    break;
                case HttpStatusCode.NoContent:
                    outputStatusCode = 201;
                    break;
                case HttpStatusCode.ResetContent:
                    outputStatusCode = 205;
                    break;
                case HttpStatusCode.PartialContent:
                    outputStatusCode = 206;
                    break;
                case HttpStatusCode.MultipleChoices | HttpStatusCode.Ambiguous:
                    outputStatusCode = 300;
                    break;
                case HttpStatusCode.MovedPermanently | HttpStatusCode.Moved:
                    outputStatusCode = 301;
                    break;
                case HttpStatusCode.Found | HttpStatusCode.Redirect:
                    outputStatusCode = 302;
                    break;
                case HttpStatusCode.SeeOther | HttpStatusCode.RedirectMethod:
                    outputStatusCode = 303;
                    break;
                case HttpStatusCode.NotModified:
                    outputStatusCode = 304;
                    break;
                case HttpStatusCode.UseProxy:
                    outputStatusCode = 305;
                    break;
                case HttpStatusCode.Unused:
                    outputStatusCode = 306;
                    break;
                case HttpStatusCode.RedirectKeepVerb | HttpStatusCode.TemporaryRedirect:
                    outputStatusCode = 307;
                    break;
                case HttpStatusCode.BadRequest:
                    outputStatusCode = 400;
                    break;
                case HttpStatusCode.Unauthorized:
                    outputStatusCode = 401;
                    break;
                case HttpStatusCode.PaymentRequired:
                    outputStatusCode = 402;
                    break;
                case HttpStatusCode.Forbidden:
                    outputStatusCode = 403;
                    break;
                case HttpStatusCode.NotFound:
                    outputStatusCode = 404;
                    break;
                case HttpStatusCode.MethodNotAllowed:
                    outputStatusCode = 405;
                    break;
                case HttpStatusCode.NotAcceptable:
                    outputStatusCode = 406;
                    break;
                case HttpStatusCode.ProxyAuthenticationRequired:
                    outputStatusCode = 407;
                    break;
                case HttpStatusCode.RequestTimeout:
                    outputStatusCode = 408;
                    break;
                case HttpStatusCode.Conflict:
                    outputStatusCode = 409;
                    break;
                case HttpStatusCode.Gone:
                    outputStatusCode = 410;
                    break;
                case HttpStatusCode.LengthRequired:                    
                    outputStatusCode = 411;
                    break;
                case HttpStatusCode.PreconditionFailed:
                    outputStatusCode = 412;
                    break;
                case HttpStatusCode.RequestEntityTooLarge:
                    outputStatusCode = 413;
                    break;
                case HttpStatusCode.RequestUriTooLong:
                    outputStatusCode = 414;
                    break;
                case HttpStatusCode.UnsupportedMediaType:
                    outputStatusCode = 415;
                    break;
                case HttpStatusCode.RequestedRangeNotSatisfiable:
                    outputStatusCode = 416;
                    break;
                case HttpStatusCode.ExpectationFailed:
                    outputStatusCode = 417;
                    break;
                case HttpStatusCode.UpgradeRequired:
                    outputStatusCode = 426;
                    break;
                case HttpStatusCode.InternalServerError:
                    outputStatusCode = 500;
                    break;
                case HttpStatusCode.NotImplemented:
                    outputStatusCode = 501;
                    break;
                case HttpStatusCode.BadGateway:
                    outputStatusCode = 502;
                    break;
                case HttpStatusCode.ServiceUnavailable:
                    outputStatusCode = 503;
                    break;
                case HttpStatusCode.GatewayTimeout:
                    outputStatusCode = 504;
                    break;
                case HttpStatusCode.HttpVersionNotSupported:
                    outputStatusCode = 505;
                    break;
                default:
                    break;
            }
            return outputStatusCode;
        }
        #region PROPERTIES
        public byte Id { get { return id; } set { id = value; } }
        public bool IsRunning { get { return isIamRunning; } }
        #endregion
    }
}
