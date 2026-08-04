using WhenWorksWeb.Common;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Tests.TestData;

/// <summary>
/// Hand-rolled test data builder for <see cref="ApplicationUser"/>.
/// </summary>
public sealed class ApplicationUserBuilder
{
    private string _userName = "testuser";
    private string _email = "testuser@example.com";
    private string _displayName = "Test User";
    private string _color = ModelConstants.DefaultParticipantColor;

    public ApplicationUserBuilder WithUserName(string userName)
    {
        _userName = userName;
        return this;
    }

    public ApplicationUserBuilder WithEmail(string email)
    {
        _email = email;
        return this;
    }

    public ApplicationUserBuilder WithDisplayName(string displayName)
    {
        _displayName = displayName;
        return this;
    }

    public ApplicationUserBuilder WithColor(string color)
    {
        _color = color;
        return this;
    }

    public ApplicationUser Build()
    {
        var now = DateTime.UtcNow;

        return new ApplicationUser
        {
            UserName = _userName,
            Email = _email,
            DisplayName = _displayName,
            Color = _color,
            CreatedAt = now,
            LastActiveAt = now
        };
    }
}
