using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace WhenWorksWeb.Services;

/// <summary>
/// Sends transactional email (account confirmation, password reset, email-change confirmation) via
/// Brevo's HTTP transactional email API, so ASP.NET Core Identity's confirmation/reset links are
/// actually delivered instead of silently dropped by Identity's default no-op sender.
/// </summary>
/// <remarks>Registered as a typed <see cref="HttpClient"/> client in Program.cs (see
/// Spec/Features/FEATURES-email-verification.ospec) — no Brevo SDK package, just a plain REST call
/// to <c>POST /v3/smtp/email</c>. The API key is read from configuration (user-secrets locally, an
/// environment variable in production) and is never logged, since it is a live credential capable of
/// sending mail as this app's verified sender.</remarks>
/// <param name="httpClient">
/// A typed client pre-configured with Brevo's base address, <c>api-key</c> header, and a request
/// timeout by the <c>AddHttpClient&lt;BrevoEmailSender&gt;</c> registration in Program.cs.
/// </param>
/// <param name="logger">Used to record send outcomes without ever logging email content or tokens.</param>
/// <param name="configuration">
/// Supplies <c>Brevo:SenderEmail</c> -- the verified "from" address configured on this app's Brevo
/// account. Read from configuration rather than hardcoded since it's expected to change (e.g. moving
/// off a placeholder address to a real domain) without needing a code change/redeploy. Not a secret
/// (it's a mailbox address, not a credential), so unlike <c>Brevo:ApiKey</c> it's fine to commit in
/// appsettings.json rather than requiring user-secrets/an environment variable.
/// </param>
public class BrevoEmailSender(HttpClient httpClient, ILogger<BrevoEmailSender> logger, IConfiguration configuration) : IEmailSender
{
    // The display name Brevo shows recipients as the "from" name. Kept as a constant, unlike
    // SenderEmail below -- it's not tied to a specific verified mailbox, so there's no operational
    // reason it would need to change independent of a code change.
    private const string SenderName = "WhenWorks";

    private readonly string _senderEmail = !string.IsNullOrWhiteSpace(configuration["Brevo:SenderEmail"])
        ? configuration["Brevo:SenderEmail"]!
        : throw new InvalidOperationException("Configuration value 'Brevo:SenderEmail' not found.");

    /// <summary>
    /// Sends an HTML email to <paramref name="email"/> via Brevo's transactional email API.
    /// </summary>
    /// <remarks>Failures are logged (status code only -- never the request/response body, which
    /// contains the recipient's confirmation/reset link) and swallowed rather than thrown, matching
    /// the no-op sender's contract that Identity's calling pages assume: a failed send should not
    /// crash the request, and no code path here reveals internal error detail back to the end user.</remarks>
    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var request = new BrevoEmailRequest(
            Sender: new BrevoSender(SenderName, _senderEmail),
            To: [new BrevoRecipient(email)],
            Subject: subject,
            HtmlContent: htmlMessage);

        try
        {
            var response = await httpClient.PostAsJsonAsync("v3/smtp/email", request);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Brevo email send to {Email} failed with status {StatusCode}.", email, (int)response.StatusCode);
            }
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Brevo email send to {Email} failed.", email);
        }
        catch (TaskCanceledException ex)
        {
            // PostAsJsonAsync surfaces the HttpClient's configured request timeout this way.
            logger.LogWarning(ex, "Brevo email send to {Email} timed out.", email);
        }
    }

    /// <summary>The JSON body <c>POST /v3/smtp/email</c> expects, per Brevo's transactional email API docs.</summary>
    private sealed record BrevoEmailRequest(
        [property: JsonPropertyName("sender")] BrevoSender Sender,
        [property: JsonPropertyName("to")] BrevoRecipient[] To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("htmlContent")] string HtmlContent);

    /// <summary>The "from" mailbox and display name Brevo shows recipients.</summary>
    private sealed record BrevoSender(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("email")] string Email);

    /// <summary>One entry in the request's <c>to</c> array -- just the recipient's address.</summary>
    private sealed record BrevoRecipient(
        [property: JsonPropertyName("email")] string Email);
}
