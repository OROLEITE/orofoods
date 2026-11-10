using System.Text;

namespace Orofoods.Web.Tests.Views;

public class CustomerPaymentConditionEncodingTests
{
    [Fact]
    public void Customer_admin_views_are_utf8_and_keep_portuguese_payment_copy_readable()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var views = new[]
        {
            Path.Combine(projectPath, "Areas", "Admin", "Views", "Customers", "Details.cshtml"),
            Path.Combine(projectPath, "Areas", "Admin", "Views", "Customers", "Edit.cshtml")
        };

        for (var index = 0; index < views.Length; index++)
        {
            var text = new UTF8Encoding(false, true).GetString(File.ReadAllBytes(views[index]));
            Assert.DoesNotContain("�", text, StringComparison.Ordinal);
            Assert.DoesNotContain("NÃ£o", text, StringComparison.Ordinal);
            Assert.Contains(index == 0 ? "Pagamento WMC" : "Condições de pagamento", text, StringComparison.Ordinal);
        }
    }
}
