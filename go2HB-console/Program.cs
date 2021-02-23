using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Topshelf;

namespace go2HB_console
{
    class Program
    {
        static void Main(string[] args)
        {
            var exitCode = HostFactory.Run(x =>
            {
                x.Service<go2HB>(s =>
                {
                    s.ConstructUsing(go2HB_service => new go2HB());
                    s.WhenStarted(go2HB_service => go2HB_service.Start());
                    s.WhenStopped(go2HB_service => go2HB_service.Stop());
                });
                x.RunAsLocalSystem();

                x.SetServiceName("go2HB");
                x.SetDisplayName("Go2HB");
                x.SetDescription("Service for connecting to server");
                x.RunAsLocalSystem();
                x.StartAutomatically();
                x.EnableServiceRecovery(recoveryOption =>
                {
                    recoveryOption.RestartService(0);
                });
            });
            int exitCodeValue = (int)Convert.ChangeType(exitCode, exitCode.GetTypeCode());
            Environment.ExitCode = exitCodeValue;
        }
    }
}
