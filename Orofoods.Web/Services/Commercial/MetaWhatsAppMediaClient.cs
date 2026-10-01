using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Orofoods.Web.Services.Commercial;

public sealed class MetaWhatsAppMediaClient(HttpClient httpClient, IOptions<WhatsAppBusinessOptions> options) : IWhatsAppMediaClient
{
    public async Task<WhatsAppMediaDownloadResult> DownloadAsync(
        string mediaId,
        string expectedMimeType,
        long maximumBytes,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.GraphApiVersion) ||
            string.IsNullOrWhiteSpace(settings.PhoneNumberId) || string.IsNullOrWhiteSpace(settings.AccessToken))
            return new(false, null, null, null, "NOT_CONFIGURED");

        using var metadataRequest = CreateRequest(HttpMethod.Get,
            $"{settings.GraphApiVersion}/{Uri.EscapeDataString(mediaId)}?phone_number_id={Uri.EscapeDataString(settings.PhoneNumberId)}",
            settings.AccessToken);
        using var metadataResponse = await httpClient.SendAsync(metadataRequest, cancellationToken);
        if (!metadataResponse.IsSuccessStatusCode)
            return new(false, null, null, null, $"META_{(int)metadataResponse.StatusCode}");

        var metadata = await metadataResponse.Content.ReadFromJsonAsync<MediaMetadata>(cancellationToken: cancellationToken);
        if (metadata is null || !Uri.TryCreate(metadata.Url, UriKind.Absolute, out var downloadUri) || !IsTrustedDownloadUri(downloadUri))
            return new(false, null, null, metadata?.FileSize, "INVALID_METADATA");

        var normalizedExpected = WhatsAppMediaPolicy.NormalizeMimeType(expectedMimeType);
        var normalizedMetadata = WhatsAppMediaPolicy.NormalizeMimeType(metadata.MimeType);
        if (normalizedExpected.Length == 0 || !string.Equals(normalizedExpected, normalizedMetadata, StringComparison.OrdinalIgnoreCase))
            return new(false, null, normalizedMetadata, metadata.FileSize, "MIME_MISMATCH");
        if (metadata.FileSize is not > 0 || metadata.FileSize > maximumBytes)
            return new(false, null, normalizedMetadata, metadata.FileSize, "SIZE_REJECTED");
        var expectedSize = metadata.FileSize.Value;

        using var downloadRequest = CreateRequest(HttpMethod.Get, downloadUri, settings.AccessToken);
        using var downloadResponse = await httpClient.SendAsync(downloadRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!downloadResponse.IsSuccessStatusCode)
            return new(false, null, normalizedMetadata, metadata.FileSize, $"DOWNLOAD_{(int)downloadResponse.StatusCode}");

        var responseMime = WhatsAppMediaPolicy.NormalizeMimeType(downloadResponse.Content.Headers.ContentType?.MediaType);
        if (responseMime.Length > 0 && !string.Equals(normalizedMetadata, responseMime, StringComparison.OrdinalIgnoreCase))
            return new(false, null, responseMime, metadata.FileSize, "MIME_MISMATCH");
        if (downloadResponse.Content.Headers.ContentLength is > 0 and var contentLength && contentLength > maximumBytes)
            return new(false, null, normalizedMetadata, contentLength, "SIZE_REJECTED");

        await using var source = await downloadResponse.Content.ReadAsStreamAsync(cancellationToken);
        var content = new MemoryStream((int)Math.Min(expectedSize, 1024 * 1024));
        var buffer = new byte[81920];
        long total = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            total += read;
            if (total > maximumBytes)
            {
                content.Dispose();
                return new(false, null, normalizedMetadata, total, "SIZE_REJECTED");
            }
            await content.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        if (total == 0)
        {
            content.Dispose();
            return new(false, null, normalizedMetadata, 0, "EMPTY_MEDIA");
        }

        content.Position = 0;
        return new(true, content, normalizedMetadata, total, null);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string uri, string accessToken) =>
        CreateRequest(method, new Uri(uri, UriKind.Relative), accessToken);

    private static HttpRequestMessage CreateRequest(HttpMethod method, Uri uri, string accessToken)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static bool IsTrustedDownloadUri(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo)) return false;
        var host = uri.IdnHost;
        return host.Equals("facebook.com", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".facebook.com", StringComparison.OrdinalIgnoreCase) ||
               host.Equals("fbcdn.net", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".fbcdn.net", StringComparison.OrdinalIgnoreCase) ||
               host.Equals("fbsbx.com", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".fbsbx.com", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record MediaMetadata(
        string? Url,
        [property: JsonPropertyName("mime_type")] string? MimeType,
        [property: JsonPropertyName("file_size")] long? FileSize);
}
