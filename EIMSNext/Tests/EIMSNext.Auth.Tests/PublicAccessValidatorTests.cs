using EIMSNext.ApiService;
using EIMSNext.Auth.Models;
using EIMSNext.Common;

namespace EIMSNext.Auth.Tests
{
    [TestClass]
    public class PublicAccessValidatorTests
    {
        private const string TargetId = "form-001";
        private const string SecretKey = "test-secret-key";
        private const string CorrectAccessCode = "secret-123";

        [TestMethod]
        public void ValidateSection_FormLink_NoAccessCode_AndChallengeValid_ReturnsTrue()
        {
            var section = NewSection(enabled: true, accessCodeEnabled: false);
            var challenge = PublicPasswordHelper.GenerateChallenge(TargetId, SecretKey, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

            var ok = PublicAccessValidator.ValidateSection(section, challenge, TargetId, SecretKey);

            Assert.IsTrue(ok, "无 accessCode 时应通过 HMAC challenge 校验");
        }

        [TestMethod]
        public void ValidateSection_FormLink_NoAccessCode_AndChallengeExpired_ReturnsFalse()
        {
            var section = NewSection(enabled: true, accessCodeEnabled: false);
            var expired = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeMilliseconds();
            var challenge = PublicPasswordHelper.GenerateChallenge(TargetId, SecretKey, expired);

            var ok = PublicAccessValidator.ValidateSection(section, challenge, TargetId, SecretKey);

            Assert.IsFalse(ok, "超时的 challenge 必须拒绝");
        }

        [TestMethod]
        public void ValidateSection_FormLink_AccessCodeEnabled_CorrectCode_ReturnsTrue()
        {
            var section = NewSection(enabled: true, accessCodeEnabled: true, accessCodeHash: CorrectAccessCode);

            var ok = PublicAccessValidator.ValidateSection(section, CorrectAccessCode, TargetId, SecretKey);

            Assert.IsTrue(ok, "accessCode 匹配时必须通过");
        }

        [TestMethod]
        public void ValidateSection_FormLink_AccessCodeEnabled_WrongCode_ReturnsFalse()
        {
            var section = NewSection(enabled: true, accessCodeEnabled: true, accessCodeHash: CorrectAccessCode);

            var ok = PublicAccessValidator.ValidateSection(section, "wrong-code", TargetId, SecretKey);

            Assert.IsFalse(ok, "accessCode 不匹配必须拒绝");
        }

        [TestMethod]
        public void ValidateSection_FormLink_AccessCodeEnabled_NoPassword_ReturnsFalse()
        {
            var section = NewSection(enabled: true, accessCodeEnabled: true, accessCodeHash: CorrectAccessCode);

            var ok = PublicAccessValidator.ValidateSection(section, null, TargetId, SecretKey);

            Assert.IsFalse(ok, "启用 accessCode 但 password 为空必须拒绝");
        }

        [TestMethod]
        public void ValidateSection_FormLink_NotEnabled_ReturnsFalse()
        {
            var section = NewSection(enabled: false, accessCodeEnabled: true, accessCodeHash: CorrectAccessCode);

            var ok = PublicAccessValidator.ValidateSection(section, CorrectAccessCode, TargetId, SecretKey);

            Assert.IsFalse(ok, "section 未启用必须拒绝");
        }

        [TestMethod]
        public void ValidateSection_FormLink_Expired_ReturnsFalse()
        {
            var section = new PublishSection
            {
                Enabled = true,
                AccessCodeEnabled = false,
                ExpireTime = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeMilliseconds()
            };

            var ok = PublicAccessValidator.ValidateSection(section, "anything", TargetId, SecretKey);

            Assert.IsFalse(ok, "过期 section 必须拒绝");
        }

        [TestMethod]
        public void ValidateSection_FormLink_NullSection_ReturnsFalse()
        {
            var ok = PublicAccessValidator.ValidateSection(null, "any", TargetId, SecretKey);

            Assert.IsFalse(ok, "section 为 null 必须拒绝");
        }

        [TestMethod]
        public void ValidateSection_StrictScopeMatching_FormLinkRejected_WhenOnlyDataLinkHasCorrectCode()
        {
            // 严格 scope 校验：FormLink scope 只能匹配 FormLink section 的 accessCode
            // 即使 DataLink 的 accessCode 正确，也不允许通过 FormLink 访问
            var setting = new PublicAccessSetting
            {
                TargetId = TargetId,
                Form = new PublicFormAccessSetting
                {
                    FormLink = NewSection(enabled: true, accessCodeEnabled: true, accessCodeHash: "form-code"),
                    DataLink = NewSection(enabled: true, accessCodeEnabled: true, accessCodeHash: CorrectAccessCode)
                }
            };

            var formLinkSection = PublicAccessValidator.ResolveSection(setting, PublicScope.FormLink);
            var ok = PublicAccessValidator.ValidateSection(formLinkSection, CorrectAccessCode, TargetId, SecretKey);

            Assert.IsFalse(ok, "FormLink scope 用 DataLink 的 accessCode 必须被拒绝（严格 scope 校验）");
        }

        [TestMethod]
        public void ValidateSection_StrictScopeMatching_AllScopes_OnlyOwnSectionAccepted()
        {
            // 严格 scope 校验：每个 scope 只能通过自己 section 的 accessCode
            var setting = new PublicAccessSetting
            {
                TargetId = TargetId,
                Form = new PublicFormAccessSetting
                {
                    FormLink = NewSection(enabled: true, accessCodeEnabled: true, accessCodeHash: "code-form"),
                    DataLink = NewSection(enabled: true, accessCodeEnabled: true, accessCodeHash: "code-data"),
                    QueryLink = NewSection(enabled: true, accessCodeEnabled: true, accessCodeHash: "code-query")
                },
                Dashboard = NewSection(enabled: true, accessCodeEnabled: true, accessCodeHash: "code-dash")
            };

            // FormLink 只能匹配 code-form
            Assert.IsTrue(PublicAccessValidator.ValidateSection(PublicAccessValidator.ResolveSection(setting, PublicScope.FormLink), "code-form", TargetId, SecretKey));
            Assert.IsFalse(PublicAccessValidator.ValidateSection(PublicAccessValidator.ResolveSection(setting, PublicScope.FormLink), "code-data", TargetId, SecretKey));
            Assert.IsFalse(PublicAccessValidator.ValidateSection(PublicAccessValidator.ResolveSection(setting, PublicScope.FormLink), "code-query", TargetId, SecretKey));
            Assert.IsFalse(PublicAccessValidator.ValidateSection(PublicAccessValidator.ResolveSection(setting, PublicScope.FormLink), "code-dash", TargetId, SecretKey));

            // DataLink 只能匹配 code-data
            Assert.IsTrue(PublicAccessValidator.ValidateSection(PublicAccessValidator.ResolveSection(setting, PublicScope.DataLink), "code-data", TargetId, SecretKey));
            Assert.IsFalse(PublicAccessValidator.ValidateSection(PublicAccessValidator.ResolveSection(setting, PublicScope.DataLink), "code-form", TargetId, SecretKey));

            // QueryLink 只能匹配 code-query
            Assert.IsTrue(PublicAccessValidator.ValidateSection(PublicAccessValidator.ResolveSection(setting, PublicScope.QueryLink), "code-query", TargetId, SecretKey));
            Assert.IsFalse(PublicAccessValidator.ValidateSection(PublicAccessValidator.ResolveSection(setting, PublicScope.QueryLink), "code-form", TargetId, SecretKey));

            // DashLink 只能匹配 code-dash
            Assert.IsTrue(PublicAccessValidator.ValidateSection(PublicAccessValidator.ResolveSection(setting, PublicScope.DashLink), "code-dash", TargetId, SecretKey));
            Assert.IsFalse(PublicAccessValidator.ValidateSection(PublicAccessValidator.ResolveSection(setting, PublicScope.DashLink), "code-form", TargetId, SecretKey));
        }

        [TestMethod]
        public void ValidateSection_DashLink_AccessCodeEnabled_NoCodeSet_ReturnsFalse()
        {
            var section = NewSection(enabled: true, accessCodeEnabled: true, accessCodeHash: "");

            var ok = PublicAccessValidator.ValidateSection(section, "any", TargetId, SecretKey);

            Assert.IsFalse(ok, "启用 accessCode 但 hash 为空必须拒绝");
        }

        [TestMethod]
        public void ValidateSection_NoneScope_ReturnsFalse()
        {
            var setting = new PublicAccessSetting
            {
                TargetId = TargetId,
                Form = new PublicFormAccessSetting
                {
                    FormLink = NewSection(enabled: true, accessCodeEnabled: false)
                }
            };

            var section = PublicAccessValidator.ResolveSection(setting, PublicScope.None);

            Assert.IsNull(section, "None scope 必须返回 null section");
        }

        private static PublishSection NewSection(bool enabled, bool accessCodeEnabled, string accessCodeHash = "")
        {
            return new PublishSection
            {
                Enabled = enabled,
                AccessCodeEnabled = accessCodeEnabled,
                AccessCodeHash = accessCodeHash
            };
        }
    }
}
