using CreditRiskScoring.Api.DTOs;
using MediatR;

namespace CreditRiskScoring.Api.Application.Queries.GetRiskFactors;

public record GetRiskFactorsQuery(Guid ProfileId) : IRequest<List<RiskFactorResponse>>;
