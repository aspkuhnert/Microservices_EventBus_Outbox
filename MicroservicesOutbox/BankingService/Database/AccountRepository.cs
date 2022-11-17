using BankingService.Domain.Model;
using BuildingBlocks.Domain.Seedwork;

namespace BankingService.Database
{
   public class AccountRepository :
      IAccountRepository
   {
      public readonly BankingContext _context;

      public IUnitOfWork UnitOfWork
      {
         get
         {
            return _context;
         }
      }

      public AccountRepository(BankingContext context)
      {
         _context = context;
      }

      public IEnumerable<Account> GetAccounts()
      {
         return _context.Accounts;
      }

      public Account Add(Account order)
      {
         return _context.Accounts.Add(order).Entity;
      }
   }
}