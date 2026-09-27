using EIMSNext.Common;
using EIMSNext.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace EIMSNext.Core.Tests;

[TestClass]
public class PostgreSqlPersistenceRegistrationTests
{
    [TestMethod]
    public void ConfigurePostgreSqlDoesNotEnableRetryingStrategyForManualTransactions()
    {
        var options = new DbContextOptionsBuilder<PostgreSqlDbContext>();
        PostgreSqlPersistenceRegistration.ConfigurePostgreSql(options, new PostgreSqlOptions
        {
            ConnectionString = "Host=localhost;Database=unused;Username=unused;Password=unused",
            MaxRetryCount = 5
        });

        using var context = new PostgreSqlDbContext(options.Options);

        Assert.IsFalse(context.Database.CreateExecutionStrategy().RetriesOnFailure);
    }
}
