using CreditRiskScoring.Api.Domain.Enums;

namespace CreditRiskScoring.Api.DTOs;

public record AuthResponse(string Token, string TokenType, int ExpiresInSeconds, string UserId, string Username);

public record MessageResponse(string Message);

public record ScoreResponse(
    Guid ProfileId,
    int Score,
    RiskTier RiskTier,
    string RiskTierLabel,
    bool LoanEligible,
    decimal DebtToIncomeRatio,
    List<RiskFactorResponse> Factors
);

public record RiskFactorResponse(
    string Name,
    string Description,
    int PointsAwarded,
    int MaxPoints,
    bool IsPositive
);

public record ExplanationResponse(
    Guid ProfileId,
    int Score,
    RiskTier RiskTier,
    string Explanation,
    List<string> KeyDrivers
);

public record SimulateResponse(
    Guid ProfileId,
    int OriginalScore,
    int SimulatedScore,
    int ScoreDelta,
    RiskTier OriginalTier,
    RiskTier SimulatedTier,
    bool EligibilityChanged,
    string Summary
);
