using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ps2Mcp.Core;
using Ps2Mcp.Emitters.TypeScript;
using Ps2Mcp.Tests.Shared;

namespace Ps2Mcp.Emitters.TypeScript.Tests;

public sealed class TypeScriptEmitterTests
{
    [Fact]
    public void RepresentativeFixture_TagsProperty_IsArrayOfStrings()
    {
        var server = RepresentativeServerFixture.Create();

        var tags = server.Tools[0].Schema.Properties.Single(property => property.Name == "Tags");

        Assert.Equal("array", tags.Type);
        Assert.NotNull(tags.Schema);
        Assert.Equal("string", tags.Schema!.Type);
        Assert.Null(tags.Schema.Items);
    }

    [Fact]
    public async Task EmitAsync_RepresentativeFixture_ReturnsEmptyResult()
    {
        var emitter = new TypeScriptEmitter();
        var server = RepresentativeServerFixture.Create();
        var options = new EmitOptions("./modules/Demo.Module/Demo.Module.psd1");

        var result = await emitter.EmitAsync(server, options);

        Assert.NotNull(result);
        Assert.Empty(result.Files);
    }

    [Fact]
    public async Task EmitAsync_NullServer_ThrowsArgumentNullException()
    {
        var emitter = new TypeScriptEmitter();
        var options = new EmitOptions("./modules/Demo.Module/Demo.Module.psd1");

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => emitter.EmitAsync(null!, options));

        Assert.Equal("server", ex.ParamName);
    }

    [Fact]
    public async Task EmitAsync_NullOptions_ThrowsArgumentNullException()
    {
        var emitter = new TypeScriptEmitter();
        var server = RepresentativeServerFixture.Create();

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => emitter.EmitAsync(server, null!));

        Assert.Equal("options", ex.ParamName);
    }

    [Fact]
    public async Task EmitAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        var emitter = new TypeScriptEmitter();
        var server = RepresentativeServerFixture.Create();
        var options = new EmitOptions("./modules/Demo.Module/Demo.Module.psd1");
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => emitter.EmitAsync(server, options, cancellationTokenSource.Token));
    }
}
