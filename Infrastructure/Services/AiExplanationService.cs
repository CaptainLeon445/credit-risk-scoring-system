using CreditRiskScoring.Api.Domain.Entities;
using CreditRiskScoring.Api.Domain.Enums;

namespace CreditRiskScoring.Api.Infrastructure.Services;

/// <summary>
/// Generates a plain-English explanation of the credit risk score.
///
/// Currently uses a deterministic template engine (simulated AI).
/// To switch to a real LLM:
///   1. Set Ai:Provider = "claude" or "openai" in appsettings.json
///   2. Implement HttpClient calls to the chosen API
///   3. Feed the profile + factors as a structured prompt
/// </summary>
public class AiExplanationService(IConfiguration config)
{
    private readonly string _provider = config["Ai:Provider"] ?? "simulated";

    public string Explain(RiskProfile profile, IEnumerable<RiskFactor> factors)
    {
        // In production, route to real LLM here:
        if (_provider is "openai" or "claude")
            return ExplainViaLlm(profile, factors);

        return ExplainViaTemplate(profile, factors);
    }

    // ── Template engine (simulated AI) ───────────────────────────────────────

    private static string ExplainViaTemplate(RiskProfile profile, IEnumerable<RiskFactor> factors)
    {
        var factorList = factors.ToList();
        var topPos  = factorList.Where(f => f.IsPositive && f.PointsAwarded > 0)
                                .OrderByDescending(f => f.PointsAwarded)
                                .Take(2)
                                .Select(f => f.Name)
                                .ToList();
        var topNeg  = factorList.Where(f => !f.IsPositive || f.PointsAwarded < f.MaxPoints / 2)
                                .OrderBy(f => f.PointsAwarded)
                                .Take(2)
                                .Select(f => f.Name)
                                .ToList();

        string tierDesc = profile.RiskTier switch
        {
            RiskTier.Excellent  => "an excellent credit profile",
            RiskTier.LowRisk    => "a low-risk credit profile",
            RiskTier.MediumRisk => "a moderate-risk credit profile",
            RiskTier.HighRisk   => "a high-risk credit profile",
            _                   => "an undetermined credit profile"
        };

        string eligibility = profile.LoanEligible
            ? "Based on these factors, you are eligible for a loan offer."
            : "At this time, your profile does not meet our minimum lending criteria.";

        string positiveStr = topPos.Count > 0
            ? $"Your strongest attributes are your {string.Join(" and ", topPos).ToLower()}. "
            : string.Empty;

        string negativeStr = topNeg.Count > 0
            ? $"The primary areas dragging your score down are {string.Join(" and ", topNeg).ToLower()}. Improving these could significantly boost your score. "
            : string.Empty;

        return $"""
            Your credit score is {profile.Score}/100, placing you in {tierDesc}. \
            {positiveStr}\
            {negativeStr}\
            {eligibility}
            """.Trim();
    }

    // ── LLM stub (wire up real HTTP call here) ────────────────────────────────

    private string ExplainViaLlm(RiskProfile profile, IEnumerable<RiskFactor> factors)
    {
        // TODO: call OpenAI / Claude API with the profile as structured input
        // For now, fall back to template so the API always works
        return ExplainViaTemplate(profile, factors);
    }
}
