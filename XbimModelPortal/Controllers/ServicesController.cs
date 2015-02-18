using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using XbimCloudCommon;
using XbimModelPortal.Models;

namespace XbimModelPortal.Controllers
{
    /// <summary>
    /// This is a pure services controler. It talks is JSON.
    /// </summary>
    public class ServicesController : Controller
    {
        #region Cloud infrastructure
        private CloudQueue _modelRequestQueue;
        private CloudQueue _dpowRequestQueue;
        private static CloudBlobContainer _modelsBlobContainer;

        public ServicesController()
        {
            InitializeStorage();

        }

        private void InitializeStorage()
        {
            // Open storage account using credentials from .cscfg file.
            var storageAccount = CloudStorageAccount.Parse(ConfigurationManager.ConnectionStrings["AzureWebJobsStorage"].ToString());

            // Get context object for working with blobs, and 
            // set a default retry policy appropriate for a web user interface.
            var blobClient = storageAccount.CreateCloudBlobClient();
            //blobClient.DefaultRequestOptions.RetryPolicy = new LinearRetry(TimeSpan.FromSeconds(3), 3);

            // Get a reference to the blob container.
            _modelsBlobContainer = blobClient.GetContainerReference("images");

            // Get context object for working with queues, and 
            // set a default retry policy appropriate for a web user interface.
            var queueClient = storageAccount.CreateCloudQueueClient();
            //queueClient.DefaultRequestOptions.RetryPolicy = new LinearRetry(TimeSpan.FromSeconds(3), 3);

            // Get a reference to the queue.
            _modelRequestQueue = queueClient.GetQueueReference("modelrequest");
            _dpowRequestQueue = queueClient.GetQueueReference("dpowrequest");
        }

        private async Task<CloudBlockBlob> UploadAndSaveBlobAsync(HttpPostedFileBase postedFile, string id)
        {
            Trace.TraceInformation("Uploading image file {0}", postedFile.FileName);

            string blobName = id + Path.GetExtension(postedFile.FileName);
            // Retrieve reference to a blob. 
            CloudBlockBlob imageBlob = _modelsBlobContainer.GetBlockBlobReference(blobName);
            // Create the blob by uploading a local file.
            using (var fileStream = postedFile.InputStream)
            {
                await imageBlob.UploadFromStreamAsync(fileStream);
            }

            Trace.TraceInformation("Uploaded image file to {0}", imageBlob.Uri.ToString());

            return imageBlob;
        }
        #endregion

        public ActionResult IsModelReady(string model)
        {
            var state = "NO_MODEL";
            InitializeStorage();
            CloudBlockBlob modelBlob = _modelsBlobContainer.GetBlockBlobReference(model);

            //check if file exists as a IFC
            if (modelBlob.Exists())
                state = "READY";

            //check if it is in process
            //check if wexbim file is available

            return Json(new ModelStateResponse()
            {
                ModelName = model,
                State = state
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult GetData(string model)
        {
            // Get reference to blob (binary content)
            CloudBlockBlob blockBlob = _modelsBlobContainer.GetBlockBlobReference(model);

            if (!blockBlob.Exists())
                return Json(new ModelStateResponse()
                {
                    ModelName = model,
                    State = "NO_MODEL"
                }, JsonRequestBehavior.AllowGet);

            var ext = Path.GetExtension(model).ToLower();
            var mime = "application/octet-stream";
            switch (ext)
            {
                case ".wexbim":
                    mime = "application/octet-stream";
                    break;
                case ".json":
                    mime = "application/json";
                    break;
            }

            var stream = new MemoryStream();
            blockBlob.DownloadToStream(stream);
            var bytes = stream.ToArray();
            return File(bytes, mime);

        }

        [HttpPost]
        public async Task<ActionResult> UploadIFC(HttpPostedFileBase ifcFile)
        {
            if (ifcFile == null)
                return Json(new ModelStateResponse()
                {
                    State = "ERROR",
                    Message = "Failed to upload IFC model."
                }, JsonRequestBehavior.AllowGet);

            var cloudModel = new XbimCloudModel { Extension = Path.GetExtension(ifcFile.FileName) };
            CloudBlockBlob modelBlob = null;

            if (ModelState.IsValid)
            {
                if (ifcFile.ContentLength != 0)
                    modelBlob = await UploadAndSaveBlobAsync(ifcFile, cloudModel.ModelId);

                if (modelBlob != null)
                {
                    var queueMessage = new CloudQueueMessage(JsonConvert.SerializeObject(cloudModel));
                    await _modelRequestQueue.AddMessageAsync(queueMessage);
                    Trace.TraceInformation("Created queue message for ModelId {0}", cloudModel.ModelId);

                    return Json(new ModelStateResponse()
                    {
                        ModelName = cloudModel.ModelId,
                        State = "UPLOADED",
                        COBieName = cloudModel.ModelId + ".json",
                        WexBIMName = cloudModel.ModelId + ".wexBIM"
                    }, JsonRequestBehavior.AllowGet);
                }
            }
            return Json(new ModelStateResponse()
            {
                ModelName = ifcFile.FileName,
                State = "ERROR",
                Message = "Invalid model state"
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public async Task<ActionResult> UploadDPoW(HttpPostedFileBase dpowFile)
        {
            if (dpowFile == null)
                return Json(new ModelStateResponse()
                {
                    State = "ERROR",
                    Message = "Failed to upload IFC model."
                }, JsonRequestBehavior.AllowGet);

            var cloudModel = new XbimCloudModel {Extension = Path.GetExtension(dpowFile.FileName)};
            CloudBlockBlob modelBlob = null;

            if (ModelState.IsValid)
            {
                if (dpowFile.ContentLength != 0)
                    modelBlob = await UploadAndSaveBlobAsync(dpowFile, cloudModel.ModelId);

                if (modelBlob != null)
                {
                    var queueMessage = new CloudQueueMessage(JsonConvert.SerializeObject(cloudModel));
                    await _dpowRequestQueue.AddMessageAsync(queueMessage);
                    Trace.TraceInformation("Created queue message for model {0}", cloudModel.ModelId);

                    return Json(new ModelStateResponse()
                    {
                        ModelName = cloudModel.ModelId,
                        State = "UPLOADED",
                        COBieName = cloudModel.ModelId + ".json"
                    }, JsonRequestBehavior.AllowGet);
                }
            }
            return Json(new ModelStateResponse()
            {
                ModelName = dpowFile.FileName,
                State = "ERROR",
                Message = "Invalid model state"
            }, JsonRequestBehavior.AllowGet);
        }

    }
}