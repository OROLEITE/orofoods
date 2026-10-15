namespace Orofoods.Web.Models.Customers;

public sealed record CustomerSearchSuggestion(
    int Id,
    string TradeName,
    string LegalName,
    string Cnpj,
    string? WmcCode,
    string? City);
