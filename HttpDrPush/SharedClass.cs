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
        private static ILog logger = null;
        private static bool hasStopSignal = false;
        private static bool isServiceCleaned = true;
        private static Dictionary<int, AccountProcessor> activeAccountProcessors = new Dictionary<int, AccountProcessor>();
        private static System.Threading.Mutex activeAccountsMutex = new System.Threading.Mutex();
        private static string connectionString = null;
        private static int houseKeepingThreadSleepTime = 10;
        private static int maxInactivity = 10;
        
        private static List<AccountPendingRequests> pendingPushRequests = null;
        public static void InitiaLizeLogger()
        {
            GlobalContext.Properties["LogName"] = DateTime.Now.ToString("yyyyMMdd");
            log4net.Config.XmlConfigurator.Configure();
            logger = LogManager.GetLogger("Log");
            //SharedClass.dumpLogger = LogManager.GetLogger("DumpLogger");
            //SharedClass.heartBeatLogger = LogManager.GetLogger("HeartBeatLogger");
        }
        public static bool AddAccountProcessor(int accountId, AccountProcessor Processor)
        {
            bool flag = false;
            SharedClass.logger.Info((object)("Adding AccountID " + (object)accountId + " Into ActiveAccountProcessors"));
            try
            {
                while (!SharedClass.activeAccountsMutex.WaitOne())
                    System.Threading.Thread.Sleep(10);
                if (!SharedClass.activeAccountProcessors.ContainsKey(accountId))
                    SharedClass.activeAccountProcessors.Add(accountId, Processor);
                flag = true;
            }
            catch (Exception ex)
            {
                SharedClass.logger.Error((object)("Error Adding UserProcessor To Map : " + ex.Message));
            }
            finally
            {
                SharedClass.activeAccountsMutex.ReleaseMutex();
            }
            return flag;
        }
        public static bool ReleaseAccountProcessor(int accountId)
        {
            bool flag = false;
            SharedClass.logger.Info((object)("Releasing AccountId " + (object)accountId + " From ActiveAccountProcessors Map"));
            try
            {
                while (!SharedClass.activeAccountsMutex.WaitOne())
                    System.Threading.Thread.Sleep(10);
                if (SharedClass.activeAccountProcessors.ContainsKey(accountId))
                    SharedClass.activeAccountProcessors.Remove(accountId);
                flag = true;
            }
            catch (Exception ex)
            {
                SharedClass.logger.Error((object)("Error Removing UserProcessor From Map : " + ex.Message));
            }
            finally
            {
                SharedClass.activeAccountsMutex.ReleaseMutex();
            }
            return flag;
        }
        public static bool IsAccountProcessorActive(int accountId)
        {
            bool flag = false;
            try
            {
                while (!SharedClass.activeAccountsMutex.WaitOne())
                    System.Threading.Thread.Sleep(10);
                if (SharedClass.activeAccountProcessors.ContainsKey(accountId))
                    flag = true;
            }
            catch (Exception ex)
            {
                SharedClass.logger.Error("Error While Chcecking ActiveAccountMap, Reason : " + ex.ToString());
            }
            finally
            {
                SharedClass.activeAccountsMutex.ReleaseMutex();
            }
            return flag;
        }
        public static void GetAccountProcessor(int accountId, out AccountProcessor accountProcessor)
        {
            while (!activeAccountsMutex.WaitOne())
                System.Threading.Thread.Sleep(10);
            activeAccountProcessors.TryGetValue(accountId, out accountProcessor);
            activeAccountsMutex.ReleaseMutex();
        }
        public static void StopActiveAccountProcessors()
        {
            while (activeAccountProcessors.Count > 0)
            {
                KeyValuePair<int, AccountProcessor> accountProcessor = activeAccountProcessors.First();
                accountProcessor.Value.Stop();
            }
        }
        public static void HouseKeeping()
        {
            logger.Info("Started");
            while (!hasStopSignal)
            {
                if (ActiveAccountProcessorsCount > 0)
                {   
                    foreach (KeyValuePair<int, AccountProcessor> accountProcesser in activeAccountProcessors)
                    {
                        if (!accountProcesser.Value.IsNecessary)
                        {
                            logger.Info("AccountProcessor " + accountProcesser.Key.ToString() + " Is Not Necessary. Stopping It.");
                            System.Threading.Thread  accountProcessorStopThread = new System.Threading.Thread(new System.Threading.ThreadStart(accountProcesser.Value.Stop));
                            accountProcessorStopThread.Name = "Account_" + accountProcesser.Key.ToString() + "_HouseKeep";
                            accountProcessorStopThread.Start();
                        }
                    }
                }
                System.Threading.Thread.Sleep(houseKeepingThreadSleepTime);
            }
        }
        public static long CurrentTimeStamp()
        {
            return Convert.ToInt64((DateTime.Now - new DateTime(1970, 1, 1)).TotalMilliseconds);
        }
        public static void SerializePendingQueue()
        {
            if (pendingPushRequests != null && pendingPushRequests.Count > 0)
            {
                logger.Info("Serializing " + pendingPushRequests.Count.ToString() + " Account Pending Requests");
                try
                {
                    System.Runtime.Serialization.Formatters.Binary.BinaryFormatter formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    System.IO.Stream stream = new System.IO.FileStream(PendingQueueFileName, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None);
                    formatter.Serialize(stream, pendingPushRequests);
                    stream.Close();
                }
                catch (Exception e)
                {
                    logger.Error("Exception while serializing Pending Queue, Reason : " + e.ToString());
                }
            }
        }
        public static ILog Logger { get { return logger == null ? log4net.LogManager.GetLogger("") : logger; } }
        public static bool IsServiceCleaned { get { return isServiceCleaned; } set { isServiceCleaned = value; } }
        public static bool HasStopSignal { get { return hasStopSignal; } set { hasStopSignal = value; } }        
        public static int ActiveAccountProcessorsCount
        {
            get
            {
                int count = 0;
                while (!activeAccountsMutex.WaitOne())
                    System.Threading.Thread.Sleep(10);
                count = activeAccountProcessors.Count();
                activeAccountsMutex.ReleaseMutex();
                return count;
            }
        }
        public static void AddAccountPendingRequests(AccountPendingRequests apr)
        {
            if (pendingPushRequests == null)
                pendingPushRequests = new List<AccountPendingRequests>();
            lock (SharedClass.pendingPushRequests)
            {
                SharedClass.pendingPushRequests.Add(apr);
            }
        }
        public static string ConnectionString { get { return connectionString == null ? System.Configuration.ConfigurationManager.ConnectionStrings["ConnectionString"].ConnectionString : connectionString; } }
        public static int HouseKeepingThreadSleepTime { get { return houseKeepingThreadSleepTime; } set { houseKeepingThreadSleepTime = value; } }
        public static int MaxInactivity { get { return maxInactivity; } set { maxInactivity = value; } }
        public static string PendingQueueFileName { get { return "PendingQueue.ser"; } }
    }
}
