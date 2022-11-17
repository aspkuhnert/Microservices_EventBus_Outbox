using BuildingBlocks.EventBus.Events;

namespace BankingService.Domain.IntegrationEvents
{
   public record TransferCreatedIntegrationEvent :
      IntegrationEvent
   {
      public int From { get; private set; }
      public int To { get; private set; }
      public decimal Amount { get; private set; }

      public TransferCreatedIntegrationEvent(int from, int to, decimal amount)
      {
         From = from;
         To = to;
         Amount = amount;
      }
   }
}