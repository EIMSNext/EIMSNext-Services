using System.Security.Claims;
using System.Reflection;
using System.Text;
using System.Text.Json;

using EIMSNext.Entities;
using EIMSNext.Identity.Host;
using EIMSNext.Identity.Host.Controllers;
using EIMSNext.Identity.Interfaces;
using EIMSNext.Identity.Models;
using EIMSNext.ApiService;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

using OpenIddict.Abstractions;

namespace EIMSNext.Identity.Tests
{
    [TestClass]
    public class BuiltInClientRequestPolicyTests
    {
        [TestMethod]
        public void ValidateTokenEndpoint_RejectsWebClient()
        {
            var policy = CreatePolicy();

            var result = policy.ValidateTokenEndpoint(InternalClients.WebClientId);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(OpenIddictConstants.Errors.InvalidClient, result.Error);
        }

        [TestMethod]
        public void ValidateTokenEndpoint_RejectsPublicClient()
        {
            var policy = CreatePolicy();

            var result = policy.ValidateTokenEndpoint(InternalClients.PublicClientId);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(OpenIddictConstants.Errors.InvalidClient, result.Error);
        }

        [TestMethod]
        public void ValidateTokenEndpoint_RejectsSystemClient()
        {
            var policy = CreatePolicy();

            var result = policy.ValidateTokenEndpoint(InternalClients.SystemClientId);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(OpenIddictConstants.Errors.InvalidClient, result.Error);
        }

        [TestMethod]
        public void ValidateLogin_RejectsOriginOutsideConfiguration()
        {
            var policy = CreatePolicy();
            var request = CreateRequest("https://evil.example.com");

            var result = policy.ValidateLogin(InternalClients.WebClientId, request);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(OpenIddictConstants.Errors.InvalidClient, result.Error);
        }

        [TestMethod]
        public void ValidateLogin_RejectsNonWebClientAsInvalidClient()
        {
            var policy = CreatePolicy();
            var request = CreateRequest("https://admin.eimsnext.com");

            var result = policy.ValidateLogin(InternalClients.SystemClientId, request);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(OpenIddictConstants.Errors.InvalidClient, result.Error);
        }

        [TestMethod]
        public void ValidateLogin_AllowsConfiguredOrigin()
        {
            var policy = CreatePolicy();
            var request = CreateRequest("https://admin.eimsnext.com");

            var result = policy.ValidateLogin(InternalClients.WebClientId, request);

            Assert.IsTrue(result.Succeeded);
        }

        [TestMethod]
        public void ValidateLogin_AllowsMissingOriginInDevelopmentWhenConfigured()
        {
            var policy = CreatePolicy(isDevelopment: true);
            var request = CreateRequest();

            var result = policy.ValidateLogin(InternalClients.WebClientId, request);

            Assert.IsTrue(result.Succeeded);
        }

        [TestMethod]
        public void ValidatePublicToken_AllowsMissingOriginWhenNotRequired()
        {
            var policy = CreatePolicy();
            var request = CreateRequest();

            var result = policy.ValidatePublicToken(InternalClients.PublicClientId, CustomGrantType.Public, request);

            Assert.IsTrue(result.Succeeded);
        }

        [TestMethod]
        public void ValidatePublicToken_RejectsWrongGrantType()
        {
            var policy = CreatePolicy();
            var request = CreateRequest();

            var result = policy.ValidatePublicToken(InternalClients.PublicClientId, OpenIddictConstants.GrantTypes.Password, request);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(OpenIddictConstants.Errors.InvalidRequest, result.Error);
        }

        [TestMethod]
        public void ValidatePublicToken_RejectsMissingGrantType()
        {
            var policy = CreatePolicy();
            var request = CreateRequest();

            var result = policy.ValidatePublicToken(InternalClients.PublicClientId, null, request);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(OpenIddictConstants.Errors.InvalidRequest, result.Error);
        }

        [TestMethod]
        public void ValidateSystemToken_AllowsSystemClientAndGrant()
        {
            var policy = CreatePolicy();

            var result = policy.ValidateSystemToken(InternalClients.SystemClientId, CustomGrantType.System);

            Assert.IsTrue(result.Succeeded);
        }

