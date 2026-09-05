using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WhenWorksWeb.Common;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Data;

/// <summary>
/// The Entity Framework Core database context for the application, combining ASP.NET Core Identity's
/// user/role tables with the application's own event, participant, and messaging tables.
/// </summary>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    /// <summary>
    /// Events created or joined by users.
    /// </summary>
    public DbSet<Event> Events => Set<Event>();

    /// <summary>
    /// Participant records linking users (or guests) to events.
    /// </summary>
    public DbSet<Participant> Participants => Set<Participant>();

    /// <summary>
    /// Chat messages posted within an event.
    /// </summary>
    public DbSet<EventMessage> EventMessages => Set<EventMessage>();

    /// <summary>
    /// Candidate dates proposed for an event.
    /// </summary>
    public DbSet<EventDate> EventDates => Set<EventDate>();

    /// <summary>
    /// Optional display settings (emoji, description) for an event.
    /// </summary>
    public DbSet<EventSettings> EventSettings => Set<EventSettings>();

    /// <summary>
    /// Participant marks of availability on candidate event dates.
    /// </summary>
    public DbSet<ParticipantAvailability> ParticipantAvailabilities => Set<ParticipantAvailability>();

    /// <summary>
    /// Organizer-chosen final date(s) for events, set independently of participant availability.
    /// </summary>
    public DbSet<EventFinalDate> EventFinalDates => Set<EventFinalDate>();

    /// <summary>
    /// Bookmarks users have saved on events.
    /// </summary>
    public DbSet<UserEventBookmark> UserEventBookmarks => Set<UserEventBookmark>();

    /// <summary>
    /// This method is called by the Entity Framework when the model is being created.
    /// It is used to configure the database schema and relationships between entities.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure custom Identity user properties.
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(e => e.DisplayName).HasMaxLength(ModelConstants.ApplicationUserDisplayNameMaxLength).IsRequired();
            entity.Property(e => e.Color).HasMaxLength(ModelConstants.HexColorLength).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.LastActiveAt).IsRequired();
        });

        // Configure the main Event table and its link to the user who created it.
        modelBuilder.Entity<Event>(entity =>
        {
            entity.Property(e => e.Code)
                .HasMaxLength(ModelConstants.UniqueCodeLength)
                .UseCollation("SQL_Latin1_General_CP1_CI_AS");

            // A SQL-level default (rather than only the C#-side default in Event.Create) so the
            // migration that adds this column backfills every pre-existing row to "UTC" -- matching
            // the app's previous implicit UTC-only behavior -- instead of leaving it null.
            entity.Property(e => e.TimeZoneId)
                .HasMaxLength(ModelConstants.EventTimeZoneIdMaxLength)
                .HasDefaultValue(ModelConstants.DefaultEventTimeZoneId);

            entity.HasOne(e => e.CreatedByUser)
                .WithMany(u => u.CreatedEvents)
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => e.CreatedByUserId);
            entity.HasIndex(e => e.LastActiveAt);
        });

        // Configure participants and their relationships to events and users.
        modelBuilder.Entity<Participant>(entity =>
        {
            entity.Property(p => p.DisplayName)
                .HasMaxLength(ModelConstants.ParticipantDisplayNameMaxLength)
                .UseCollation("SQL_Latin1_General_CP1_CS_AS");

            entity.Property(p => p.Color)
                .HasMaxLength(ModelConstants.HexColorLength);

            entity.HasOne(p => p.Event)
                .WithMany(e => e.Participants)
                .HasForeignKey(p => p.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(p => p.User)
                .WithMany(u => u.Participations)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(p => new { p.EventId, p.UserId });

            entity.HasIndex(p => new { p.EventId, p.CanManageOrganizers });

            entity.HasIndex(p => new { p.EventId, p.DisplayName })
                .IsUnique();

            entity.HasIndex(p => new { p.EventId, p.Color })
                .IsUnique();

            entity.HasCheckConstraint(
                "CK_Participants_DisplayName_Trimmed",
                "[DisplayName] = LTRIM(RTRIM([DisplayName]))");
        });

        // Configure the available dates for each event.
        modelBuilder.Entity<EventDate>(entity =>
        {
            entity.HasOne(d => d.Event)
                .WithMany(e => e.Dates)
                .HasForeignKey(d => d.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(d => new { d.EventId, d.Date }).IsUnique();
        });

        // Configure the participant-to-date availability join table. Composite key means a
        // participant can only mark a given date available once. Deleting a candidate date
        // cascades and cleans up its availability marks; deleting a participant does NOT
        // cascade here (DeleteBehavior.NoAction) — Event -> EventDate -> ParticipantAvailability
        // and Event -> Participant -> ParticipantAvailability would otherwise be two cascade
        // paths to the same table, which SQL Server rejects (the same issue documented for
        // EventMessage.ParticipantId). Deleting a Participant must first remove its
        // ParticipantAvailability rows via ExecuteDeleteAsync (see MyEventsController.Delete).
        modelBuilder.Entity<ParticipantAvailability>(entity =>
        {
            entity.HasKey(a => new { a.ParticipantId, a.EventDateId });

            entity.HasOne(a => a.Participant)
                .WithMany(p => p.Availabilities)
                .HasForeignKey(a => a.ParticipantId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(a => a.EventDate)
                .WithMany(d => d.Availabilities)
                .HasForeignKey(a => a.EventDateId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure the organizer's final date entries, kept independent of EventDate/votes.
        modelBuilder.Entity<EventFinalDate>(entity =>
        {
            entity.HasOne(f => f.Event)
                .WithMany(e => e.FinalDates)
                .HasForeignKey(f => f.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(f => f.EventId);
        });

        // Configure optional event settings stored separately from the main event row.
        modelBuilder.Entity<EventSettings>(entity =>
        {
            entity.HasOne(s => s.Event)
                .WithOne(e => e.Settings)
                .HasForeignKey<EventSettings>(s => s.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure messages posted in an event and the participant who sent them.
        modelBuilder.Entity<EventMessage>(entity =>
        {
            entity.HasOne(m => m.Event)
                .WithMany(e => e.Messages)
                .HasForeignKey(m => m.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(m => m.Participant)
                .WithMany(p => p.Messages)
                .HasForeignKey(m => m.ParticipantId)
                .OnDelete(DeleteBehavior.NoAction); // avoids SQL Server multiple cascade path errors; participant deletion is handled manually

            entity.HasIndex(m => m.EventId);
            entity.HasIndex(m => m.ParticipantId);
            entity.HasIndex(m => m.SentAt);
            entity.HasIndex(m => new { m.EventId, m.SentAt });
        });

        // Configure saved bookmarks between users and events.
        modelBuilder.Entity<UserEventBookmark>(entity =>
        {
            entity.HasKey(b => new { b.UserId, b.EventId });

            entity.HasOne(b => b.User)
                .WithMany(u => u.EventBookmarks)
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(b => b.Event)
                .WithMany(e => e.UserBookmarks)
                .HasForeignKey(b => b.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(b => b.EventId);
        });
    }

    /// <summary>
    /// This method is called when changes to the database are being saved.
    /// It is overridden here to automatically update the CreatedAt and LastActiveAt timestamps for events whenever they are added or modified.
    /// This ensures that the event activity tracking is always up to date without requiring manual updates in the application code.
    /// </summary>
    public override int SaveChanges()
    {
        ApplyEventTimestamps();
        return base.SaveChanges();
    }

    /// <summary>
    /// This asynchronous method is called when changes to the database are being saved.
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyEventTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// This method iterates through all the tracked Event entities and updates their CreatedAt and LastActiveAt timestamps
    /// based on their state (added or modified).
    /// </summary>
    private void ApplyEventTimestamps()
    {
        var utcNow = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<Event>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = utcNow;
                entry.Entity.LastActiveAt = utcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.LastActiveAt = utcNow;
            }
        }
    }
}
