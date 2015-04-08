using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.WindowsAzure.Storage.Blob;
using Xbim.CobieLiteUK.Validation;
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
            if (input == null) return;

            Facility facility = null;
            string msg = null;
            var log = new StringWriter();
            switch (blobInfo.Extension)
            {
                case ".ifc":
                case ".ifczip":
                case ".ifcxml":
                    facility = GetFacilityFromIfc(input, blobInfo.Extension);
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

            state.WriteLine("Fixed COBie data model created. Model will be serialized to XLSX according to BS 1192-4.");
            using (var ms = fixedCobie.OpenWrite())
            {
                facility.WriteCobie(ms, ExcelTypeEnum.XLSX, out msg);
                fixedCobie.Properties.ContentType = ".xlsx";
                ms.Close();
            }
            state.WriteLine("Processing finished");

        }

        

        private static Facility GetFacilityFromIfc(Stream file, string extension)
        {
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
                    model.CreateFrom(temp, null, null, true);
                    var facilities = new List<Facility>();
                    var ifcToCoBieLiteUkExchanger = new IfcToCOBieLiteUkExchanger(model, facilities);
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
