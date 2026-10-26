using System.Text;
using System.Net.Http.Headers;

namespace Orofoods.Web.Integrations.Erp.Wmc;

internal static class WmcMobileFormUrlEncoder
{
    public static ByteArrayContent Create(string fieldName, string value)
    {
        var body = $"{EncodeComponent(fieldName)}={EncodeComponent(value)}";
        var content = new ByteArrayContent(Encoding.ASCII.GetBytes(body));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
        return content;
    }

    private static string EncodeComponent(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var valueByte in Encoding.UTF8.GetBytes(value))
        {
            if (IsUnreserved(valueByte))
            {
                builder.Append((char)valueByte);
            }
            else if (valueByte == 0x20)
            {
                builder.Append('+');
            }
            else
            {
                builder.Append('%');
                builder.Append(valueByte.ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        return builder.ToString();
    }

    private static bool IsUnreserved(byte valueByte) =>
        valueByte is >= (byte)'A' and <= (byte)'Z' or
            >= (byte)'a' and <= (byte)'z' or
            >= (byte)'0' and <= (byte)'9' or
            (byte)'-' or (byte)'_' or (byte)'.' or (byte)'*';
}
