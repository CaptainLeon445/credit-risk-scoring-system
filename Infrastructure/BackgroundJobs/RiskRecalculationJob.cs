using CreditRiskScoring.Api.Infrastructure.Data;
using CreditRiskScoring.Api.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace CreditRiskScoring.Api.Infrastructure.BackgroundJobs;

/// <summary>
/// Hosted service that runs every 6 hours and recalculates scores for stale profiles.
/// This ensures scores reflect any model updates without requiring user re-submission.
/// It also logs GC and heap telemetry for observability.
/// </summary>
public class RiskRecalculationJob(
    IServiceScopeFactory scopeFactory,
    ILogger<RiskRecalculationJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("[RiskRecalculationJob] Background service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RecalculateStaleProfilesAsync(stoppingToken);
                LogGcStats();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "[RiskRecalculationJob] Error during recalculation cycle.");
            }

            // Wait 6 hours before next cycle
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }

    private async Task RecalculateStaleProfilesAsync(CancellationToken ct)
    {
        await using var scope  = scopeFactory.CreateAsyncScope();
        var db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var engine = scope.ServiceProvider.GetRequiredService<ScoringEngine>();

        // SQLite's EF Core provider can't translate DateTimeOffset comparisons server-side,
        // so filter in memory after materializing — fine at this table's demo scale.
        var cutoff   = DateTimeOffset.UtcNow.AddDays(-7);
        var stale    = (await db.RiskProfiles.ToListAsync(ct))
                               .Where(p => p.UpdatedAt < cutoff)
                               .ToList();

        foreach (var profile in stale)
        {
            var (score, tier, eligible, _) = engine.Calculate(profile);
            profile.Score        = score;
            profile.RiskTier     = tier;
            profile.LoanEligible = eligible;
            profile.UpdatedAt    = DateTimeOffset.UtcNow;
        }

        if (stale.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("[RiskRecalculationJob] Recalculated {Count} stale profiles.", stale.Count);
        }
    }

    private void LogGcStats()
    {
        var gcInfo0 = GC.GetGCMemoryInfo();
        long heapMb = GC.GetTotalMemory(false) / 1_048_576;

        logger.LogInformation(
            "[GC] Heap used: {HeapMb}MB | Gen0: {G0} | Gen1: {G1} | Gen2: {G2} | Compacted: {Compacted}",
            heapMb,
            GC.CollectionCount(0),
            GC.CollectionCount(1),
            GC.CollectionCount(2),
            gcInfo0.Compacted);
    }
}
