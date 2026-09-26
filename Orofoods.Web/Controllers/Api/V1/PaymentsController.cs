using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Orofoods.Web.Authorization;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Payments;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Controllers.Api.V1;

/// <summary>Authenticated (cookie session) customer endpoints that create and read Mercado Pago payment attempts.</summary>
[ApiController]
[Route("api/v1/payments")]
[Authorize(Policy = OrofoodsPolicies.ApprovedCustomer)]
[EnableRateLimiting("api")]
public class PaymentsController(
    PaymentOrchestrationService orchestrationService,
    UserManager<ApplicationUser> userManager,
    CustomerAccessService customerAccessService,
    AdminCustomerContextService adminCustomerContextService) : ControllerBase
{
    [HttpPost("pix")]
    public async Task<ActionResult<PaymentAttemptResponse>> CreatePix(CreatePixAttemptRequest request, CancellationToken cancellationToken)
    {
        var customer = await GetCurrentCustomerAsync();
        if (customer is null)
        {
            return Forbid();
        }

        try
        {
            var payment = await orchestrationService.CreatePixAsync(request.OrderId, customer.Id, request.IdempotencyKey, cancellationToken);
            return Ok(PaymentAttemptResponse.FromPayment(payment));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("card")]
    public async Task<ActionResult<PaymentAttemptResponse>> CreateCard(CreateCardAttemptRequest request, CancellationToken cancellationToken)
    {
        var customer = await GetCurrentCustomerAsync();
        if (customer is null)
        {
            return Forbid();
        }

        try
        {
            var payment = await orchestrationService.CreateCreditCardAsync(
                request.OrderId, customer.Id, request.IdempotencyKey, request.CardToken, request.PaymentMethodId, request.Installments, cancellationToken);
            return Ok(PaymentAttemptResponse.FromPayment(payment));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PaymentAttemptResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var customer = await GetCurrentCustomerAsync();
        if (customer is null)
        {
            return Forbid();
        }

        var payment = await orchestrationService.GetOwnedPaymentAsync(id, customer.Id, cancellationToken);
        return payment is null ? NotFound() : Ok(PaymentAttemptResponse.FromPayment(payment));
    }

    private async Task<Customer?> GetCurrentCustomerAsync()
    {
        if (User.IsInRole("Administrador"))
        {
            var customerId = adminCustomerContextService.GetSelectedCustomerId(HttpContext.Session);
            if (customerId is not null)
            {
                return await customerAccessService.GetApprovedCustomerByIdAsync(customerId.Value);
            }
        }

        var userId = userManager.GetUserId(User);
        return await customerAccessService.GetApprovedCustomerAsync(userId);
    }
}
