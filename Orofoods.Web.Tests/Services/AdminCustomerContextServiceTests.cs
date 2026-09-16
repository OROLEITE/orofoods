using Microsoft.AspNetCore.Http;
using Orofoods.Web.Services.Identity;

namespace Orofoods.Web.Tests.Services;

public class AdminCustomerContextServiceTests
{
    [Fact]
    public void Selected_customer_id_is_available_only_after_selection()
    {
        var session = new TestSession();
        var sut = new AdminCustomerContextService();

        Assert.Null(sut.GetSelectedCustomerId(session));

        sut.SetSelectedCustomerId(session, 42);

        Assert.Equal(42, sut.GetSelectedCustomerId(session));
        sut.Clear(session);
        Assert.Null(sut.GetSelectedCustomerId(session));
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _values = [];

        public IEnumerable<string> Keys => _values.Keys;
        public string Id => "test-session";
        public bool IsAvailable => true;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Clear() => _values.Clear();
        public void Remove(string key) => _values.Remove(key);
        public void Set(string key, byte[] value) => _values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _values.TryGetValue(key, out value!);
    }
}
