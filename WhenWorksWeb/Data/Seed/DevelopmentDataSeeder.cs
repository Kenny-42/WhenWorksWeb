using System.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Data.Seed;

/// <summary>
/// Provides methods to seed the database with initial development data for testing and local development environments.
/// </summary>
/// <remarks>This class is intended to be used only in development or test scenarios. It ensures that sample
/// users, events, participants, roles, dates, settings, messages, and bookmarks are created only if the database does
/// not already contain event data. Using this seeder in production environments is not recommended, as it may introduce
/// test data into live systems.</remarks>
public sealed class DevelopmentDataSeeder
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<DevelopmentDataSeeder> _logger;

    /// <summary>
    /// Initializes a new instance of the DevelopmentDataSeeder class with the specified database context, user manager,
    /// and logger.
    /// </summary>
    public DevelopmentDataSeeder(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        ILogger<DevelopmentDataSeeder> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Asynchronously seeds the database with initial development data if no events exist.
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        // Check if any events exist to avoid seeding duplicate data
        if (await _dbContext.Events.AnyAsync(cancellationToken))
        {
            return;
        }

        // Use a transaction to ensure all seed data is inserted atomically
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Create sample users
        var alice = await EnsureUserAsync(
            userName: "dev_alice",
            email: "alice.dev@local.test",
            displayName: "Alice Dev",
            color: "ff66c4",
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
            Code = "LUNCH1",
            Title = "Team Lunch Poll",
            CreatedByUserId = alice.Id,
            CreatedAt = eventCreatedAt,
            LastActiveAt = eventLastActiveAt
        };

        var tripEvent = new Event
        {
            Code = "TRIP22",
            Title = "Weekend Trip",
            CreatedByUserId = ben.Id,
            CreatedAt = eventCreatedAt,
            LastActiveAt = eventLastActiveAt
        };

        var planningEvent = new Event
        {
            Code = "PLAN33",
            Title = "Sprint Planning",
            CreatedByUserId = null,
            CreatedAt = eventCreatedAt,
            LastActiveAt = eventLastActiveAt
        };

        // Set CreatedAt and LastActiveAt to now for all events and save to generate IDs for participants
        _dbContext.Events.AddRange(lunchEvent, tripEvent, planningEvent);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Create participants for the events
        var lunchAlice = new Participant
        {
            EventId = lunchEvent.Id,
            UserId = alice.Id,
            DisplayName = "Alice Dev",
            Color = "ff66c4"
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
            Color = "4d96ff"
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
            Color = "ff66c4"
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
        _dbContext.Participants.AddRange(
            lunchAlice, lunchBen, lunchGuest,
            tripBen, tripChloe, tripGuest,
            planningAlice, planningChloe, planningGuest);

        // Save participants to generate IDs for the roles
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Create roles for participants
        _dbContext.EventRoles.AddRange(
            new EventRole { ParticipantId = lunchAlice.Id, Name = "Organizer" },
            new EventRole { ParticipantId = tripBen.Id, Name = "Planner" },
            new EventRole { ParticipantId = planningChloe.Id, Name = "Host" });

        // Create event dates for the events
        _dbContext.EventDates.AddRange(
            new EventDate { EventId = lunchEvent.Id, Date = DateTimeOffset.UtcNow.AddDays(2).AddHours(12).AddMinutes(30) },
            new EventDate { EventId = lunchEvent.Id, Date = DateTimeOffset.UtcNow.AddDays(2).AddHours(13).AddMinutes(0) },
            new EventDate { EventId = tripEvent.Id, Date = DateTimeOffset.UtcNow.AddDays(7).AddHours(9) },
            new EventDate { EventId = tripEvent.Id, Date = DateTimeOffset.UtcNow.AddDays(7).AddHours(17) },
            new EventDate { EventId = planningEvent.Id, Date = DateTimeOffset.UtcNow.AddDays(1).AddHours(14) });

        // Create event settings for the events
        _dbContext.EventSettings.AddRange(
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
        _dbContext.EventMessages.AddRange(
            new EventMessage
            {
                EventId = lunchEvent.Id,
                ParticipantId = lunchAlice.Id,
                Body = "I can do 12:30.",
                SentAt = DateTime.UtcNow.AddDays(-2)
            },
            new EventMessage
            {
                EventId = lunchEvent.Id,
                ParticipantId = lunchBen.Id,
                Body = "1:00 works for me.",
                SentAt = DateTime.UtcNow.AddDays(-2).AddMinutes(10)
            },
            new EventMessage
            {
                EventId = tripEvent.Id,
                ParticipantId = tripChloe.Id,
                Body = "I am free all day Saturday.",
                SentAt = DateTime.UtcNow.AddDays(-1)
            },
            new EventMessage
            {
                EventId = planningEvent.Id,
                ParticipantId = planningAlice.Id,
                Body = "Agenda posted in the notes.",
                SentAt = DateTime.UtcNow.AddHours(-6)
            });

        // Create user event bookmarks
        _dbContext.UserEventBookmarks.AddRange(
            new UserEventBookmark { UserId = alice.Id, EventId = tripEvent.Id },
            new UserEventBookmark { UserId = alice.Id, EventId = planningEvent.Id },
            new UserEventBookmark { UserId = ben.Id, EventId = lunchEvent.Id },
            new UserEventBookmark { UserId = chloe.Id, EventId = tripEvent.Id });

        // Save all changes to the database
        await _dbContext.SaveChangesAsync(cancellationToken);
        // Commit the transaction
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation("Development seed data inserted.");
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
        var existingUser = await _userManager.FindByNameAsync(userName);
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

        // Attempt to create the user and handle any errors that may occur during creation. The default password "Dev123" is used
        var result = await _userManager.CreateAsync(user, "Dev123");
        
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"Failed to seed user '{userName}': {errors}");
        }

        return user;
    }
}