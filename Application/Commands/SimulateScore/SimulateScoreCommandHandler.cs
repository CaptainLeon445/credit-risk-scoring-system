using CreditRiskScoring.Api.Domain.Entities;
using CreditRiskScoring.Api.DTOs;
using CreditRiskScoring.Api.Infrastructure.Data;
using CreditRiskScoring.Api.Infrastructure.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CreditRiskScoring.Api.Application.Commands.SimulateScore;

public class SimulateScoreCommandHandler(AppDbContext db, ScoringEngine engine)
    : IRequestHandler<SimulateScoreCommand, SimulateResponse>
{
    public async Task<SimulateResponse> Handle(SimulateScoreCommand command, CancellationToken ct)
    {
        var req = command.Request;

        var original = await db.RiskProfiles
                               .AsNoTracking()
                               .FirstOrDefaultAsync(p => p.Id == req.ProfileId, ct)
                       ?? throw new KeyNotFoundException($"Profile {req.ProfileId} not found.");

        // Build a hypothetical profile with the requested changes applied
        var hypothetical = new RiskProfile
        {
            Id                  = original.Id,
            AnnualIncome        = req.NewAnnualIncome        ?? original.AnnualIncome,
            TotalDebt           = req.NewTotalDebt           ?? original.TotalDebt,
            Assets              = req.NewAssets              ?? original.Assets,
            MissedPayments      = req.NewMissedPayments      ?? original.MissedPayments,
            EmploymentStatus    = req.NewEmploymentStatus    ?? original.EmploymentStatus,
            MonthlyExpenses     = original.MonthlyExpenses,
            CreditHistoryMonths = original.CreditHistoryMonths,
            LoanPurpose         = original.LoanPurpose,
            UserId              = original.UserId
        };

        var (simScore, simTier, simEligible, _) = engine.Calculate(hypothetical);

        string summary = simScore > original.Score
            ? $"Your score improves by {simScore - original.Score} points ({original.RiskTier} → {simTier})."
            : simScore < original.Score
                ? $"Your score decreases by {original.Score - simScore} points ({original.RiskTier} → {simTier})."
                : $"No change in score. Risk tier remains {original.RiskTier}.";

        return new SimulateResponse(
            original.Id,
            original.Score,
            simScore,
            simScore - original.Score,
            original.RiskTier,
            simTier,
            simEligible != original.LoanEligible,
            summary
        );
    }
}
