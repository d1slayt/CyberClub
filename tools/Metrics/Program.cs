using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.Json;

var root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
var source = File.ReadAllText(Path.Combine(root, "CyberClub.Core", "Models.cs"));
var method = CSharpSyntaxTree.ParseText(source).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.Text == "Calculate");
var tokens = method.Body!.DescendantTokens().ToArray();
bool IsProcedure(SyntaxToken t)
{
    if (t.Parent is not IdentifierNameSyntax id)
        return false;
    return id.Parent is InvocationExpressionSyntax || id.Parent is ObjectCreationExpressionSyntax || id.Parent is MemberAccessExpressionSyntax member && member.Name == id && member.Parent is InvocationExpressionSyntax;
}

bool IsOperand(SyntaxToken t) => !IsProcedure(t) && (t.IsKind(SyntaxKind.IdentifierToken) || t.IsKind(SyntaxKind.NumericLiteralToken) || t.IsKind(SyntaxKind.StringLiteralToken));
var operators = tokens.Where(t => !IsOperand(t)).GroupBy(t => t.Text).ToDictionary(g => g.Key, g => g.Count());
var operands = tokens.Where(IsOperand).GroupBy(t => t.Text).ToDictionary(g => g.Key, g => g.Count());
int n1 = operators.Count, n2 = operands.Count, N1 = operators.Values.Sum(), N2 = operands.Values.Sum();
var volume = (N1 + N2) * Math.Log2(n1 + n2);
var difficulty = n1 / 2.0 * N2 / n2;
var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories).Where(f => (f.EndsWith(".cs") || f.EndsWith(".xaml")) && !f.Split(Path.DirectorySeparatorChar).Any(p => p is "bin" or "obj" or ".git")).Order().Select(f => new { file = Path.GetRelativePath(root, f), physical = File.ReadAllLines(f).Length, nonblank = File.ReadAllLines(f).Count(l => !string.IsNullOrWhiteSpace(l)) }).ToArray();
var cc = 1 + method.Body.DescendantNodes().Count(n => n is IfStatementSyntax);
var result = new
{
    scope = "Billing.Calculate body only",
    convention = "Roslyn tokens including each separator separately; called method names and constructors are operators; other identifiers and numeric/string literals are operands; keywords and punctuation are operators; comments and signature excluded",
    uniqueOperators = n1,
    uniqueOperands = n2,
    totalOperators = N1,
    totalOperands = N2,
    vocabulary = n1 + n2,
    length = N1 + N2,
    volume,
    difficulty,
    effort = volume * difficulty,
    cyclomatic = cc,
    operators,
    operands,
    files,
    physicalLines = files.Sum(f => f.physical),
    nonblankLines = files.Sum(f => f.nonblank)
};
var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
Console.WriteLine(json);
if (args.Length > 1)
    File.WriteAllText(args[1], json);
