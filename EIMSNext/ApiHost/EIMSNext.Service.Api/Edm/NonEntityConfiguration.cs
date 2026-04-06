using Asp.Versioning;

using EIMSNext.Auth.Entities;
using EIMSNext.Core;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Extensions;
using EIMSNext.Service.Entities;

using Microsoft.OData.ModelBuilder;

namespace EIMSNext.Service.Api.Edm
{
    /// <summary>
    /// 
    /// </summary>
    public class NonEntityConfiguration : ModelConfigurationBase
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="apiVersion"></param>
        /// <param name="routePrefix"></param>
        /// <exception cref="NotImplementedException"></exception>
        public override void Apply(ODataModelBuilder builder, ApiVersion apiVersion, string? routePrefix)
        {
            builder.EnumType<DbAction>();
            builder.EnumType<FormType>();
            builder.EnumType<FlowType>();
            builder.EnumType<EventSourceType>();
            builder.EnumType<FieldChangeType>();
            builder.EnumType<PlatformType>();
            builder.EnumType<CandidateType>();
            builder.EnumType<WfNodeType>();
            builder.EnumType<WfApprovalMode>();
            builder.EnumType<PrintTemplateType>();
            builder.EnumType<AuthGroupType>();
            //builder.EnumType<DataPerms>();
            builder.EnumType<MemberType>();
            builder.EnumType<DataChangeType>();
            builder.EnumType<FormNotifyTriggerMode>();
            builder.EnumType<FormNotifyChannel>();

            builder.ComplexType<UserCorp>();
            builder.ComplexType<EmpRole>();
            builder.ComplexType<FieldDef>();
            builder.ComplexType<FieldProp>();
            builder.ComplexType<ValueProp>();
            builder.ComplexType<FieldChangeLog>();
            builder.ComplexType<FormContent>();
            builder.ComplexType<AppMenu>();
            builder.ComplexType<Member>();
            builder.ComplexType<FieldPerm>();

            builder.ComplexType<WfMetadata>();
            builder.ComplexType<WfStep>();
            builder.ComplexType<ApprovalCandidate>();
        }
    }
}
