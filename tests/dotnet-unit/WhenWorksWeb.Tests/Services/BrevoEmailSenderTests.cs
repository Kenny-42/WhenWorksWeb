using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using WhenWorksWeb.Services;

namespace WhenWorksWeb.Tests.Services;

/// <summary>
/// Tests for <see cref="BrevoEmailSender"/>: the request it sends to Brevo's transactional email
/// API -- including the exact JSON shape Brevo's <c>POST /v3/smtp/email</c> endpoint requires -- that
/// the configured sender address is used, and that send failures are swallowed (logged, not thrown)
/// so a broken/unreachable Brevo call never crashes the Identity page that triggered it.
/// </summary>
public class BrevoEmailSenderTests
{
    private const string ConfiguredSenderEmail = "whenworks@yahoo.com";

    private static (BrevoEmailSender Sender, DelegateHttpMessageHandler Handler) CreateSender(
        HttpStatusCode responseStatus = HttpStatusCode.OK, string? senderEmail = ConfiguredSenderEmail)
    {
        var handler = DelegateHttpMessageHandler.ReturningStatus(responseStatus);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.brevo.com/") };
        var logger = Substitute.For<ILogger<BrevoEmailSender>>();
        var configuration = BuildConfiguration(senderEmail);

        return (new BrevoEmailSender(httpClient, logger, configuration), handler);
    }

    /// <summary>
    /// Builds a real <see cref="IConfiguration"/> (rather than a mock) with <c>Brevo:SenderEmail</c>
    /// set to <paramref name="senderEmail"/>, or omitted entirely when null -- exercises the same
    /// <c>configuration["Brevo:SenderEmail"]</c> lookup Program.cs's configuration binding performs.
    /// </summary>
    private static IConfiguration BuildConfiguration(string? senderEmail)
    {
        var data = senderEmail is null
            ? new Dictionary<string, string?>()
            : new Dictionary<string, string?> { ["Brevo:SenderEmail"] = senderEmail };

        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }

    [Fact]
    public async Task SendEmailAsync_PostsToBrevoSmtpEmailEndpoint()
    {
        var (sender, handler) = CreateSender();

        await sender.SendEmailAsync("user@example.com", "Confirm your email", "<a href='https://example.com'>link</a>");

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal(new Uri("https://api.brevo.com/v3/smtp/email"), handler.LastRequest.RequestUri);
    }

