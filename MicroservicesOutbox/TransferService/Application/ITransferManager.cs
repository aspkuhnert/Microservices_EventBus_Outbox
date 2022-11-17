using TransferService.Domain.Model;

namespace TransferService.Application
{
    public interface ITransferManager
    {
        IEnumerable<TransferLog> GetTransferLogs();
    }
}