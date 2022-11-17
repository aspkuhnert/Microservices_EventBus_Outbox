using BankingService.Database;
using BankingService.Domain.IntegrationEvents;
using BuildingBlocks.EventBus.Extensions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Serilog.Context;

namespace BankingService.Application.Behaviours
{
    public class TransactionBehaviour<TRequest, TResponse> :
       IPipelineBehavior<TRequest, TResponse>
       where TRequest : IRequest<TResponse>
    {
        private readonly ILogger<TransactionBehaviour<TRequest, TResponse>> _logger;
        private readonly BankingContext _context;
        private readonly IBankingIntegrationEventService _bankingIntegrationEventService;

        public TransactionBehaviour(ILogger<TransactionBehaviour<TRequest, TResponse>> logger, BankingContext context, IBankingIntegrationEventService bankingIntegrationEventService)
        {
            _logger = logger;
            _context = context;
            _bankingIntegrationEventService = bankingIntegrationEventService;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var response = default(TResponse);
            var typeName = request.GetGenericTypeName();

            try
            {
                if (_context.HasActiveTransaction)
                {
                    return await next();
                }

                var strategy = _context.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    Guid transactionId;

                    using var transaction = await _context.BeginTransactionAsync();
                    using (LogContext.PushProperty("TransactionContext", transaction.TransactionId))
                    {
                        _logger.LogInformation("----- Begin transaction {TransactionId} for {CommandName} ({@Command})", transaction.TransactionId, typeName, request);

                        response = await next();

                        _logger.LogInformation("----- Commit transaction {TransactionId} for {CommandName}", transaction.TransactionId, typeName);

                        try
                        {
                            await _context.CommitTransactionAsync(transaction);
                        }
                        catch (Exception ex)
                        {
                            var y = ex.Message;
                            _logger.LogError(ex, "ERROR Handling transaction for {CommandName} ({@Command})", typeName, request);
                        }

                        transactionId = transaction.TransactionId;
                    }

                    await _bankingIntegrationEventService.PublishEventsThroughEventBusAsync(transactionId);
                });

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR Handling transaction for {CommandName} ({@Command})", typeName, request);

                throw;
            }
        }
    }
}