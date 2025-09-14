using System.Dynamic;

using EIMSNext.Core.Entity;
using EIMSNext.Entity;

namespace EIMSNext.Flow.Core
{
    public class WfDataContext
    {
        private WfDataContext() { }
        public WfDataContext(string corpId, string appId, string formId, string dataId, Operator? starter, CascadeMode cascade, string? eventIds)
        {
            CorpId = corpId;
            AppId = appId;
            FormId = formId;
            DataId = dataId;
            WfStarter = starter;
            DfCascade = cascade;
            EventIds = eventIds;
        }

        public static WfDataContext FromExpando(ExpandoObject expando)
        {
            var ctx = new WfDataContext();
            ctx.CorpId = expando.GetValue(WfConsts.CorpId, string.Empty);
            ctx.AppId = expando.GetValue(WfConsts.AppId, string.Empty);
            ctx.FormId = expando.GetValue(WfConsts.FormId, string.Empty);
            ctx.DataId = expando.GetValue(WfConsts.DataId, string.Empty);

            var empdo = expando.GetValueOrDefault<ExpandoObject>(WfConsts.WfStarter);
            if (empdo != null)
            {
                var userId = empdo.GetValue(WfConsts.UserId, string.Empty);
                var empId = empdo.GetValue(WfConsts.EmpId, string.Empty);
                var empName = empdo.GetValue(WfConsts.EmpName, string.Empty);
                ctx.WfStarter = new Operator(ctx.CorpId, userId, empId, empName);
            }

            ctx.DfCascade = (CascadeMode)expando.GetValue<int>(WfConsts.DfCascade, 0);
            ctx.EventIds = expando.GetValue<string?>(WfConsts.EventIds, null);

            return ctx;
        }

        public string CorpId { get; private set; } = string.Empty;
        public string AppId { get; private set; } = string.Empty;
        public string FormId { get; private set; } = string.Empty;
        public string DataId { get; private set; } = string.Empty;
        public Operator? WfStarter { get; private set; }
        public CascadeMode DfCascade { get; private set; }
        public string? EventIds { get; private set; }
        public bool MatchedResult { get; set; }
        public bool MatchParallel {  get; set; }

        public ExpandoObject ToExpando()
        {
            var data = new ExpandoObject();
            data.AddOrUpdate(WfConsts.CorpId, CorpId);
            data.AddOrUpdate(WfConsts.AppId, AppId);
            data.AddOrUpdate(WfConsts.FormId, FormId);
            data.AddOrUpdate(WfConsts.DataId, DataId);

            var empdo = new ExpandoObject();
            empdo.AddOrUpdate(WfConsts.CorpId, CorpId);
            if (WfStarter != null)
            {
                empdo.AddOrUpdate(WfConsts.UserId, WfStarter.UserId);
                empdo.AddOrUpdate(WfConsts.EmpId, WfStarter.EmpId);
                empdo.AddOrUpdate(WfConsts.EmpName, WfStarter.EmpName);
            }

            data.AddOrUpdate(WfConsts.WfStarter, empdo);
            data.AddOrUpdate(WfConsts.MatchedResult, MatchedResult);
            data.AddOrUpdate(WfConsts.MatchParallel, MatchParallel);
            data.AddOrUpdate(WfConsts.DfCascade, (int)DfCascade);
            data.AddOrUpdate(WfConsts.EventIds, EventIds);

            return data;
        }
    }
}
