using System;
using System.Configuration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using log4net.Config;
using Microsoft.Azure.WebJobs;
using StackExchange.Redis;

namespace AzureMessageQueueExamples
{
    public class Functions
    {
        private const string ChannelName = "TestPubSub";
        private const string KeyName = "TestKey";
        // Azure queue name can contain only lower case letters, numbers, and hyphens.
        private const string BlobQueueName = "testblobqueue";
        private const string SbQueueName = "testsbqueue";

        private static readonly string RedisConnectionString =
            ConfigurationManager.ConnectionStrings["RedisConnection"]?.ConnectionString;

        private static readonly Lazy<ConnectionMultiplexer> LazyRedis = new Lazy<ConnectionMultiplexer>(() =>
        {
            if (string.IsNullOrEmpty(RedisConnectionString))
                throw new Exception("Redis connection string is missing.");
            return ConnectionMultiplexer.Connect(RedisConnectionString);
        });

        private static readonly Lazy<ILog> LazyLogger = new Lazy<ILog>(() =>
        {
            var log = LogManager.GetLogger(typeof(Functions));
            XmlConfigurator.Configure();
            return log;
        });

        private static readonly ConnectionMultiplexer Redis = LazyRedis.Value;
        private static readonly Lazy<ISubscriber> LazySubscriber = new Lazy<ISubscriber>(() => Redis.GetSubscriber());
        private static readonly ISubscriber Subscriber = LazySubscriber.Value;
        private static readonly ILog Logger = LazyLogger.Value;
        private static readonly Lazy<IDatabase> LazyRedisDb = new Lazy<IDatabase>(() => Redis.GetDatabase());
        private static readonly IDatabase RedisDb = LazyRedisDb.Value;

        /// <summary>
        ///     Original design by other engineer.
        ///     When Azure scale out the 'App Service' instances, this design occurs duplicate message processing.
        ///     It doesnt work under colud architecture.
        /// </summary>
        [Singleton("PubSubLock", SingletonScope.Host)]
        [NoAutomaticTrigger]
        public static void ProcessRedisPubSubMessage()
        {
            Subscriber.Subscribe(ChannelName, (channel, value) => { Logger.Info($"RedisPubSub: {value}"); });
        }

        [Singleton("SortedSetLock", SingletonScope.Host)] //Use this for singleton queue worker
        [NoAutomaticTrigger]
        public static async Task ProcessRedisQueueMessage()
        {
            while (true)
            {
                var score = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
                var values = await RedisDb.SortedSetRangeByScoreAsync(KeyName, 0, score);
                await RedisDb.SortedSetRemoveRangeByScoreAsync(KeyName, 0, score);
                if (!values.Any())
                {
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                    continue;
                }
                foreach (var value in values)
                {
                    Logger.Info($"RedisSortedSet: {value}"); // Invoke other workers to process message
                }
            }
            // ReSharper disable once FunctionNeverReturns
        }

        /// <summary>
        ///     Blob queue is a simple architecture for message queue, no need singleton.
        ///     Reference: https://github.com/Azure/azure-webjobs-sdk/wiki/Queues
        /// </summary>
        /// <param name="queueMessage"></param>
        public static void ProcessBlobQueueMessage([QueueTrigger(BlobQueueName)] string queueMessage)
        {
            if (!string.IsNullOrEmpty(queueMessage))
            {
                Logger.Info($"BlobQueue: {queueMessage}");
            }
        }

        /// <summary>
        ///     Service bus have better performance than blob queue.
        ///     Reference: https://github.com/Azure/azure-webjobs-sdk-samples/tree/master/BasicSamples/ServiceBus
        /// </summary>
        /// <param name="queueMessage"></param>
        public static void ProcessServiceBusMessage([ServiceBusTrigger(SbQueueName)] string queueMessage)
        {
            if (!string.IsNullOrEmpty(queueMessage))
            {
                Logger.Info($"SbQueue: {queueMessage}");
            }
        }
    }
}