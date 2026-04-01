namespace SyncFolderPair.Core.Types;

public abstract record Pair<T>
{
    public sealed record Both(T LValue, T RValue) : Pair<T>();
    public sealed record Left(T Value) : Pair<T>();
    public sealed record Right(T Value) : Pair<T>();
}
