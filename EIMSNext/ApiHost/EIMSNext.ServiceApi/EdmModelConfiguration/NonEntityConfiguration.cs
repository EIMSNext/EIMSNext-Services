using Asp.Versioning;

using EIMSNext.Auth.Entity;
using EIMSNext.Core.Entity;
using EIMSNext.Entity;

using Microsoft.OData.ModelBuilder;

namespace EIMSNext.ServiceApi.EdmModelConfiguration
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
            builder.EnumType<FormType>();
            builder.EnumType<FlowType>();
            builder.EnumType<EventSourceType>();
            builder.EnumType<ChangeType>();
            builder.EnumType<PlatformType>();
            builder.EnumType<CandidateType>();
            builder.EnumType<WfNodeType>();
            builder.EnumType<WfApprovalMode>();
            builder.EnumType<PrintTemplateType>();
            builder.EnumType<AuthGroupType>();
            //builder.EnumType<DataPerms>();
            builder.EnumType<MemberType>();

            builder.ComplexType<UserCorp>();
            builder.ComplexType<EmpRole>();
            builder.ComplexType<FieldDef>();
            builder.ComplexType<FieldOpt>();
            builder.ComplexType<ValueOpt>();
            builder.ComplexType<FieldChangeLog>();
            builder.ComplexType<FormContent>();
            builder.ComplexType<AppMenu>();
            builder.ComplexType<Member>();
            builder.ComplexType<FieldPerm>();

            builder.ComplexType<Operator>().Ignore(x => x.CorpId);
            builder.ComplexType<Operator>().Ignore(x => x.UserId);

            builder.ComplexType<WfMetadata>();
            builder.ComplexType<WfStep>();
            builder.ComplexType<ApprovalCandidate>();
        }
    }
}
