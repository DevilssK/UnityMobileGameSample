using System;

namespace DevilCorp.MobileFoundation.Server.State;

/// <summary>An expected business refusal. Its message starts with the code so the client can read it.</summary>
public sealed class GameOperationException : Exception
{
    public GameOperationException(string code, string detail)
        : base($"{code}: {detail}")
    {
        Code = code;
    }

    public string Code { get; }
}
