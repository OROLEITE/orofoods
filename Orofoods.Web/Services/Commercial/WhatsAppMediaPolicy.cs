using Orofoods.Web.Models.Commercial;

namespace Orofoods.Web.Services.Commercial;

public static class WhatsAppMediaPolicy
{
    private static readonly IReadOnlyDictionary<string, string> Extensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
        ["audio/aac"] = ".aac",
        ["audio/amr"] = ".amr",
        ["audio/mpeg"] = ".mp3",
        ["audio/mp4"] = ".m4a",
        ["audio/ogg"] = ".ogg",
        ["video/mp4"] = ".mp4",
        ["video/3gpp"] = ".3gp",
        ["application/pdf"] = ".pdf",
        ["text/plain"] = ".txt",
        ["application/msword"] = ".doc",
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = ".docx",
        ["application/vnd.ms-excel"] = ".xls",
        ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = ".xlsx"
    };

    public static bool IsMedia(WhatsAppMessageType type) => type is
        WhatsAppMessageType.Image or WhatsAppMessageType.Audio or WhatsAppMessageType.Video or
        WhatsAppMessageType.Document or WhatsAppMessageType.Sticker;

    public static string NormalizeMimeType(string? value) =>
        (value ?? string.Empty).Split(';', 2)[0].Trim().ToLowerInvariant();

    public static bool IsAllowed(WhatsAppMessageType type, string? mimeType)
    {
        var mime = NormalizeMimeType(mimeType);
        return type switch
        {
            WhatsAppMessageType.Image => mime is "image/jpeg" or "image/png",
            WhatsAppMessageType.Audio => mime is "audio/aac" or "audio/amr" or "audio/mpeg" or "audio/mp4" or "audio/ogg",
            WhatsAppMessageType.Video => mime is "video/mp4" or "video/3gpp",
            WhatsAppMessageType.Document => Extensions.ContainsKey(mime) && !mime.StartsWith("image/", StringComparison.Ordinal) &&
                                           !mime.StartsWith("audio/", StringComparison.Ordinal) && !mime.StartsWith("video/", StringComparison.Ordinal),
            WhatsAppMessageType.Sticker => mime == "image/webp",
            _ => false
        };
    }

    public static string ExtensionFor(string mimeType) =>
        Extensions.TryGetValue(NormalizeMimeType(mimeType), out var extension) ? extension : string.Empty;

    public static string SafeDisplayFileName(string? supplied, string mimeType)
    {
        var extension = ExtensionFor(mimeType);
        var raw = Path.GetFileName((supplied ?? string.Empty).Replace('\\', '/'));
        var stem = Path.GetFileNameWithoutExtension(raw);
        var safeStem = new string(stem.Where(character => char.IsLetterOrDigit(character) || character is ' ' or '-' or '_' or '.').ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(safeStem)) safeStem = "arquivo";
        if (safeStem.Length > 200) safeStem = safeStem[..200];
        return safeStem + extension;
    }
}
