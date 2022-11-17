using BuildingBlocks.Domain.Commands;

namespace BankingService.Domain.Commands
{
    public class CreateTransferCommand :
       Command
    {
        public int From { get; protected set; }
        public int To { get; protected set; }
        public decimal Amount { get; protected set; }

        protected CreateTransferCommand()
        {
        }

        public CreateTransferCommand(int from, int to, decimal amount)
        {
            From = from;
            To = to;
            Amount = amount;
        }
    }
}