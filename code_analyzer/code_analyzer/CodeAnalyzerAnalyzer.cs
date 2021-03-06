using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace code_analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public partial class CodeAnalyzerAnalyzer : DiagnosticAnalyzer
    {
        private const string Category = "Severes";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
            EncapsulateFieldRule,
            ContextualKeyWordRule,
            LiskovSubsitutionPrincipleRule,
            ToArrayToListInsideForeachDeclaration,
            SwitchWithoutDefaultCaseRule,
            AggregateExceptionRule,
            ExceptionWithoutContextRule,
            EnumDefaultValueRule,
            MethodWithMoreThanFourParamtersRule,
            MethodWithBoolAsParameterRule,
            PreferClassOverStructRule,
            BlankCodeRule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeFieldToBeEncapsulate, SyntaxKind.FieldDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeContextualKeyWord, SyntaxKind.IdentifierName);
            context.RegisterSyntaxNodeAction(AnalyzeClassesAreNounRule, SyntaxKind.ClassDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeBlankBlockCode, SyntaxKind.Block);
            context.RegisterSyntaxNodeAction(
                AnalyzeLiskovSubstitutionPrincipal,
                SyntaxKind.Parameter,
                SyntaxKind.GenericName,
                SyntaxKind.PredefinedType);

            context.RegisterSyntaxNodeAction(AnalyzeToArrayToListInForeachDeclaration, SyntaxKind.ForEachStatement);
            context.RegisterSyntaxNodeAction(AnalyzeSwitchWithoutDefaultLabel, SyntaxKind.SwitchStatement);
            context.RegisterSyntaxNodeAction(AnalyzeGenericExceptionCode, SyntaxKind.IdentifierName);
            context.RegisterSyntaxNodeAction(AnalyzeExceptionWithoutContextCode, SyntaxKind.ObjectCreationExpression);
            context.RegisterSyntaxNodeAction(AnalyzeEnumWithoutDefaultCode, SyntaxKind.EnumDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeMethodWithMoreThanFourParameters, SyntaxKind.ParameterList);
            context.RegisterSyntaxNodeAction(AnalyzeMethodWithBoolAsParameter, SyntaxKind.ParameterList);
            context.RegisterSyntaxNodeAction(AnalyzeStructCode, SyntaxKind.StructDeclaration);
        }

        private static void AnalyzeStructCode(SyntaxNodeAnalysisContext context)
        {
            var root = context.Node as StructDeclarationSyntax;

            if (root == null)
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(PreferClassOverStructRule, root.GetLocation(), "This code defines a `struct` that should be a `class`"));
        }

        private static void AnalyzeMethodWithBoolAsParameter(SyntaxNodeAnalysisContext context)
        {
            var root = context.Node as ParameterListSyntax;

            if (root == null ||
                root.Parent is ParenthesizedLambdaExpressionSyntax ||
                !root.Parameters.Any() ||
                root.Parameters.All(x =>
                {
                    var type = x.Type.ToString();
                    return type != "bool" && type != "Boolean";
                }))
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                MethodWithBoolAsParameterRule,
                root.GetLocation(),
                "This method receives a bool argument. This is prone to be against SRP from SOLID"));
        }

        private static void AnalyzeMethodWithMoreThanFourParameters(SyntaxNodeAnalysisContext context)
        {
            var root = context.Node as ParameterListSyntax;

            if (root == null ||
                root.Parent is ParenthesizedLambdaExpressionSyntax ||
                root.Parameters.Count < 5)
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(MethodWithMoreThanFourParamtersRule, root.GetLocation(), "This method receives too many parameters (>= 5)"));
        }

        private static void AnalyzeEnumWithoutDefaultCode(SyntaxNodeAnalysisContext context)
        {
            var root = context.Node as EnumDeclarationSyntax;

            if (root == null ||
                root.Members.Any(x => x.EqualsValue.Value.ToString() == "0"))
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(EnumDefaultValueRule, root.GetLocation(), "This enumeration does not contain a value for 0 (zero)."));
        }

        private static void AnalyzeExceptionWithoutContextCode(SyntaxNodeAnalysisContext context)
        {
            var root = context.Node as ObjectCreationExpressionSyntax;

            var exceptionWithoutContext = root?.Parent is ThrowStatementSyntax &&
                                          !root.ArgumentList.Arguments.Any();

            if (root == null || !exceptionWithoutContext)
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                ExceptionWithoutContextRule,
                root.GetLocation(),
                "This exception message does not provide context description."));
        }

        private static void AnalyzeGenericExceptionCode(SyntaxNodeAnalysisContext context)
        {
            var root = context.Node as IdentifierNameSyntax;
            var genericExceptions = new[]
            {
                "Exception",
                "ApplicationException",
                "SystemException",
                "ExecutionEngineException",
                "IndexOutOfRangeException",
                "NullReferenceException",
                "OutOfMemoryException"
            };

            if (root == null ||
                genericExceptions.All(x=> root.Identifier.ValueText != x) ||
                !(root.Parent is ObjectCreationExpressionSyntax &&
                  root.Parent.Parent is ThrowStatementSyntax))
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                AggregateExceptionRule,
                root.GetLocation(),
                "A method raises an exception type that is too general or that is reserved by the runtime. Use specific or Aggregation Exception"));
        }

        private static void AnalyzeSwitchWithoutDefaultLabel(SyntaxNodeAnalysisContext context)
        {
            var root = context.Node as SwitchStatementSyntax;

            if (root == null ||
                root.DescendantNodes()
                    .OfType<DefaultSwitchLabelSyntax>().Any())
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(SwitchWithoutDefaultCaseRule, root.GetLocation(), "Missing default case for switch"));
        }

        private static void AnalyzeToArrayToListInForeachDeclaration(SyntaxNodeAnalysisContext context)
        {
            var root = context.Node as ForEachStatementSyntax;

            if (root == null)
            {
                return;
            }

            var enumerable = root.Expression.ToString();

            if (!(enumerable.Contains(".ToArray()") ||
                  enumerable.Contains(".ToList()")))
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                ToArrayToListInsideForeachDeclaration,
                root.Expression.GetLocation(),
                "ToArray/ToList inside foreach declaration. Remove the `ToArray()` or `ToList()` call and use the `IEnumerable` instance directly"));
        }

        private static void AnalyzeLiskovSubstitutionPrincipal(SyntaxNodeAnalysisContext context)
        {
            var predefinedType = context.Node as PredefinedTypeSyntax;
            var parameterType = context.Node as ParameterSyntax;
            var genericType = context.Node as GenericNameSyntax;

            var collectionTypes = new[]
            {
                "Enumerable",
                "ReadOnlyCollection",
                "Collection",
                "ReadOnlyList",
                "Dictionary",
                "List"
            };

            if (predefinedType == null &&
                genericType == null &&
                parameterType == null)
            {
                return;
            }

            if (genericType?.Parent is ObjectCreationExpressionSyntax ||
                genericType?.Parent is TypeOfExpressionSyntax ||
                predefinedType?.Parent is TypeOfExpressionSyntax ||
                parameterType?.Parent is TypeOfExpressionSyntax)
            {
                return;
            }

            var type = predefinedType?.ToString() ??
                       parameterType?.Type.ToString() ??
                       genericType?.Identifier.ValueText ??
                       string.Empty;

            if (type.Contains("."))
            {
                type = type
                    .Split('.')
                    .Last();
            }

            if (!collectionTypes.Any(x => type.StartsWith(x)))
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                LiskovSubsitutionPrincipleRule,
                context.Node.GetLocation(),
                "This member publicly exposes a concrete collection type"));
        }

        private static void AnalyzeContextualKeyWord(SyntaxNodeAnalysisContext context)
        {
            var root = context.Node as IdentifierNameSyntax;
            var valueKeyWord = "value";
            var contextualKeywords = new[]
            {
                "add",
                "alias",
                "ascending",
                "async",
                "await",
                "by",
                "descending",
                "dynamic",
                "equals",
                "from",
                "get",
                "global",
                "group",
                "into",
                "join",
                "let",
                "nameof",
                "on",
                "orderby",
                "partial",
                "remove",
                "select",
                "set",
                valueKeyWord,
                "var",
                "when",
                "where",
                "yield"
            };

            if (root == null ||
                root.Parent is InvocationExpressionSyntax ||
                root.Parent is VariableDeclarationSyntax ||
                root.Parent is ForEachStatementSyntax ||
                root.Parent is ForStatementSyntax ||
                root.Parent is WhileStatementSyntax ||
                root.Parent is SwitchStatementSyntax ||
                root.Parent is DeclarationExpressionSyntax ||
                root.Parent is TypeOfExpressionSyntax ||
                root.Parent is MethodDeclarationSyntax ||
                root.Parent is ParameterSyntax ||
                root.Parent.Parent is GenericNameSyntax ||
                contextualKeywords.All(x => x != root.Identifier.ValueText))
            {
                return;
            }

            if (root.Ancestors().Any(x => x.IsKind(SyntaxKind.SetAccessorDeclaration)) &&
                root.Identifier.ValueText == valueKeyWord )
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                ContextualKeyWordRule,
                root.GetLocation(),
                "This code uses the contextual keyword as a variable or member name. " +
                "An important improvement would be to rename this variable so that it is not named after a keyword"));
        }

        private static void AnalyzeClassesAreNounRule(SyntaxNodeAnalysisContext context)
        {
            var root = context.Node as ClassDeclarationSyntax;
            var keywords = new[]
            {
                "Manager",
                "Processor",
                "Data",
                "Info"
            };

            if (root != null &&
                keywords.Any(x => root.Identifier.ValueText.EndsWith(x)))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    ContextualKeyWordRule,
                    root.GetLocation(),
                    @"Classes are nouns. Rename this class in order to eliminate ""Processor"", ""Data"" or ""Info"" word and " +
                    "keep a high level of expressiveness and meaningfulness."));
            }
        }

        private static void AnalyzeBlankBlockCode(SyntaxNodeAnalysisContext context)
        {
            var root = context.Node as BlockSyntax;

            if (root == null ||
                root.DescendantNodes().Any() ||
                root.Parent is SimpleLambdaExpressionSyntax ||
                root.Parent is LambdaExpressionSyntax ||
                root.Parent is ParenthesizedLambdaExpressionSyntax)
            {
                return;
            }

            var commonMessage = "This code has a blank block to do nothing. Sometimes this means the code missed to implement here";

            if (root.Parent is IfStatementSyntax)
            {
                commonMessage = "This method contains an unnecessary empty if statement";
            }
            else if (root.Parent is CatchClauseSyntax)
            {
                commonMessage = @"The exception is ignored (""swallowed"") by the try-catch block.";
            }

            context.ReportDiagnostic(Diagnostic.Create(BlankCodeRule, root.GetLocation(), commonMessage));
        }

        private static void AnalyzeFieldToBeEncapsulate(SyntaxNodeAnalysisContext context)
        {
            if (!(context.Node is FieldDeclarationSyntax root) ||
                root.Modifiers.ToString().Contains("private") ||
                root.Modifiers.ToString().Contains("internal") ||
                root.Modifiers.ToString().Contains("const") ||
                root.Modifiers.ToString().Contains("static") ||
                string.IsNullOrWhiteSpace(root.Modifiers.ToString()) ||
                root.Declaration.Variables.Count > 1)
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                EncapsulateFieldRule,
                root.GetLocation(),
                "This code exposes a field as public or protected. Encapsulate this field into a property"));
        }
    }
}