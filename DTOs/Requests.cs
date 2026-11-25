using System.ComponentModel.DataAnnotations;

namespace CreditRiskScoring.Api.DTOs;

// ── Auth ──────────────────────────────────────────────────────────────────────

public record RegisterRequest(
    [Required, MinLength(3)] string Username,
    [Required, EmailAddress]  string Email,
    [Required, MinLength(8)]  string Password
);

public record LoginRequest(
    [Required] string Username,
    [Required] string Password
);

// ── Risk Scoring ──────────────────────────────────────────────────────────────

/// <summary>Financial profile submitted for credit risk scoring.</summary>
public record ScoreRequest(
    [Required, Range(0, double.MaxValue)]
    decimal AnnualIncome,

    [Required, Range(0, double.MaxValue)]
    decimal TotalDebt,

    [Range(0, double.MaxValue)]
    decimal MonthlyExpenses,

    [Range(0, double.MaxValue)]
    decimal Assets,

    /// <summary>Number of months the borrower has had any credit account open.</summary>
    [Range(0, 600)]
    int CreditHistoryMonths,

    /// <summary>Total missed/late payments in credit history.</summary>
    [Range(0, 100)]
    int MissedPayments,

    /// <summary>Employed | SelfEmployed | Unemployed</summary>
    [Required]
    string EmploymentStatus,

    string LoanPurpose,
    string UserId
);

/// <summary>Hypothetical change to simulate — e.g. "What if income increases 20%?"</summary>
public record SimulateRequest(
    [Required] Guid ProfileId,
    decimal? NewAnnualIncome,
    decimal? NewTotalDebt,
    decimal? NewAssets,
    int? NewMissedPayments,
    string? NewEmploymentStatus
);

public record ExplainRequest(
    [Required] Guid ProfileId
);
