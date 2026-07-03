using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Wallymathieu.Auctions.Tools.CSharpToDafny;

/// <summary>
///     Walks a Roslyn compilation, discovers methods marked <c>[Verify]</c> and extracts their signatures and
///     <c>Contract.Requires</c>/<c>Contract.Ensures</c> clauses, translated to Dafny.
/// </summary>
internal sealed class VerifiedMethodExtractor(Compilation compilation, TypeRegistry registry, TextWriter log)
{
    public List<VerifiedMethod> Extract()
    {
        var result = new List<VerifiedMethod>();
        foreach (var tree in compilation.SyntaxTrees.OrderBy(t => t.FilePath, StringComparer.Ordinal))
        {
            var model = compilation.GetSemanticModel(tree);
            var methods = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
                .Where(HasVerifyAttribute);
            foreach (var method in methods)
            {
                var extracted = ExtractMethod(method, model);
                if (extracted != null) result.Add(extracted);
            }
        }

        return result
            .OrderBy(m => m.ContainingType, StringComparer.Ordinal)
            .ThenBy(m => m.Name, StringComparer.Ordinal)
            .ThenBy(m => m.SourceLine)
            .ToList();
    }

    private static bool HasVerifyAttribute(MethodDeclarationSyntax method)
    {
        return method.AttributeLists
            .SelectMany(list => list.Attributes)
            .Select(attribute => attribute.Name.ToString())
            .Select(name => name.Contains('.', StringComparison.Ordinal)
                ? name[(name.LastIndexOf('.') + 1)..]
                : name)
            .Any(name => name is "Verify" or "VerifyAttribute");
    }

    private VerifiedMethod? ExtractMethod(MethodDeclarationSyntax method, SemanticModel model)
    {
        var symbol = model.GetDeclaredSymbol(method);
        var location = method.GetLocation().GetLineSpan();
        var sourcePath = Path.GetRelativePath(Environment.CurrentDirectory, location.Path)
            .Replace(Path.DirectorySeparatorChar, '/');
        var line = location.StartLinePosition.Line + 1;
        var display = $"{symbol?.ContainingType.ToDisplayString()}.{method.Identifier.Text}";

        if (symbol == null)
        {
            log.WriteLine($"warning: {sourcePath}:{line}: could not resolve symbol for {method.Identifier.Text}; skipped.");
            return null;
        }

        if (!symbol.IsStatic)
        {
            log.WriteLine($"warning: {sourcePath}:{line}: {display} is not static; only static (pure) methods are supported; skipped.");
            return null;
        }

        var parameters = new List<DafnyParameter>();
        foreach (var parameter in symbol.Parameters)
        {
            var dafnyType = MapType(parameter.Type);
            if (dafnyType == null)
            {
                log.WriteLine($"warning: {sourcePath}:{line}: {display}: unsupported parameter type '{parameter.Type.ToDisplayString()}'; skipped.");
                return null;
            }

            parameters.Add(new DafnyParameter(parameter.Name, dafnyType));
        }

        var returnType = MapType(symbol.ReturnType);
        if (returnType == null)
        {
            log.WriteLine($"warning: {sourcePath}:{line}: {display}: unsupported return type '{symbol.ReturnType.ToDisplayString()}'; skipped.");
            return null;
        }

        var (requires, ensures, resultName, untranslated) = ExtractContracts(method, model, parameters);

        var body = method.Body?.ToString() ?? (method.ExpressionBody != null ? $"=> {method.ExpressionBody.Expression};" : "");

        return new VerifiedMethod(
            symbol.ContainingNamespace.ToDisplayString(),
            symbol.ContainingType.Name,
            method.Identifier.Text,
            sourcePath,
            line,
            parameters,
            returnType,
            resultName,
            requires,
            ensures,
            untranslated,
            body);
    }

