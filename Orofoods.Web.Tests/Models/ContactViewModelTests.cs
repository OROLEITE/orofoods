using System.ComponentModel.DataAnnotations;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Tests.Models;

public class ContactViewModelTests
{
    [Fact]
    public void Contact_view_model_requires_privacy_consent()
    {
        var input = new ContactViewModel
        {
            Name = "Ana Souza",
            Email = "ana@burger.com",
            Phone = "(19) 3000-1000",
            City = "Campinas",
            Message = "Quero conhecer a Orofoods.",
            AcceptPrivacyPolicy = false
        };
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(input, new ValidationContext(input), results, validateAllProperties: true);

        Assert.Contains(results, x => x.MemberNames.Contains(nameof(ContactViewModel.AcceptPrivacyPolicy)));
    }
}
