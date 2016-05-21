using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace HttpDrPush
{
    public partial class Service1 : ServiceBase
    {
        System.Threading.Thread serviceThread = null;
        ApplicationController appController = null;
        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            appController = new ApplicationController();
            serviceThread = new System.Threading.Thread(new System.Threading.ThreadStart(appController.Start));
            serviceThread.Name = "ServiceThread";
            serviceThread.Start();
        }

        protected override void OnStop()
        {
            System.Threading.Thread.CurrentThread.Name = "StopSignal";
            SharedClass.Logger.Info("========= Service Stop Signal Received ===========");
            SharedClass.HasStopSignal = true;
            appController.Stop();
            while (!SharedClass.IsServiceCleaned)
            {
                System.Threading.Thread.Sleep(1000);
                SharedClass.Logger.Info("Service Not Yet Cleaned");
            }
            SharedClass.Logger.Info("============= Service Stopped ===========");
        }
    }
}