        [TestMethod]
        public void SystemToken_IsHiddenFromApiExplorer()
        {
            var method = typeof(AuthorizationController).GetMethod(nameof(AuthorizationController.SystemToken));
            var attribute = method?.GetCustomAttribute<ApiExplorerSettingsAttribute>();

            Assert.IsNotNull(attribute);
            Assert.IsTrue(attribute.IgnoreApi);
        }

        [TestMethod]
        public async Task SystemToken_UsesSystemGrantAndPreservesTaskParameters()
        {
            var handler = new RecordingTokenRequestHandler
            {
                NextResult = TokenRequestResult.Success(
                    "system",
                    CustomGrantType.System,
                    3600,
                    ["api.readwrite"],
                    [
                        new Claim(IdentityClaimTypes.Subject, "system"),
                        new Claim(IdentityClaimTypes.Name, "wf_instance-001"),
                        new Claim(IdentityClaimTypes.Id, "system"),
                        new Claim(IdentityClaimTypes.Corp, "corp-001"),
                        new Claim(IdentityClaimTypes.IdentityType, IdentityType.System.ToString())
                    ])
            };

            var controller = CreateController(handler);
            var context = CreateHttpContext();
            context.Request.ContentType = "application/x-www-form-urlencoded";
            context.Request.Form = new FormCollection(new Dictionary<string, StringValues>
            {
                ["grant_type"] = CustomGrantType.System,
                ["client_id"] = InternalClients.SystemClientId,
                ["client_secret"] = InternalClients.SystemClientSecret,
                ["scope"] = "api.readwrite",
                ["corp_id"] = "corp-001",
                ["object_type"] = "wf",
                ["object_id"] = "instance-001"
            });
            controller.ControllerContext = new ControllerContext { HttpContext = context };

            var result = await controller.SystemToken(CancellationToken.None);

            Assert.IsInstanceOfType<SignInResult>(result);
            Assert.AreEqual(1, handler.CallCount);
            Assert.AreEqual(CustomGrantType.System, handler.LastRequest?.GrantType);
            Assert.AreEqual(InternalClients.SystemClientId, handler.LastRequest?.ClientId);
            Assert.AreEqual(InternalClients.SystemClientSecret, handler.LastRequest?.ClientSecret);
            Assert.AreEqual("corp-001", handler.LastRequest?.GetParameter("corp_id")?.ToString());
            Assert.AreEqual("wf", handler.LastRequest?.GetParameter("object_type")?.ToString());
            Assert.AreEqual("instance-001", handler.LastRequest?.GetParameter("object_id")?.ToString());
        }

        [TestMethod]
        public async Task Login_RejectsPublicClientBeforeTokenHandlerRuns()
        {
            var handler = new RecordingTokenRequestHandler();
            var controller = CreateController(handler);
            controller.ControllerContext = new ControllerContext { HttpContext = CreateHttpContext("https://admin.eimsnext.com") };

            var result = await controller.Login(CreateEncryptedBody(new Dictionary<string, string>
            {
                ["username"] = "admin@eimsnext.com",
                ["password"] = "123456",
                ["client_id"] = InternalClients.PublicClientId
            }), CancellationToken.None);

            Assert.IsInstanceOfType<BadRequestObjectResult>(result);
            Assert.AreEqual(0, handler.CallCount);
        }

        [TestMethod]
        public async Task PublicToken_UsesPublicClientAndPublicGrant()
        {
            var handler = new RecordingTokenRequestHandler
            {
                NextResult = TokenRequestResult.Success(
                    "public_target",
                    CustomGrantType.Public,
                    3600,
                    ["DashLink"],
                    [
                        new Claim(IdentityClaimTypes.Subject, "public_target"),
                        new Claim(IdentityClaimTypes.Name, "public"),
                        new Claim(IdentityClaimTypes.Id, "public_target"),
                        new Claim(IdentityClaimTypes.Corp, "corp-001")
                    ])
            };

            var controller = CreateController(handler);
            controller.ControllerContext = new ControllerContext { HttpContext = CreateHttpContext() };

            var result = await controller.PublicToken(CreateEncryptedBody(new Dictionary<string, string>
            {
                ["username"] = "public_target",
                ["password"] = "code",
                ["client_id"] = InternalClients.PublicClientId,
                ["grant_type"] = CustomGrantType.Public,
                ["scope"] = "DashLink"
            }), CancellationToken.None);

            Assert.IsInstanceOfType<SignInResult>(result);
            Assert.AreEqual(1, handler.CallCount);
            Assert.AreEqual(InternalClients.PublicClientId, handler.LastRequest?.ClientId);
            Assert.AreEqual(CustomGrantType.Public, handler.LastRequest?.GrantType);
        }

