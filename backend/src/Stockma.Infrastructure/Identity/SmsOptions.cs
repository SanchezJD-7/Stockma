namespace Stockma.Infrastructure.Identity;

public sealed class SmsOptions
{
    public const string SectionName = "Sms";

    public const string ConsoleProvider = "console";
    public const string TwilioProvider = "twilio";

    public const int MaximumRetryCount = 5;
    public const int MaximumRetryDelayMs = 10000;

    public const string MissingProviderMessage =
        "No hay proveedor de SMS configurado para este entorno. El sender de consola sólo corre en Development "
        + "porque escribe el código y el celular en el log; conectá un proveedor real (T049) antes de levantar "
        + "la API fuera de Development.";

    public const string ConsoleOutsideDevelopmentMessage =
        "Sms:Provider 'console' sólo corre en Development: fuera de ese entorno el código se escribiría en el log "
        + "y nadie recibiría un SMS real (ADR-016). Configurá Sms:Provider = 'twilio'.";

    public const string UnknownProviderMessage =
        "Sms:Provider desconocido: se admite 'twilio', y 'console' únicamente en Development.";

    public const string MissingAccountSidMessage =
        "Sms:AccountSid es obligatorio con Sms:Provider = 'twilio'. Es el SID de la cuenta de Twilio, empieza con 'AC'.";

    public const string MissingApiKeyMessage =
        "Sms:ApiKey es obligatorio con Sms:Provider = 'twilio'. Es el auth token o el secret de la API key; "
        + "va en user-secrets o en una variable de entorno, nunca en el repositorio.";

    public const string InvalidSenderMessage =
        "Sms:Sender es el número remitente y debe venir en formato E.164 (+5491155551234) o ser un remitente "
        + "alfanumérico, sin espacios.";

    public static readonly string InvalidRetryCountMessage =
        $"Sms:RetryCount debe estar entre 0 y {MaximumRetryCount}.";

    public static readonly string InvalidRetryDelayMessage =
        $"Sms:RetryDelayMs debe estar entre 0 y {MaximumRetryDelayMs}.";

    public string Provider { get; set; } = string.Empty;
    public string AccountSid { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string Sender { get; set; } = string.Empty;
    public int RetryCount { get; set; } = 2;
    public int RetryDelayMs { get; set; } = 250;

    public bool IsTwilio =>
        string.Equals(Provider?.Trim(), TwilioProvider, StringComparison.OrdinalIgnoreCase);

    public bool IsConsole =>
        string.Equals(Provider?.Trim(), ConsoleProvider, StringComparison.OrdinalIgnoreCase);

    public bool HasWellFormedSender
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Sender))
            {
                return false;
            }

            return !Sender.StartsWith('+') || Sender.Skip(1).All(char.IsDigit);
        }
    }
}
