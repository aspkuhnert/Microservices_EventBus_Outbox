using BankingService.Domain.Model;
using BuildingBlocks.Domain.Seedwork;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace BankingService.Database
{
   public class BankingContext :
      DbContext,
      IUnitOfWork
   {
      private readonly IMediator _mediator;
      private IDbContextTransaction _currentTransaction;

      public BankingContext(DbContextOptions<BankingContext> options)
         : base(options)
      {
      }

      public BankingContext(DbContextOptions<BankingContext> options, IMediator mediator)
             : base(options)
      {
         _mediator = mediator;
      }

      public DbSet<Account> Accounts { get; set; }
      public bool HasActiveTransaction => _currentTransaction != null;

      public async Task<IDbContextTransaction> BeginTransactionAsync()
      {
         if (_currentTransaction != null) return null;

         _currentTransaction = await Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

         return _currentTransaction;
      }

      public async Task CommitTransactionAsync(IDbContextTransaction transaction)
      {
         if (transaction == null) throw new ArgumentNullException(nameof(transaction));
         if (transaction != _currentTransaction) throw new InvalidOperationException($"Transaction {transaction.TransactionId} is not current");

         try
         {
            await SaveChangesAsync();
            transaction.Commit();
         }
         catch
         {
            RollbackTransaction();
            throw;
         }
         finally
         {
            if (_currentTransaction != null)
            {
               _currentTransaction.Dispose();
               _currentTransaction = null;
            }
         }
      }

      public override void Dispose()
      {
         base.Dispose();
      }

      public override ValueTask DisposeAsync()
      {
         return base.DisposeAsync();
      }

      public IDbContextTransaction GetCurrentTransaction() => _currentTransaction;

      public void RollbackTransaction()
      {
         try
         {
            _currentTransaction?.Rollback();
         }
         finally
         {
            if (_currentTransaction != null)
            {
               _currentTransaction.Dispose();
               _currentTransaction = null;
            }
         }
      }

      public async Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default)
      {
         // Dispatch Domain Events collection.
         // Choices:
         // A) Right BEFORE committing data (EF SaveChanges) into the DB will make a single transaction including
         // side effects from the domain event handlers which are using the same DbContext with "InstancePerLifetimeScope" or "scoped" lifetime
         // B) Right AFTER committing data (EF SaveChanges) into the DB will make multiple transactions.
         // You will need to handle eventual consistency and compensatory actions in case of failures in any of the Handlers.
         await _mediator.DispatchDomainEventsAsync(this);

         // After executing this line all the changes (from the Command Handler and Domain Event Handlers)
         // performed through the DbContext will be committed
         var result = await base.SaveChangesAsync(cancellationToken);

         return true;
      }
   }
}