namespace Orofoods.Web.Integrations.Erp.Wmc;

/// <summary>External-system row shapes. Field names/tables mirror only the WMC columns already confirmed.</summary>
public sealed record WmcCustomerRecord(string CodCliente, string Nome);

public sealed record WmcProductRecord(
    string CodProduto,
    string Produto,
    string? Situacao,
    string? Un,
    decimal? EstoqueDisponivel,
    decimal? EstoqueAtual,
    string? Ean = null,
    string? CodMarca = null,
    string? UnAltern = null,
    decimal? QtdeEmbalagem = null,
    decimal? QtdeConversao = null,
    decimal? PrecoCusto = null,
    decimal? PrecoVenda = null,
    decimal? EstoqueReservado = null,
    DateTime? DataAlteracao = null);
