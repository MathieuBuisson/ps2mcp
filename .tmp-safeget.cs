using System;
using System.Management.Automation.Language;

var ast = Parser.ParseInput("@{ RequiredModules = @(@{ ModuleName = 'Az.Accounts' }, 'Pester') }", "sample.psd1", out _, out var errors);
Console.WriteLine(errors.Length);
var value = ast.EndBlock.Statements[0] is PipelineAst pipeline && pipeline.PipelineElements[0] is CommandExpressionAst { Expression: HashtableAst hashtable }
    ? hashtable.SafeGetValue()
    : null;
Console.WriteLine(value?.GetType().FullName ?? "null");
