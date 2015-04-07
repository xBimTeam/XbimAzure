using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using Microsoft.WindowsAzure.Storage.Blob;
using XbimCloudCommon;
using Microsoft.Azure.WebJobs;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;
using Xbim.CobieLiteUK.Validation;
using Xbim.IO;
using XbimGeometry.Interfaces;
using Xbim.ModelGeometry.Scene;
using Xbim.DPoW;
using XbimExchanger.DPoWToCOBieLiteUK;
using XbimExchanger.IfcToCOBieLiteUK;

namespace XbimModelPortalWebJob
{
    public class Functions
    {
        // This function will get triggered/executed when a new message is written 
        // on an Azure Queue called queue.
        //public static void ProcessQueueMessage([QueueTrigger("queue")] string message, TextWriter log)
        //{
        //    log.WriteLine(message);
        //}

        public static void GenerateModel(
            [QueueTrigger("modelrequest")] XbimCloudModel blobInfo,
            [Blob("images/{ModelId}{Extension}", FileAccess.Read)] Stream input,
            [Blob("images/{ModelId}.requirements.json", FileAccess.Read)] Stream requirements,
            [Blob("images/{ModelId}.wexBIM")] CloudBlockBlob outputWexbimBlob,
            [Blob("images/{ModelId}.validation.json")] CloudBlockBlob outputValidationBlob,
            [Blob("images/{ModelId}.json")] CloudBlockBlob outputCobieBlob)
        {
            if (input == null || requirements == null) return;

            var facility = ConvertIfcToWexbimAndCobie(input, outputWexbimBlob, outputCobieBlob, blobInfo.Extension);
            outputWexbimBlob.Properties.ContentType = ".wexBIM";
            outputCobieBlob.Properties.ContentType = ".json";

            //do validation (validation file should be there already)
            var vd = new FacilityValidator();
            var req = Xbim.COBieLiteUK.Facility.ReadJson(requirements);
            var validated = vd.Validate(req, facility);

            using (var ms = new MemoryStream())
            {
                validated.WriteJson(ms);
                var bytes = ms.ToArray();
                outputValidationBlob.UploadFromByteArray(bytes, 0, bytes.Length);
                outputValidationBlob.Properties.ContentType = ".json"; 
                ms.Close();
            }
        }

        public static void ConvertDpowToCobie(Stream input, CloudBlockBlob outputCobieBlob)
        {
            var temp = Path.GetTempFileName();
            try
            {
                var dpow = PlanOfWork.OpenJson(input);
                var facility = new Xbim.COBieLiteUK.Facility();
                var exchanger = new DPoWToCOBieLiteUKExchanger(dpow, facility);
                exchanger.Convert();

                using (var tw = File.Create(temp))
                {
                    facility.WriteJson(tw);
                    tw.Close();
                }
                outputCobieBlob.UploadFromFile(temp, FileMode.Open);
            }
            finally
            {
                //tidy up
                if (File.Exists(temp)) File.Delete(temp);
            }
           
        }

        public static Xbim.COBieLiteUK.Facility ConvertIfcToWexbimAndCobie(Stream input, CloudBlockBlob outputWexbimBlob, CloudBlockBlob outputCobieBlob, string inputExtension)
        {
            //temp files 
            var fileName = Path.GetTempPath() + Guid.NewGuid() + inputExtension;
            var xbimFileName = Path.ChangeExtension(fileName, ".xBIM");
            var wexBimFileName = Path.ChangeExtension(fileName, ".wexBIM");
            var cobieFileName = Path.ChangeExtension(fileName, ".json");
            try
            {
                Xbim.COBieLiteUK.Facility facility = null;
                using (var fileStream = File.OpenWrite(fileName))
                {
                    input.CopyTo(fileStream);
                    fileStream.Flush();
                    fileStream.Close();
                    //open the model and import
                    using (var model = new XbimModel())
                    {
                        model.CreateFrom(fileName, xbimFileName, null, true);
                        var m3DModelContext = new Xbim3DModelContext(model);

                        using (var wexBimFile = new FileStream(wexBimFileName, FileMode.Create))
                        {
                            using (var bw = new BinaryWriter(wexBimFile))
                            {
                                m3DModelContext.CreateContext(XbimGeometryType.PolyhedronBinary);
                                m3DModelContext.Write(bw);
                                bw.Close();
                                wexBimFile.Close();
                                outputWexbimBlob.UploadFromFile(wexBimFileName, FileMode.Open);
                            }
                        }

                        using (var cobieFile = new FileStream(cobieFileName, FileMode.Create))
                        {
                            var facilities = new List<Xbim.COBieLiteUK.Facility>();
                            var ifcToCoBieLiteUkExchanger = new IfcToCOBieLiteUkExchanger(model, facilities);
                            facilities = ifcToCoBieLiteUkExchanger.Convert();
                            facility = facilities.FirstOrDefault();
                            if (facility != null)
                            {
                                facility.WriteJson(cobieFile);
                                cobieFile.Close();
                                outputCobieBlob.UploadFromFile(cobieFileName, FileMode.Open);
                            }
                        }

                        model.Close();
                    }
                }
                return facility;
            }
            finally
            {
                //tidy up
                if (File.Exists(fileName)) File.Delete(fileName);
                if (File.Exists(xbimFileName)) File.Delete(xbimFileName);
                if (File.Exists(wexBimFileName)) File.Delete(wexBimFileName);
                if (File.Exists(cobieFileName)) File.Delete(cobieFileName);
            }
        }

    }
}
