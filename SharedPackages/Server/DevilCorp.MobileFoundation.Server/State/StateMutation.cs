using System.Collections.Generic;

namespace DevilCorp.MobileFoundation.Server.State;

/// <summary>What a decision wants written, and what the operation returns once written.</summary>
public sealed class StateMutation<TResult>
{
    internal StateMutation(TResult result, IReadOnlyDictionary<string, object> writes)
    {
        Result = result;
        Writes = writes;
    }

    public TResult Result { get; }
    public IReadOnlyDictionary<string, object> Writes { get; }
}

public static class StateMutation
{
    public static StateMutation<TResult> Read<TResult>(TResult result) =>
        new(result, new Dictionary<string, object>());

    public static StateMutation<TResult> Write<TResult>(TResult result, params (string Key, object Value)[] writes)
    {
        var map = new Dictionary<string, object>();
        foreach (var (key, value) in writes)
        {
            map[key] = value;
        }

        return new StateMutation<TResult>(result, map);
    }

    public static StateMutation<TResult> Write<TResult>(TResult result, IReadOnlyDictionary<string, object> writes) =>
        new(result, writes);
}
