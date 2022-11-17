using TransferService.Domain.Model;

namespace TransferService.Application
{
    public class TransferManager :
       ITransferManager
    {
        private readonly ITransferRepository _transferRepository;

        public TransferManager(ITransferRepository transferRepository)
        {
            _transferRepository = transferRepository;
        }

        public IEnumerable<TransferLog> GetTransferLogs()
        {
            return _transferRepository.GetTransferLogs();
        }
    }
}