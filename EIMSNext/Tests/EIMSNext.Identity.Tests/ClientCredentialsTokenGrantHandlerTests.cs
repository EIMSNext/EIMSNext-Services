using EIMSNext.ApiService;
using EIMSNext.Entities;
using EIMSNext.Identity.Interfaces;
using EIMSNext.Identity.Services;
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;

namespace EIMSNext.Identity.Tests;

[TestClass]
public class ClientCredentialsTokenGrantHandlerTests
{
    [TestMethod]
    public async Task HandleAsync_WritesSuccessAudit()
    {
        var auditLoginService = new RecordingIdentityLoginAuditService();
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

    private sealed class RecordingIdentityLoginAuditService : IIdentityLoginAuditService
    {
        public List<IdentityLoginAudit> Entries { get; } = [];

        public Task AddIdentityLoginAudit(IdentityLoginAudit entity)
        {
            Entries.Add(entity);
            return Task.CompletedTask;
        }
    }
}
