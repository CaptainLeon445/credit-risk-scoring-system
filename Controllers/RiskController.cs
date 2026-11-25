using CreditRiskScoring.Api.Application.Commands.ExplainScore;
using CreditRiskScoring.Api.Application.Commands.ScoreProfile;
using CreditRiskScoring.Api.Application.Commands.SimulateScore;
using CreditRiskScoring.Api.Application.Queries.GetRiskFactors;
using CreditRiskScoring.Api.DTOs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CreditRiskScoring.Api.Controllers;

[ApiController]
[Route("api/v1/risk")]
[Authorize]
[Produces("application/json")]
public class RiskController(IMediator mediator) : ControllerBase
{
    /// <summary>Submit a financial profile and receive a risk score (0–100) and risk tier.</summary>
    [HttpPost("score")]
    [ProducesResponseType(typeof(ScoreResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Score([FromBody] ScoreRequest request)
    {
        var result = await mediator.Send(new ScoreProfileCommand(request));
        return Ok(result);
    }

    /// <summary>Get a plain-English AI explanation of why a score was given.</summary>
    [HttpPost("explain")]
    [ProducesResponseType(typeof(ExplanationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Explain([FromBody] ExplainRequest request)
    {
        var result = await mediator.Send(new ExplainScoreCommand(request.ProfileId));
        return Ok(result);
    }

    /// <summary>List the top positive and negative factors affecting a profile's score.</summary>
    [HttpGet("factors/{profileId:guid}")]
    [ProducesResponseType(typeof(List<RiskFactorResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFactors(Guid profileId)
    {
        var result = await mediator.Send(new GetRiskFactorsQuery(profileId));
        return Ok(result);
    }

    /// <summary>Simulate what the score would be if the financial profile were different.</summary>
    [HttpPost("simulate")]
    [ProducesResponseType(typeof(SimulateResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Simulate([FromBody] SimulateRequest request)
    {
        var result = await mediator.Send(new SimulateScoreCommand(request));
        return Ok(result);
    }
}
