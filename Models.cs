using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FluentValidation;

namespace NodeWorker;

/// <summary>Запрос на обработку закодированной HTML-страницы.</summary>
public sealed record ProcessRequest
{
    [JsonPropertyName("selector")]
    public string? Selector { get; init; }

    [JsonPropertyName("attribute")]
    public string? Attribute { get; init; }

    [JsonPropertyName("url_b64")]
    public string? UrlB64 { get; init; }

    [JsonPropertyName("encrypted_text_bytes_b64")]
    public string? EncryptedTextBytesB64 { get; init; }

    [JsonPropertyName("key_bytes_b64")]
    public string? KeyBytesB64 { get; init; }

    [JsonPropertyName("page_b64")]
    public string? PageB64 { get; init; }
}

/// <summary>Результат обработки страницы и найденных в ней данных.</summary>
public sealed record ProcessResponse
{
    [JsonPropertyName("is_error")]
    public int IsError { get; init; }

    [JsonPropertyName("error_code")]
    public string ErrorCode { get; init; } = string.Empty;

    [JsonPropertyName("error_message")]
    public string ErrorMessage { get; init; } = string.Empty;

    [JsonPropertyName("elements_count")]
    public int ElementsCount { get; init; }

    [JsonPropertyName("emails_count")]
    public int EmailsCount { get; init; }

    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    [JsonPropertyName("decrypted_plain_text")]
    public string DecryptedPlainText { get; init; } = string.Empty;

    [JsonPropertyName("elements_attr_list")]
    public List<string> ElementsAttrList { get; init; } = [];

    [JsonPropertyName("emails_list")]
    public List<string> EmailsList { get; init; } = [];

    public static ProcessResponse Failure(string code, string message) => new()
    {
        IsError = 1,
        ErrorCode = code,
        ErrorMessage = message
    };
}

public sealed record ElementRecord(string AttributeValue, string Html);

public sealed class ProcessRequestValidator : AbstractValidator<ProcessRequest>
{
    public ProcessRequestValidator()
    {
        RuleFor(request => request.Selector)
            .NotEmpty()
            .WithErrorCode("SELECTOR_EMPTY")
            .WithMessage("The CSS selector is required.");

        RuleFor(request => request.Attribute)
            .NotEmpty()
            .WithErrorCode("ATTRIBUTE_EMPTY")
            .WithMessage("The HTML attribute name is required.");

        RuleFor(request => request.UrlB64)
            .NotEmpty()
            .WithErrorCode("URL_MISSING")
            .WithMessage("The URL base64 value is required.");

        RuleFor(request => request.EncryptedTextBytesB64)
            .NotEmpty()
            .WithErrorCode("ENCRYPTED_TEXT_MISSING")
            .WithMessage("The encrypted text base64 value is required.");

        RuleFor(request => request.KeyBytesB64)
            .NotEmpty()
            .WithErrorCode("KEY_MISSING")
            .WithMessage("The AES key base64 value is required.");

        RuleFor(request => request.PageB64)
            .NotEmpty()
            .WithErrorCode("PAGE_MISSING")
            .WithMessage("The page base64 value is required.");
    }
}

public sealed class RequestProcessingException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}