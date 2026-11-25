namespace CreditRiskScoring.Api.Domain.Entities;

public class RiskFactor
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProfileId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int PointsAwarded { get; set; }
    public int MaxPoints { get; set; }
    public bool IsPositive { get; set; }

    public RiskProfile Profile { get; set; } = null!;
}
