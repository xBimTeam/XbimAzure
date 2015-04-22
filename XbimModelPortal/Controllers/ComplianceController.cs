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
    public class ComplianceController : CloudStorageController
    {
        // GET: Compliance
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Convert()
        {
            return View();
        }

        public ActionResult Review()
        {
            return RedirectToAction("Index","Model");
        }

        public ActionResult Validate()
        {
            return View();
        }

        public ActionResult Verify()
        {
            return View();
        }

        [HttpPost]
        public async Task<ActionResult> CreateSubmission(HttpPostedFileBase file)
        {
            try
            {
                if (file == null)
                    return Json(new { uploaded = false, message = "Error: File wasn't uploaded to server." });

                //get source data and create COBieLiteUK from that.
                var ext = Path.GetExtension(file.FileName ?? "").ToLower();
                var allowed = new[] { ".ifc", ".ifczip", ".ifcxml" };
                if (!allowed.Contains(ext))
                    return Json(new { uploaded = false, message = "Error: Invalid file type." });

                var cloudModel = new XbimCloudModel { Extension = ext };
                CloudBlockBlob blob = null;
                if (ModelState.IsValid)
                {
                    if (file.ContentLength != 0)
                        blob = await UploadAndSaveBlobAsync(file, cloudModel.ModelId);

                    if (blob != null)
                    {
                        var queueMessage = new CloudQueueMessage(JsonConvert.SerializeObject(cloudModel));
                        await CobieCreationQueue.AddMessageAsync(queueMessage);
                        Trace.TraceInformation("Created queue message for ModelId {0} in queue {1}", cloudModel.ModelId, CobieCreationQueue.Name);

                        return Json(new
                        {
                            uploaded = true,
                            submission = cloudModel.ModelId + ".xlsx",
                            state = cloudModel.ModelId + ".state",
                            message = String.Format("File {0} was uploaded and saved for validation.", file.FileName)
                        }, JsonRequestBehavior.AllowGet);
                    }
                }

                return Json(new { uploaded = false, message = String.Format("Error: File {0} was uploaded but wasn't saved for processing.", file.FileName) });
        
            }
            catch (Exception e)
            {
                return Json(new { uploaded = false, message = String.Format("Error: {0}", e.Message) });
            }
        }

    }
}