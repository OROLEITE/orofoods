using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;

namespace Orofoods.Web.Services.Customers;

public class CustomerRegistrationService(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
{
    public async Task<CustomerRegistrationResult> RegisterAsync(
        CustomerRegistrationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await db.Customers.AnyAsync(x => x.Cnpj == request.Cnpj, cancellationToken))
        {
            throw new InvalidOperationException("Este CNPJ já está cadastrado.");
        }

        var customer = new Customer
        {
            LegalName = request.LegalName,
            TradeName = request.TradeName,
            Cnpj = request.Cnpj,
            StateRegistration = request.StateRegistration,
            ResponsibleName = request.ResponsibleName,
            ResponsibleDocument = request.ResponsibleDocument,
            Email = request.Email,
            Phone = request.Phone,
            WhatsApp = request.WhatsApp,
            Status = CustomerStatus.Pending,
            IsActive = true
        };

        customer.Addresses.Add(new CustomerAddress
        {
            Label = "Principal",
            ZipCode = request.ZipCode,
            Street = request.Street,
            Number = request.Number,
            Complement = request.Complement,
            District = request.District,
            City = request.City,
            State = request.State,
            IsPrimary = true,
            IsActive = true
        });

        db.Customers.Add(customer);
        await db.SaveChangesAsync(cancellationToken);

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            CustomerId = customer.Id,
            IsActive = true
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", createResult.Errors.Select(DescribeIdentityError).Distinct()));
        }

        if (!await userManager.IsInRoleAsync(user, "Cliente"))
        {
            await userManager.AddToRoleAsync(user, "Cliente");
        }

        return new CustomerRegistrationResult(customer.Id, user.Id);
    }

    private string DescribeIdentityError(IdentityError error) => error.Code switch
    {
        "DuplicateUserName" or "DuplicateEmail" => "Este e-mail já está cadastrado.",
        "InvalidEmail" => "Informe um endereço de e-mail válido.",
        "InvalidUserName" => "O e-mail informado não pode ser usado como nome de acesso.",
        "PasswordTooShort" => $"A senha deve ter pelo menos {userManager.Options.Password.RequiredLength} caracteres.",
        "PasswordRequiresNonAlphanumeric" => "A senha deve conter pelo menos um caractere especial.",
        "PasswordRequiresDigit" => "A senha deve conter pelo menos um número.",
        "PasswordRequiresLower" => "A senha deve conter pelo menos uma letra minúscula.",
        "PasswordRequiresUpper" => "A senha deve conter pelo menos uma letra maiúscula.",
        "PasswordRequiresUniqueChars" => $"A senha deve conter pelo menos {userManager.Options.Password.RequiredUniqueChars} caracteres diferentes.",
        _ => "Não foi possível criar o acesso. Confira os dados e tente novamente."
    };
}

public sealed class CustomerRegistrationRequest
{
    public string LegalName { get; set; } = "";
    public string TradeName { get; set; } = "";
    public string Cnpj { get; set; } = "";
    public string StateRegistration { get; set; } = "";
    public string ResponsibleName { get; set; } = "";
    public string ResponsibleDocument { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string WhatsApp { get; set; } = "";
    public string ZipCode { get; set; } = "";
    public string Street { get; set; } = "";
    public string Number { get; set; } = "";
    public string Complement { get; set; } = "";
    public string District { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string Password { get; set; } = "";
}

public sealed record CustomerRegistrationResult(int CustomerId, string UserId);
