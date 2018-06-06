using System;
using System.Configuration;
using System.Text;
using Microsoft.Azure.ServiceBus;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using StackExchange.Redis;

namespace MessageSender
{
    internal class Program
    {
        private const string ChannelName = "TestPubSub";
        private const string KeyName = "TestKey";
        // queue name can contain only lowercase letters, numbers, and hyphens
        private const string BlobQueueName = "testblobqueue";
        private const string SbQueueName = "testsbqueue";

        private static readonly string RedisConnectionString =
            ConfigurationManager.ConnectionStrings["RedisConnection"]?.ConnectionString;

        private static readonly string StorageConnectionString =
            ConfigurationManager.ConnectionStrings["StorageConnection"]?.ConnectionString;

        private static readonly string ServiceBusConnectionString =
            ConfigurationManager.ConnectionStrings["ServiceBusConnection"]?.ConnectionString;

        private static readonly Lazy<ConnectionMultiplexer> LazyRedis = new Lazy<ConnectionMultiplexer>(() =>
        {
            if (string.IsNullOrEmpty(RedisConnectionString))
                throw new Exception("Redis connection string is missing.");
            return ConnectionMultiplexer.Connect(RedisConnectionString);
        });

        private static readonly ConnectionMultiplexer Redis = LazyRedis.Value;
        private static readonly Lazy<ISubscriber> LazySubscriber = new Lazy<ISubscriber>(() => Redis.GetSubscriber());
        private static readonly ISubscriber Subscriber = LazySubscriber.Value;
        private static readonly Lazy<IDatabase> LazyRedisDb = new Lazy<IDatabase>(() => Redis.GetDatabase());
        private static readonly IDatabase RedisDb = LazyRedisDb.Value;

        private static readonly Lazy<CloudStorageAccount> LazyStorage = new Lazy<CloudStorageAccount>(() =>
        {
            if (string.IsNullOrEmpty(StorageConnectionString))
                throw new Exception("Storage connection string is missing.");
            return CloudStorageAccount.Parse(StorageConnectionString);
        });

        private static readonly CloudStorageAccount Storage = LazyStorage.Value;

        private static readonly Lazy<CloudQueueClient> LazyQueueClient =
            new Lazy<CloudQueueClient>(() => Storage.CreateCloudQueueClient());

        private static readonly CloudQueueClient QueueClient = LazyQueueClient.Value;

        private static readonly Lazy<QueueClient> LazySbQueue = new Lazy<QueueClient>(() =>
        {
            if (string.IsNullOrEmpty(ServiceBusConnectionString))
                throw new Exception("Service bus connection string is missing.");
            return new QueueClient(ServiceBusConnectionString, SbQueueName);
        });

        private static readonly QueueClient SbQueue = LazySbQueue.Value;

        private static void Main()
        {
            while (true)
            {
                var input = Console.ReadLine();
                if (string.IsNullOrEmpty(input)) continue;

                Subscriber.Publish(ChannelName, input);
                RedisDb.SortedSetAdd(KeyName, input, new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds());

                // Reference: https://docs.microsoft.com/zh-tw/azure/storage/queues/storage-dotnet-how-to-use-queues
                var queue = QueueClient.GetQueueReference(BlobQueueName);
                queue.CreateIfNotExists();
                var message = new CloudQueueMessage(input);
                queue.AddMessage(message);

                // Reference: https://docs.microsoft.com/en-us/azure/service-bus-messaging/service-bus-dotnet-get-started-with-queues
                SbQueue.SendAsync(new Message(Encoding.UTF8.GetBytes(input)));
            }

            // ReSharper disable once FunctionNeverReturns
        }
    }
}