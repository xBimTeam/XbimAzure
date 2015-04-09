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
using Newtonsoft.Json.Linq;
using XbimCloudCommon;
using XbimModelPortal.Models;

namespace XbimModelPortal.Controllers
{
    /// <summary>
    /// This is a pure services controler. It talks is JSON.
    /// </summary>
    public class ModelController : CloudStorageController
    {
        [HttpPost]
        public async Task<ActionResult> UploadModelAndRequirements(HttpPostedFileBase ifcfile, HttpPostedFileBase dpowfile)
        {
            if (ifcfile == null || dpowfile == null)
                return Json( new ModelStateResponse{State =  "ERROR", Message = "Both files have to be defined"});
            
            var ifcCloudModel = new XbimCloudModel { Extension = Path.GetExtension(ifcfile.FileName) };
            CloudBlockBlob ifcModelBlob = null;
            CloudBlockBlob reqModelBlob = null;

            if (ModelState.IsValid)
            {
                //upload requirements
                if (dpowfile.ContentLength != 0)
                    reqModelBlob = await UploadAndSaveBlobAsync(dpowfile, ifcCloudModel.ModelId + ".requirements");

                //upload IFC and let it be processed by the web job
                if (ifcfile.ContentLength != 0)
                    ifcModelBlob = await UploadAndSaveBlobAsync(ifcfile, ifcCloudModel.ModelId);

                if (ifcModelBlob != null && reqModelBlob != null)
                {
                    var queueMessage = new CloudQueueMessage(JsonConvert.SerializeObject(ifcCloudModel));
                    await ModelRequestQueue.AddMessageAsync(queueMessage);
                    Trace.TraceInformation("Created queue message for ModelId {0}", ifcCloudModel.ModelId);

                    return Json(new ModelStateResponse()
                    {
                        ModelName = ifcCloudModel.ModelId,
                        State = "UPLOADED",
                        COBieName = ifcCloudModel.ModelId + ".json",
                        WexBIMName = ifcCloudModel.ModelId + ".wexBIM",
                        ValidationCOBieName = ifcCloudModel.ModelId + ".validation.json"
                    }, JsonRequestBehavior.AllowGet);
                }
            }
            return Json(new ModelStateResponse()
            {
                ModelName = ifcfile.FileName,
                State = "ERROR",
                Message = "Invalid model state"
            }, JsonRequestBehavior.AllowGet);
        }

        public ActionResult Index()
        {
            return View();
        }
    }
}