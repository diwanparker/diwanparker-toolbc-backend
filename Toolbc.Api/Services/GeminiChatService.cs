using System.Net.Http.Json;
using System.Text.Json;
using Toolbc.Api.Contracts;
using Toolbc.Api.Domain;

namespace Toolbc.Api.Services;

public interface IGeminiChatService
{
    Task<ChatReplyResponse> GenerateReplyAsync(ChatReplyRequest request, CancellationToken cancellationToken);
}

public sealed class GeminiChatService(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<GeminiChatService> logger) : IGeminiChatService
{
    public async Task<ChatReplyResponse> GenerateReplyAsync(ChatReplyRequest request, CancellationToken cancellationToken)
    {
        var provider = PreferredProvider();
        var providers = provider == "openai"
            ? new[] { "openai", "gemini" }
            : new[] { "gemini", "openai" };
        var hasOpenAi = !string.IsNullOrWhiteSpace(configuration["OpenAI:ApiKey"]);
        var hasGemini = GeminiApiKeys().Count > 0;

        foreach (var item in providers)
        {
            try
            {
                if (item == "openai" && hasOpenAi)
                {
                    return new ChatReplyResponse(
                        await GenerateOpenAiReplyAsync(request, cancellationToken),
                        true);
                }

                if (item == "gemini" && hasGemini)
                {
                    return new ChatReplyResponse(
                        await GenerateGeminiReplyAsync(request, cancellationToken),
                        true);
                }
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(exception, "{Provider} provider failed; trying next configured provider.", item);
                // Try the next configured provider before falling back.
            }
        }

        return new ChatReplyResponse(FallbackReply(request.Mode), false);
    }

    private async Task<string> GenerateOpenAiReplyAsync(ChatReplyRequest request, CancellationToken cancellationToken)
    {
        var apiKey = configuration["OpenAI:ApiKey"]!;
        var model = configuration["OpenAI:Model"] ?? "gpt-5-mini";
        var payload = new
        {
            model,
            instructions = SystemPrompt(request.Mode),
            input = request.History.TakeLast(8).Select(turn => new
            {
                role = turn.FromUser ? "user" : "assistant",
                content = turn.Text
            }),
            max_output_tokens = 360
        };

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses")
        {
            Content = JsonContent.Create(payload)
        };
        requestMessage.Headers.Authorization = new("Bearer", apiKey);

        using var response = await httpClient.SendAsync(requestMessage, cancellationToken);
        await EnsureSuccessAsync(response, "OpenAI", cancellationToken);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var text = ExtractOpenAiText(json);
        return string.IsNullOrWhiteSpace(text)
            ? throw new InvalidOperationException("OpenAI response did not include text.")
            : text.Trim();
    }

    private async Task<string> GenerateGeminiReplyAsync(ChatReplyRequest request, CancellationToken cancellationToken)
    {
        var apiKeys = GeminiApiKeys();
        var failures = new List<Exception>();

        for (var index = 0; index < apiKeys.Count; index++)
        {
            try
            {
                return await GenerateGeminiReplyWithKeyAsync(request, apiKeys[index], cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Gemini provider failed for configured API key index {KeyIndex}.", index + 1);
                failures.Add(exception);
            }
        }

        throw new AggregateException("Gemini failed for all configured API keys.", failures);
    }

    private async Task<string> GenerateGeminiReplyWithKeyAsync(
        ChatReplyRequest request,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var model = configuration["Gemini:Model"] ?? "gemini-2.5-flash";
        var uri = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";
        var payload = new
        {
            systemInstruction = new
            {
                parts = new[] { new { text = SystemPrompt(request.Mode) } }
            },
            contents = request.History.TakeLast(8).Select(turn => new
            {
                role = turn.FromUser ? "user" : "model",
                parts = new[] { new { text = turn.Text } }
            }),
            generationConfig = new
            {
                temperature = 0.45,
                topP = 0.9,
                maxOutputTokens = 360
            }
        };

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(payload)
        };
        requestMessage.Headers.Add("x-goog-api-key", apiKey);

        using var response = await httpClient.SendAsync(requestMessage, cancellationToken);
        await EnsureSuccessAsync(response, "Gemini", cancellationToken);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var text = ExtractGeminiText(json);
        return string.IsNullOrWhiteSpace(text)
            ? throw new InvalidOperationException("Gemini response did not include text.")
            : text.Trim();
    }

