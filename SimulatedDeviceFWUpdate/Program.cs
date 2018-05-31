using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;

namespace SimulatedDeviceFWUpdate
{
    class Program
    {
        // cut and paste the device connection string from the Azure IoT Hub Device Portal Page
        static string DeviceConnectionString = "HostName=azure-devices.net;DeviceId=iothubtpmdevice1;SharedAccessKey=";
        static DeviceClient Client = null;

        static async Task reportFwUpdateThroughTwin(Twin twin, TwinCollection fwUpdateValue)
        {
            try
            {
                TwinCollection patch = new TwinCollection();
                TwinCollection iothubDM = new TwinCollection();

                iothubDM["firmwareUpdate"] = fwUpdateValue;
                patch["iothubDM"] = iothubDM;

                await Client.UpdateReportedPropertiesAsync(patch);
                Console.WriteLine("Twin state reported: {0}", fwUpdateValue["status"]);
            }
            catch
            {
                Console.WriteLine("Error updating device twin");
                throw;
            }
        }

        static async Task<byte[]> simulateDownloadImage(string imageUrl)
        {
            var image = "[fake image data]";

            Console.WriteLine("Downloading image from " + imageUrl);

            await Task.Delay(4000);

            return Encoding.ASCII.GetBytes(image);

        }

        static async Task simulateApplyImage(byte[] imageData)
        {
            if (imageData == null)
            {
                throw new ArgumentNullException();
            }

            await Task.Delay(4000);

        }

        static async Task waitToDownload(Twin twin, string fwUpdateUri)
        {
            var now = DateTime.Now;
            TwinCollection status = new TwinCollection();
            status["fwPackageUri"] = fwUpdateUri;
            status["status"] = "waiting";
            status["error"] = null;
            status["startedWaitingTime"] = DateTime.Now;
            status["downloadCompleteTime"] = null;
            status["startedApplyingImage"] = null;
            status["lastFirmwareUpdate"] = null;

            await reportFwUpdateThroughTwin(twin, status);

            await Task.Delay(2000);
        }

        static async Task<byte[]> downloadImage(Twin twin, string fwUpdateUri)
        {
            try
            {
                TwinCollection statusUpdate = new TwinCollection();
                statusUpdate["status"] = "downloading";
                await reportFwUpdateThroughTwin(twin, statusUpdate);

                byte[] imageData = await simulateDownloadImage(fwUpdateUri);

                statusUpdate = new TwinCollection();
                statusUpdate["status"] = "downloadComplete";
                statusUpdate["downloadCompleteTime"] = DateTime.Now;
                await reportFwUpdateThroughTwin(twin, statusUpdate);
                return imageData;
            }
            catch (Exception ex)
            {
                TwinCollection statusUpdate = new TwinCollection();
                statusUpdate["status"] = "downloadFailed";
                statusUpdate["error"] = new TwinCollection();
                statusUpdate["error"]["code"] = ex.GetType().ToString();
                statusUpdate["error"]["message"] = ex.Message;
                await reportFwUpdateThroughTwin(twin, statusUpdate);
                throw;
            }
        }

        static async Task applyImage(Twin twin, byte[] imageData)
        {
            try
            {
                TwinCollection statusUpdate = new TwinCollection();
                statusUpdate["status"] = "applying";
                statusUpdate["startedApplyingImage"] = DateTime.Now;
                await reportFwUpdateThroughTwin(twin, statusUpdate);

                await simulateApplyImage(imageData);

                statusUpdate = new TwinCollection();
                statusUpdate["status"] = "applyComplete";
                statusUpdate["lastFirmwareUpdate"] = DateTime.Now;
                await reportFwUpdateThroughTwin(twin, statusUpdate);
            }
            catch (Exception ex)
            {
                TwinCollection statusUpdate = new TwinCollection();
                statusUpdate["status"] = "applyFailed";
                statusUpdate["error"] = new TwinCollection();
                statusUpdate["error"]["code"] = ex.GetType().ToString();
                statusUpdate["error"]["message"] = ex.Message;
                await reportFwUpdateThroughTwin(twin, statusUpdate);
                throw;
            }
        }

        static async Task doUpdate(string fwUpdateUrl)
        {
            try
            {
                Twin twin = await Client.GetTwinAsync();
                await waitToDownload(twin, fwUpdateUrl);
                byte[] imageData = await downloadImage(twin, fwUpdateUrl);
                await applyImage(twin, imageData);
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("Error during update: {0}", ex.Message);
            }
        }

        static Task<MethodResponse> onFirmwareUpdate(MethodRequest methodRequest, object userContext)
        {
            string fwUpdateUrl = (string)JObject.Parse(methodRequest.DataAsJson)["fwPackageUri"];
            Console.WriteLine("\nMethod: {0} triggered by service, URI is: {1}", methodRequest.Name, fwUpdateUrl);

            Task updateTask = Task.Run(() => (doUpdate(fwUpdateUrl)));

            string result = "'FirmwareUpdate started.'";
            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
        }

        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("Connecting to hub");
                Client = DeviceClient.CreateFromConnectionString(DeviceConnectionString, TransportType.Mqtt);

                // setup callback for "firmware update" method
                Client.SetMethodHandlerAsync("firmwareUpdate", onFirmwareUpdate, null).Wait();
                Console.WriteLine("Waiting for firmware update direct method call\n Press enter to exit.");
                Console.ReadLine();

                Console.WriteLine("Exiting...");

                // as a good practice, remove the firmware update handler
                Client.SetMethodHandlerAsync("firmwareUpdate", null, null).Wait();
                Client.CloseAsync().Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("Error in sample: {0}", ex.Message);
            }
        }
    }
}