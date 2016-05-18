using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;

namespace HttpDrPush
{
    public static class SharedClass
    {
        private static ILog logger = null;
        private static bool hasStopSignal = false;
        private static Dictionary<long, AccountProcessor> activeAccountProcessors = new Dictionary<long, AccountProcessor>();
        private static System.Threading.Mutex activeAccountsMutex = new System.Threading.Mutex();
        private static string connectionString = null;
        public static void InitiaLizeLogger()
        {
            GlobalContext.Properties["LogName"] = DateTime.Now.ToString("yyyyMMdd");
            log4net.Config.XmlConfigurator.Configure();
            logger = LogManager.GetLogger("Log");
            //SharedClass.dumpLogger = LogManager.GetLogger("DumpLogger");
            //SharedClass.heartBeatLogger = LogManager.GetLogger("HeartBeatLogger");
        }
        public static bool AddAccountProcessor(long accountId, AccountProcessor Processor)
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

        public static bool ReleaseAccountProcessor(long accountId)
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

        public static bool IsAccountProcessorActive(long accountId)
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
                SharedClass.logger.Error((object)("Error While Chcecking ActiveAccountMap, Reason : " + ex.ToString()));
            }
            finally
            {
                SharedClass.activeAccountsMutex.ReleaseMutex();
            }
            return flag;
        }

        public static long CurrentTimeStamp()
        {
            return Convert.ToInt64((DateTime.Now - new DateTime(1970, 1, 1)).TotalMilliseconds);
        }
        public static ILog Logger { get { return logger == null ? log4net.LogManager.GetLogger("") : logger; } }
        public static bool HasStopSignal { get { return hasStopSignal; } set { hasStopSignal = value; } }
        public static Dictionary<long, AccountProcessor> ActiveAccountProcessors { get { return SharedClass.activeAccountProcessors; } }
        public static string ConnectionString { get { return connectionString == null ? System.Configuration.ConfigurationManager.ConnectionStrings["ConnectionString"].ConnectionString : connectionString; } }
    }
}
