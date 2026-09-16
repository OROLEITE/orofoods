using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Controllers;

[AllowAnonymous]
public class CustomerRegistrationController(
    CustomerRegistrationService registrationService,
    CustomerAccessService customerAccessService,
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager) : Controller
{
    [HttpGet("/cadastro")]
    public IActionResult Register()
    {
        return View(new CustomerRegistrationViewModel());
    }

    [HttpPost("/cadastro")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(CustomerRegistrationViewModel input)
    {
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        try
        {
            var result = await registrationService.RegisterAsync(new CustomerRegistrationRequest
            {
                LegalName = input.LegalName,
                TradeName = input.TradeName,
                Cnpj = input.Cnpj,
                StateRegistration = input.StateRegistration,
                ResponsibleName = input.ResponsibleName,
                ResponsibleDocument = input.ResponsibleDocument,
                Email = input.Email,
                Phone = input.Phone,
                WhatsApp = input.WhatsApp,
                ZipCode = input.ZipCode,
                Street = input.Street,
                Number = input.Number,
                Complement = input.Complement,
                District = input.District,
                City = input.City,
                State = input.State,
                Password = input.Password
            });

            var user = await userManager.FindByIdAsync(result.UserId)
                ?? throw new InvalidOperationException("Registered user not found.");
            await signInManager.SignInAsync(user, isPersistent: false);
            TempData["RegistrationSuccess"] = "Cadastro enviado com sucesso. Sua empresa será analisada pela equipe comercial.";
            return RedirectToAction("Index", "Home");
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(input);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Pending()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var userId = userManager.GetUserId(User);
            if (await customerAccessService.HasApprovedCustomerAccessAsync(userId))
            {
                return RedirectToAction("Dashboard", "Portal");
            }
        }

        return View();
    }
}
