using CreditRiskScoring.Api.Domain.Enums;

namespace CreditRiskScoring.Api.Domain.Entities;

public class RiskProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // ── Financial inputs ────────────────────────────────────────────────────
    public decimal AnnualIncome { get; set; }
    public decimal TotalDebt { get; set; }
    public decimal MonthlyExpenses { get; set; }
    public decimal Assets { get; set; }
    public int CreditHistoryMonths { get; set; }
    public int MissedPayments { get; set; }
    public string EmploymentStatus { get; set; } = string.Empty; // Employed | SelfEmployed | Unemployed
    public string LoanPurpose { get; set; } = string.Empty;

    // ── Computed outputs ─────────────────────────────────────────────────────
    public int Score { get; set; }
    public RiskTier RiskTier { get; set; }
    public bool LoanEligible { get; set; }
    public string? AiExplanation { get; set; }

    // ── Metadata ─────────────────────────────────────────────────────────────
    public string UserId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<RiskFactor> Factors { get; set; } = new List<RiskFactor>();

    // ── Derived ──────────────────────────────────────────────────────────────
    public decimal DebtToIncomeRatio =>
        AnnualIncome > 0 ? TotalDebt / AnnualIncome : 1m;
}
