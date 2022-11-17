using Microsoft.EntityFrameworkCore;
using TransferService.Domain.Model;

namespace TransferService.Database
{
    public class TransferContext :
       DbContext
    {
        public TransferContext(DbContextOptions options) : base(options)
        {
        }

        public DbSet<TransferLog> TransferLogs { get; set; }
    }
}