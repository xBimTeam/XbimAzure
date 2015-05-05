using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace XbimModelPortal
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
#if !LOCAL
            InitializeStorage();
            WakeUpWebJobs();
#endif

        }

        private void WakeUpWebJobs()
        {
            const string websiteName = "openbim";
            const string webjobName = "XbimModelPortalWebJob";

            //get username and password from the file which is not under source control
            var assembly = Assembly.GetExecutingAssembly();
            var names = assembly.GetManifestResourceNames();
            var pswFileName = names.FirstOrDefault(n => n.EndsWith("openbim.user"));
#if DEBUG
            throw new Exception("There is no configuration file usable to wake up webjobs");
#endif
            if(pswFileName == null) return;

            using (var stream = assembly.GetManifestResourceStream(pswFileName))
            {
                if(stream == null) return;
                
                using (var reader = new StreamReader(stream))
                {
                    var line = reader.ReadLine();
                    if (line == null) return;
                    var parts = line.Split(':');
                    var userName = parts[0];
                    var userPwd = parts[1];
                    var webjobUrl = string.Format("https://{0}.scm.azurewebsites.net/api/continuouswebjobs/{1}", websiteName, webjobName);

                    //if we get the webjob state it will wake it up even if it was sleeping before.
                    GetWebjobState(webjobUrl, userName, userPwd);
                }
            }

        }

        private static JObject GetWebjobState(string webjobUrl, string userName, string userPwd)
        {
            HttpClient client = new HttpClient();
            string auth = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(userName + ':' + userPwd));
            client.DefaultRequestHeaders.Add("authorization", auth);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var data = client.GetStringAsync(webjobUrl).Result;
            var result = JsonConvert.DeserializeObject(data) as JObject;
            return result;
        }

        private void InitializeStorage()
        {
            // Open storage account using credentials from .cscfg file.
            var storageAccount = CloudStorageAccount.Parse
                (ConfigurationManager.ConnectionStrings["AzureWebJobsStorage"].ToString());

            Trace.TraceInformation("Creating model blob container");
            var blobClient = storageAccount.CreateCloudBlobClient();
            var modelsBlobContainer = blobClient.GetContainerReference("models");
            if (modelsBlobContainer.CreateIfNotExists())
            {
                // Enable public access on the newly created "images" container.
                modelsBlobContainer.SetPermissions(
                    new BlobContainerPermissions
                    {
                        PublicAccess = BlobContainerPublicAccessType.Blob
                    });
            }

            Trace.TraceInformation("Creating queues");
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
            var blobnameQueue = queueClient.GetQueueReference("modelrequest");
            blobnameQueue.CreateIfNotExists();

            Trace.TraceInformation("Storage initialized");
        }
    }
}
