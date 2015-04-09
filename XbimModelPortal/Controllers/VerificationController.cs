using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;
using XbimCloudCommon;

namespace XbimModelPortal.Controllers
{
    public class VerificationController : CloudStorageController
    {
        // GET: Verification
        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<ActionResult> VerifyCobieFile(HttpPostedFileBase cobie, HttpPostedFileBase dpow)
        {
            if (cobie == null || dpow == null)
                return Json(new { uploaded = false, message = "Files were not uploaded to server." });

            //get source data and create COBieLiteUK from that.
            var cobieExt = Path.GetExtension(cobie.FileName ?? "").ToLower();
            var cobieAllowed = new[] { ".ifc", ".ifczip", ".ifcxml", ".xls", ".xlsx", ".xml", ".json" };
            var dpowExt = Path.GetExtension(dpow.FileName ?? "").ToLower();
            var dpowAllowed = new[] { ".xml", ".json" };


            if (!cobieAllowed.Contains(cobieExt) || !dpowAllowed.Contains(dpowExt))
                return Json(new { uploaded = false, message = "Invalid file type." });

            var cloudModel = new XbimCloudModel { Extension = cobieExt, CreateGeometry = false, Extension2 = dpowExt};

            CloudBlockBlob cobieBlob = null;
            CloudBlockBlob dpowBlob = null;
            if (ModelState.IsValid)
            {
                if (cobie.ContentLength != 0)
                    cobieBlob = await UploadAndSaveBlobAsync(cobie, cloudModel.ModelId);
                if(dpow.ContentLength != 0)
                    dpowBlob = await UploadAndSaveBlobAsync(dpow, cloudModel.ModelId + ".requirements");

                if (cobieBlob != null && dpowBlob != null)
                {
                    var queueMessage = new CloudQueueMessage(JsonConvert.SerializeObject(cloudModel));
                    await CobieVerificationQueue.AddMessageAsync(queueMessage);
                    Trace.TraceInformation("Created queue message for ModelId {0}", cloudModel.ModelId);

                    return Json(new
                    {
                        uploaded = true,
                        modelId = cloudModel.ModelId,
                        report = cloudModel.ModelId + ".json",
                        state = cloudModel.ModelId + ".state",
                        xlsReport = cloudModel.ModelId + ".report.xlsx",
                        message = String.Format("Files were uploaded and saved for verification.")
                    }, JsonRequestBehavior.AllowGet);
                }
            }

            return Json(new {uploaded = false, message = String.Format("File {0} was uploaded but wasn't saved for processing.", cobie.FileName)});
        }
    }
}