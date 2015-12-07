using System;
using Microsoft.Azure.WebJobs;

namespace IfcExtraction
{
    // To learn more about Microsoft Azure WebJobs SDK, please see http://go.microsoft.com/fwlink/?LinkID=320976
    internal class Program
    {
        // Please set the following connection strings in app.config for this WebJob to run:
        // AzureWebJobsDashboard and AzureWebJobsStorage
        public static void Main()
        {
            //set configuration of the host (http://azure.microsoft.com/en-us/documentation/articles/websites-dotnet-webjobs-sdk-storage-queues-how-to/#config)
            var config = new JobHostConfiguration();
            //limit of paralell executions (default is 16)
            config.Queues.BatchSize = 16;
            //limit of retries befor message goes to poisoned queue (default is 5)
            config.Queues.MaxDequeueCount = 3;
            //maximum time to check the queue again if it is empty. (default is 1 minute!)
            config.Queues.MaxPollingInterval = TimeSpan.FromSeconds(2);

            //start host with configuration
            var host = new JobHost(config);
            // The following code ensures that the WebJob will be running continuously
            host.RunAndBlock();
        }
    }
}
