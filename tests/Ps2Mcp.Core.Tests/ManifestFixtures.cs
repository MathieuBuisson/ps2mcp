using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Ps2Mcp.Core.Tests;

/// <summary>
/// Factory methods for building test <see cref="ManifestDefinition"/> instances.
/// </summary>
public static class ManifestFixtures
{
    /// <summary>
    /// Builds a minimal manifest with one tool, one secure parameter, and a schema property.
    /// </summary>
    public static ManifestDefinition MakeDefault() =>
        new(
            Module: new ModuleDefinition("MyModule", "1.0.0"),
            Tools: ImmutableArray.Create(
                new ManifestToolDefinition(
                    ToolName: "GetFoo",
                    SourceCommand: "Get-Foo",
                    Parameters: ImmutableArray.Create(
                        new ManifestParameterDefinition(
                            Name: "Password",
                            Type: "SecureString",
                            IsMandatory: true,
                            IsSecure: true,
                            Aliases: ImmutableArray.Create("Secret"),
                            ParameterSets: ImmutableArray.Create("Default"))),
                    RequiredParameterSet: "Default",
                    Schema: new SchemaDefinition(
                        Type: "object",
                        Properties: ImmutableArray.Create(
                            new SchemaProperty(
                                Name: "Password",
                                Type: "string",
                                Enum: null,
                                Minimum: null,
                                Maximum: null,
                                Pattern: null,
                                Schema: null)),
                        Required: ImmutableArray.Create("Password"),
                        Items: null))),
            IrVersion: 3,
            ContentHash: "sha256:abc123");

    /// <summary>
    /// Returns the expected JSON property names for <typeparamref name="T"/>
    /// in the order defined by <see cref="JsonPropertyOrderAttribute"/>.
    /// </summary>
    public static string[] GetJsonPropertyOrder<T>()
    {
        return typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .OrderBy(p => p.GetCustomAttribute<JsonPropertyOrderAttribute>()?.Order ?? 0)
            .Select(p => p.Name)
            .ToArray();
    }
}
