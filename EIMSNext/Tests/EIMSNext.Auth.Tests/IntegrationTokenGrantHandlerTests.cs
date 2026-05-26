using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Interfaces;
using EIMSNext.Auth.Models;
using EIMSNext.Auth.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace EIMSNext.Auth.Tests
{
    [TestClass]
    public class IntegrationTokenGrantHandlerTests
    {
        [TestMethod]
        public async Task HandleAsync_UsesFailureMessageFromIntegrationAuthService()
        {
            var handler = new IntegrationTokenGrantHandler(
                new FakeIntegrationAuthService(new IntegrationValidationResult
                {
                    FailureMessage = "自定义未绑定提示"
                }),
                new FakeAuditLoginService(),
                CreateHttpContextAccessor());

            var result = await handler.HandleAsync(
                new Client { AccessTokenLifetime = 28800 },
                new OpenIddictRequest
                {
                    Username = "wechat",
                    Password = "code"
                },
                ["openid"]);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(Errors.InvalidGrant, result.Error);
            Assert.AreEqual("自定义未绑定提示", result.ErrorDescription);
        }

        private static IHttpContextAccessor CreateHttpContextAccessor()
        {
            var context = new DefaultHttpContext();
            context.Request.Headers["X-Forwarded-For"] = new StringValues("127.0.0.1");
            return new HttpContextAccessor { HttpContext = context };
        }

        private sealed class FakeIntegrationAuthService(IntegrationValidationResult result) : IIntegrationAuthService
        {
            public Task<IntegrationValidationResult> ValidateAsync(string? integrationType, string? password, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(result);
            }

            public Task<IntegrationAuthorizationUrlResult> GetAuthorizationUrlAsync(string integrationType, string state, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new IntegrationAuthorizationUrlResult());
            }
        }

        private sealed class FakeAuditLoginService : IAuditLoginService
        {
            public Task AddAuditLogin(AuditLogin entity) => Task.CompletedTask;
        }
    }
}
