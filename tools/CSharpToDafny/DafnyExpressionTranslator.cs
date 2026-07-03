using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Wallymathieu.Auctions.Tools.CSharpToDafny;

/// <summary>
///     Translates the pure boolean/arithmetic expression subset used in <c>Contract</c> clauses to Dafny
///     syntax. Anything outside the subset raises <see cref="TranslationException" /> so the clause can be
///     surfaced as a TODO instead of silently producing a wrong specification.
/// </summary>
internal sealed class DafnyExpressionTranslator(SemanticModel model, VerifiedMethodExtractor extractor)
{
    private readonly Dictionary<string, string> _renames = new(StringComparer.Ordinal);

    public void Rename(string from, string to)
    {
        _renames[from] = to;
    }

    public string Translate(ExpressionSyntax expression)
    {
        switch (expression)
        {
            case ParenthesizedExpressionSyntax parenthesized:
                return $"({Translate(parenthesized.Expression)})";

            case BinaryExpressionSyntax binary:
                return $"{Translate(binary.Left)} {TranslateOperator(binary)} {Translate(binary.Right)}";

            case PrefixUnaryExpressionSyntax unary when unary.IsKind(SyntaxKind.LogicalNotExpression):
                return $"!{Translate(unary.Operand)}";

            case PrefixUnaryExpressionSyntax unary when unary.IsKind(SyntaxKind.UnaryMinusExpression):
                return $"-{Translate(unary.Operand)}";

            case IdentifierNameSyntax identifier:
                return _renames.TryGetValue(identifier.Identifier.Text, out var renamed)
                    ? renamed
                    : identifier.Identifier.Text;

            case MemberAccessExpressionSyntax member:
                return TranslateMemberAccess(member);

            case LiteralExpressionSyntax literal:
                return TranslateLiteral(literal);

            default:
                throw new TranslationException($"unsupported expression '{expression}' ({expression.Kind()})");
        }
    }

    private static string TranslateOperator(BinaryExpressionSyntax binary)
    {
        return binary.Kind() switch
        {
            SyntaxKind.EqualsExpression => "==",
            SyntaxKind.NotEqualsExpression => "!=",
            SyntaxKind.LessThanExpression => "<",
            SyntaxKind.LessThanOrEqualExpression => "<=",
            SyntaxKind.GreaterThanExpression => ">",
            SyntaxKind.GreaterThanOrEqualExpression => ">=",
            SyntaxKind.LogicalAndExpression => "&&",
            SyntaxKind.LogicalOrExpression => "||",
            SyntaxKind.AddExpression => "+",
            SyntaxKind.SubtractExpression => "-",
            SyntaxKind.MultiplyExpression => "*",
            SyntaxKind.BitwiseOrExpression => "|",
            SyntaxKind.BitwiseAndExpression => "&",
            _ => throw new TranslationException($"unsupported operator '{binary.OperatorToken.Text}'")
        };
    }

    private string TranslateMemberAccess(MemberAccessExpressionSyntax member)
    {
        switch (model.GetSymbolInfo(member).Symbol)
        {
            // Enum members such as Errors.None become prelude constants such as Errors_None.
            case IFieldSymbol { ContainingType.TypeKind: TypeKind.Enum } field:
                extractor.MapType(field.ContainingType);
                return $"{field.ContainingType.Name}_{field.Name}";

            // Numeric constants such as long.MaxValue become integer literals: Dafny ints are unbounded,
            // so bounds used in overflow preconditions translate to their concrete values.
            case IFieldSymbol { HasConstantValue: true, ConstantValue: long or int or short or sbyte or ulong or uint or ushort or byte } constant:
                return Convert.ToString(constant.ConstantValue, System.Globalization.CultureInfo.InvariantCulture)!;

            default:
                throw new TranslationException($"unsupported member access '{member}'");
        }
    }

    private static string TranslateLiteral(LiteralExpressionSyntax literal)
    {
        return literal.Kind() switch
        {
            SyntaxKind.TrueLiteralExpression => "true",
            SyntaxKind.FalseLiteralExpression => "false",
            SyntaxKind.NumericLiteralExpression => literal.Token.ValueText,
            SyntaxKind.StringLiteralExpression => literal.Token.Text,
            _ => throw new TranslationException($"unsupported literal '{literal}'")
        };
    }
}