    [Fact]
    public async Task SendEmailAsync_SendsRecipientSubjectAndHtmlContentInRequestBody()
    {
        var (sender, handler) = CreateSender();

        await sender.SendEmailAsync("user@example.com", "Confirm your email", "<a href='https://example.com'>link</a>");

        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        var root = body.RootElement;

        Assert.Equal("user@example.com", root.GetProperty("to")[0].GetProperty("email").GetString());
        Assert.Equal("Confirm your email", root.GetProperty("subject").GetString());
        Assert.Equal("<a href='https://example.com'>link</a>", root.GetProperty("htmlContent").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("sender").GetProperty("email").GetString()));
    }

    /// <summary>
    /// Pins the request body to exactly the shape Brevo's <c>POST /v3/smtp/email</c> endpoint
    /// documents: <c>sender.name</c>/<c>sender.email</c>, a <c>to</c> array of <c>{email}</c> objects,
    /// <c>subject</c>, and <c>htmlContent</c>, and nothing else at the top level. A field renamed or
    /// removed here (e.g. <c>[JsonPropertyName]</c> drifting from Brevo's contract) would otherwise
    /// only surface as a silently-logged 400 from Brevo in production, since <see
    /// cref="BrevoEmailSender.SendEmailAsync"/> swallows failures by design.
    /// </summary>
    [Fact]
    public async Task SendEmailAsync_RequestBodyMatchesBrevosDocumentedShape()
    {
        var (sender, handler) = CreateSender();

        await sender.SendEmailAsync("user@example.com", "Confirm your email", "<p>body</p>");

        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        var root = body.RootElement;

        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal(["sender", "to", "subject", "htmlContent"], root.EnumerateObject().Select(p => p.Name).ToArray());

        var sender_ = root.GetProperty("sender");
        Assert.Equal(JsonValueKind.Object, sender_.ValueKind);
        Assert.Equal(["name", "email"], sender_.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal("WhenWorks", sender_.GetProperty("name").GetString());
        Assert.Equal(ConfiguredSenderEmail, sender_.GetProperty("email").GetString());

        var to = root.GetProperty("to");
        Assert.Equal(JsonValueKind.Array, to.ValueKind);
        Assert.Single(to.EnumerateArray());
        Assert.Equal(["email"], to[0].EnumerateObject().Select(p => p.Name).ToArray());
    }

    [Fact]
    public async Task SendEmailAsync_UsesSenderEmailFromConfiguration()
    {
        var (sender, handler) = CreateSender(senderEmail: "custom-sender@example.com");

        await sender.SendEmailAsync("user@example.com", "Confirm your email", "<p>body</p>");

        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal("custom-sender@example.com", body.RootElement.GetProperty("sender").GetProperty("email").GetString());
    }

    /// <summary>
    /// Matches Program.cs's fail-fast convention for the rest of this feature's configuration
    /// (Brevo:ApiKey, Authentication:Google:ClientId/ClientSecret) -- a missing sender address should
    /// throw at startup/construction, not silently send mail with a blank "from" address.
    /// </summary>
    [Fact]
    public void Constructor_WithoutSenderEmailConfigured_Throws()
    {
        var httpClient = new HttpClient(DelegateHttpMessageHandler.ReturningStatus(HttpStatusCode.OK)) { BaseAddress = new Uri("https://api.brevo.com/") };
        var logger = Substitute.For<ILogger<BrevoEmailSender>>();
        var configuration = BuildConfiguration(senderEmail: null);

        Assert.Throws<InvalidOperationException>(() => new BrevoEmailSender(httpClient, logger, configuration));
    }

    /// <summary>
    /// Matches the fail-fast fix for the blank-string gap: an empty/whitespace-only
    /// <c>Brevo:SenderEmail</c> must throw the same as a missing (<c>null</c>) one, not silently
    /// construct a sender that sends mail with a blank "from" address.
    /// </summary>
    [Fact]
    public void Constructor_WithBlankSenderEmailConfigured_Throws()
    {
        var httpClient = new HttpClient(DelegateHttpMessageHandler.ReturningStatus(HttpStatusCode.OK)) { BaseAddress = new Uri("https://api.brevo.com/") };
        var logger = Substitute.For<ILogger<BrevoEmailSender>>();
        var configuration = BuildConfiguration(senderEmail: "");

        Assert.Throws<InvalidOperationException>(() => new BrevoEmailSender(httpClient, logger, configuration));
    }

    [Fact]
    public async Task SendEmailAsync_WithFailureStatusCode_DoesNotThrow()
    {
        var (sender, _) = CreateSender(HttpStatusCode.Unauthorized);

        // The whole point of catching here: a bad/rejected Brevo call must not crash the Identity
        // page (Register, ResendEmailConfirmation, Manage/Email) that awaited this call.
        await sender.SendEmailAsync("user@example.com", "Confirm your email", "<p>body</p>");
    }

    [Fact]
    public async Task SendEmailAsync_WhenHttpClientThrows_DoesNotThrow()
    {
        var handler = DelegateHttpMessageHandler.ThatThrows(new HttpRequestException("Simulated network failure."));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.brevo.com/") };
        var sender = new BrevoEmailSender(httpClient, Substitute.For<ILogger<BrevoEmailSender>>(), BuildConfiguration(ConfiguredSenderEmail));

        await sender.SendEmailAsync("user@example.com", "Confirm your email", "<p>body</p>");
    }

    [Fact]
    public async Task SendEmailAsync_WhenRequestTimesOut_DoesNotThrow()
    {
        // PostAsJsonAsync surfaces the HttpClient's configured request timeout as a
        // TaskCanceledException rather than an HttpRequestException.
        var handler = DelegateHttpMessageHandler.ThatThrows(new TaskCanceledException("Simulated request timeout."));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.brevo.com/") };
        var sender = new BrevoEmailSender(httpClient, Substitute.For<ILogger<BrevoEmailSender>>(), BuildConfiguration(ConfiguredSenderEmail));

        await sender.SendEmailAsync("user@example.com", "Confirm your email", "<p>body</p>");
    }

    /// <summary>
    /// A single reusable <see cref="HttpMessageHandler"/> whose <see cref="SendAsync"/> behavior is
    /// supplied per-test as a delegate, replacing what would otherwise be a dedicated subclass per
    /// failure mode (canned response, thrown network exception, thrown timeout exception, ...) --
    /// each new scenario is one more factory method/lambda rather than a whole new class.
    /// </summary>
    private sealed class DelegateHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handle) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        /// <summary>Captures the last request sent through it and returns a canned response.</summary>
        public static DelegateHttpMessageHandler ReturningStatus(HttpStatusCode responseStatus) =>
            new((_, _) => Task.FromResult(new HttpResponseMessage(responseStatus)));

        /// <summary>Simulates a network-level failure or timeout by throwing <paramref name="exception"/>.</summary>
        public static DelegateHttpMessageHandler ThatThrows(Exception exception) =>
            new((_, _) => throw exception);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

            return await handle(request, cancellationToken);
        }
    }
}
