using System.Dynamic;

using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Entities;
using EIMSNext.Core.Extensions;

namespace EIMSNext.Flow.Core
{
    public class WfDataContext
    {
        private WfDataContext() { }
        public WfDataContext(string corpId, string userId, string accessToken, string appId, string formId, string dataId, Operator? starter, CascadeMode cascade, string? eventIds)
        {
            CorpId = corpId;
            UserId = userId;
            AccessToken = accessToken;
            AppId = appId;
            FormId = formId;
            DataId = dataId;
            WfStarter = starter;
            EfCascade = cascade;
            EventIds = eventIds;
        }

        public static WfDataContext FromData(IDictionary<string, object?> data)
        {
            var ctx = new WfDataContext();
            ctx.CorpId = data.GetValue(WfConsts.CorpId, string.Empty);
            ctx.UserId = data.GetValue(WfConsts.UserId, string.Empty);
            ctx.AccessToken = data.GetValue(WfConsts.AccessToken, string.Empty);
            ctx.AppId = data.GetValue(WfConsts.AppId, string.Empty);
            ctx.FormId = data.GetValue(WfConsts.FormId, string.Empty);
            ctx.DataId = data.GetValue(WfConsts.DataId, string.Empty);

            var starter = data.GetValueOrDefault<IDictionary<string, object?>>(WfConsts.WfStarter);
            if (starter != null)
            {
                var empId = starter.GetValue(WfConsts.EmpId, string.Empty);
                var empCode = starter.GetValue(WfConsts.EmpCode, string.Empty);
                var empName = starter.GetValue(WfConsts.EmpName, string.Empty);
                ctx.WfStarter = new Operator(empId, empCode, empName);
            }

            ctx.EfCascade = (CascadeMode)data.GetValue<int>(WfConsts.EfCascade, 0);
            ctx.EventIds = data.GetValue<string?>(WfConsts.EventIds, null);
            ctx.Round = data.GetValue<int>(WfConsts.ApprovalRounnd, 1);

            return ctx;
        }

        public string CorpId { get; private set; } = string.Empty;
        public string UserId { get; private set; } = string.Empty;
        public string AccessToken { get; private set; } = string.Empty;
        public string AppId { get; private set; } = string.Empty;
        public string FormId { get; private set; } = string.Empty;
        public string DataId { get; private set; } = string.Empty;
        public Operator? WfStarter { get; private set; }
        public CascadeMode EfCascade { get; private set; }
        public string? EventIds { get; private set; }
        public bool MatchedResult { get; set; }
        public bool MatchParallel { get; set; }
        public int Round { get; set; } = 1;

        // WorkflowCore 的 WorkflowInstance.Data 是包内的 ExpandoObject 类型，写入口保持 ExpandoObject；
        // 读取侧一律走 IDictionary（见 FromData），不关心实际容器。
        public ExpandoObject ToExpando()
        {
            var data = new ExpandoObject();
            data.AddOrUpdate(WfConsts.CorpId, CorpId);
            data.AddOrUpdate(WfConsts.UserId, UserId);
            data.AddOrUpdate(WfConsts.AccessToken, AccessToken);
            data.AddOrUpdate(WfConsts.AppId, AppId);
            data.AddOrUpdate(WfConsts.FormId, FormId);
            data.AddOrUpdate(WfConsts.DataId, DataId);

            var empdo = new ExpandoObject();
            empdo.AddOrUpdate(WfConsts.CorpId, CorpId);
            if (WfStarter != null)
            {
                empdo.AddOrUpdate(WfConsts.EmpId, WfStarter.Id);
                empdo.AddOrUpdate(WfConsts.EmpCode, WfStarter.Value);
                empdo.AddOrUpdate(WfConsts.EmpName, WfStarter.Label);
            }

            data.AddOrUpdate(WfConsts.WfStarter, empdo);
            data.AddOrUpdate(WfConsts.MatchedResult, MatchedResult);
            data.AddOrUpdate(WfConsts.MatchParallel, MatchParallel);
            data.AddOrUpdate(WfConsts.EfCascade, (int)EfCascade);
            data.AddOrUpdate(WfConsts.EventIds, EventIds);
            data.AddOrUpdate(WfConsts.ApprovalRounnd, Round);

            return data;
        }
    }
}
