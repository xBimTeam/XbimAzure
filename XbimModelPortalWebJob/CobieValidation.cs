using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using Microsoft.Azure.WebJobs;
using Microsoft.WindowsAzure.Storage.Blob;
using Xbim.CobieLiteUK.Validation;
using Xbim.CobieLiteUK.Validation.Reporting;
using Xbim.COBieLiteUK;
using Xbim.IO;
using XbimCloudCommon;
using XbimExchanger.IfcToCOBieLiteUK;

namespace XbimModelPortalWebJob
{
    public class CobieValidation
    {
        public static void ValidateCobie(
           [QueueTrigger("cobievalidationqueue")] XbimCloudModel blobInfo,
           [Blob("images/{ModelId}{Extension}", FileAccess.Read)] Stream input,
           [Blob("images/{ModelId}.txt")] CloudBlockBlob validationReport,
           [Blob("images/{ModelId}.state")] CloudBlockBlob state,
           [Blob("images/{ModelId}.fixed.xlsx")] CloudBlockBlob fixedCobie)
        {
            try
            {
                if (input == null) return;

                Facility facility = null;
                string msg = null;
                var log = new StringWriter();
                switch (blobInfo.Extension)
                {
                    case ".ifc":
                    case ".ifczip":
                    case ".ifcxml":
                        facility = GetFacilityFromIfc(input, blobInfo.Extension, state);
                        break;
                    case ".json":
                        facility = Facility.ReadJson(input);
                        break;
                    case ".xml":
                        facility = Facility.ReadXml(input);
                        break;
                    case ".xls":
                        facility = Facility.ReadCobie(input, ExcelTypeEnum.XLS, out msg);
                        break;
                    case ".xlsx":
                        facility = Facility.ReadCobie(input, ExcelTypeEnum.XLSX, out msg);
                        break;
                }

                state.WriteLine("COBie data parsed. Created computable model.");

                if (facility == null)
                {
                    return;
                }

                if (msg != null)
                    log.Write(msg);

                facility.ValidateUK2012(log, true);
                state.WriteLine("COBie data validated. Validation report is being created.");
                using (var logStream = validationReport.OpenWrite())
                {
                    using (var writer = new StreamWriter(logStream))
                    {
                        writer.Write(log.ToString());
                        writer.Flush();
                        writer.Close();
                    }
                }

                state.WriteLine("Fixed COBie data model created. Model will be serialized to XLSX in accordance with BS 1192-4.");
                using (var ms = fixedCobie.OpenWrite())
                {
                    facility.WriteCobie(ms, ExcelTypeEnum.XLSX, out msg);
                    fixedCobie.Properties.ContentType = ".xlsx";
                    ms.Close();
                }
                state.WriteLine("Processing finished");
            }
            catch (Exception e)
            {
                state.WriteLine("Error in processing! <br />" + SecurityElement.Escape(e.Message));
            }
        }

        public static void CreateCobie(
           [QueueTrigger("cobiecreationqueue")] XbimCloudModel blobInfo,
           [Blob("images/{ModelId}{Extension}", FileAccess.Read)] Stream input,
           [Blob("images/{ModelId}.state")] CloudBlockBlob state,
           [Blob("images/{ModelId}.xlsx")] CloudBlockBlob submission)
        {
            try
            {
                if (input == null) return;

                Facility facility = null;
                string msg = null;
                var log = new StringWriter();
                switch (blobInfo.Extension.ToLower())
                {
                    case ".ifc":
                    case ".ifczip":
                    case ".ifcxml":
                        facility = GetFacilityFromIfc(input, blobInfo.Extension, state);
                        break;
                    case ".xls":
                        facility = Facility.ReadCobie(input, ExcelTypeEnum.XLS, out msg);
                        break;
                    case ".xlsx":
                        facility = Facility.ReadCobie(input, ExcelTypeEnum.XLSX, out msg);
                        break;
                }


                if (facility == null)
                {
                    state.WriteLine("Error: COBie not created.");
                    return;
                }
                state.WriteLine("COBie data parsed. Created computable model.");

                if (msg != null)
                    log.Write(msg);

                state.WriteLine("COBie data validated. Model will be saved as XLSX according to BS 1192-4.");
                using (var ms = submission.OpenWrite())
                {
                    facility.WriteCobie(ms, ExcelTypeEnum.XLSX, out msg);
                    submission.Properties.ContentType = ".xlsx";
                    ms.Close();
                }
                state.WriteLine("Processing finished");
            }
            catch (Exception e)
            {
                state.WriteLine("Error: " + SecurityElement.Escape(e.Message));
            }
        }

