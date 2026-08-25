using HKH.Mef2.Integration;
using System.Net;
using System.Text;
using EIMSNext.Auth.Entities;
using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Services;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Service.Entities;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Contracts;
using Microsoft.Extensions.Configuration;

namespace EIMSNext.ApiService
{
	public class CorporateApiService(IResolver resolver) : ApiServiceBase<Corporate, CorporateViewModel, ICorporateService>(resolver)
	{
		public override async Task AddAsync(Corporate entity)
		{
			await base.AddAsync(entity);

			var recipients = GetServiceContractRecipients();
			if (recipients.Count == 0)
			{
				return;
			}

			var owner = ServiceContext.User;
			var ownerRegistrationTime = string.IsNullOrWhiteSpace(owner?.Id)
				? string.Empty
				: FormatRegistrationTime(Resolver.GetService<User>().Get(owner.Id)?.CreateTime);
			await Resolver.Resolve<IOutboxPublisher>().EnqueueAsync(new EmailNotifyTaskArgs
			{
				TaskType = EmailTaskType.PlatWork,
				CorpId = entity.Id,
				NotifyId = entity.Id,
				Title = $"新企业创建通知 - {entity.Name}",
				Detail = BuildCorporateCreatedEmail(entity, owner, ownerRegistrationTime),
				Receivers = recipients.Select(email => new NotifyReceiver
				{
					Email = email,
					EmpName = "ServiceContracts"
				}).ToList(),
				EventStamp = entity.CreateTime
			});
		}

		private List<string> GetServiceContractRecipients()
		{
			var value = Resolver.Resolve<IConfiguration>()["ServiceContracts"];
			return string.IsNullOrWhiteSpace(value)
				? []
				: value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.ToList();
		}

		private static string BuildCorporateCreatedEmail(
			Corporate corporate,
			EIMSNext.Core.Abstractions.IUser? owner,
			string ownerRegistrationTime)
		{
			var html = new StringBuilder();
			html.Append("<h2>新企业创建通知</h2><h3>企业信息</h3><table border=\"1\" cellpadding=\"6\" cellspacing=\"0\">");
			AppendRow(html, "企业 ID", corporate.Id);
			AppendRow(html, "企业名称", corporate.Name);
			AppendRow(html, "企业编码", corporate.Code);
			AppendRow(html, "企业简介", corporate.Description);
			AppendRow(html, "注册来源", corporate.Platform.ToString());
			html.Append("</table><h3>企业 Owner 用户信息</h3><table border=\"1\" cellpadding=\"6\" cellspacing=\"0\">");
			AppendRow(html, "用户 ID", owner?.Id);
			AppendRow(html, "用户名称", owner?.Name);
			AppendRow(html, "邮箱", owner?.Email);
			AppendRow(html, "电话", owner?.Phone);
			AppendRow(html, "注册时间", ownerRegistrationTime);
			html.Append("</table>");
			return html.ToString();
		}

		private static string FormatRegistrationTime(long? createTime)
		{
			return createTime is > 0
				? createTime.Value.ToDateTimeMs().ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
				: string.Empty;
		}

		private static void AppendRow(StringBuilder html, string label, string? value)
		{
			html.Append("<tr><th>")
				.Append(WebUtility.HtmlEncode(label))
				.Append("</th><td>")
				.Append(WebUtility.HtmlEncode(value ?? string.Empty))
				.Append("</td></tr>");
		}
	}
}
