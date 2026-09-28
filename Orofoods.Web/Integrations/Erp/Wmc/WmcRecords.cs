namespace Orofoods.Web.Integrations.Erp.Wmc;

/// <summary>External-system row shapes. Field names/tables mirror only the WMC columns already confirmed.</summary>
public sealed record WmcCustomerRecord(string CodCliente, string Nome);

public sealed record WmcProductRecord(
    string CodProduto,
    string Produto,
    string? Situacao,
    string? Un,
    int? EstoqueDisponivel,
    int? EstoqueAtual);
