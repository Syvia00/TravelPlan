namespace TravelPlan.Shared.Models;

/// <summary>Standalone OTP cache — not tied to a trip or user.</summary>
public class PhoneVerification
{
    public int Id { get; set; }

    public string PhoneNumber { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public int Attempts { get; set; }

    public DateTime CreatedAt { get; set; }
}
