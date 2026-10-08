namespace Orofoods.Web.Models.Integrations;

public sealed class WmcSyncRun
{
    public int Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public bool InitialLoad { get; set; }
    public string Status { get; set; } = "Running";
    public string? FailedStage { get; set; }
    public string? ErrorMessage { get; set; }
    public int CustomersRead { get; set; }
    public int ProductsRead { get; set; }
    public int SellersRead { get; set; }
    public int StockRead { get; set; }
    public int ProductsCreated { get; set; }
    public int ProductsUpdated { get; set; }
    public int StockCreated { get; set; }
    public int StockUpdated { get; set; }
}
