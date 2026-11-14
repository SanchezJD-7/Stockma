namespace Stockma.Infrastructure.Email;

public sealed class EmailOptions
{
    public const string SectionName = "SendGrid";

    public const string MissingApiKeyMessage =
        "SendGrid:ApiKey es obligatorio fuera de Development. Genere una API key en SendGrid y defínala con "
        + "dotnet user-secrets set \"SendGrid:ApiKey\" ... (dev) o la variable de entorno SendGrid__ApiKey (prod); "
        + "nunca en appsettings.json.";

    public const string MissingTemplateIdMessage =
        "SendGrid:TemplateId es obligatorio: es el ID de la dynamic template de confirmación (empieza con 'd-') "
        + "ya creada en la cuenta de SendGrid. Defínalo con user-secrets o con la variable de entorno "
        + "SendGrid__TemplateId.";

    public const string MissingFromEmailMessage =
        "SendGrid:FromEmail es obligatorio: es la dirección remitente verificada en SendGrid. Defínala con "
        + "user-secrets o con la variable de entorno SendGrid__FromEmail.";

    public string ApiKey { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
}