    private (List<string> Requires, List<string> Ensures, string ResultName, List<string> Untranslated)
        ExtractContracts(MethodDeclarationSyntax method, SemanticModel model, List<DafnyParameter> parameters)
    {
        var requires = new List<string>();
        var ensures = new List<string>();
        var untranslated = new List<string>();
        var resultName = parameters.Any(p => string.Equals(p.Name, "result", StringComparison.Ordinal)) ? "result'" : "result";

        if (method.Body == null) return (requires, ensures, resultName, untranslated);

        foreach (var statement in method.Body.Statements.OfType<ExpressionStatementSyntax>())
        {
            if (statement.Expression is not InvocationExpressionSyntax invocation) continue;
            var name = invocation.Expression switch
            {
                MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.Text: "Contract" } } member =>
                    member.Name.Identifier.Text,
                IdentifierNameSyntax identifier => identifier.Identifier.Text,
                GenericNameSyntax generic => generic.Identifier.Text,
                _ => null
            };
            if (name is not ("Requires" or "Ensures")) continue;
            if (invocation.ArgumentList.Arguments.Count != 1) continue;
            var argument = invocation.ArgumentList.Arguments[0].Expression;

            try
            {
                var translator = new DafnyExpressionTranslator(model, this);
                if (string.Equals(name, "Requires", StringComparison.Ordinal))
                {
                    requires.Add(translator.Translate(argument));
                }
                else
                {
                    var (lambdaParameter, condition) = argument switch
                    {
                        SimpleLambdaExpressionSyntax { ExpressionBody: { } expressionBody } lambda =>
                            (lambda.Parameter.Identifier.Text, expressionBody),
                        ParenthesizedLambdaExpressionSyntax { ExpressionBody: { } expressionBody, ParameterList.Parameters: [var single] } =>
                            (single.Identifier.Text, expressionBody),
                        _ => throw new TranslationException("Ensures argument must be a single-parameter expression lambda")
                    };
                    translator.Rename(lambdaParameter, resultName);
                    ensures.Add(translator.Translate(condition));
                }
            }
            catch (TranslationException e)
            {
                untranslated.Add($"{name}({argument}) — {e.Message}");
                log.WriteLine($"warning: could not translate {name} clause '{argument}': {e.Message}");
            }
        }

        return (requires, ensures, resultName, untranslated);
    }

    /// <summary>Maps a C# type to its Dafny counterpart, registering shared types; null when unsupported.</summary>
    internal string? MapType(ITypeSymbol type)
    {
        switch (type.SpecialType)
        {
            case SpecialType.System_Int32:
            case SpecialType.System_Int64:
                return "int";
            case SpecialType.System_Boolean:
                return "bool";
            case SpecialType.System_String:
                return "string";
        }

        if (type is { Name: "DateTimeOffset", ContainingNamespace.Name: "System" })
        {
            registry.UsesTime = true;
            return "Time";
        }

        if (type.TypeKind == TypeKind.Enum)
        {
            RegisterEnum(type);
            return type.Name;
        }

        // Domain records and classes without type parameters become opaque Dafny types with equality.
        if (type is INamedTypeSymbol { TypeParameters.Length: 0, TypeKind: TypeKind.Class or TypeKind.Struct } named
            && named.ContainingNamespace.ToDisplayString().StartsWith("Wallymathieu.", StringComparison.Ordinal))
        {
            registry.OpaqueTypes.TryAdd(named.Name, named.ToDisplayString());
            return named.Name;
        }

        return null;
    }

    private void RegisterEnum(ITypeSymbol type)
    {
        if (registry.Enums.ContainsKey(type.Name)) return;
        var isFlags = type.GetAttributes().Any(a => string.Equals(a.AttributeClass?.Name, "FlagsAttribute", StringComparison.Ordinal));
        var members = type.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(f => f.HasConstantValue)
            .Select(f => (f.Name, Convert.ToInt64(f.ConstantValue, System.Globalization.CultureInfo.InvariantCulture)))
            .OrderBy(m => m.Item2)
            .ThenBy(m => m.Name, StringComparer.Ordinal)
            .ToList();
        registry.Enums.Add(type.Name, new EnumModel(type.Name, type.ToDisplayString(), isFlags, members));
    }
}
