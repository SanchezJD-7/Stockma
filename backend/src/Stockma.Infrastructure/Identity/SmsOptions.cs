namespace Stockma.Infrastructure.Identity;

public sealed class SmsOptions
{
    public const string MissingProviderMessage =
        "No hay proveedor de SMS configurado para este entorno. El sender de consola sólo corre en Development "
        + "porque escribe el código y el celular en el log; conectá un proveedor real (T049) antes de levantar "
        + "la API fuera de Development.";
}
