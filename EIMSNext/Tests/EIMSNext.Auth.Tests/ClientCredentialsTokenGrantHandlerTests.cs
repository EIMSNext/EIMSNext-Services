using EIMSNext.ApiService;
using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Interfaces;
using EIMSNext.Auth.Services;
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;

namespace EIMSNext.Auth.Tests;

[TestClass]
public class ClientCredentialsTokenGrantHandlerTests
{
    [TestMethod]
    public async Task HandleAsync_WritesSuccessAudit()
    {
        var auditLoginService = new RecordingAuditLoginService();
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
        var handler = new ClientCredentialsTokenGrantHandler(
            auditLoginService,
            new HttpContextAccessor { HttpContext = context });
        var client = new Client
        {
            Id = "partner-client",
            Name = "Partner",
            CorpId = "corp-001",
            Enabled = true,
            AllowedScopes = [new ClientScope { Scope = "api.readwrite" }]
        };

        var result = await handler.HandleAsync(
            client,
            new OpenIddictRequest { GrantType = CustomGrantType.ClientCredentials },
            ["api.readwrite"]);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(1, auditLoginService.Entries.Count);
        var audit = auditLoginService.Entries[0];
        Assert.AreEqual(CustomGrantType.ClientCredentials, audit.GrantType);
        Assert.AreEqual("partner-client", audit.LoginId);
        Assert.AreEqual("corp-001", audit.CorpId);
        Assert.AreEqual("127.0.0.1", audit.ClientIp);
    }

    private sealed class RecordingAuditLoginService : IAuditLoginService
    {
        public List<AuditLogin> Entries { get; } = [];

        public Task AddAuditLogin(AuditLogin entity)
        {
            Entries.Add(entity);
            return Task.CompletedTask;
        }
    }
}
