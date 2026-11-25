using Orofoods.Web.Models.Integrations;

namespace Orofoods.Web.Tests.Models;

public sealed class WmcProductImportSummaryTests
{
    [Fact]
    public void New_product_without_changes_is_counted_only_as_new()
    {
        var candidate = Candidate("101900", productId: null, changeCount: 0);

        var summary = WmcProductImportSummary.From([candidate], [candidate]);

        Assert.Equal(1, summary.Consulted);
        Assert.Equal(1, summary.Selected);
        Assert.Equal(0, summary.Blocked);
        Assert.Equal(1, summary.NewProducts);
        Assert.Equal(0, summary.ExistingWithChanges);
        Assert.Equal(0, summary.ExistingWithoutChanges);
    }

    [Fact]
    public void Existing_products_are_split_by_changes_without_counting_new_products()
    {
        var newProduct = Candidate("101900", productId: null, changeCount: 0);
        var changedExisting = Candidate("101901", productId: 10, changeCount: 1);
        var unchangedExisting = Candidate("101902", productId: 11, changeCount: 0);

        var summary = WmcProductImportSummary.From(
            [newProduct, changedExisting, unchangedExisting],
            [newProduct, changedExisting, unchangedExisting]);

        Assert.Equal(1, summary.NewProducts);
        Assert.Equal(1, summary.ExistingWithChanges);
        Assert.Equal(1, summary.ExistingWithoutChanges);
        Assert.Equal(summary.Selected, summary.NewProducts + summary.ExistingWithChanges + summary.ExistingWithoutChanges);
    }

    [Fact]
    public void Consulted_selected_and_blocked_counts_use_their_own_populations()
    {
        var selectedNew = Candidate("101900", productId: null, changeCount: 0);
        var selectedExisting = Candidate("101901", productId: 10, changeCount: 1);
        var blocked = Candidate("101902", productId: null, changeCount: 0, selectable: false);

        var summary = WmcProductImportSummary.From(
            [selectedNew, selectedExisting, blocked],
            [selectedNew, selectedExisting]);

        Assert.Equal(3, summary.Consulted);
        Assert.Equal(2, summary.Selected);
        Assert.Equal(1, summary.Blocked);
        Assert.Equal(1, summary.NewProducts);
        Assert.Equal(1, summary.ExistingWithChanges);
        Assert.Equal(0, summary.ExistingWithoutChanges);
    }

    private static WmcProductImportCandidate Candidate(
        string code,
        int? productId,
        int changeCount,
        bool selectable = true) =>
        new(
            WmcCode: code,
            Name: $"Produto {code}",
            Ean: null,
            Situation: "A",
            BrandCode: "205",
            BrandName: "AVIAÇÃO",
            Unit: "UN",
            ProductId: productId,
            Status: selectable ? "Novo" : "Bloqueado",
            Selectable: selectable,
            Changes: Enumerable.Range(1, changeCount)
                .Select(index => new WmcProductFieldChange($"Campo {index}", "atual", "WMC"))
                .ToArray(),
            Units: [],
            SituationKind: WmcProductSituationFilter.Active);
}
