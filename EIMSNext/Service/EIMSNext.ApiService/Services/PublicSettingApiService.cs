using HKH.Mef2.Integration;
using EIMSNext.Common;
using EIMSNext.Service.Entities;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Component;
using EIMSNext.Service.Contracts;
using MongoDB.Driver;

namespace EIMSNext.ApiService
{
	public class PublicSettingApiService(IResolver resolver) : ApiServiceBase<PublicSetting, PublicSettingViewModel, IPublicSettingService>(resolver)
	{
        protected override Task AddAsyncCore(PublicSetting entity)
        {
            Normalize(entity);
            Resolver.Resolve<AdminPermissionEvaluator>().EnsureCanManageApp(entity.AppId);
            EnsurePublicFormFields(entity);
            return base.AddAsyncCore(entity);
        }

        protected override Task<ReplaceOneResult> ReplaceAsyncCore(PublicSetting entity)
        {
            Normalize(entity);
            Resolver.Resolve<AdminPermissionEvaluator>().EnsureCanManageApp(entity.AppId);
            EnsurePublicFormFields(entity);
            return base.ReplaceAsyncCore(entity);
        }

        protected override async Task<object> DeleteAsyncCore(IEnumerable<string> ids)
        {
            var idList = ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            var settings = CoreService.All()
                .Where(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag && idList.Contains(x.Id))
                .ToList();

            if (settings.Count != idList.Count)
            {
                throw new BadRequestException("公开设置不存在");
            }

            var evaluator = Resolver.Resolve<AdminPermissionEvaluator>();
            foreach (var setting in settings)
            {
                evaluator.EnsureCanManageApp(setting.AppId);
            }

            return await base.DeleteAsyncCore(idList);
        }

        private void EnsurePublicFormFields(PublicSetting setting)
        {
            if (setting.TargetType != PublicTargetType.Form || string.IsNullOrWhiteSpace(setting.TargetId))
            {
                return;
            }

            var formService = Resolver.Resolve<IFormDefService>();
            var form = formService.Get(setting.TargetId);
            if (form == null || form.DeleteFlag)
            {
                throw new BadRequestException("表单不存在");
            }

            if (form.AppId != setting.AppId)
            {
                throw new BadRequestException("公开设置与表单不属于同一应用");
            }

            PublicFormSystemFieldHelper.EnsureRequiredFields(form, setting);
            form.Content.Items = Resolver.Resolve<FormLayoutParser>().Parse(form.Content.Layout);
            formService.Replace(form);
        }

        private static void Normalize(PublicSetting setting)
        {
            setting.Form ??= new PublicFormSetting();
            setting.Form.FormLink ??= new PublicFormLinkSetting();
            setting.Form.DataLink ??= new PublicDataLinkSetting();
            setting.Form.QueryLink ??= new PublicQueryLinkSetting();
            setting.Form.FormLink.Wechat ??= new PublicWechatSetting();
            setting.Form.FormLink.ExtLink ??= new PublicExtLinkSetting();
            setting.Dashboard ??= new PublicDashboardSetting();
        }
	}
}
