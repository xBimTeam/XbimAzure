using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage;
using System.Configuration;
using System.Diagnostics;

namespace XbimModelPortalCleanUp
{
    public class Functions
    {
        // This function will be triggered based on the schedule you have set for this WebJob
        // This function will enqueue a message on an Azure Queue called queue
        [NoAutomaticTrigger]
        public static void CleanUp()
        {
            try
            {
                var blobs = _modelsBlobContainer.ListBlobs(null, true, BlobListingDetails.All);
                foreach (var lblob in blobs)
                {
                    var blob = lblob as CloudBlockBlob;
                    try
                    {
                        if (blob == null) continue;
                        var lastMod = blob.Properties.LastModified ?? DateTimeOffset.UtcNow;
                        if (DateTimeOffset.UtcNow.UtcDateTime - lastMod.UtcDateTime > TimeSpan.FromDays(1))
                        {
                            //if blob is older than a day we can delete it
                            blob.Delete();
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(String.Format("Couldn't delete blob {0}. Message: {1}\n", blob != null ? blob.Name : "No blob", e.Message));
                    }
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(String.Format("Couldn't delete blobs Message: {0}\n", exception.Message));
            }
            
        }

        #region Cloud infrastructure

        private static CloudBlobContainer _modelsBlobContainer;

        static Functions()
        {
            InitializeStorage();
        }

        private static void InitializeStorage()
        {
            // Open storage account using credentials from .cscfg file.
            var storageAccount =
                CloudStorageAccount.Parse(ConfigurationManager.ConnectionStrings["AzureWebJobsStorage"].ToString());

            // Get context object for working with blobs, and 
            // set a default retry policy appropriate for a web user interface.
            var blobClient = storageAccount.CreateCloudBlobClient();
            //blobClient.DefaultRequestOptions.RetryPolicy = new LinearRetry(TimeSpan.FromSeconds(3), 3);

            // Get a reference to the blob container.
            _modelsBlobContainer = blobClient.GetContainerReference("images");
        }

        #endregion
    }
}
