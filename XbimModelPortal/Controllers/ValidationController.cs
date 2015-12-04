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
    public class ValidationController : CloudStorageController
    {
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
                    await CobieValidationQueue.AddMessageAsync(queueMessage);
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
