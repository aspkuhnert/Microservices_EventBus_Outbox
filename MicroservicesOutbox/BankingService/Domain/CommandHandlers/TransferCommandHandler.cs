using BankingService.Domain.Commands;
using BankingService.Domain.IntegrationEvents;
using MediatR;

namespace BankingService.Domain.CommandHandlers
{
    public class TransferCommandHandler :
      IRequestHandler<CreateTransferCommand, bool>
    {
        //private readonly IEventBus _bus;
        private readonly IBankingIntegrationEventService _integrationService;

        private readonly ILoggerFactory _logger;

        //public TransferCommandHandler(IEventBus bus)
        //{
        //   _bus = bus;
        //}
        public TransferCommandHandler(IBankingIntegrationEventService service, ILoggerFactory logger)
        {
            _integrationService = service ?? throw new ArgumentNullException(nameof(service));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> Handle(CreateTransferCommand request, CancellationToken cancellationToken)
        {
            _logger.CreateLogger<CreateTransferCommand>()
               .LogTrace("Transfer command {0} {1} - {2}", request.From, request.To, request.Amount);

            //publish event to RabbitMQ
            //_bus.Publish(new TransferCreatedIntegrationEvent(request.From, request.To, request.Amount));
            // TODO: neu !!!!
            //await _integrationService.PublishEventsThroughEventBusAsync(transactionId);
            await _integrationService.AddAndSaveEventAsync(new TransferCreatedIntegrationEvent(request.From, request.To, request.Amount));

            //var account = new Account()
            //{
            //   AccountType = "",
            //   AccountBalance = request.Amount
            //};

            return true;
        }
    }
}