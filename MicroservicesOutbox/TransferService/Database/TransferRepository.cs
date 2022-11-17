using Microsoft.EntityFrameworkCore;
using TransferService.Domain.Model;

namespace TransferService.Database
{
    public class TransferRepository :
       ITransferRepository
    {
        private TransferContext _context;

        public TransferRepository(TransferContext context)
        {
            _context = context;
        }

        public void Add(TransferLog transferLog)
        {
            var c = _context.Database.GetDbConnection();
            _context.TransferLogs.Add(transferLog);

            try
            {
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                var x = ex.Message;
            }
        }

        public IEnumerable<TransferLog> GetTransferLogs()
        {
            return _context.TransferLogs;
        }
    }
}