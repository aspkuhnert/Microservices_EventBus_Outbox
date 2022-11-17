using BankingService.Application.Model;
using BankingService.Domain.Model;

namespace BankingService.Application
{
    public interface IAccountManager
    {
        IEnumerable<Account> GetAccounts();

        void Transfer(AccountTransfer accountTransfer);
    }
}