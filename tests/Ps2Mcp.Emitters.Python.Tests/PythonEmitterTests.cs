using System;
using System.Threading;
using System.Threading.Tasks;
using Ps2Mcp.Core;
using Ps2Mcp.Emitters.Python;
using Ps2Mcp.Tests.Shared;

namespace Ps2Mcp.Emitters.Python.Tests;

public sealed class PythonEmitterTests
{
    [Fact]
    public async Task EmitAsync_RepresentativeFixture_ReturnsEmptyResult()
    {
        var emitter = new PythonEmitter();
        var server = RepresentativeServerFixture.Create();
        var options = new EmitOptions("./modules/Demo.Module/Demo.Module.psd1");

        var result = await emitter.EmitAsync(server, options);

        Assert.NotNull(result);
        Assert.Empty(result.Files);
    }

    [Fact]
    public async Task EmitAsync_NullServer_ThrowsArgumentNullException()
    {
        var emitter = new PythonEmitter();
        var options = new EmitOptions("./modules/Demo.Module/Demo.Module.psd1");

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => emitter.EmitAsync(null!, options));

        Assert.Equal("server", ex.ParamName);
    }

    [Fact]
    public async Task EmitAsync_NullOptions_ThrowsArgumentNullException()
    {
        var emitter = new PythonEmitter();
        var server = RepresentativeServerFixture.Create();

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => emitter.EmitAsync(server, null!));

        Assert.Equal("options", ex.ParamName);
    }

    [Fact]
    public async Task EmitAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        var emitter = new PythonEmitter();
        var server = RepresentativeServerFixture.Create();
        var options = new EmitOptions("./modules/Demo.Module/Demo.Module.psd1");
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => emitter.EmitAsync(server, options, cancellationTokenSource.Token));
    }
}
