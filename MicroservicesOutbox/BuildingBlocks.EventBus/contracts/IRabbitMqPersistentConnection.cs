using RabbitMQ.Client;

namespace BuildingBlocks.EventBus
{
    public interface IRabbitMqPersistentConnection
     : IDisposable
    {
        bool IsConnected { get; }

        bool TryConnect();

        IModel CreateModel();
    }
}