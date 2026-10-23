using System.Text;

using EIMSNext.Identity.Utilities;

namespace EIMSNext.Identity.Tests
{
    [TestClass]
    public class TokenRequestHelperTests
    {
        private static string Encode(Dictionary<string, string> payload) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(payload)));

        [TestMethod]
        public void UnscrambleEncrypted_RestoresRandomDigitPrefixAndPadding()
        {
            var original = Encode(new Dictionary<string, string> { ["username"] = "admin@eimsnext.com", ["password"] = "123456" });
            StringAssert.StartsWith(original, "eyJ");

            // 模拟前端混淆：前 3 位换随机数字，去掉末尾 =
            var scrambled = "742" + original[3..].TrimEnd('=');
            Assert.IsFalse(scrambled.Contains('='));
            Assert.AreEqual("742", scrambled[..3]);

            var restored = TokenRequestHelper.UnscrambleEncrypted(scrambled);

            Assert.AreEqual(original, restored);
            var parsed = TokenRequestHelper.ParseEncryptedFields(restored);
            Assert.IsTrue(parsed.Succeeded);
            Assert.AreEqual("admin@eimsnext.com", parsed.Fields!["username"]);
        }

        [TestMethod]
        public void UnscrambleEncrypted_RejectsUnscrambledInput()
        {
            // encrypted 必须是混淆后的变体：仍是真实前缀 eyJ（未混淆）的输入直接拒绝
            var original = Encode(new Dictionary<string, string> { ["grant_type"] = "authorization_code" });
            Assert.AreEqual(string.Empty, TokenRequestHelper.UnscrambleEncrypted(original));
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        public void UnscrambleEncrypted_EmptyInputReturnsEmpty(string? encrypted)
        {
            Assert.AreEqual(string.Empty, TokenRequestHelper.UnscrambleEncrypted(encrypted));
        }

        [TestMethod]
        public void UnscrambleEncrypted_InvalidLengthRestoresPrefixWithoutPadding()
        {
            // 长度 %4 == 1 属非法 base64：还原前缀但不补位，交由 ParseEncryptedFields 报错
            var invalid = "12345";
            Assert.AreEqual("eyJ45", TokenRequestHelper.UnscrambleEncrypted(invalid));
        }
    }
}
