using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhenWorksWeb.Common;
using WhenWorksWeb.Data;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Data.Seed;

/// <summary>
/// Provides methods to seed the database with initial development data for testing and local development environments.
/// </summary>
/// <remarks>This class is intended to be used only in development or test scenarios. It ensures that sample
/// users, events, participants, roles, dates, settings, messages, and bookmarks are created only if the database does
/// not already contain event data. Using this seeder in production environments is not recommended, as it may introduce
/// test data into live systems.</remarks>
public sealed class DevelopmentDataSeeder(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    ILogger<DevelopmentDataSeeder> logger)
{
    /// <summary>
    /// Asynchronously seeds the database with initial development data if no events exist.
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        // Check if any events exist to avoid seeding duplicate data
        if (await dbContext.Events.AnyAsync(cancellationToken))
        {
            return;
        }

        // Use a transaction to ensure all seed data is inserted atomically
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Create sample users
        var alice = await EnsureUserAsync(
            userName: "dev_alice",
            email: "alice.dev@local.test",
            displayName: "Alice Dev",
            color: ModelConstants.DefaultParticipantColor,
            createdAt: DateTime.UtcNow.AddDays(-10),
            lastActiveAt: DateTime.UtcNow.AddHours(-2));

        var ben = await EnsureUserAsync(
            userName: "dev_ben",
            email: "ben.dev@local.test",
            displayName: "Ben Carter",
            color: "4d96ff",
            createdAt: DateTime.UtcNow.AddDays(-8),
            lastActiveAt: DateTime.UtcNow.AddHours(-4));

        var chloe = await EnsureUserAsync(
            userName: "dev_chloe",
            email: "chloe.dev@local.test",
            displayName: "Chloe Reed",
            color: "7bc043",
            createdAt: DateTime.UtcNow.AddDays(-6),
            lastActiveAt: DateTime.UtcNow.AddHours(-1));

        // For sample events, set CreatedAt to 5 days ago and LastActiveAt to now for all events to simulate
        // active events with some history
        var eventCreatedAt = DateTimeOffset.UtcNow.AddDays(-5);
        var eventLastActiveAt = DateTimeOffset.UtcNow;

        // Create sample events
        var lunchEvent = new Event
        {
            Code = "BRG7K2",
            Title = "Team Lunch Poll",
            CreatedByUserId = alice.Id,
            CreatedAt = eventCreatedAt,
            LastActiveAt = eventLastActiveAt
        };

        var tripEvent = new Event
        {
            Code = "TRP922",
            Title = "Weekend Trip",
            CreatedByUserId = ben.Id,
            CreatedAt = eventCreatedAt,
            LastActiveAt = eventLastActiveAt
        };

        var planningEvent = new Event
        {
            Code = "PRJ633",
            Title = "Sprint Planning",
            CreatedByUserId = null,
            CreatedAt = eventCreatedAt,
            LastActiveAt = eventLastActiveAt
        };

        // Set CreatedAt and LastActiveAt to now for all events and save to generate IDs for participants
        dbContext.Events.AddRange(lunchEvent, tripEvent, planningEvent);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Create participants for the events
        var lunchAlice = new Participant
        {
            EventId = lunchEvent.Id,
            UserId = alice.Id,
            DisplayName = "Alice Dev",
            Color = ModelConstants.DefaultParticipantColor,
            IsCreator = true,
            IsOrganizer = true,
            CanManageOrganizers = true
        };

        var lunchBen = new Participant
        {
            EventId = lunchEvent.Id,
            UserId = ben.Id,
            DisplayName = "Ben Carter",
            Color = "4d96ff"
        };

        var lunchGuest = new Participant
        {
            EventId = lunchEvent.Id,
            UserId = null,
            DisplayName = "Guest One",
            Color = "999999"
        };

        var tripBen = new Participant
        {
            EventId = tripEvent.Id,
            UserId = ben.Id,
            DisplayName = "Ben Carter",
            Color = "4d96ff",
            IsCreator = true,
            IsOrganizer = true,
            CanManageOrganizers = true
        };

        var tripChloe = new Participant
        {
            EventId = tripEvent.Id,
            UserId = chloe.Id,
            DisplayName = "Chloe Reed",
            Color = "7bc043"
        };

        var tripGuest = new Participant
        {
            EventId = tripEvent.Id,
            UserId = null,
            DisplayName = "Guest Two",
            Color = "999999"
        };

        var planningAlice = new Participant
        {
            EventId = planningEvent.Id,
            UserId = alice.Id,
            DisplayName = "Alice Dev",
            Color = ModelConstants.DefaultParticipantColor
        };

        var planningChloe = new Participant
        {
            EventId = planningEvent.Id,
            UserId = chloe.Id,
            DisplayName = "Chloe Reed",
            Color = "7bc043"
        };

        var planningGuest = new Participant
        {
            EventId = planningEvent.Id,
            UserId = null,
            DisplayName = "Guest Three",
            Color = "999999"
        };

        // Set CreatedAt and LastActiveAt to now for all participants
        dbContext.Participants.AddRange(
            lunchAlice, lunchBen, lunchGuest,
            tripBen, tripChloe, tripGuest,
            planningAlice, planningChloe, planningGuest);

        // Save participants to generate IDs for the dependent rows below (dates, settings, messages, bookmarks).
        await dbContext.SaveChangesAsync(cancellationToken);

