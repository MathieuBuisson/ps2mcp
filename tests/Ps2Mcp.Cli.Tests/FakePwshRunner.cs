using System;
using System.Collections.Generic;

namespace Ps2Mcp.Cli.Tests;

internal sealed class FakePwshRunner : IPwshRunner
{
    private Func<PwshInvocation, PwshInvocationResult>? onInvoke;

    public List<PwshInvocation> Invocations { get; } = new();

    public Func<PwshInvocation, PwshInvocationResult> OnInvoke
    {
        get => onInvoke ?? throw new InvalidOperationException("FakePwshRunner: OnInvoke was not configured.");
        set => onInvoke = value;
    }

    public PwshInvocationResult Invoke(PwshInvocation invocation)
    {
        Invocations.Add(invocation);
        return OnInvoke(invocation);
    }
}
