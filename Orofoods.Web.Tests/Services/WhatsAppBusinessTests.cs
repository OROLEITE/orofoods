using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Orofoods.Web.Models.Commercial;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class WhatsAppBusinessTests
{
    [Fact]
    public async Task Signed_text_webhook_creates_known_conversation_message_and_notification_once()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var user = new ApplicationUser { Id = "internal", UserName = "internal", Email = "internal@test.local", IsActive = true };
        var customer = new Customer { LegalName = "Cliente", TradeName = "Cliente", Cnpj = "11.111.111/0001-11", WhatsApp = "5511999990000", Status = CustomerStatus.Approved, IsActive = true, InternalSalesUserId = user.Id };
        db.AddRange(user, customer);
        await db.SaveChangesAsync();
        var options = Options.Create(new WhatsAppBusinessOptions { Enabled = true, AppSecret = "test-app-secret", VerifyToken = "test-verify" });
        var service = new WhatsAppConversationService(db, options, new UserNotificationService(db));
        var body = "{\"entry\":[{\"changes\":[{\"value\":{\"messages\":[{\"id\":\"wamid.test.1\",\"from\":\"+55 (11) 99999-0000\",\"type\":\"text\",\"text\":{\"body\":\"Bom dia\"}}]}}]}]}";
        var signature = "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(options.Value.AppSecret), Encoding.UTF8.GetBytes(body))).ToLowerInvariant();

        Assert.True(service.IsValidSignature(body, signature));
        using var document = JsonDocument.Parse(body);
        await service.ProcessAsync(document);
        await service.ProcessAsync(document);

        Assert.Single(db.WhatsAppConversations);
        Assert.Single(db.WhatsAppMessages);
        Assert.Single(db.UserNotifications);
        var conversation = db.WhatsAppConversations.Single();
        Assert.Equal(customer.Id, conversation.CustomerId);
        Assert.Equal(user.Id, conversation.AssignedUserId);
        Assert.Equal(1, conversation.UnreadCount);
        Assert.Equal("5511999990000", conversation.PhoneNumber);
    }

    [Fact]
    public async Task Invalid_signature_does_not_process_payload()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var service = new WhatsAppConversationService(db, Options.Create(new WhatsAppBusinessOptions { Enabled = true, AppSecret = "secret" }), new UserNotificationService(db));
        Assert.False(service.IsValidSignature("{}", "sha256=invalid"));
        Assert.Empty(db.WhatsAppConversations);
        Assert.Empty(db.WhatsAppMessages);
    }

    [Fact]
    public async Task Message_status_does_not_regress_from_read()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var conversation = new WhatsAppConversation { PhoneNumber = "5511999990000" };
        var message = new WhatsAppMessage { Conversation = conversation, ExternalMessageId = "wamid.out.1", Direction = WhatsAppMessageDirection.Outbound, Type = WhatsAppMessageType.Text, Status = WhatsAppMessageStatus.Read };
        db.Add(message);
        await db.SaveChangesAsync();
        var service = new WhatsAppConversationService(db, Options.Create(new WhatsAppBusinessOptions { Enabled = true, AppSecret = "secret" }), new UserNotificationService(db));
        using var document = JsonDocument.Parse("{\"entry\":[{\"changes\":[{\"value\":{\"statuses\":[{\"id\":\"wamid.out.1\",\"status\":\"delivered\",\"timestamp\":\"1750000000\"}]}}]}]}");
        await service.ProcessAsync(document);
        Assert.Equal(WhatsAppMessageStatus.Read, db.WhatsAppMessages.Single().Status);
    }

    [Fact]
    public async Task Gateway_uses_fake_http_and_parses_external_message_id_without_real_credentials()
    {
        var handler = new FakeHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://graph.facebook.com/") };
        var gateway = new MetaWhatsAppBusinessGateway(client, Options.Create(new WhatsAppBusinessOptions { Enabled = true, GraphApiVersion = "v23.0", PhoneNumberId = "phone-test", AccessToken = "fake-token" }));
        var result = await gateway.SendTextAsync("5511999990000", "Olá");
        Assert.True(result.Succeeded);
        Assert.Equal("wamid.sent.1", result.ExternalMessageId);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Contains("v23.0/phone-test/messages", handler.RequestUri);
        Assert.DoesNotContain("fake-token", handler.Body);
        Assert.DoesNotContain("cvv", handler.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unknown_phone_creates_unidentified_conversation_without_customer()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var service = new WhatsAppConversationService(db, Options.Create(new WhatsAppBusinessOptions { Enabled = true, AppSecret = "secret" }), new UserNotificationService(db));
        using var document = JsonDocument.Parse("{\"entry\":[{\"changes\":[{\"value\":{\"messages\":[{\"id\":\"wamid.unknown\",\"from\":\"5511888880000\",\"type\":\"text\",\"text\":{\"body\":\"Olá\"}}]}}]}]}");

        await service.ProcessAsync(document);

        var conversation = Assert.Single(db.WhatsAppConversations);
        Assert.Null(conversation.CustomerId);
        Assert.Null(conversation.AssignedUserId);
        Assert.Empty(db.Customers);
    }

    [Fact]
    public async Task Ambiguous_phone_does_not_auto_link_customer()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.Customers.AddRange(
            new Customer { LegalName = "A", TradeName = "A", Cnpj = "11.111.111/0001-11", WhatsApp = "5511777770000", Status = CustomerStatus.Approved, IsActive = true },
            new Customer { LegalName = "B", TradeName = "B", Cnpj = "22.222.222/0001-22", Phone = "5511777770000", Status = CustomerStatus.Approved, IsActive = true });
        await db.SaveChangesAsync();
        var service = new WhatsAppConversationService(db, Options.Create(new WhatsAppBusinessOptions { Enabled = true, AppSecret = "secret" }), new UserNotificationService(db));
        using var document = JsonDocument.Parse("{\"entry\":[{\"changes\":[{\"value\":{\"messages\":[{\"id\":\"wamid.ambiguous\",\"from\":\"5511777770000\",\"type\":\"text\",\"text\":{\"body\":\"Olá\"}}]}}]}]}");

        await service.ProcessAsync(document);

        Assert.Null(Assert.Single(db.WhatsAppConversations).CustomerId);
    }

    [Fact]
    public async Task Gateway_returns_sanitized_error_for_meta_failure_and_disabled_mode()
    {
        var failingClient = new HttpClient(new FakeHandler(HttpStatusCode.Unauthorized)) { BaseAddress = new Uri("https://graph.facebook.com/") };
        var gateway = new MetaWhatsAppBusinessGateway(failingClient, Options.Create(new WhatsAppBusinessOptions { Enabled = true, GraphApiVersion = "v23.0", PhoneNumberId = "phone-test", AccessToken = "fake-token" }));
        var failed = await gateway.SendTextAsync("5511999990000", "Olá");
        Assert.False(failed.Succeeded);
        Assert.Equal("401", failed.ErrorCode);
        Assert.DoesNotContain("fake-token", failed.ErrorMessage ?? "");

        var disabled = new MetaWhatsAppBusinessGateway(new HttpClient(new FakeHandler()), Options.Create(new WhatsAppBusinessOptions { Enabled = false }));
        var disabledResult = await disabled.SendTextAsync("5511999990000", "Olá");
        Assert.Equal("DISABLED", disabledResult.ErrorCode);
    }

    [Fact]
    public async Task Gateway_propagates_cancellation_from_timeout_handler()
    {
        var gateway = new MetaWhatsAppBusinessGateway(new HttpClient(new TimeoutHandler()) { BaseAddress = new Uri("https://graph.facebook.com/") }, Options.Create(new WhatsAppBusinessOptions { Enabled = true, GraphApiVersion = "v23.0", PhoneNumberId = "phone-test", AccessToken = "fake-token" }));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gateway.SendTextAsync("5511999990000", "Olá", new CancellationToken(true)));
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public string RequestUri { get; private set; } = "";
        public string Body { get; private set; } = "";
        private readonly HttpStatusCode statusCode;
        public FakeHandler(HttpStatusCode statusCode = HttpStatusCode.OK) => this.statusCode = statusCode;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri?.ToString() ?? "";
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode) { Content = JsonContent.Create(new { messages = new[] { new { id = "wamid.sent.1" } } }) };
        }
    }

    private sealed class TimeoutHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromCanceled<HttpResponseMessage>(cancellationToken);
    }
}
