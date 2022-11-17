using BuildingBlocks.EventBus.Events;

namespace BankingService.Domain.IntegrationEvents
{
    public interface IBankingIntegrationEventService
    {
        Task PublishEventsThroughEventBusAsync(Guid transactionId);

        Task AddAndSaveEventAsync(IntegrationEvent evt);
    }
}