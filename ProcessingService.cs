using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using Dapper;
using Npgsql;

namespace NodeWorker;

public sealed partial class ProcessingService(IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString("Database")
        ?? throw new InvalidOperationException("ConnectionStrings:Database is not configured.");

    public async Task<ProcessResponse> ProcessAsync(ProcessRequest request, CancellationToken cancellationToken)
    {
        var url = DecodeText(request.UrlB64!, "URL_BASE64_INVALID", "The URL is not valid base64.");
        var page = DecodeText(request.PageB64!, "PAGE_BASE64_INVALID", "The page is not valid base64.");
        var key = DecodeBytes(request.KeyBytesB64!, "KEY_BASE64_INVALID", "The AES key is not valid base64.");
        var encryptedText = DecodeBytes(
            request.EncryptedTextBytesB64!,
            "ENCRYPTED_TEXT_BASE64_INVALID",
            "The encrypted text is not valid base64.");

        if (key.Length != 32 || encryptedText.Length == 0 || encryptedText.Length % 16 != 0)
        {
            throw new RequestProcessingException(
                "AES_INPUT_INVALID",
                "AES-256 requires a 32-byte key and non-empty ciphertext in 16-byte blocks.");
        }

        var parser = new HtmlParser();
        var document = await parser.ParseDocumentAsync(page, cancellationToken);
        var selectedElements = document.QuerySelectorAll(request.Selector!);
        var elementRecords = selectedElements
            .Select(element => new ElementRecord(
                element.GetAttribute(request.Attribute!) ?? string.Empty,
                element.OuterHtml))
            .ToList();
        var attributeValues = elementRecords.Select(element => element.AttributeValue).ToList();
        var emails = EmailAddressMatcher.EmailRegex().Matches(page)
            .Select(match => match.Value)
            .ToList();
        var plainText = await DecryptAsync(encryptedText, key, cancellationToken);

        await SaveElementsAsync(elementRecords, cancellationToken);

        return new ProcessResponse
        {
            ElementsCount = elementRecords.Count,
            EmailsCount = emails.Count,
            Url = url,
            DecryptedPlainText = plainText,
            ElementsAttrList = attributeValues,
            EmailsList = emails
        };
    }

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            CREATE TABLE IF NOT EXISTS elements (
                id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                attribute_value TEXT NOT NULL,
                html TEXT NOT NULL
            );
            """,
            cancellationToken: cancellationToken));
    }

    private async Task SaveElementsAsync(List<ElementRecord> elements, CancellationToken cancellationToken)
    {
        if (elements.Count == 0)
        {
            return;
        }

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO elements (attribute_value, html) VALUES (@AttributeValue, @Html)",
            elements,
            transaction,
            cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
    }

    private static string DecodeText(string encoded, string code, string message)
    {
        try
        {
            return new UTF8Encoding(false, true).GetString(Convert.FromBase64String(encoded));
        }
        catch (Exception exception) when (exception is FormatException or DecoderFallbackException)
        {
            throw new RequestProcessingException(code, message);
        }
    }

    private static byte[] DecodeBytes(string encoded, string code, string message)
    {
        try
        {
            return Convert.FromBase64String(encoded);
        }
        catch (FormatException)
        {
            throw new RequestProcessingException(code, message);
        }
    }

    private static async Task<string> DecryptAsync(
        byte[] encryptedText,
        byte[] key,
        CancellationToken cancellationToken)
    {
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Key = key;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;

        await using var output = new MemoryStream();
        await using (var cryptoStream = new CryptoStream(
            output,
            aes.CreateDecryptor(),
            CryptoStreamMode.Write,
            leaveOpen: true))
        {
            await cryptoStream.WriteAsync(encryptedText, cancellationToken);
            await cryptoStream.FlushFinalBlockAsync(cancellationToken);
        }

        var plaintext = new UTF8Encoding(false, true).GetString(output.ToArray());
        return plaintext.TrimEnd('\0');
    }

    private static partial class EmailAddressMatcher
    {
        [GeneratedRegex(@"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        public static partial Regex EmailRegex();
    }
}