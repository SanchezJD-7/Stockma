namespace Stockma.Infrastructure.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Brevo";

    public const string MissingApiKeyMessage =
        "Brevo:ApiKey es obligatorio fuera de Development. Genere una API key en Brevo (Settings > SMTP & API, pestaña API) y defínala con "
        + "dotnet user-secrets set \"Brevo:ApiKey\" ... (dev) o la variable de entorno Brevo__ApiKey (prod); "
        + "nunca en appsettings.json.";

    public const string MissingTemplateIdMessage =
        "Brevo:TemplateId es obligatorio: es el ID numérico de la plantilla transaccional de confirmación "
        + "ya creada y activada en la cuenta de Brevo. Defínalo con user-secrets o con la variable de entorno "
        + "Brevo__TemplateId.";

    public const string MissingFromEmailMessage =
        "Brevo:FromEmail es obligatorio: es la dirección remitente verificada en Brevo. Defínala con "
        + "user-secrets o con la variable de entorno Brevo__FromEmail.";

    public string ApiKey { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public int TemplateId { get; set; }
}