        // Note: the planning event (PRJ633) is guest-created (CreatedByUserId is null), so none of its
        // participants are flagged as creator/organizer — it's left in the seed data on purpose to exercise
        // the "no organizer yet" fallback state, where organizer-only actions are open to every participant.

        // Create event dates for the events
        dbContext.EventDates.AddRange(
            new EventDate { EventId = lunchEvent.Id, Date = DateTimeOffset.UtcNow.AddDays(2).AddHours(12).AddMinutes(30) },
            new EventDate { EventId = lunchEvent.Id, Date = DateTimeOffset.UtcNow.AddDays(2).AddHours(13).AddMinutes(0) },
            new EventDate { EventId = tripEvent.Id, Date = DateTimeOffset.UtcNow.AddDays(7).AddHours(9) },
            new EventDate { EventId = tripEvent.Id, Date = DateTimeOffset.UtcNow.AddDays(7).AddHours(17) },
            new EventDate { EventId = planningEvent.Id, Date = DateTimeOffset.UtcNow.AddDays(1).AddHours(14) });

        // Create event settings for the events
        dbContext.EventSettings.AddRange(
            new EventSettings
            {
                EventId = lunchEvent.Id,
                Emoji = "🍽️",
                Description = "Vote on a lunch time."
            },
            new EventSettings
            {
                EventId = tripEvent.Id,
                Emoji = "🧳",
                Description = "Share your availability."
            },
            new EventSettings
            {
                EventId = planningEvent.Id,
                Emoji = "🗓️",
                Description = "Sprint planning notes."
            });

        // Create event messages for the events
        dbContext.EventMessages.AddRange(
            new EventMessage
            {
                EventId = lunchEvent.Id,
                ParticipantId = lunchAlice.Id,
                SenderDisplayName = lunchAlice.DisplayName,
                SenderColor = lunchAlice.Color,
                Body = "I can do 12:30.",
                SentAt = DateTime.UtcNow.AddDays(-2)
            },
            new EventMessage
            {
                EventId = lunchEvent.Id,
                ParticipantId = lunchBen.Id,
                SenderDisplayName = lunchBen.DisplayName,
                SenderColor = lunchBen.Color,
                Body = "1:00 works for me.",
                SentAt = DateTime.UtcNow.AddDays(-2).AddMinutes(10)
            },
            new EventMessage
            {
                EventId = tripEvent.Id,
                ParticipantId = tripChloe.Id,
                SenderDisplayName = tripChloe.DisplayName,
                SenderColor = tripChloe.Color,
                Body = "I am free all day Saturday.",
                SentAt = DateTime.UtcNow.AddDays(-1)
            },
            new EventMessage
            {
                EventId = planningEvent.Id,
                ParticipantId = planningAlice.Id,
                SenderDisplayName = planningAlice.DisplayName,
                SenderColor = planningAlice.Color,
                Body = "Agenda posted in the notes.",
                SentAt = DateTime.UtcNow.AddHours(-6)
            });

        // Create user event bookmarks
        dbContext.UserEventBookmarks.AddRange(
            new UserEventBookmark { UserId = alice.Id, EventId = tripEvent.Id },
            new UserEventBookmark { UserId = alice.Id, EventId = planningEvent.Id },
            new UserEventBookmark { UserId = ben.Id, EventId = lunchEvent.Id },
            new UserEventBookmark { UserId = chloe.Id, EventId = tripEvent.Id });

        // Save all changes to the database
        await dbContext.SaveChangesAsync(cancellationToken);
        // Commit the transaction
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Development seed data inserted.");
    }

    /// <summary>
    /// Ensures that a user with the specified details exists, creating the user if necessary.
    /// </summary>
    /// <param name="userName">The unique user name to search for or assign to the new user. Cannot be null or empty.</param>
    /// <param name="email">The email address to assign to the user. Cannot be null or empty.</param>
    /// <param name="displayName">The display name to assign to the user. Used for presentation purposes.</param>
    /// <param name="color">The color associated with the user, typically used for UI representation.</param>
    /// <param name="createdAt">The date and time when the user account was created.</param>
    /// <param name="lastActiveAt">The date and time when the user was last active.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the existing or newly created
    /// ApplicationUser instance.</returns>
    private async Task<ApplicationUser> EnsureUserAsync(
        string userName,
        string email,
        string displayName,
        string color,
        DateTime createdAt,
        DateTime lastActiveAt)
    {
        // Check if a user with the specified user name already exists and return it if found
        var existingUser = await userManager.FindByNameAsync(userName);
        if (existingUser is not null)
        {
            return existingUser;
        }

        // Create a new user with the provided details and a default password
        var user = new ApplicationUser
        {
            UserName = userName,
            Email = email,
            EmailConfirmed = true,
            DisplayName = displayName,
            Color = color,
            CreatedAt = createdAt,
            LastActiveAt = lastActiveAt
        };

        // Attempt to create the user and handle any errors that may occur during creation. The default password
        // "Dev123!@" is used -- meets IdentityConfiguration's password policy (length 8+, upper/lower/digit/symbol)
        // per Spec/Features/FEATURES-tighten-account-validation.ospec's strengthened rules.
        var result = await userManager.CreateAsync(user, "Dev123!@");

        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"Failed to seed user '{userName}': {errors}");
        }

        return user;
    }
}
