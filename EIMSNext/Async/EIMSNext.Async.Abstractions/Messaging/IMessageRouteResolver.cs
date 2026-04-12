namespace EIMSNext.Async.Abstractions.Messaging
{
    public interface IMessageRouteResolver
    {
        string ResolveQueueName(Type messageType);
    }
}
