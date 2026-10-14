using Microsoft.EntityFrameworkCore;
using RaceDay.Models;

namespace RaceDay.Data;

public class RaceDayDbContext : DbContext
{
    public RaceDayDbContext(
        DbContextOptions<RaceDayDbContext> options)
        : base(options)
    {
    }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<Result> Results => Set<Result>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. ROLE
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Role");

            entity.HasKey(r => r.RoleId);

            entity.Property(r => r.RoleName)
                .HasMaxLength(50)
                .IsRequired();

            entity.HasIndex(r => r.RoleName)
                .IsUnique();

            // Fixed IDs match the registration DTO.
            entity.HasData(
                new Role
                {
                    RoleId = 1,
                    RoleName = "Organiser"
                },
                new Role
                {
                    RoleId = 2,
                    RoleName = "Participant"
                });
        });

        // 2. USER
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.ToTable("User");

            // AppUser's key must be explicitly configured.
            entity.HasKey(u => u.UserId);

            entity.Property(u => u.FirstName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(u => u.LastName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(u => u.Email)
                .HasMaxLength(255)
                .IsRequired();

            entity.HasIndex(u => u.Email)
                .IsUnique();

            entity.Property(u => u.PasswordHash)
                .HasMaxLength(500)
                .IsRequired();

            entity.HasOne(u => u.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // 3. EVENT
        modelBuilder.Entity<Event>(entity =>
        {
            entity.ToTable("Event", table =>
            {
                table.HasCheckConstraint(
                    "CK_Event_Distance",
                    "[Distance] > 0");

                table.HasCheckConstraint(
                    "CK_Event_EventType",
                    "[EventType] IN ('Running', 'Walking', 'Cycling')");
            });

            entity.HasKey(e => e.EventId);

            entity.Property(e => e.EventName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasMaxLength(1000);

            entity.Property(e => e.EventDate)
                .HasColumnType("date");

            entity.Property(e => e.Location)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.Distance)
                .HasPrecision(6, 2);

            entity.Property(e => e.EventType)
                .HasMaxLength(50)
                .IsRequired();

            entity.HasOne(e => e.Organiser)
                .WithMany(u => u.OrganisedEvents)
                .HasForeignKey(e => e.OrganiserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // 4. CATEGORY
        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("Category", table =>
            {
                table.HasCheckConstraint(
                    "CK_Category_MinimumAge",
                    "[MinimumAge] IS NULL OR [MinimumAge] >= 0");

                table.HasCheckConstraint(
                    "CK_Category_MaximumAge",
                    "[MaximumAge] IS NULL OR [MaximumAge] >= 0");

                table.HasCheckConstraint(
                    "CK_Category_AgeRange",
                    "[MaximumAge] IS NULL OR [MinimumAge] IS NULL " +
                    "OR [MaximumAge] >= [MinimumAge]");
            });

            entity.HasKey(c => c.CategoryId);

            entity.Property(c => c.CategoryName)
                .HasMaxLength(100)
                .IsRequired();

            entity.HasIndex(c => new
            {
                c.EventId,
                c.CategoryName
            }).IsUnique();

            entity.HasOne(c => c.Event)
                .WithMany(e => e.Categories)
                .HasForeignKey(c => c.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 5. ENROLMENT
        modelBuilder.Entity<Enrolment>(entity =>
        {
            entity.ToTable("Enrolment");

            entity.HasKey(e => e.EnrolmentId);

            entity.Property(e => e.EnrolmentDate)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSDATETIME()");

            // A participant may enter each event only once.
            entity.HasIndex(e => new
            {
                e.ParticipantId,
                e.EventId
            }).IsUnique();

            entity.HasOne(e => e.Participant)
                .WithMany(u => u.Enrolments)
                .HasForeignKey(e => e.ParticipantId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Event)
                .WithMany(ev => ev.Enrolments)
                .HasForeignKey(e => e.EventId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Category)
                .WithMany(c => c.Enrolments)
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // 6. RESULT
        modelBuilder.Entity<Result>(entity =>
        {
            entity.ToTable("Result", table =>
            {
                table.HasCheckConstraint(
                    "CK_Result_FinishingPosition",
                    "[FinishingPosition] IS NULL " +
                    "OR [FinishingPosition] > 0");
            });

            entity.HasKey(r => r.ResultId);

            entity.Property(r => r.FinishTime)
                .HasColumnType("time(0)");

            // Each enrolment has at most one result.
            entity.HasIndex(r => r.EnrolmentId)
                .IsUnique();

            entity.HasOne(r => r.Enrolment)
                .WithOne(e => e.Result)
                .HasForeignKey<Result>(r => r.EnrolmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}