    private string PreferredProvider()
    {
        var configured = configuration["AI:Provider"]?.Trim().ToLowerInvariant();
        if (configured is "openai" or "gemini")
        {
            return configured;
        }

        return string.IsNullOrWhiteSpace(configuration["OpenAI:ApiKey"]) ? "gemini" : "openai";
    }

    private IReadOnlyList<string> GeminiApiKeys()
    {
        var keys = new List<string?>();
        var configuredList = configuration.GetSection("Gemini:ApiKeys").Get<string[]>();
        if (configuredList is not null)
        {
            keys.AddRange(configuredList);
        }

        keys.Add(configuration["Gemini:ApiKey1"]);
        keys.Add(configuration["Gemini:ApiKey2"]);
        keys.Add(configuration["Gemini:ApiKey3"]);
        keys.Add(configuration["Gemini:ApiKey"]);

        return keys
            .Select(static key => key?.Trim())
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .Select(static key => key!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string provider,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            $"{provider} API error {(int)response.StatusCode}: {TrimApiError(body)}",
            inner: null,
            response.StatusCode);
    }

    private static string TrimApiError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return "empty response body";
        }

        var normalized = body.ReplaceLineEndings(" ").Trim();
        return normalized.Length <= 500 ? normalized : normalized[..500];
    }

    private static string? ExtractOpenAiText(JsonElement body)
    {
        if (body.TryGetProperty("output_text", out var outputText) &&
            outputText.ValueKind == JsonValueKind.String)
        {
            return outputText.GetString();
        }

        if (!body.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var chunks = new List<string>();
        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content) ||
                content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var part in content.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var text) &&
                    text.ValueKind == JsonValueKind.String)
                {
                    chunks.Add(text.GetString() ?? string.Empty);
                }
            }
        }

        return string.Join("\n", chunks);
    }

    private static string? ExtractGeminiText(JsonElement body)
    {
        if (!body.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
        {
            return null;
        }

        var first = candidates[0];
        if (!first.TryGetProperty("content", out var content) ||
            !content.TryGetProperty("parts", out var parts))
        {
            return null;
        }

        var chunks = new List<string>();
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var text))
            {
                chunks.Add(text.GetString() ?? string.Empty);
            }
        }

        return string.Join("\n", chunks);
    }

    private static string FallbackReply(UserRole role)
    {
        var roleText = role switch
        {
            UserRole.Doctor => "dokter dapat memantau pasien, kepatuhan obat, reminder, dan eskalasi.",
            UserRole.Admin => "admin dapat membuat akun pasien dan dokter sesuai domain email role.",
            _ => "pasien dapat mencatat minum obat, mengisi checkup gejala, melihat riwayat, dan membaca notifikasi."
        };

        return $"AI provider belum dikonfigurasi di backend. Untuk sementara, ToolBC tetap bisa dipakai: {roleText}";
    }

    private static string SystemPrompt(UserRole mode)
    {
        var roleName = mode switch
        {
            UserRole.Doctor => "dokter",
            UserRole.Admin => "admin/resepsionis",
            _ => "pasien/user"
        };

        return $"""
            Kamu AI ToolBC/TBC Care untuk role {roleName}. Jawab singkat, ramah, Bahasa Indonesia.
            ToolBC memantau pengobatan TBC, kepatuhan minum obat, checkup harian, reminder, riwayat progres, dan komunikasi perawatan.
            Beri edukasi umum, bukan diagnosis atau pengganti dokter. Jangan ubah dosis/obat. Untuk gejala berat, sarankan bantuan medis segera.
            """;
    }
}
