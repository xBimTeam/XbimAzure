using System;
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
    public class ExtractionController : CloudStorageController
    {
        // GET: Extraction
        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<ActionResult> Extract(HttpPostedFileBase input, string ids, bool includeGeometry, string outFormat)
        {
            if (input == null)
                return Json(new { uploaded = false, message = "File was not saved to server. Wrong input file format." });

            //get source data and create COBieLiteUK from that.
            var extension = Path.GetExtension(input.FileName ?? "").ToLower();
            var allowed = new[] { ".ifc", ".ifczip", ".ifcxml"};


            if (!allowed.Contains(extension))
                return Json(new { uploaded = false, message = "Invalid input file type." });
            if (!allowed.Contains(outFormat))
                return Json(new { uploaded = false, message = "Invalid output file type." });

            var cloudModel = new XbimCloudModel
            {
                Extension = extension, 
                CreateGeometry = includeGeometry, 
                Extension2 = outFormat, 
                Ids = ids
            };

            CloudBlockBlob blob = null;
            if (!ModelState.IsValid)
                return
                    Json(
                        new
                        {
                            uploaded = false,
                            message =
                                string.Format("File {0} was uploaded but wasn't saved for processing.", input.FileName)
                        });

            if (input.ContentLength != 0)
                blob = await UploadAndSaveBlobAsync(input, cloudModel.ModelId);

            if (blob != null)
            {
                var queueMessage = new CloudQueueMessage(JsonConvert.SerializeObject(cloudModel));
                await ExtractionQueue.AddMessageAsync(queueMessage);
                Trace.TraceInformation("Created queue message for ModelId {0}", cloudModel.ModelId);

                return Json(new
                {
                    uploaded = true,
                    modelId = cloudModel.ModelId,
                    result = cloudModel.ModelId + ".extracted" + outFormat,
                    state = cloudModel.ModelId + ".state",
                    message = "File was uploaded and saved for extraction."
                }, JsonRequestBehavior.AllowGet);
            }

            return Json(new { uploaded = false, message = String.Format("File {0} was uploaded but wasn't saved for processing.", input.FileName) });
        }
    }
}