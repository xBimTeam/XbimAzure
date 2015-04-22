using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using XbimModelPortal.Models;

namespace XbimModelPortal.Controllers
{
    public abstract class CloudStorageController : Controller
    {
         #region Cloud infrastructure
        protected static CloudQueue ModelRequestQueue;
        protected static CloudQueue DpowRequestQueue;
        protected static CloudQueue CobieValidationQueue;
        protected static CloudQueue CobieVerificationQueue;
        protected static CloudQueue CobieCreationQueue;
        protected static CloudBlobContainer ModelsBlobContainer;

        static CloudStorageController()
        {
#if !LOCAL
            InitializeStorage();
#endif
        }

        private static void InitializeStorage()
        {
            // Open storage account using credentials from .cscfg file.
            var storageAccount = CloudStorageAccount.Parse(ConfigurationManager.ConnectionStrings["AzureWebJobsStorage"].ToString());

            // Get context object for working with blobs, and 
            // set a default retry policy appropriate for a web user interface.
            var blobClient = storageAccount.CreateCloudBlobClient();
            //blobClient.DefaultRequestOptions.RetryPolicy = new LinearRetry(TimeSpan.FromSeconds(3), 3);

            // Get a reference to the blob container.
            ModelsBlobContainer = blobClient.GetContainerReference("images");
            ModelsBlobContainer.CreateIfNotExists();

            // Get context object for working with queues, and 
            // set a default retry policy appropriate for a web user interface.
            var queueClient = storageAccount.CreateCloudQueueClient();
            //queueClient.DefaultRequestOptions.RetryPolicy = new LinearRetry(TimeSpan.FromSeconds(3), 3);

            // Get a reference to the queue. Queue name must be lowercase without spaces.
            ModelRequestQueue = queueClient.GetQueueReference("modelrequest");
            DpowRequestQueue = queueClient.GetQueueReference("dpowrequest");
            CobieValidationQueue = queueClient.GetQueueReference("cobievalidationqueue");
            CobieVerificationQueue = queueClient.GetQueueReference("cobieverificationqueue");
            CobieCreationQueue = queueClient.GetQueueReference("cobiecreationqueue");

            ModelRequestQueue.CreateIfNotExists();
            DpowRequestQueue.CreateIfNotExists();
            CobieValidationQueue.CreateIfNotExists();
            CobieVerificationQueue.CreateIfNotExists();
            CobieCreationQueue.CreateIfNotExists();
        }

        protected async Task<CloudBlockBlob> UploadAndSaveBlobAsync(HttpPostedFileBase postedFile, string id)
        {
            Trace.TraceInformation("Uploading image file {0}", postedFile.FileName);

            var blobName = id + Path.GetExtension(postedFile.FileName);
            // Retrieve reference to a blob. 
            CloudBlockBlob imageBlob = ModelsBlobContainer.GetBlockBlobReference(blobName);
            // Create the blob by uploading a local file.
            using (var fileStream = postedFile.InputStream)
            {
                await imageBlob.UploadFromStreamAsync(fileStream);
            }

            Trace.TraceInformation("Uploaded image file to {0}", imageBlob.Uri);

            return imageBlob;
        }
        #endregion

        
        public ActionResult IsModelReady(string model)
        {
            var state = "NO_MODEL";
            CloudBlockBlob modelBlob = ModelsBlobContainer.GetBlockBlobReference(model);

            //check if file exists
            if (modelBlob.Exists())
                state = "READY";

            //check if it is in process
            //check if wexbim file is available

            return Json(new ModelStateResponse
            {
                ModelName = model,
                State = state
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult GetData(string model, string name = null)
        {
            // Get reference to blob (binary content)
            CloudBlockBlob blockBlob = ModelsBlobContainer.GetBlockBlobReference(model);

            if (!blockBlob.Exists())
                return Json(new ModelStateResponse
                {
                    ModelName = model,
                    State = "NO_MODEL"
                }, JsonRequestBehavior.AllowGet);

            var ext = Path.GetExtension(model ?? "").ToLower();
            var mime = "application/octet-stream";
            switch (ext)
            {
                case ".wexbim":
                    mime = "application/octet-stream";
                    break;
                case ".json":
                    mime = "application/json";
                    break;
                case ".txt":
                case ".state":
                    mime = "text/plain";
                    break;
                case ".xls":
                    mime = "application/vnd.ms-excel";
                    break;
                case ".xlsx":
                    mime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    break;
            }

            var stream = new MemoryStream();
            blockBlob.DownloadToStream(stream);
            var bytes = stream.ToArray();
            return File(bytes, mime, (name ?? "file") + ext);
        }
    }
}