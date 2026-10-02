namespace Orofoods.Web.Tests.Services;

public class WhatsAppPollingUiTests
{
    [Fact]
    public void Polling_is_incremental_single_flight_and_preserves_the_composer_value()
    {
        var script = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "Orofoods.Web", "wwwroot", "js", "whatsapp-composer.js"));

        Assert.Contains("setInterval(synchronize, 4000)", script);
        Assert.Contains("requestInFlight", script);
        Assert.Contains("document.hidden", script);
        Assert.Contains("AbortController", script);
        Assert.Contains("cache: 'no-store'", script);
        Assert.Contains("America/Sao_Paulo", script);
        Assert.Contains("data-message-id", script);
        Assert.Contains("article.dataset.messageId", script);
        Assert.DoesNotContain("textarea.value =", script);
        Assert.DoesNotContain("location.reload", script);
    }

    [Fact]
    public void Inbound_sound_is_opt_in_to_new_ids_and_batch_coalesced()
    {
        var script = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "Orofoods.Web", "wwwroot", "js", "whatsapp-composer.js"));

        Assert.Contains("localStorage", script);
        Assert.Contains("knownInboundIds", script);
        Assert.Contains("message.direction === 'inbound'", script);
        Assert.Contains("!initialState", script);
        Assert.Contains("playInboundBeep()", script);
        Assert.Contains("soundToggle", script);
        Assert.Contains("AudioContext", script);
    }

    [Fact]
    public void Customer_linking_uses_search_modal_instead_of_loading_a_permanent_select()
    {
        var view = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "Orofoods.Web", "Areas", "Admin", "Views", "WhatsApp", "Index.cshtml"));

        Assert.Contains("whatsappCustomerDialog", view);
        Assert.Contains("CustomerSearch", view);
        Assert.DoesNotContain("Model.CustomerChoices", view);
        Assert.DoesNotContain("<select id=\"customerId\"", view);
    }

    [Fact]
    public void Theme_and_conversation_search_are_additive_to_the_existing_whatsapp_page()
    {
        var root = FindRepositoryRoot();
        var view = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "Areas", "Admin", "Views", "WhatsApp", "Index.cshtml"));
        var script = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "wwwroot", "js", "whatsapp-composer.js"));
        var styles = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "wwwroot", "css", "whatsapp-composer.css"));

        Assert.Contains("whatsapp-theme-toggle", view);
        Assert.Contains("orofoods.crm.theme", script);
        Assert.Contains("whatsapp-search-input", view);
        Assert.Contains("data-search-phone", view);
        Assert.Contains("conversation.textContent", script);
        Assert.Contains("has-explicit-selection", view);
        Assert.Contains("whatsapp-mobile-back", view);
        Assert.Contains("grid-template-columns: minmax(300px, 330px) minmax(0, 1fr) minmax(300px, 330px)", styles);
        Assert.Contains("[data-theme=\"dark\"]", styles);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "Orofoods.Web")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