        [TestMethod]
        public async Task Login_UsesConfiguredWebClientWhenOriginAllowed()
        {
            var handler = new RecordingTokenRequestHandler
            {
                NextResult = TokenRequestResult.Success(
                    "admin",
                    OpenIddictConstants.GrantTypes.Password,
                    3600,
                    ["openid"],
                    [
                        new Claim(IdentityClaimTypes.Subject, "admin"),
                        new Claim(IdentityClaimTypes.Name, "Admin"),
                        new Claim(IdentityClaimTypes.Id, "admin"),
                        new Claim(IdentityClaimTypes.Corp, "corp-001")
                    ])
            };

            var controller = CreateController(handler);
            controller.ControllerContext = new ControllerContext { HttpContext = CreateHttpContext("https://mobile.eimsnext.com") };

            var result = await controller.Login(CreateEncryptedBody(new Dictionary<string, string>
            {
                ["username"] = "admin@eimsnext.com",
                ["password"] = "123456",
                ["client_id"] = InternalClients.WebClientId
            }), CancellationToken.None);

            Assert.IsInstanceOfType<SignInResult>(result);
            Assert.AreEqual(1, handler.CallCount);
            Assert.AreEqual(InternalClients.WebClientId, handler.LastRequest?.ClientId);
            Assert.AreEqual(OpenIddictConstants.GrantTypes.Password, handler.LastRequest?.GrantType);
        }

        private static IBuiltInClientRequestPolicy CreatePolicy(bool isDevelopment = false)
        {
            var options = Options.Create(new BuiltInClientsOptions
            {
                Web = new BuiltInClientPolicyOptions
                {
                    RequireOrigin = true,
                    AllowMissingOriginInDevelopment = true,
                    AllowedOrigins =
                    [
                        "https://admin.eimsnext.com",
                        "https://mobile.eimsnext.com"
                    ]
                },
                Public = new BuiltInClientPolicyOptions
                {
                    RequireOrigin = false
                }
            });

            return new BuiltInClientRequestPolicy(options, new FakeHostEnvironment(isDevelopment));
        }

        private static AuthorizationController CreateController(RecordingTokenRequestHandler handler)
        {
            return new AuthorizationController(handler, CreatePolicy());
        }

        private static HttpRequest CreateRequest(string? origin = null)
        {
            return CreateHttpContext(origin).Request;
        }

        private static DefaultHttpContext CreateHttpContext(string? origin = null)
        {
            var context = new DefaultHttpContext();
            if (!string.IsNullOrWhiteSpace(origin))
            {
                context.Request.Headers.Origin = origin;
            }
            return context;
        }

        private static EncryptedLoginRequest CreateEncryptedBody(Dictionary<string, string> fields)
        {
            return new EncryptedLoginRequest
            {
                Encrypted = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(fields)))
            };
        }

        private sealed class RecordingTokenRequestHandler : ITokenRequestHandler
        {
            public int CallCount { get; private set; }

            public OpenIddictRequest? LastRequest { get; private set; }

            public TokenRequestResult NextResult { get; set; } = TokenRequestResult.Failure("unexpected", "unexpected");

            public Task<TokenRequestResult> HandleAsync(OpenIddictRequest request, CancellationToken cancellationToken = default)
            {
                CallCount++;
                LastRequest = request;
                return Task.FromResult(NextResult);
            }
        }

        private sealed class FakeHostEnvironment : IHostEnvironment
        {
            public FakeHostEnvironment(bool isDevelopment)
            {
                EnvironmentName = isDevelopment ? Environments.Development : Environments.Production;
            }

            public string EnvironmentName { get; set; }

            public string ApplicationName { get; set; } = "EIMSNext.Identity.Tests";

            public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

            public IFileProvider ContentRootFileProvider { get; set; } = null!;
        }
    }
}
