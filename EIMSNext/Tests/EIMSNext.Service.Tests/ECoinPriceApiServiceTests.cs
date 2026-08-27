using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.Common;
using EIMSNext.Entities;

namespace EIMSNext.Service.Tests
{
    [TestClass]
    public class ECoinPriceApiServiceTests
    {
        [TestMethod]
        public void NormalizeBatch_NormalizesNonPluginAndKeepsPluginFeature()
        {
            var result = ECoinPriceApiService.NormalizeBatch(
            [
                new ECoinPriceBatchItemRequest
                {
                    TargetType = ECoinTargetType.SMS,
                    FeatureId = "ignored",
                    PluginId = "ignored",
                    FeatureDesc = " SMS ",
                    Price = 1.25m,
                    ChargeType = ECoinChargeType.ECoin
                },
                new ECoinPriceBatchItemRequest
                {
                    TargetType = ECoinTargetType.Plugin,
                    PluginId = " sampleplugin ",
                    FeatureId = " EchoReceipt ",
                    FeatureDesc = " Receipt ",
                    Price = 2m,
                    ChargeType = ECoinChargeType.Subscription
                }
            ]);

            Assert.AreEqual("SMS", result[0].FeatureId);
            Assert.AreEqual(string.Empty, result[0].PluginId);
            Assert.AreEqual("sampleplugin", result[1].PluginId);
            Assert.AreEqual("EchoReceipt", result[1].FeatureId);
            Assert.AreEqual("Receipt", result[1].FeatureDesc);
        }

        [TestMethod]
        public void NormalizeBatch_RejectsNegativeDuplicateAndIncompletePluginPrices()
        {
            Assert.ThrowsExactly<BadRequestException>(() => ECoinPriceApiService.NormalizeBatch(
            [
                new ECoinPriceBatchItemRequest { TargetType = ECoinTargetType.SMS, Price = -1 }
            ]));

            Assert.ThrowsExactly<BadRequestException>(() => ECoinPriceApiService.NormalizeBatch(
            [
                new ECoinPriceBatchItemRequest { TargetType = ECoinTargetType.EMail },
                new ECoinPriceBatchItemRequest { TargetType = ECoinTargetType.EMail, FeatureId = "ignored" }
            ]));

            Assert.ThrowsExactly<BadRequestException>(() => ECoinPriceApiService.NormalizeBatch(
            [
                new ECoinPriceBatchItemRequest { TargetType = ECoinTargetType.Plugin, PluginId = "sampleplugin" }
            ]));
        }
    }
}
