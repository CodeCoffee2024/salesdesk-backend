using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Infrastructure.Services;

/// <summary>
/// Parses pasted quote/invoice text via Google's Gemini API (TASK-033) using its
/// structured-output mode (responseSchema), which forces the model to return JSON
/// matching a fixed shape rather than free-form prose. Registered in place of
/// UnconfiguredQuoteTextParser only once Gemini:ApiKey is set — see
/// DependencyInjection, matching ResendEmailSender's own conditional-registration
/// pattern.
///
/// The requested schema only ever asks for raw quantity/unit_price per line item,
/// never a subtotal or total — the model is never given the chance to do the
/// arithmetic, which is the whole of TASK-033's "Deterministic Math Guardrail".
/// All totals still come from the app's own line-item calculation, unchanged.
/// </summary>
public sealed class GeminiQuoteTextParser(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiQuoteTextParser> logger) : IQuoteTextParser
{
    private static readonly JsonSerializerOptions ResponseJsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<ParsedQuoteText> ParseAsync(string rawText, CancellationToken cancellationToken)
    {
        var model = configuration["Gemini:Model"];
        if (string.IsNullOrWhiteSpace(model))
        {
            model = "gemini-3.6-flash";
        }

        var apiKey = configuration["Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            // DependencyInjection only registers this class when the key is present,
            // so reaching here means it was cleared at runtime after startup.
            throw new AiParsingUnavailableException("AI text parsing isn't configured on this server. Set Gemini:ApiKey to enable it.");
        }

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = BuildPrompt(rawText) }
                    }
                }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                responseSchema = ResponseSchema
            }
        };

        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsJsonAsync($"v1beta/models/{model}:generateContent?key={Uri.EscapeDataString(apiKey)}", requestBody, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new AiParsingFailedException("Could not reach the AI parsing service. Please try again.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("Gemini parse request failed ({Status}): {Body}", response.StatusCode, errorBody);
            throw new AiParsingFailedException("The AI parsing service returned an error. Please try again, or fill in the form manually.");
        }

        var envelope = await response.Content.ReadFromJsonAsync<GeminiResponseEnvelope>(cancellationToken);
        var jsonText = envelope?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

        if (string.IsNullOrWhiteSpace(jsonText))
        {
            logger.LogError("Gemini parse response had no text part to parse");
            throw new AiParsingFailedException("The AI parsing service returned an empty response. Please try again, or fill in the form manually.");
        }

        ParsedPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<ParsedPayload>(jsonText, ResponseJsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Gemini parse response was not valid JSON: {JsonText}", jsonText);
            throw new AiParsingFailedException("The AI parsing service returned something unexpected. Please try again, or fill in the form manually.");
        }

        if (payload is null)
        {
            throw new AiParsingFailedException("The AI parsing service returned something unexpected. Please try again, or fill in the form manually.");
        }

        return new ParsedQuoteText(
            new ParsedCustomerText(payload.Customer?.Name, payload.Customer?.Email, payload.Customer?.Phone, payload.Customer?.Company),
            payload.LineItems?.Select(li => new ParsedLineItemText(li.Description ?? string.Empty, li.Quantity, li.UnitPrice)).ToList() ?? [],
            payload.DepositPercentage,
            payload.ValidityDays);
    }

    private static string BuildPrompt(string rawText) =>
        "Extract structured quote/invoice data from the pasted text below. Only extract facts explicitly stated or unambiguously implied. " +
        "Do not calculate a subtotal, tax, or total, and do not multiply quantity by unit price. Extract only the raw quantity and unit price " +
        "for each line item exactly as they appear; leave a field null when the text doesn't mention it. Text:\n\n" + rawText;

    /// <summary>Gemini's schema format is an OpenAPI-3.0 subset with uppercase type names (STRING, NUMBER, OBJECT, ARRAY, INTEGER).</summary>
    private static readonly object ResponseSchema = new
    {
        type = "OBJECT",
        properties = new
        {
            customer = new
            {
                type = "OBJECT",
                properties = new
                {
                    name = new { type = "STRING", nullable = true },
                    email = new { type = "STRING", nullable = true },
                    phone = new { type = "STRING", nullable = true },
                    company = new { type = "STRING", nullable = true }
                }
            },
            lineItems = new
            {
                type = "ARRAY",
                items = new
                {
                    type = "OBJECT",
                    properties = new
                    {
                        description = new { type = "STRING" },
                        quantity = new { type = "NUMBER" },
                        unitPrice = new { type = "NUMBER" }
                    },
                    required = new[] { "description", "quantity", "unitPrice" }
                }
            },
            depositPercentage = new { type = "NUMBER", nullable = true },
            validityDays = new { type = "INTEGER", nullable = true }
        },
        required = new[] { "customer", "lineItems" }
    };

    private sealed class ParsedPayload
    {
        public ParsedCustomerPayload? Customer { get; set; }
        public List<ParsedLineItemPayload>? LineItems { get; set; }
        public decimal? DepositPercentage { get; set; }
        public int? ValidityDays { get; set; }
    }

    private sealed class ParsedCustomerPayload
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Company { get; set; }
    }

    private sealed class ParsedLineItemPayload
    {
        public string? Description { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    private sealed class GeminiResponseEnvelope
    {
        [JsonPropertyName("candidates")]
        public List<GeminiCandidate>? Candidates { get; set; }
    }

    private sealed class GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiContent? Content { get; set; }
    }

    private sealed class GeminiContent
    {
        [JsonPropertyName("parts")]
        public List<GeminiPart>? Parts { get; set; }
    }

    private sealed class GeminiPart
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}
