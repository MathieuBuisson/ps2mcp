using System;
using System.Diagnostics;
using System.IO;

namespace Ps2Mcp.Cli;

// Thin abstraction over System.Diagnostics.Process so PwshRunner can be unit-tested
// without spawning real OS processes. Production binds SystemProcessRunner; tests bind
// a fake that returns canned stdout/stderr/exit codes and can simulate timeouts.
internal interface IProcessRunner
{
    IProcessHandle Start(ProcessStartInfo startInfo);
}

internal interface IProcessHandle : IDisposable
{
    int ExitCode { get; }
    TextReader StandardOutput { get; }
    TextReader StandardError { get; }

    // Returns true if the process exited within the timeout, false otherwise.
    // The caller is expected to Kill() the process on a false return.
    bool WaitForExit(TimeSpan timeout);

    void Kill();
}
