using HKH.Mef2.Integration;

namespace EIMSNext.Async.Core.MQ.Consumers
{
    [Queue("email")] // 👉 关键：决定监听 biz_email_tasks
    public class EmailConsumer : TaskConsumerBase<EmailConsumer>
    {
       
        // 依赖注入业务服务（由 Host 项目注册）
        public EmailConsumer(IResolver resolver)
            : base(resolver)
        { }

        protected override async Task HandleTaskAsync(string taskType, string argsJson, CancellationToken ct)
        {
            // 简化：实际项目建议预注册处理器字典避免反射
            //var args = JsonSerializer.Deserialize<EmailArgs>(argsJson);
            //await _emailService.SendAsync(args.To, args.Subject, args.Body, ct);
        }
    }
}
