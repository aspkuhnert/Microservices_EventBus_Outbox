using Microsoft.AspNetCore.Mvc;
using TransferService.Application;
using TransferService.Domain.Model;

namespace TransferService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TransferController :
       ControllerBase
    {
        private readonly ITransferManager _transferManager;

        public TransferController(ITransferManager transferManager)
        {
            _transferManager = transferManager;
        }

        // GET api/transfer
        [HttpGet]
        public ActionResult<IEnumerable<TransferLog>> Get()
        {
            return Ok(_transferManager.GetTransferLogs());
        }
    }
}