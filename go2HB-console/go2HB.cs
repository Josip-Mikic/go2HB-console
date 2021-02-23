using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Timers;

namespace go2HB_console
{
    public class go2HB
    {
        private static object _locker = new object();
        private int CheckFlag = 0;
        private int DiskFlag = 0;
        private int counter = 0;
        private long disk;
        private const string filePath = "C:\\go2HB\\";
        private string configName = "config.txt";
        public string serviceName = "go2HB";
        private readonly Timer _timer;
        private JsonRecieve recieveJson;
        private Json send;
        private Config cfgFile;

        public go2HB()
        {
            DiskFlag = 1;

            //Check config file and load it
            if (!Directory.Exists(filePath)) Directory.CreateDirectory(filePath);
            CheckConfig();
            //Set default values for cpu and bios information
           

            _timer = new Timer(cfgFile.interval) { AutoReset = true };
            _timer.Elapsed += TimerElapsed;
        }

        public void RestartService(string serviceName, int timeoutMilliseconds)
        {
            ServiceController service = new ServiceController(serviceName);
            try
            {
                int milisec = Environment.TickCount;
                TimeSpan timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);

                service.Stop();
                service.WaitForStatus(ServiceControllerStatus.Stopped, timeout);

                int milisec2 = Environment.TickCount;
                timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);

                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, timeout);

            }

            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
        private void TimerElapsed(object sender, ElapsedEventArgs e)
        {
#if DEBUG
            Console.WriteLine("Checking for work");
            if(counter != 3)
            {
                counter++;
            }
            else
            {
                counter = 0;
            }

#endif
            if (DiskFlag == 1)
            {
                DiskFlag = 0;
                GetFreeSpace();
            }
            if (SendJson() != -1)
            {
                if (recieveJson != null)
                    ExecuteCommand();
            }



        }

        public void GetFreeSpace()
        {
            try
            {
                DriveInfo driveInfo = new DriveInfo(@"C:");
                disk = (long)Math.Truncate((double)driveInfo.AvailableFreeSpace / driveInfo.TotalSize * 100);
            }
            catch (System.IO.IOException errorMesage)
            {
                Console.WriteLine(errorMesage);
            }

        }

        public string GetCPUID()
        {
            var cliProcess = new Process()
            {
                StartInfo = new ProcessStartInfo("wmic", "cpu get Name")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                }
            };
            cliProcess.Start();
            string cliOut = cliProcess.StandardOutput.ReadToEnd();
            cliProcess.WaitForExit();
            cliProcess.Close();
            string[] format = cliOut.Split('\n');

            return format[1].Trim();
        }


        
        public string GetSerialNumber()
        {
            var cliProcess = new Process()
            {
                StartInfo = new ProcessStartInfo("wmic", "bios get serialnumber")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                }
            };
            cliProcess.Start();
            string cliOut = cliProcess.StandardOutput.ReadToEnd();
            cliProcess.WaitForExit();
            cliProcess.Close();
            string[] format = cliOut.Split('\n');

            return format[1].Trim();
        }

        private int SendJson()
        {
            send = new Json(cfgFile, disk);
            string json = JsonConvert.SerializeObject(send);
            int flag = 0;
            //Try connecting to server1
            flag = HandlePostRequest(json, "http://" + cfgFile.serverIP + ":" + cfgFile.server1port + cfgFile.apiPath);
            if (flag == 1)
                //Try connecting to server2
                flag = HandlePostRequest(json, "http://" + cfgFile.serverIPAlternative + ":" + cfgFile.server2port + cfgFile.apiPath);

            //Wait some more if fails
            if (flag == 2) CheckFlag = -1;
            //Execute if successfull*/
            return CheckFlag;
        }




        private void CheckConfig()
        {
            cfgFile = new Config();
            if (File.Exists(filePath + configName))
            {
                ReadConfig();
            }
            else
            {

                try
                {
                    UpdateConfig();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Some Io mistake. Error message: ");
                    Console.WriteLine(e.Message);
                }

            }
        }

        private void ReadConfig()
        {
            cfgFile = new Config();
            var path = filePath + configName;
            using var sw = new StreamReader(path);
            string line = sw.ReadLine();
            while (line != null)
            {
                string[] a = line.Split('=');
                string temp = a[0].ToUpper().Trim();
                switch (temp)
                {
                    case "SERVER1IP":
                        cfgFile.serverIP = a[1].Trim();
                        break;
                    case "SERVER2IP":
                        cfgFile.serverIPAlternative = a[1].Trim();
                        break;
                    case "APIPATH":
                        cfgFile.apiPath = a[1].Trim();
                        break;
                    case "SERVER1PORT":
                        cfgFile.server1port = a[1].Trim();
                        break;
                    case "SERVER2PORT":
                        cfgFile.server2port = a[1].Trim();
                        break;
                    case "INTERVAL":
                        cfgFile.interval = Int32.Parse(a[1].Trim());
                        break;
                    case "TYPE":
                        cfgFile.type = a[1].Trim();
                        break;
                    case "GO2ID":
                        cfgFile.go2id = a[1].Trim();
                        break;
                    case "BOXID":
                        cfgFile.boxID = a[1].Trim();
                        break;
                    case "CPUID":
                    case "BOXSN":
                        break;
                    default:
                        Console.WriteLine("Greska u citanju config filea");
                        break;
                }
                line = sw.ReadLine();
            }
            cfgFile.cpuID = GetCPUID();
            cfgFile.boxSN = GetSerialNumber();

        }


        private void UpdateConfig()
        {
            List<string> configItems = new List<string>();
            configItems.Add("server1ip" + " = " + cfgFile.serverIP);
            configItems.Add("server2ip" + " = " + cfgFile.serverIPAlternative);
            configItems.Add("server1port" + " = " + cfgFile.server1port);
            configItems.Add("server2port" + " = " + cfgFile.server2port);
            configItems.Add("apipath" + " = " + cfgFile.apiPath);
            configItems.Add("interval" + " = " + cfgFile.interval);
            configItems.Add("type" + " = " + cfgFile.type);
            configItems.Add("go2id" + " = " + cfgFile.go2id);
            cfgFile.cpuID = GetCPUID();
            configItems.Add("cpuID" + " = " + cfgFile.cpuID);
            cfgFile.boxSN = GetSerialNumber();
            configItems.Add("boxSN" + " = " + cfgFile.boxSN);
            configItems.Add("boxID" + " = " + cfgFile.boxID);



            string output = string.Join(Environment.NewLine, configItems.ToArray());
            System.IO.File.WriteAllText(filePath + configName, output);

            Console.WriteLine("Config file has been written !");

        }

        public void Start()
        {
#if DEBUG
            Console.WriteLine("Service starting");
#endif
            _timer.Start();
        }
        public void Stop()
        {
            DiskFlag = 0;
#if DEBUG
            Console.WriteLine("Service stoping");
#endif
            _timer.Stop();
            _timer.Dispose();
        }
        /// <summary>
        /// Create web request, get json file and store it into recieveJson
        /// </summary>
        /// <param name="json"></param>
        /// <param name="ip"></param>
        /// <returns></returns>
        private int HandlePostRequest(string json, string ip)
        {
            try
            {
                var httpWebRequest = (HttpWebRequest)WebRequest.Create(ip);
                httpWebRequest.ContentType = "application/json";
                httpWebRequest.Method = "POST";
                using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                    streamWriter.Write(json);
                }

                var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();

                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    string serverResponse;
                    serverResponse = streamReader.ReadToEnd();
                    recieveJson = JsonConvert.DeserializeObject<JsonRecieve>(serverResponse);


                }
                if (recieveJson.interval != -1 && recieveJson.interval != cfgFile.interval)
                {
                    Console.WriteLine("Writing another interval!");
                    cfgFile.interval = recieveJson.interval;
                    UpdateConfig();
                    //for non-Topshelf version i have a method called Restartservice();
                    Environment.Exit(1);
                }
            }
            catch (Exception e)
            {
                //Ako prva konekcija nije uspjela postavimo flag za 2 pokusaj 
                CheckFlag++;
                Console.WriteLine(e.Message);
            }


            return CheckFlag;
        }
        /// <summary>
        /// parsing the server message
        /// </summary>
        /// 
        
        private void ExecuteCommand()
        {
            if (recieveJson == null) Console.WriteLine("Json is null");
            else
            {
                //SHUTDOWN
                if (recieveJson.message.Trim().ToUpper() == "SHUTDOWN")
                {
                    var psi = new ProcessStartInfo("shutdown", "/s /t 0");
                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;
                    Process.Start(psi);
                }
                //REBOOT
                else if (recieveJson.message.ToUpper() == "REBOOT")
                {
                    var psi = new ProcessStartInfo("shutdown", "-r -t 0");
                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;
                    Process.Start(psi);
                }
                else if (recieveJson.message.ToUpper() == "NONE")
                    Console.WriteLine("Nothing to do.");
            }
        }
        class JsonRecieve
        {
            public int code;
            public string message;
            public int interval;
            JsonRecieve()
            {
                code = 0;
                message = "Default message";
                interval = -1;
            }
        }
        class Config
        {
            public string serverIP;
            public string serverIPAlternative;
            public string server1port;
            public string server2port;

            public string cpuID { get; set; }
            public string boxSN { get; internal set; }

            public string apiPath;
            public int interval;
            public string type;
            public string go2id;
            public string boxID;

            public Config()
            {
                apiPath = "/api/v1/ehlo";
                go2id = "novoRacunalo";
                interval = 3600000;
                serverIP = "172.16.147.10";
                serverIPAlternative = "172.16.147.10";
                type = "service";
                server1port = "80";
                server2port = "80";
                cpuID = "Cpu - ID";
                boxSN = "0000";
                boxID = "box0";
            }

           
        }
        class Json
        {
            public string type { get; set; }
            public string go2id { get; set; }
            public string ip { get; set; }

            public int interval { get; set; }

            public long disk { get; set; }

            public string boxId { get; set; }
            public string cpuID { get; set; }
            public string boxSN { get; set; }

            public Json(Config cfgFile, long disk)
            {

                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        this.ip = ip.ToString();
                    }
                }
                type = cfgFile.type;
                go2id = cfgFile.go2id;
                this.disk = disk;
                interval = cfgFile.interval;
                boxId = cfgFile.boxID;
                cpuID = cfgFile.cpuID;
                boxSN = cfgFile.boxSN;
            }
        }
    }

}