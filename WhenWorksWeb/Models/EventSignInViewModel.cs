namespace WhenWorksWeb.Models;

/// <summary>
/// View model for the event sign-in page.
/// </summary>
public sealed class EventSignInViewModel
{
    /// <summary>
    /// Gets the code used to identify the event.
    /// </summary>
    /// <remarks>The code consists of exactly six alphanumeric characters, excluding the letters A, E, I,
    /// L, O, U and the digits 0 and 1.</remarks>
    public required string Code { get; init; }

    /// <summary>
    /// Gets the name of the event associated with this instance.
    /// </summary>
    /// <remarks>The name has a maximum length of 30 characters and minimum of 1 character</remarks>
    public required string EventName { get; init; }
}
