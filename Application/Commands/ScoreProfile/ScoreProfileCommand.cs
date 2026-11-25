using CreditRiskScoring.Api.DTOs;
using MediatR;

namespace CreditRiskScoring.Api.Application.Commands.ScoreProfile;

public record ScoreProfileCommand(ScoreRequest Request) : IRequest<ScoreResponse>;
