using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;
using XbimCloudCommon;
using XbimModelPortal.Models;

namespace XbimModelPortal.Controllers
{
    public class ValidationController : Controller
    {
         #region Cloud infrastructure
        private CloudQueue _cobieValidationQueue;
        private static CloudBlobContainer _modelsBlobContainer;

        public ValidationController()
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
            _modelsBlobContainer.CreateIfNotExists();

            // Get context object for working with queues, and 
            // set a default retry policy appropriate for a web user interface.
            var queueClient = storageAccount.CreateCloudQueueClient();
            //queueClient.DefaultRequestOptions.RetryPolicy = new LinearRetry(TimeSpan.FromSeconds(3), 3);

            // Get a reference to the queue.
            _cobieValidationQueue = queueClient.GetQueueReference("cobievalidationqueue");
            _cobieValidationQueue.CreateIfNotExists();
        }

        private async Task<CloudBlockBlob> UploadAndSaveBlobAsync(HttpPostedFileBase postedFile, string id)
        {
            Trace.TraceInformation("Uploading image file {0}", postedFile.FileName);

            string blobName = id + Path.GetExtension(postedFile.FileName);
            // Retrieve reference to a blob. 
            CloudBlockBlob blob = _modelsBlobContainer.GetBlockBlobReference(blobName);
            // Create the blob by uploading a local file.
            using (var fileStream = postedFile.InputStream)
            {
                await blob.UploadFromStreamAsync(fileStream);
            }

            Trace.TraceInformation("Uploaded image file to {0}", blob.Uri.ToString());

            return blob;
        }
        #endregion


        // GET: Validation
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult IsModelReady(string model)
        {
            var state = "NO_MODEL";
            InitializeStorage();
            CloudBlockBlob modelBlob = _modelsBlobContainer.GetBlockBlobReference(model);

            //check if file exists
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
        public ActionResult GetData(string model, string name = null)
        {
            // Get reference to blob (binary content)
            CloudBlockBlob blockBlob = _modelsBlobContainer.GetBlockBlobReference(model);

            if (!blockBlob.Exists())
                return Json(new ModelStateResponse()
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

        public async Task<ActionResult> ValidateCobieFile(HttpPostedFileBase file)
        {
            if(file == null)
                return Json(new { uploaded = false, message = "File wasn't uploaded to server." });

            //get source data and create COBieLiteUK from that.
            var ext = Path.GetExtension(file.FileName ?? "").ToLower();
            var allowed = new[] { ".ifc",".ifczip",".ifcxml",".xls",".xlsx",".xml",".json" };
            if(!allowed.Contains(ext))
                return Json(new { uploaded = false, message = "Invalid file type." });
            
            var cloudModel = new XbimCloudModel { Extension = ext };
            CloudBlockBlob blob = null;
            if (ModelState.IsValid)
            {
                if (file.ContentLength != 0)
                    blob = await UploadAndSaveBlobAsync(file, cloudModel.ModelId);

                if (blob != null)
                {
                    var queueMessage = new CloudQueueMessage(JsonConvert.SerializeObject(cloudModel));
                    await _cobieValidationQueue.AddMessageAsync(queueMessage);
                    Trace.TraceInformation("Created queue message for ModelId {0}", cloudModel.ModelId);

                    return Json(new {
                        uploaded = true,
                        modelId = cloudModel.ModelId,
                        report = cloudModel.ModelId + ".txt",
                        state = cloudModel.ModelId + ".state",
                        fixedCobie = cloudModel.ModelId + ".fixed.xlsx",
                        message = String.Format("File {0} was uploaded and saved for validation.", file.FileName)
                    }, JsonRequestBehavior.AllowGet);
                }
            }

            return Json(new {uploaded = false, message = String.Format("File {0} was uploaded but wasn't saved for processing.", file.FileName)});
        }
    }
}
