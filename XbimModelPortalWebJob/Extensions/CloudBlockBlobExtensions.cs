using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

// ReSharper disable once CheckNamespace
namespace Microsoft.WindowsAzure.Storage.Blob
{
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
