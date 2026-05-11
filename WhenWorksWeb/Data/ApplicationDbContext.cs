using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WhenWorksWeb.Common;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    // Database tables used by the application.
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<EventRole> EventRoles => Set<EventRole>();
    public DbSet<EventMessage> EventMessages => Set<EventMessage>();
    public DbSet<EventDate> EventDates => Set<EventDate>();
    public DbSet<EventSettings> EventSettings => Set<EventSettings>();
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
                .HasMaxLength(ModelConstants.EventCodeLength)
                .UseCollation("SQL_Latin1_General_CP1_CI_AS");

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

            entity.HasIndex(p => new { p.EventId, p.UserId })
                .IsUnique()
                .HasFilter("[UserId] IS NOT NULL");

            entity.HasIndex(p => new { p.EventId, p.DisplayName })
                .IsUnique();

            entity.HasCheckConstraint(
                "CK_Participants_DisplayName_Trimmed",
                "[DisplayName] = LTRIM(RTRIM([DisplayName]))");
        });

        // Configure the one-to-one role record attached to a participant.
        modelBuilder.Entity<EventRole>(entity =>
        {
            entity.HasOne(r => r.Participant)
                .WithOne(p => p.Role)
                .HasForeignKey<EventRole>(r => r.ParticipantId)
                .OnDelete(DeleteBehavior.Cascade);
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
                .OnDelete(DeleteBehavior.NoAction); // avoids multiple cascade paths

            entity.HasIndex(m => m.EventId);
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
