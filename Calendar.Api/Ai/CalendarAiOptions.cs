namespace Calendar.Api.Ai;

/// <summary>
/// Configures the server-side calendar text interpretation provider.
/// </summary>
public sealed class CalendarAiOptions
{
    /// <summary>Gets or sets the provider name, either <c>Fake</c> or <c>OpenAI</c>.</summary>
    public string? Provider { get; set; }

    /// <summary>Gets or sets the absolute OpenAI-compatible endpoint URL.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Gets or sets the model deployment name.</summary>
    public string? Model { get; set; }

    /// <summary>Gets or sets the server-side API key.</summary>
    public string? ApiKey { get; set; }
}