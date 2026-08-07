using Files.Contracts;

namespace Files.Implementation;

internal static class AttachmentUploadValidator
{
    public const long FileLimitBytes = 10L * 1024 * 1024;

    public static async Task<ValidatedAttachmentUpload> ValidateAsync(
        UploadAttachment command,
        Stream content,
        CancellationToken cancellationToken)
    {
        var fileName = ValidateFileName(command.OriginalFileName);
        if (command.DeclaredLength is < 0 or > FileLimitBytes)
        {
            throw Rule("attachment_too_large", "Eine Datei darf höchstens 10 MiB groß sein.");
        }

        await using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        while (true)
        {
            var read = await content.ReadAsync(chunk, cancellationToken);
            if (read == 0)
            {
                break;
            }
            if (buffer.Length + read > FileLimitBytes)
            {
                throw Rule("attachment_too_large", "Eine Datei darf höchstens 10 MiB groß sein.");
            }
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
        if (command.DeclaredLength is { } declaredLength && declaredLength != buffer.Length)
        {
            throw Rule("attachment_length_mismatch", "Die gemeldete Dateigröße stimmt nicht mit der Datei überein.");
        }

        var format = Detect(fileName, command.DeclaredContentType, buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length)));
        return new ValidatedAttachmentUpload(
            fileName,
            format.MediaType,
            format.ContentType,
            buffer.ToArray());
    }

    private static string ValidateFileName(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length is 0 or > 255
            || trimmed.IndexOfAny(['/', '\\', '\r', '\n']) >= 0)
        {
            throw Rule("attachment_filename_invalid", "Der Dateiname ist ungültig.");
        }
        return trimmed;
    }

    private static AttachmentFormat Detect(string fileName, string declaredContentType, ReadOnlySpan<byte> bytes)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var contentType = declaredContentType.Trim().ToLowerInvariant();
        var candidates = new[]
        {
            new AttachmentFormat(AttachmentMediaType.Pdf, "application/pdf", [".pdf"], [0x25, 0x50, 0x44, 0x46, 0x2d]),
            new AttachmentFormat(AttachmentMediaType.Jpeg, "image/jpeg", [".jpg", ".jpeg"], [0xff, 0xd8, 0xff]),
            new AttachmentFormat(AttachmentMediaType.Png, "image/png", [".png"], [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
            new AttachmentFormat(AttachmentMediaType.WebP, "image/webp", [".webp"], [])
        };
        var format = candidates.SingleOrDefault(item =>
            item.ContentType == contentType && item.Extensions.Contains(extension, StringComparer.Ordinal));
        if (format is null || !MatchesMagic(format, bytes))
        {
            throw Rule(
                "attachment_format_invalid",
                "Dateiendung, Dateityp und tatsächlicher Dateiinhalt müssen übereinstimmen.");
        }
        return format;
    }

    private static bool MatchesMagic(AttachmentFormat format, ReadOnlySpan<byte> bytes)
    {
        if (format.MediaType == AttachmentMediaType.WebP)
        {
            return bytes.Length >= 12
                && bytes[..4].SequenceEqual("RIFF"u8)
                && bytes.Slice(8, 4).SequenceEqual("WEBP"u8);
        }
        return bytes.StartsWith(format.Magic);
    }

    private static FilesRuleException Rule(string code, string message) => new(code, message);

    private sealed record AttachmentFormat(
        AttachmentMediaType MediaType,
        string ContentType,
        string[] Extensions,
        byte[] Magic);
}

internal sealed record ValidatedAttachmentUpload(
    string FileName,
    AttachmentMediaType MediaType,
    string ContentType,
    byte[] Content);
