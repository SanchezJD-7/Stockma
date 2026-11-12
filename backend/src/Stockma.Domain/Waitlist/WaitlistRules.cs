namespace Stockma.Domain.Waitlist;

public static class WaitlistRules
{
    public const int MinNameLength = 2;
    public const int MaxNameLength = 120;

    public const int MinPhoneDigits = 7;
    public const int MaxPhoneDigits = 15;

    public const int MaxEmailLength = 256;
    public const int MaxCountryCodeLength = 8;
}
