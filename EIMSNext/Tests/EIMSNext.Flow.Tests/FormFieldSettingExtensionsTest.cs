using EIMSNext.Flow.Core.Nodes.Dataflow;
using EIMSNext.Common;
using EIMSNext.Service.Entities;

namespace EIMSNext.Flow.Tests
{
    [TestClass]
    public class FormFieldSettingExtensionsTest
    {
        [TestMethod]
        public void Formula_Should_UseDrivingField_AsSubField()
        {
            var setting = new FormFieldSetting
            {
                ValueType = FieldValueType.Formula,
                ValueField = new FormFieldValueSetting
                {
                    SingleResultNode = false,
                    Field = new FormField
                    {
                        NodeId = "n1",
                        Field = "detail>amount",
                        IsSubField = true,
                        Type = FieldType.Number,
                    }
                }
            };

            Assert.IsTrue(setting.ValueIsSubField());
            Assert.IsFalse(setting.ValueIsSingleResultNode());
        }

        [TestMethod]
        public void Formula_MainField_Should_UseDrivingField_AsMultiResult()
        {
            var setting = new FormFieldSetting
            {
                ValueType = FieldValueType.Formula,
                ValueField = new FormFieldValueSetting
                {
                    SingleResultNode = false,
                    Field = new FormField
                    {
                        NodeId = "n2",
                        Field = "amount",
                        IsSubField = false,
                        Type = FieldType.Number,
                    }
                }
            };

            Assert.IsFalse(setting.ValueIsSubField());
            Assert.IsFalse(setting.ValueIsSingleResultNode());
        }
    }
}
