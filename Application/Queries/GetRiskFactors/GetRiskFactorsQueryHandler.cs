using CreditRiskScoring.Api.DTOs;
using CreditRiskScoring.Api.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CreditRiskScoring.Api.Application.Queries.GetRiskFactors;

public class GetRiskFactorsQueryHandler(AppDbContext db)
    : IRequestHandler<GetRiskFactorsQuery, List<RiskFactorResponse>>
{
    public async Task<List<RiskFactorResponse>> Handle(GetRiskFactorsQuery query, CancellationToken ct)
    {
        bool profileExists = await db.RiskProfiles.AnyAsync(p => p.Id == query.ProfileId, ct);
        if (!profileExists)
            throw new KeyNotFoundException($"Profile {query.ProfileId} not found.");

        return await db.RiskFactors
                       .Where(f => f.ProfileId == query.ProfileId)
                       .OrderByDescending(f => Math.Abs(f.PointsAwarded))
                       .Select(f => new RiskFactorResponse(
                           f.Name, f.Description, f.PointsAwarded, f.MaxPoints, f.IsPositive))
                       .ToListAsync(ct);
    }
}
