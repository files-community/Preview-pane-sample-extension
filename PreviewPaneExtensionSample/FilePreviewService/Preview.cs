using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace FilePreviewService
{
    public sealed class Preview : IBackgroundTask
    {
        private BackgroundTaskDeferral backgroundTaskDeferral;
        private AppServiceConnection appServiceconnection;
        private String[] inventoryItems = new string[] { "Robot vacuum", "Chair" };
        private double[] inventoryPrices = new double[] { 129.99, 88.99 };

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            // Get a deferral so that the service isn't terminated.
            this.backgroundTaskDeferral = taskInstance.GetDeferral();

            // Associate a cancellation handler with the background task.
            taskInstance.Canceled += OnTaskCanceled;

            // Retrieve the app service connection and set up a listener for incoming app service requests.
            var details = taskInstance.TriggerDetails as AppServiceTriggerDetails;
            appServiceconnection = details.AppServiceConnection;
            appServiceconnection.RequestReceived += OnRequestReceived;
        }

        private async void OnRequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            // This function is called when the app service receives a request.
            // Get a deferral because we use an awaitable API below (SendResponseAsync()) to respond to the message
            // and we don't want this call to get cancelled while we are waiting.
            AppServiceDeferral messageDeferral = args.GetDeferral();
            var message = args.Request.Message;
            var returnMessage = new ValueSet();

            // As part of the request, files sends over the file buffer in the form of a byte array
            var bytearray = message["byteArray"] as byte[];
            // files also sends the path, too, in case it is needed
            var path = message["filePath"] as string;

            var buffer = bytearray.AsBuffer();
            var text = "";


            using (var dataReader = Windows.Storage.Streams.DataReader.FromBuffer(buffer))
            {
                text = dataReader.ReadString(buffer.Length);
            }

            var result = await GetEncodedImage();

            // in order to be usable by files, images must be encoded into a base64 string, and added as the EncodedImage property
            string xaml = $"<ScrollViewer xml:space=\"preserve\" xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:controls=\"using:Files.UserControls\"> <StackPanel Orientation=\"Vertical\"><TextBlock Text=\"{System.Security.SecurityElement.Escape(text)}\"/><controls:StringEncodedImage><controls:StringEncodedImage.EncodedImage>{ System.Security.SecurityElement.Escape(result) }</controls:StringEncodedImage.EncodedImage></controls:StringEncodedImage></StackPanel></ScrollViewer>";
            returnMessage.Add("preview", xaml);

            var props = new List<FileProperty>() { 
                new FileProperty() {LocalizedName = "Hello", Value = "World!!!"}
            };

            // This code loads in a sample details for Files to display in the preview pane
            var json = JsonConvert.SerializeObject(props);
            returnMessage.Add("details", json);
            await args.Request.SendResponseAsync(returnMessage);
            
            messageDeferral.Complete();
        }
        

        private async Task<string> GetEncodedImage()
        {
            var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/FilesHome.png"));
            var stream = await file.OpenReadAsync();

            using (var memoryStream = new MemoryStream())
            {
                memoryStream.Capacity = (int)stream.Size;
                var ibuffer = memoryStream.GetWindowsRuntimeBuffer();
                ibuffer = await stream.ReadAsync(ibuffer, (uint)stream.Size, InputStreamOptions.None).AsTask().ConfigureAwait(false);
                var byteArray = ibuffer.ToArray();
                return Convert.ToBase64String(byteArray);
            }
        }

        private void OnTaskCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            if (this.backgroundTaskDeferral != null)
            {
                // Complete the service deferral.
                this.backgroundTaskDeferral.Complete();
            }
        }

        internal struct FileProperty
        {
            public string LocalizedName { get; set; }
            public object Value { get; set; }
        }
    }
}
