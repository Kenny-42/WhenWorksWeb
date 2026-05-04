namespace WhenWorksWeb.Models;

/// <summary>
/// View model for the event sign-in page.
/// </summary>
public sealed class EventSignInViewModel
{
    // The code used to identify the event. Must be exactly six alphanumeric characters, excluding the letters A, E, I, L, O, U
    // and the digits 0 and 1. This property is typically used to validate and reference events within the application.
    public required string Code { get; init; }
}
