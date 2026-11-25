using CreditRiskScoring.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CreditRiskScoring.Api.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<RiskProfile> RiskProfiles => Set<RiskProfile>();
    public DbSet<RiskFactor> RiskFactors => Set<RiskFactor>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RiskProfile>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.AnnualIncome).HasPrecision(18, 2);
            e.Property(p => p.TotalDebt).HasPrecision(18, 2);
            e.Property(p => p.Assets).HasPrecision(18, 2);
            e.HasMany(p => p.Factors)
             .WithOne(f => f.Profile)
             .HasForeignKey(f => f.ProfileId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RiskFactor>(e =>
        {
            e.HasKey(f => f.Id);
        });
    }
}
