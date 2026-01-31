namespace EIMSNext.Core.Entity
{
    public interface IMongoEntity
    {
        string Id { get; set; }
    }
    public interface ICorpOwned
    {
        string? CorpId {  get; set; }
    }
    public interface IDeleteFlag
    {
        bool DeleteFlag { get; set; }
    }
    public interface IEntity : IMongoEntity, IDeleteFlag
    {
        Operator? CreateBy { get; set; }
        long CreateTime { get; set; }
        Operator? UpdateBy { get; set; }
        long? UpdateTime { get; set; }
    }
}
