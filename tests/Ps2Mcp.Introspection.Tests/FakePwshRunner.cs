using System;
using System.Collections.Generic;

namespace Ps2Mcp.Introspection.Tests;

// Test double for IPwshRunner. Records each invocation and returns a canned
// PwshInvocationResult. This version stays local to the introspection test
// project to avoid cross-project test dependencies.
internal sealed class FakePwshRunner : IPwshRunner
{
    public Func<PwshInvocation, PwshInvocationResult> OnInvoke { get; set; } =
        _ => new PwshInvocationResult(0, string.Empty, string.Empty);

    public List<PwshInvocation> StartCalls { get; } = new();

    public PwshInvocationResult Invoke(PwshInvocation invocation)
    {
        StartCalls.Add(invocation);
        return OnInvoke(invocation);
    }
}