        public static void VerifyCobie(
           [QueueTrigger("cobieverificationqueue")] XbimCloudModel blobInfo,
           [Blob("images/{ModelId}{Extension}", FileAccess.Read)] Stream input,
           [Blob("images/{ModelId}.requirements{Extension2}", FileAccess.Read)] Stream inputRequirements,
           [Blob("images/{ModelId}.report.json")] CloudBlockBlob report,
           [Blob("images/{ModelId}.state")] CloudBlockBlob state,
           [Blob("images/{ModelId}.report.xlsx")] CloudBlockBlob reportXls)
        {

            try
            {
                if (input == null) return;

                Facility facility = null;
                string msg;
                switch (blobInfo.Extension)
                {
                    case ".ifc":
                    case ".ifczip":
                    case ".ifcxml":
                        facility = GetFacilityFromIfc(input, blobInfo.Extension, state);
                        break;
                    case ".json":
                        facility = Facility.ReadJson(input);
                        break;
                    case ".xml":
                        facility = Facility.ReadXml(input);
                        break;
                    case ".xls":
                        facility = Facility.ReadCobie(input, ExcelTypeEnum.XLS, out msg);
                        break;
                    case ".xlsx":
                        facility = Facility.ReadCobie(input, ExcelTypeEnum.XLSX, out msg);
                        break;
                }

                state.WriteLine("COBie data parsed. Created computable model.");

                Facility requirements = null;
                switch (blobInfo.Extension2)
                {
                    case ".xml":
                        requirements = Facility.ReadXml(inputRequirements);
                        break;
                    case ".json":
                        requirements = Facility.ReadJson(inputRequirements);
                        break;
                    case ".xls":
                        requirements = Facility.ReadCobie(inputRequirements, ExcelTypeEnum.XLS, out msg);
                        break;
                    case ".xlsx":
                        requirements = Facility.ReadCobie(inputRequirements, ExcelTypeEnum.XLSX, out msg);
                        break;
                }

                state.WriteLine("DPoW requirements data parsed. Created computable model.");

                if (facility == null || requirements == null)
                    return;

                var vd = new FacilityValidator();
                var validated = vd.Validate(requirements, facility);
                using (var repStream = report.OpenWrite())
                {
                    validated.WriteJson(repStream);
                    repStream.Close();
                    report.Properties.ContentType = ".json";
                }
                state.WriteLine("Structured validation report created. Excel report to be created.");

                var rep = new ExcelValidationReport();
                using (var stream = new MemoryStream())
                {
                    rep.Create(validated, stream, ExcelValidationReport.SpreadSheetFormat.Xlsx);
                    var data = stream.ToArray();
                    reportXls.UploadFromByteArray(data, 0, data.Length);
                    state.WriteLine("XLSX validation report created.");
                }

                state.WriteLine("Processing finished.");
            }
            catch (Exception e)
            {
                state.WriteLine("Error in processing! <br />" + SecurityElement.Escape(e.Message));
            }

            
        }

        private static Facility GetFacilityFromIfc(Stream file, string extension, CloudBlockBlob state)
        {
            state.WriteLine("IFC file processing started...");
            var temp = Path.GetTempPath() + Guid.NewGuid() + extension;
            try
            {
                //store temporarily
                using (var fileStream = File.OpenWrite(temp))
                {
                    file.CopyTo(fileStream);
                    fileStream.Flush();
                    fileStream.Close();
                }

                using (var model = new XbimModel())
                {
                    model.CreateFrom(temp, null, (a, b) =>
                    {
                        state.WriteLine("{0} loaded from IFC.", a);
                    }, true);
                    state.WriteLine("IFC file loaded. Converting to COBie model...");
                    var facilities = new List<Facility>();
                    var ifcToCoBieLiteUkExchanger = new IfcToCOBieLiteUkExchanger(model, facilities);
                    state.WriteLine("IFC converted to COBie model...");
                    return ifcToCoBieLiteUkExchanger.Convert().FirstOrDefault();
                }
            }
            //tidy up
            finally
            {
                if(File.Exists(temp))
                    File.Delete(temp);
            }

            
        }
    }
}
