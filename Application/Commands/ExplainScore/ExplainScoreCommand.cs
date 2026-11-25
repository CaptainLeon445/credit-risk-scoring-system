using CreditRiskScoring.Api.DTOs;
using MediatR;

namespace CreditRiskScoring.Api.Application.Commands.ExplainScore;

public record ExplainScoreCommand(Guid ProfileId) : IRequest<ExplanationResponse>;
