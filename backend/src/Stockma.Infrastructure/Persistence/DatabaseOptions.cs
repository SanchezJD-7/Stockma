namespace Stockma.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "ConnectionStrings";

    public const string MissingRuntimeConnectionMessage =
        "ConnectionStrings:Postgres es obligatorio y appsettings.json no trae ninguno: definí la variable de entorno "
        + "ConnectionStrings__Postgres con el rol app_user. Nunca uses el rol propietario ni "
        + "ConnectionStrings__PostgresMigrations en el runtime. Ver ADR-017.";

    public string Postgres { get; set; } = string.Empty;
}
