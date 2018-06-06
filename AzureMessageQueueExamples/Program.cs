using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.ServiceBus;

namespace AzureMessageQueueExamples
{
    // To learn more about Microsoft Azure WebJobs SDK, please see https://go.microsoft.com/fwlink/?LinkID=320976
    internal class Program
    {
        // Please set the following connection strings in app.config for this WebJob to run:
        // AzureWebJobsDashboard and AzureWebJobsStorage
        private static void Main()
        {
            var config = new JobHostConfiguration();

            if (config.IsDevelopment) config.UseDevelopmentSettings();

            var serviceBusConfig = new ServiceBusConfiguration
            {
                ConnectionString = AmbientConnectionStringProvider.Instance.GetConnectionString(ConnectionStringNames.ServiceBus)
            };
            config.UseServiceBus(serviceBusConfig);

            var host = new JobHost(config);

            // Invoke custom functions, blob queue trigger and service bus trigger will be invoked via WebJob SDK.
            host.Call(typeof(Functions).GetMethod("ProcessRedisPubSubMessage"));
            host.CallAsync(typeof(Functions).GetMethod("ProcessRedisQueueMessage"));

            // The following code ensures that the WebJob will be running continuously
            host.RunAndBlock();
        }
    }
}