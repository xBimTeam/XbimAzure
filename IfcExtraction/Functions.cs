using System;
using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.WindowsAzure.Storage.Blob;
using XbimCloudCommon;

namespace IfcExtraction
{
    public class Functions
    {
        public static void ExtractData(
            [QueueTrigger("extractionqueue")] XbimCloudModel blobInfo,
            [Blob("images/{ModelId}{Extension}")] CloudBlockBlob input,
            [Blob("images/{ModelId}.state")] CloudBlockBlob state,
            [Blob("images/{ModelId}.extracted{Extension2}")] CloudBlockBlob output)
        {
            try
            {
                var extractor = new Extractor();
                extractor.ExtractData(blobInfo, input, state, output);
            }
            catch (Exception e)
            {
                state.WriteLine("Error: {0}", e.Message);
            }
            
        }
    }
    public static class CloudBlockBlobExtensions
    {
        public static void WriteLine(this CloudBlockBlob blob, string msg)
        {
            using (var writer = new StreamWriter(blob.OpenWrite()))
            {
                writer.WriteLine(msg);
            }
        }

        public static void WriteLine(this CloudBlockBlob blob, string format, params object[] arg)
        {
            using (var writer = new StreamWriter(blob.OpenWrite()))
            {
                writer.WriteLine(format, arg);
            }
        }
    }
}
