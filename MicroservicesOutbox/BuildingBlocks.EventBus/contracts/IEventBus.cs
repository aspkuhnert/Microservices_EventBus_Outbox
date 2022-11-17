using BuildingBlocks.EventBus.Events;

namespace BuildingBlocks.EventBus
{
    public interface IEventBus
    {
        void Publish(IntegrationEvent @event);

        void Subscribe<TIntegrationEvent, TIntegrationEventHandler>()
              where TIntegrationEvent : IntegrationEvent
              where TIntegrationEventHandler : IIntegrationEventHandler<TIntegrationEvent>;
    }
}