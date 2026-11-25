using CreditRiskScoring.Api.DTOs;
using MediatR;

namespace CreditRiskScoring.Api.Application.Commands.SimulateScore;

public record SimulateScoreCommand(SimulateRequest Request) : IRequest<SimulateResponse>;
