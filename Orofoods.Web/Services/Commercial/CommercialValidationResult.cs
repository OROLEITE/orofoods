namespace Orofoods.Web.Services.Commercial;

public sealed record CommercialValidationResult(bool IsValid, string? ErrorMessage = null)
{
    public static CommercialValidationResult Success() => new(true);
    public static CommercialValidationResult Failure(string errorMessage) => new(false, errorMessage);
}
