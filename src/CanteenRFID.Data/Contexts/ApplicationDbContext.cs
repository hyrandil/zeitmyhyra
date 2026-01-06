using CanteenRFID.Core.Enums;
using CanteenRFID.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace CanteenRFID.Data.Contexts;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Reader> Readers => Set<Reader>();
    public DbSet<Stamp> Stamps => Set<Stamp>();
    public DbSet<MealRule> MealRules => Set<MealRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasIndex(u => u.PersonnelNo)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Uid)
            .IsUnique();

        modelBuilder.Entity<Reader>()
            .HasIndex(r => r.ReaderId)
            .IsUnique();

        modelBuilder.Entity<Stamp>()
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId);

        modelBuilder.Entity<MealRule>()
            .Property(r => r.MealType)
            .HasConversion<string>();

        modelBuilder.Entity<Stamp>()
            .Property(s => s.MealType)
            .HasConversion<string>();
    }
}
