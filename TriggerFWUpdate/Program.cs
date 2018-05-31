using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Shared;

namespace TriggerFWUpdate
{
    class Program
    {
        static RegistryManager registryManager;
        static string connString = "HostName=azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=yourkey";
        static ServiceClient client;
        static string targetDevice = "iothubtpmdevice1";

        public static async Task QueryTwinFWUpdateReported(DateTime startTime)
        {
            DateTime lastUpdated = startTime;

            while (true)
            {
                Twin twin = await registryManager.GetTwinAsync(targetDevice);

                if (twin.Properties.Reported.GetLastUpdated().ToUniversalTime() > lastUpdated.ToUniversalTime())
                {
                    lastUpdated = twin.Properties.Reported.GetLastUpdated().ToUniversalTime();
                    Console.WriteLine("\n" + twin.Properties.Reported["iothubDM"].ToJson());

                    var status = twin.Properties.Reported["iothubDM"]["firmwareUpdate"]["status"].Value;
                    if ((status == "downloadFailed") || (status == "applyFailed") || (status == "applyComplete"))
                    {
                        Console.WriteLine("\nStop polling.");
                        return;
                    }
                }
                await Task.Delay(50000);
            }
        }

        public static async Task StartFirmwareUpdate()
        {
            client = ServiceClient.CreateFromConnectionString(connString);
            CloudToDeviceMethod method = new CloudToDeviceMethod("firmwareUpdate");
            method.ResponseTimeout = TimeSpan.FromSeconds(30);
            method.SetPayloadJson(
                @"{
           fwPackageUri : 'https://someurl'
        }");

            CloudToDeviceMethodResult result = await client.InvokeDeviceMethodAsync(targetDevice, method);

            Console.WriteLine("Invoked firmware update on device.");
        }

        static void Main(string[] args)
        {
            registryManager = RegistryManager.CreateFromConnectionString(connString);

            Task queryTask = Task.Run(() => (QueryTwinFWUpdateReported(DateTime.Now)));

            StartFirmwareUpdate().Wait();
            Console.WriteLine("Press ENTER to exit.");
            Console.ReadLine();
        }
    }
}
