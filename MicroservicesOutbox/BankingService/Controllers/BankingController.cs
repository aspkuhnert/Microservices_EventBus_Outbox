using BankingService.Application;
using BankingService.Application.Model;
using BankingService.Domain.Commands;
using BankingService.Domain.Model;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BankingService.Controllers
{
   [Route("api/[controller]")]
   [ApiController]
   public class BankingController :
      ControllerBase
   {
      private readonly IMediator _mediator;
      private readonly IAccountManager _manager;

      public BankingController(
         IAccountManager manager,
         IMediator mediator)
      {
         _mediator = mediator;
         _manager = manager;
      }

      [HttpPost]
      public async Task<IActionResult> Post([FromBody] AccountTransfer accountTransfer)
      {
         var createTransferCommand = new CreateTransferCommand(
                    accountTransfer.FromAccount,
                    accountTransfer.ToAccount,
                    accountTransfer.TransferAmount
                );

         await _mediator.Send(createTransferCommand);

         return Ok(accountTransfer);
      }

      [HttpGet]
      public ActionResult<IEnumerable<Account>> Get()
      {
         return Ok(_manager.GetAccounts());
      }
   }
}