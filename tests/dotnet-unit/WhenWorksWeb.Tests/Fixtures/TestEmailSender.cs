using System.Collections.Concurrent;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace WhenWorksWeb.Tests.Fixtures;

/// <summary>
/// Test double for <see cref="IEmailSender"/>, registered in place of <c>BrevoEmailSender</c> by
/// <see cref="CustomWebApplicationFactory"/> so Tier 3 tests never make a real network call to
/// Brevo's API and never need a real <c>Brevo:ApiKey</c> configured. Records every call so a test can
/// assert a confirmation/reset/change-email link was actually sent -- e.g. for Register.cshtml.cs or
/// ExternalLogin.cshtml.cs's confirmation flow.
/// </summary>
/// <remarks>
/// <c>ConcurrentBag</c> rather than <c>List</c> since <see cref="CustomWebApplicationFactory"/>
/// registers this as a singleton shared across a whole test class's requests, which can run
/// concurrently under xUnit.
/// </remarks>
public class TestEmailSender : IEmailSender
{
    private readonly ConcurrentBag<SentEmail> _sentEmails = [];

    /// <summary>Every email sent through this instance so far, in no particular order.</summary>
    public IReadOnlyCollection<SentEmail> SentEmails => _sentEmails;

    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        _sentEmails.Add(new SentEmail(email, subject, htmlMessage));
        return Task.CompletedTask;
    }

    /// <summary>One recorded call to <see cref="SendEmailAsync"/>.</summary>
    public record SentEmail(string Email, string Subject, string HtmlMessage);
}
