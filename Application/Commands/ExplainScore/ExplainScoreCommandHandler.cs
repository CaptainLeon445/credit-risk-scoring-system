using CreditRiskScoring.Api.DTOs;
using CreditRiskScoring.Api.Infrastructure.Data;
using CreditRiskScoring.Api.Infrastructure.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CreditRiskScoring.Api.Application.Commands.ExplainScore;

public class ExplainScoreCommandHandler(AppDbContext db, AiExplanationService aiService)
    : IRequestHandler<ExplainScoreCommand, ExplanationResponse>
{
    public async Task<ExplanationResponse> Handle(ExplainScoreCommand command, CancellationToken ct)
    {
        var profile = await db.RiskProfiles
                              .Include(p => p.Factors)
                              .FirstOrDefaultAsync(p => p.Id == command.ProfileId, ct)
                      ?? throw new KeyNotFoundException($"Profile {command.ProfileId} not found.");

        string explanation = aiService.Explain(profile, profile.Factors);

        var keyDrivers = profile.Factors
            .OrderByDescending(f => Math.Abs(f.PointsAwarded))
            .Take(3)
            .Select(f => $"{(f.IsPositive ? "+" : "-")} {f.Name}: {f.Description}")
            .ToList();

        // Persist explanation
        profile.AiExplanation = explanation;
        profile.UpdatedAt     = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return new ExplanationResponse(profile.Id, profile.Score, profile.RiskTier, explanation, keyDrivers);
    }
}
