namespace Stockma.Infrastructure.Identity;

public sealed class OtpOptions
{
    public const string SectionName = "Otp";
    public int MaxAttempts { get; set; } = 5;
    public int MaxIssuesPerWindow { get; set; } = 5;
    public int IssueWindowMinutes { get; set; } = 15;
}
