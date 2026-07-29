using Asp.Versioning;

using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Auth.Entities;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Core.Abstractions.Extensions;
using EIMSNext.Service.Entities;

using Microsoft.OData.ModelBuilder;

namespace EIMSNext.Service.Host.Edm
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
            builder.EnumType<PlatformType>();
            builder.EnumType<CandidateType>();
            builder.EnumType<WfNodeType>();
            builder.EnumType<WfApprovalMode>();
            builder.EnumType<ApproverType>();
            builder.EnumType<PrintDefType>();
            builder.EnumType<AuthGroupType>();
            builder.EnumType<FormListViewType>();
            builder.EnumType<MobileFormListViewType>();
            //builder.EnumType<DataPerms>();
            builder.EnumType<MemberType>();
            builder.EnumType<DataChangeType>();
            builder.EnumType<NotifyTargetType>();
            builder.EnumType<FormNotifyTriggerMode>();
            builder.EnumType<NotifyChannel>();
            builder.EnumType<WfExpireActionType>();
            builder.EnumType<TimeUnit>();

            builder.ComplexType<UserCorp>();
            builder.ComplexType<EmpRole>();
            builder.ComplexType<EmpDept>();
            builder.ComplexType<Operator>();
            builder.ComplexType<DepartmentRef>();
            builder.ComplexType<EmployeeDepartmentRequest>();
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
            builder.ComplexType<ByLevelApprovalSetting>();
        }
    }
}
