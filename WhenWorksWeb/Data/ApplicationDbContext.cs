using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            /// <summary>
            /// Configures the model for the ApplicationUser entity to ensure that the DisplayName and Color properties have appropriate maximum lengths, 
            /// and that the CreatedAt and LastActiveAt properties are required.
            /// </summary>
            modelBuilder.Entity<ApplicationUser>(entity =>
            {
                entity.Property(e => e.DisplayName)
                      .HasMaxLength(16);

                entity.Property(e => e.Color)
                      .HasMaxLength(6);

                entity.Property(e => e.CreatedAt)
                      .IsRequired();

                entity.Property(e => e.LastActiveAt)
                      .IsRequired();
            });
        }
    }
}
