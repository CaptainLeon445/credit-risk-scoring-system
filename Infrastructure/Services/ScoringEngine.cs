using CreditRiskScoring.Api.Domain.Entities;
using CreditRiskScoring.Api.Domain.Enums;

namespace CreditRiskScoring.Api.Infrastructure.Services;

/// <summary>
/// Deterministic weighted-formula scoring model.
///
/// The score (0–100) is the sum of points across five weighted pillars:
///   Income          30 pts
///   Debt-to-Income  25 pts
///   Credit History  20 pts
///   Payment History 15 pts
///   Assets          10 pts
///
/// In production this model can be replaced with an ML.NET trained model:
///   - Train a RegressionTrainer (e.g. FastTree) on historical loan data
///   - Export the model as an .mlnet file
///   - Inject MLContext and load the model here
/// </summary>
public class ScoringEngine
{
    public (int Score, RiskTier Tier, bool Eligible, List<RiskFactor> Factors)
        Calculate(RiskProfile profile)
    {
        var factors = new List<RiskFactor>();
        int total = 0;

        // ── Pillar 1: Annual Income (max 30 pts) ─────────────────────────────
        var (incomePoints, incomeDesc) = profile.AnnualIncome switch
        {
            >= 100_000 => (30, "Annual income ≥ $100k — excellent earning capacity"),
            >= 70_000  => (25, "Annual income $70k–$100k — strong earning capacity"),
            >= 40_000  => (18, "Annual income $40k–$70k — adequate earning capacity"),
            >= 20_000  => (10, "Annual income $20k–$40k — moderate earning capacity"),
            _          => (2,  "Annual income < $20k — limited earning capacity")
        };
        factors.Add(Factor("Annual Income", incomeDesc, incomePoints, 30, incomePoints >= 15, profile.Id));
        total += incomePoints;

        // ── Pillar 2: Debt-to-Income Ratio (max 25 pts) ──────────────────────
        decimal dti = profile.DebtToIncomeRatio;
        var (dtiPoints, dtiDesc) = dti switch
        {
            <= 0.10m => (25, $"DTI {dti:P0} — very low debt relative to income"),
            <= 0.20m => (20, $"DTI {dti:P0} — manageable debt load"),
            <= 0.30m => (15, $"DTI {dti:P0} — moderate debt load"),
            <= 0.40m => (8,  $"DTI {dti:P0} — elevated debt load"),
            <= 0.50m => (3,  $"DTI {dti:P0} — high debt load"),
            _        => (0,  $"DTI {dti:P0} — unsustainable debt load")
        };
        factors.Add(Factor("Debt-to-Income Ratio", dtiDesc, dtiPoints, 25, dtiPoints >= 12, profile.Id));
        total += dtiPoints;

        // ── Pillar 3: Credit History Length (max 20 pts) ─────────────────────
        var (histPoints, histDesc) = profile.CreditHistoryMonths switch
        {
            >= 84  => (20, $"{profile.CreditHistoryMonths / 12}+ years of credit history — excellent"),
            >= 60  => (16, $"{profile.CreditHistoryMonths / 12} years of credit history — good"),
            >= 36  => (11, $"{profile.CreditHistoryMonths / 12} years of credit history — fair"),
            >= 12  => (6,  "1–3 years of credit history — limited"),
            _      => (1,  "Less than 1 year of credit history — very limited")
        };
        factors.Add(Factor("Credit History", histDesc, histPoints, 20, histPoints >= 10, profile.Id));
        total += histPoints;

        // ── Pillar 4: Payment History (max 15 pts) ───────────────────────────
        var (payPoints, payDesc) = profile.MissedPayments switch
        {
            0      => (15, "No missed payments — perfect payment history"),
            1      => (10, "1 missed payment — minor negative impact"),
            2 or 3 => (5,  $"{profile.MissedPayments} missed payments — moderate risk signal"),
            _      => (0,  $"{profile.MissedPayments} missed payments — significant risk signal")
        };
        factors.Add(Factor("Payment History", payDesc, payPoints, 15, payPoints >= 10, profile.Id));
        total += payPoints;

        // ── Pillar 5: Assets (max 10 pts) ────────────────────────────────────
        var (assetPoints, assetDesc) = profile.Assets switch
        {
            >= 500_000 => (10, "Substantial assets — strong collateral"),
            >= 50_000  => (7,  "Moderate assets — reasonable collateral"),
            >= 10_000  => (4,  "Limited assets — minimal collateral"),
            _          => (1,  "Negligible assets — no collateral")
        };
        factors.Add(Factor("Assets & Collateral", assetDesc, assetPoints, 10, assetPoints >= 5, profile.Id));
        total += assetPoints;

        // ── Employment bonus / penalty ─────────────────────────────────────
        if (profile.EmploymentStatus == "Unemployed")
        {
            int penalty = Math.Min(5, total);
            total -= penalty;
            factors.Add(Factor("Employment Status", "Currently unemployed — income stability concern",
                               -penalty, 0, false, profile.Id));
        }
        else if (profile.EmploymentStatus == "Employed")
        {
            factors.Add(Factor("Employment Status", "Employed — stable income source", 0, 0, true, profile.Id));
        }

        total = Math.Clamp(total, 0, 100);

        var tier = total switch
        {
            >= 81 => RiskTier.Excellent,
            >= 61 => RiskTier.LowRisk,
            >= 41 => RiskTier.MediumRisk,
            _     => RiskTier.HighRisk
        };

        bool eligible = tier is RiskTier.Excellent or RiskTier.LowRisk;

        return (total, tier, eligible, factors);
    }

    private static RiskFactor Factor(string name, string description, int points, int max,
                                     bool positive, Guid profileId) =>
        new()
        {
            ProfileId    = profileId,
            Name         = name,
            Description  = description,
            PointsAwarded = points,
            MaxPoints    = max,
            IsPositive   = positive
        };
}
