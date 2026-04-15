namespace ETLWorker.Abstractions;

public interface IDimensionLoader
{
    string TableName { get; }
    Task<int> LoadAsync(CancellationToken ct = default);
}
