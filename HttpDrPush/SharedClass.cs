using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;

namespace HttpDrPush
{   
    [Serializable]
    public static class SharedClass
    {
        #region PRIVATE_VARIABLES
        private static ILog _logger = null;
        private static bool _hasStopSignal = false;
        private static bool _isServiceCleaned = true;
        private static Dictionary<int, AccountProcessor> _activeAccountProcessors = new Dictionary<int, AccountProcessor>();
        private static System.Threading.Mutex _activeAccountsMutex = new System.Threading.Mutex();
        private static string _connectionString = null;
        private static int _houseKeepingThreadSleepTimeInSeconds = 20;
        private static int _maxInactivityInSeconds = 30;

        private static List<PendingPushRequests> _pendingPushRequests = null;
        #endregion
        #region READONLY_VARIABLES
        public static readonly byte MAX_FAILED_ATTEMPTS = GetApplicationKey("MaxFailedAttempts") == null ? Convert.ToByte(0) : Convert.ToByte(GetApplicationKey("MaxFailedAttempts"));
        public static readonly byte RETRY_DELAY_IN_SECONDS = GetApplicationKey("RetryDelayInSeconds") == null ? Convert.ToByte(10) : Convert.ToByte(GetApplicationKey("RetryDelayInSeconds"));
        public static readonly byte RETRY_STRATEGY = GetApplicationKey("RetryStrategy") == null ? Convert.ToByte(1) : Convert.ToByte(GetApplicationKey("RetryStrategy"));
        public static readonly byte CONCURRENT_CONNECTIONS = GetApplicationKey("ConcurrentConnections") == null ? Convert.ToByte(1) : Convert.ToByte(GetApplicationKey("ConcurrentConnections"));
        public static readonly string UUID_PARAMETER_NAME = GetApplicationKey("UUIDParameterName") == null ? "MessageUUID" : GetApplicationKey("UUIDParameterName");
        public static readonly string MOBILE_NUMBER_PARAMETER_NAME = GetApplicationKey("MobileNumberParameterName") == null ? "Number" : GetApplicationKey("MobileNumberParameterName");
        public static readonly string STATUS_PARAMETER_NAME = GetApplicationKey("StatusParameterName") == null ? "Status" : GetApplicationKey("StatusParameterName");
        public static readonly string STATUS_CODE_PARAMETER_NAME = GetApplicationKey("StatusCodeParameterName") == null ? "StatusCode" : GetApplicationKey("StatusCodeParameterName");
        public static readonly string STATUS_TIME_PARAMETER_NAME = GetApplicationKey("StatusTimeParameterName") == null ? "StatusTime" : GetApplicationKey("StatusTimeParameterName");
        public static readonly string TEXT_PARAMETER_NAME = GetApplicationKey("TextParameterName") == null ? "Text" : GetApplicationKey("TextParameterName");
        public static readonly string SENDER_NAME_PARAMETER_NAME = GetApplicationKey("SenderNameParameterName") == null ? "SenderId" : GetApplicationKey("SenderNameParameterName");
        public static readonly string COST_PARAMETER_NAME = GetApplicationKey("CostParameterName") == null ? "Cost" : GetApplicationKey("CostParameterName");
        public static readonly string ROOT_ELEMENT_NAME = GetApplicationKey("RootElementName") == null ? "SMS" : GetApplicationKey("RootElementName");
        #endregion
        #region PRIVATE_METHODS
        private static string GetApplicationKey(string key)
        {
            return System.Configuration.ConfigurationManager.AppSettings[key];
        }
        #endregion
        #region PUBLIC_METHODS
        public static void InitiaLizeLogger()
        {
            GlobalContext.Properties["LogName"] = DateTime.Now.ToString("yyyyMMdd");
            log4net.Config.XmlConfigurator.Configure();
            _logger = LogManager.GetLogger("Log");
        }
        public static bool AddAccountProcessor(int accountId, AccountProcessor Processor)
        {
            bool flag = false;
            SharedClass._logger.Info((object)("Adding AccountID " + (object)accountId + " Into ActiveAccountProcessors"));
            try
            {
                while (!SharedClass._activeAccountsMutex.WaitOne())
                    System.Threading.Thread.Sleep(10);
                if (!SharedClass._activeAccountProcessors.ContainsKey(accountId))
                    SharedClass._activeAccountProcessors.Add(accountId, Processor);
                flag = true;
            }
            catch (Exception ex)
            {
                SharedClass._logger.Error((object)("Error Adding UserProcessor To Map : " + ex.Message));
            }
            finally
            {
                SharedClass._activeAccountsMutex.ReleaseMutex();
            }
            return flag;
        }
        public static bool ReleaseAccountProcessor(int accountId)
        {
            bool flag = false;
            SharedClass._logger.Info((object)("Releasing AccountId " + (object)accountId + " From ActiveAccountProcessors Map"));
            try
            {
                while (!SharedClass._activeAccountsMutex.WaitOne())
                    System.Threading.Thread.Sleep(10);
                if (SharedClass._activeAccountProcessors.ContainsKey(accountId))
                    SharedClass._activeAccountProcessors.Remove(accountId);
                flag = true;
            }
            catch (Exception ex)
            {
                SharedClass._logger.Error((object)("Error Removing UserProcessor From Map : " + ex.Message));
            }
            finally
            {
                SharedClass._activeAccountsMutex.ReleaseMutex();
            }
            return flag;
        }
        public static bool IsAccountProcessorActive(int accountId)
        {
            bool flag = false;
            try
            {
                while (!SharedClass._activeAccountsMutex.WaitOne())
                    System.Threading.Thread.Sleep(10);
                if (SharedClass._activeAccountProcessors.ContainsKey(accountId))
                    flag = true;
            }
            catch (Exception ex)
            {
                SharedClass._logger.Error("Error While Chcecking ActiveAccountMap, Reason : " + ex.ToString());
            }
            finally
            {
                SharedClass._activeAccountsMutex.ReleaseMutex();
            }
            return flag;
        }
        public static void GetAccountProcessor(int accountId, out AccountProcessor accountProcessor)
        {
            while (!_activeAccountsMutex.WaitOne())
                System.Threading.Thread.Sleep(10);
            _activeAccountProcessors.TryGetValue(accountId, out accountProcessor);
            _activeAccountsMutex.ReleaseMutex();
        }
        public static void StopActiveAccountProcessors()
        {
            while (_activeAccountProcessors.Count > 0)
            {
                KeyValuePair<int, AccountProcessor> accountProcessor = _activeAccountProcessors.First();
                accountProcessor.Value.Stop();
            }
        }
        public static void HouseKeeping()
        {
            _logger.Info("Started");
            while (!_hasStopSignal)
            {
                if (ActiveAccountProcessorsCount > 0)
                {
                    foreach (KeyValuePair<int, AccountProcessor> accountProcesser in _activeAccountProcessors)
                    {
                        if (!accountProcesser.Value.IsNecessary)
                        {
                            _logger.Info("AccountProcessor " + accountProcesser.Key.ToString() + " Is Not Necessary. Stopping It.");
                            System.Threading.Thread accountProcessorStopThread = new System.Threading.Thread(new System.Threading.ThreadStart(accountProcesser.Value.Stop));
                            accountProcessorStopThread.Name = "Account_" + accountProcesser.Key.ToString() + "_HouseKeep";
                            accountProcessorStopThread.Start();
                        }
                    }
                }
                System.Threading.Thread.Sleep(_houseKeepingThreadSleepTimeInSeconds * 1000);
            }
        }
        public static long CurrentTimeStamp()
        {
            return Convert.ToInt64((DateTime.Now - new DateTime(1970, 1, 1)).TotalMilliseconds);
        }
        public static void SerializePendingQueue()
        {
            if (_pendingPushRequests != null && _pendingPushRequests.Count > 0)
            {
                _logger.Info("Serializing " + _pendingPushRequests.Count.ToString() + " Account Pending Requests");
                try
                {
                    System.Runtime.Serialization.Formatters.Binary.BinaryFormatter formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    System.IO.Stream stream = new System.IO.FileStream(PendingQueueFileName, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None);
                    formatter.Serialize(stream, _pendingPushRequests);
                    stream.Close();
                }
                catch (Exception e)
                {
                    _logger.Error("Exception while serializing Pending Queue, Reason : " + e.ToString());
                }
            }
        }
        #endregion        
        #region PROPERTIES
        public static ILog Logger { get { return _logger == null ? log4net.LogManager.GetLogger("") : _logger; } }
        public static bool IsServiceCleaned { get { return _isServiceCleaned; } set { _isServiceCleaned = value; } }
        public static bool HasStopSignal { get { return _hasStopSignal; } set { _hasStopSignal = value; } }
        public static int ActiveAccountProcessorsCount
        {
            get
            {
                int count = 0;
                while (!_activeAccountsMutex.WaitOne())
                    System.Threading.Thread.Sleep(10);
                count = _activeAccountProcessors.Count();
                _activeAccountsMutex.ReleaseMutex();
                return count;
            }
        }
        public static void AddAccountPendingRequests(PendingPushRequests apr)
        {
            if (_pendingPushRequests == null)
                _pendingPushRequests = new List<PendingPushRequests>();
            lock (SharedClass._pendingPushRequests)
            {
                SharedClass._pendingPushRequests.Add(apr);
            }
        }
        public static string ConnectionString
        {
            get
            {
                if (_connectionString == null)
                    _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["ConnectionString"].ConnectionString;
                return _connectionString;
            }
        }
        public static int HouseKeepingThreadSleepTime { get { return _houseKeepingThreadSleepTimeInSeconds; } set { _houseKeepingThreadSleepTimeInSeconds = value; } }
        public static int MaxInactivity { get { return _maxInactivityInSeconds; } set { _maxInactivityInSeconds = value; } }
        public static string PendingQueueFileName { get { return "PendingQueue.ser"; } }
        #endregion
    }
}
