using EIMSNext.Common;
using EIMSNext.Core.Entities;
using EIMSNext.Service.Entities;

namespace EIMSNext.Service.Tests
{
    [TestClass]
    public class SerialNoFieldTypeTests
    {
        [TestMethod]
        public void IsInputField_Input_ReturnsTrue()
        {
            Assert.IsTrue(FieldType.IsInputField(FieldType.Input));
        }

        [TestMethod]
        public void IsInputField_SerialNo_ReturnsTrue()
        {
            // 流水号字段与 input 一样,纳入 IsInputField 集合,
            // 这样 FormLayoutParser / FormDataFormatter / ConditionList 等所有
            // "基于 IsInputField 判定"的代码自动支持流水号(条件/运算/动态过滤一致)。
            Assert.IsTrue(FieldType.IsInputField(FieldType.SerialNo));
        }

        [TestMethod]
        public void IsInputField_Unknown_ReturnsFalse()
        {
            Assert.IsFalse(FieldType.IsInputField("nonsense"));
        }

        [TestMethod]
        public void IsInputField_Empty_ReturnsFalse()
        {
            Assert.IsFalse(FieldType.IsInputField(""));
        }

        [TestMethod]
        public void AllFieldTypes_ContainsSerialNo()
        {
            CollectionAssert.Contains(FieldType.AllFieldTypes, FieldType.SerialNo);
        }

        [TestMethod]
        public void SerialNoType_Form_Exists()
        {
            // 流水号基于现有 SerialNoType.Form 分支
            Assert.IsTrue(System.Enum.IsDefined(typeof(SerialNoType), SerialNoType.Form));
        }

        [TestMethod]
        public void SerialNoSequence_HasKeyField()
        {
            // 同表单内多字段独立计数
            var seq = new SerialNoSequence
            {
                CorpId = "c1",
                AppId = "a1",
                FormId = "f1",
                Key = "f_abc",
                SerialNoType = SerialNoType.Form,
                CurrId = 1,
            };
            Assert.AreEqual("f_abc", seq.Key);
        }

        [TestMethod]
        public void SerialNoResetCycle_AllValuesDefined()
        {
            // 4 个重置周期:Never / Day / Month / Year
            Assert.AreEqual(0, (int)SerialNoResetCycle.Never);
            Assert.AreEqual(1, (int)SerialNoResetCycle.Day);
            Assert.AreEqual(2, (int)SerialNoResetCycle.Month);
            Assert.AreEqual(3, (int)SerialNoResetCycle.Year);
        }
    }
}
