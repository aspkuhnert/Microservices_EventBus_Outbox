using BankingService.Database;
using BuildingBlocks.EventBus;
using BuildingBlocks.EventBus.Events;
using BuildingBlocks.IntegrationEventLog;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace BankingService.Domain.IntegrationEvents
{
   public class BankingIntegrationEventService :
      IBankingIntegrationEventService
   {
      private readonly Func<DbConnection, IIntegrationEventLogService> _integrationEventLogServiceFactory;
      private readonly IEventBus _eventBus;
      private readonly BankingContext _context;
      private IIntegrationEventLogService _eventLogService;
      private readonly ILogger<BankingIntegrationEventService> _logger;

      public BankingIntegrationEventService(
         Func<DbConnection, IIntegrationEventLogService> integrationEventLogServiceFactory,
         IEventBus eventBus,
         BankingContext context,
         ILogger<BankingIntegrationEventService> logger)
      {
         _context = context ?? throw new ArgumentNullException(nameof(context));
         _integrationEventLogServiceFactory = integrationEventLogServiceFactory ?? throw new ArgumentNullException(nameof(integrationEventLogServiceFactory));
         _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
         _eventLogService = _integrationEventLogServiceFactory(_context.Database.GetDbConnection());
         _logger = logger ?? throw new ArgumentNullException(nameof(logger));
      }

      public async Task AddAndSaveEventAsync(IntegrationEvent evt)
      {
         _logger.LogInformation("----- Enqueuing integration event {IntegrationEventId} to repository ({@IntegrationEvent})", evt.Id, evt);

         await _eventLogService.SaveEventAsync(evt, _context.GetCurrentTransaction());
      }

      public async Task PublishEventsThroughEventBusAsync(Guid transactionId)
      {
         var pendingLogEvents = await _eventLogService.RetrieveEventLogsPendingToPublishAsync(transactionId);

         foreach (var logEvt in pendingLogEvents)
         {
            _logger.LogInformation("----- Publishing integration event: {IntegrationEventId} from {AppName} - ({@IntegrationEvent})", logEvt.EventId, "Simple", logEvt.IntegrationEvent);

            try
            {
               await _eventLogService.MarkEventAsInProgressAsync(logEvt.EventId);
               _eventBus.Publish(logEvt.IntegrationEvent);
               await _eventLogService.MarkEventAsPublishedAsync(logEvt.EventId);
            }
            catch (Exception ex)
            {
               _logger.LogError(ex, "ERROR publishing integration event: {IntegrationEventId} from {AppName}", logEvt.EventId, "Simple");

               await _eventLogService.MarkEventAsFailedAsync(logEvt.EventId);
            }
         }
      }
   }
}