using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Services.Integrations;

namespace Orofoods.Web.Tests.Services;

public sealed class WmcIntegrationStatusPresenterTests
{
    [Fact]
    public void New_order_is_pending()
    {
        Assert.Equal(WmcIntegrationStatusDisplay.Pending, WmcIntegrationStatusPresenter.GetStatus(new Order()));
        Assert.Equal("Pendente", WmcIntegrationStatusPresenter.GetLabel(WmcIntegrationStatusDisplay.Pending));
    }

    [Fact]
    public void Order_with_unknown_integration_outcome_is_shown_as_sent_even_without_an_audit()
    {
        var order = new Order { IntegrationStatus = IntegrationStatus.Unknown };

        Assert.Equal(WmcIntegrationStatusDisplay.Sent, WmcIntegrationStatusPresenter.GetStatus(order));
        Assert.Contains("Reconcilie", WmcIntegrationStatusPresenter.GetDetail(order));
    }

    [Fact]
    public void Generated_txt_does_not_count_as_wmc_integration_success()
    {
        var order = new Order
        {
            IntegrationStatus = IntegrationStatus.Succeeded,
            WmcOrderExportState = new WmcOrderExportState { Status = WmcOrderExportStatus.Exported },
            WmcExportAudits =
            [
                new WmcExportAudit
                {
                    Source = WmcExportSource.Automatic,
                    Outcome = WmcExportOutcome.Available,
                    Succeeded = true,
                    FileName = "WMC_ORO2026000016.txt"
                }
            ]
        };

        Assert.Equal(WmcIntegrationStatusDisplay.Pending, WmcIntegrationStatusPresenter.GetStatus(order));
        Assert.Equal("TXT gerado não confirma a importação no WMC; integração pendente.", WmcIntegrationStatusPresenter.GetDetail(order));
    }

    [Fact]
    public void Manual_review_overrides_an_older_http_success_until_reconciled()
    {
        var order = OrderWithJsonAudit(WmcExportOutcome.Succeeded);
        order.WmcOrderExportState = new WmcOrderExportState
        {
            Status = WmcOrderExportStatus.Processing,
            RequiresManualReview = true,
            LastError = "Confira o histórico antes de agir."
        };

        Assert.Equal(WmcIntegrationStatusDisplay.Pending, WmcIntegrationStatusPresenter.GetStatus(order));
        Assert.Contains("Revisão manual", WmcIntegrationStatusPresenter.GetDetail(order));
    }

    [Theory]
    [InlineData(WmcExportOutcome.Started)]
    [InlineData(WmcExportOutcome.Unknown)]
    public void Unconfirmed_json_attempt_is_shown_as_sent_and_requires_reconciliation(WmcExportOutcome outcome)
    {
        var order = OrderWithJsonAudit(outcome);

        Assert.Equal(WmcIntegrationStatusDisplay.Sent, WmcIntegrationStatusPresenter.GetStatus(order));
        Assert.Equal("Enviado", WmcIntegrationStatusPresenter.GetLabel(WmcIntegrationStatusPresenter.GetStatus(order)));
        Assert.Contains("Reconcilie", WmcIntegrationStatusPresenter.GetDetail(order));
    }

    [Fact]
    public void Confirmed_json_response_is_shown_as_confirmed()
    {
        var order = OrderWithJsonAudit(WmcExportOutcome.Succeeded);
        order.WmcExportAudits.Single().HttpStatusCode = 200;
        order.WmcExportAudits.Single().ResponseBody = "importado com sucesso";

        Assert.Equal(WmcIntegrationStatusDisplay.Confirmed, WmcIntegrationStatusPresenter.GetStatus(order));
    }

    [Fact]
    public void Json_failure_is_shown_as_error()
    {
        var order = OrderWithJsonAudit(WmcExportOutcome.Failed);
        order.WmcExportAudits.Single().Error = "Configuração ausente";

        Assert.Equal(WmcIntegrationStatusDisplay.Error, WmcIntegrationStatusPresenter.GetStatus(order));
        Assert.Equal("Configuração ausente", WmcIntegrationStatusPresenter.GetDetail(order));
    }

    [Theory]
    [InlineData(WmcOrderExportStatus.Rejected)]
    [InlineData(WmcOrderExportStatus.Error)]
    public void Latest_uncertain_json_attempt_overrides_an_older_txt_error(WmcOrderExportStatus oldTxtStatus)
    {
        var order = OrderWithJsonAudit(WmcExportOutcome.Unknown);
        order.IntegrationStatus = IntegrationStatus.Unknown;
        order.WmcOrderExportState = new WmcOrderExportState { Status = oldTxtStatus };
        order.WmcExportAudits.Add(new WmcExportAudit
        {
            Source = WmcExportSource.Manual,
            Outcome = WmcExportOutcome.Rejected,
            FileName = "WMC_ORO2026000016.txt",
            ExportedAt = DateTime.UtcNow.AddMinutes(-5)
        });

        Assert.Equal(WmcIntegrationStatusDisplay.Sent, WmcIntegrationStatusPresenter.GetStatus(order));
        Assert.Contains("Reconcilie", WmcIntegrationStatusPresenter.GetDetail(order));
    }

    [Fact]
    public void Confirmed_manual_txt_import_is_shown_as_confirmed()
    {
        var order = new Order
        {
            WmcOrderExportState = new WmcOrderExportState
            {
                Status = WmcOrderExportStatus.Imported,
                ImportedAt = DateTime.UtcNow
            }
        };

        Assert.Equal(WmcIntegrationStatusDisplay.Confirmed, WmcIntegrationStatusPresenter.GetStatus(order));
    }

    private static Order OrderWithJsonAudit(WmcExportOutcome outcome) => new()
    {
        WmcExportAudits =
        [
            new WmcExportAudit
            {
                Source = WmcExportSource.Automatic,
                Outcome = outcome,
                WmcTransmissionId = 9016,
                WmcDestination = "https://wmc.example.invalid/integracao",
                ExportedAt = DateTime.UtcNow
            }
        ]
    };
}
