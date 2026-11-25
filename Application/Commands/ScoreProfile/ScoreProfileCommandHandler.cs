using CreditRiskScoring.Api.Domain.Entities;
using CreditRiskScoring.Api.DTOs;
using CreditRiskScoring.Api.Infrastructure.Data;
using CreditRiskScoring.Api.Infrastructure.Services;
using MediatR;

namespace CreditRiskScoring.Api.Application.Commands.ScoreProfile;

public class ScoreProfileCommandHandler(AppDbContext db, ScoringEngine engine)
    : IRequestHandler<ScoreProfileCommand, ScoreResponse>
{
    public async Task<ScoreResponse> Handle(ScoreProfileCommand command, CancellationToken ct)
    {
        var req     = command.Request;
        var profile = new RiskProfile
        {
            AnnualIncome        = req.AnnualIncome,
            TotalDebt           = req.TotalDebt,
            MonthlyExpenses     = req.MonthlyExpenses,
            Assets              = req.Assets,
            CreditHistoryMonths = req.CreditHistoryMonths,
            MissedPayments      = req.MissedPayments,
            EmploymentStatus    = req.EmploymentStatus,
            LoanPurpose         = req.LoanPurpose ?? string.Empty,
            UserId              = req.UserId ?? string.Empty
        };

        var (score, tier, eligible, factors) = engine.Calculate(profile);
        profile.Score        = score;
        profile.RiskTier     = tier;
        profile.LoanEligible = eligible;

        db.RiskProfiles.Add(profile);
        factors.ForEach(f => db.RiskFactors.Add(f));
        await db.SaveChangesAsync(ct);

        return new ScoreResponse(
            profile.Id,
            score,
            tier,
            tier.ToString(),
            eligible,
            Math.Round(profile.DebtToIncomeRatio, 2),
            factors.Select(f => new RiskFactorResponse(
                f.Name, f.Description, f.PointsAwarded, f.MaxPoints, f.IsPositive)).ToList()
        );
    }
}
