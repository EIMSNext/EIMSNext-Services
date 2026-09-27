namespace EIMSNext.Common;

public sealed class PostgreSqlOptions
{
    public const string SectionName = "PostgreSql";
    public string ConnectionString { get; set; } = "Host=localhost;Port=5432;Database=EIMS;Username=postgres;Password=sa123;Pooling=true;Command Timeout=30;Timeout=15;";
    public int MaxRetryCount { get; set; } = 1;
    public bool EnableSensitiveDataLogging { get; set; }
}
