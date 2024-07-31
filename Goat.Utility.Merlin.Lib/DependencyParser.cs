using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

namespace Goat.Utility.Merlin.Lib
{
    public class DependencyParser
    {
        private readonly ILogger logger;

        public DependencyParser(ILogger logger)
        {
            this.logger = logger;
        }

        public class DependencyInfo
        {
            public HashSet<string> Candidates { get; } = new HashSet<string>();
            public HashSet<string> AllDependencies { get; } = new HashSet<string>();
            public HashSet<string> ParentClasses { get; } = new HashSet<string>();
            public HashSet<string> DerivedClasses { get; } = new HashSet<string>();
            public HashSet<string> FieldDependencies { get; } = new HashSet<string>();
            public HashSet<string> PropertyDependencies { get; } = new HashSet<string>();
            public HashSet<string> MethodDependencies { get; } = new HashSet<string>();
            public HashSet<string> EnumDependencies { get; } = new HashSet<string>();
            public Dictionary<string, string> FullClassNameToFile { get; } = new Dictionary<string, string>();
        }

        public DependencyInfo GetClassDependencies(string path, string className)
        {
            var files = Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories);
            return GetClassDependencies(files, className);
        }

        public DependencyInfo GetClassDependencies(IReadOnlyList<string> files, string className)
        {
            var dependencyInfo = new DependencyInfo();

            logger.LogInformation($"Searching dependencies for class {className}:");

            var fullClassNameToClassDeclaration = new Dictionary<string, ClassDeclarationSyntax>();
            var fullEnumNameToEnumDeclaration = new Dictionary<string, EnumDeclarationSyntax>();

            foreach (var file in files)
            {
                if (string.IsNullOrWhiteSpace(file))
                {
                    continue;
                }

                var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file));
                var root = tree.GetCompilationUnitRoot();

                foreach (var classDeclaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
                {
                    var fullClassName = GetFullClassName(classDeclaration);

                    fullClassNameToClassDeclaration[fullClassName] = classDeclaration;
                    dependencyInfo.FullClassNameToFile.Add(fullClassName, file);

                    if (string.Equals(classDeclaration.Identifier.Text, className, StringComparison.InvariantCultureIgnoreCase) ||
                        string.Equals(fullClassName, className, StringComparison.InvariantCultureIgnoreCase))
                    {
                        //dependencyInfo.Candidates.Add(fullClassName);
                        AddDependencyIfPresent(fullClassName, dependencyInfo.Candidates, dependencyInfo.AllDependencies,
                            fullClassNameToClassDeclaration, fullEnumNameToEnumDeclaration);
                    }

                }

                foreach (var enumDeclaration in root.DescendantNodes().OfType<EnumDeclarationSyntax>())
                {
                    var fullEnumName = GetFullEnumName(enumDeclaration);
                    fullEnumNameToEnumDeclaration[fullEnumName] = enumDeclaration;
                    dependencyInfo.FullClassNameToFile.Add(fullEnumName, file);

                    AddDependencyIfPresent(fullEnumName, dependencyInfo.EnumDependencies, dependencyInfo.AllDependencies,
                        fullClassNameToClassDeclaration, fullEnumNameToEnumDeclaration);
                }
            }

            if (dependencyInfo.Candidates.Count == 0)
            {
                logger.LogWarning($"Class {className} not found in the provided files");
                return dependencyInfo;
            }

            foreach (var classCandidate in dependencyInfo.Candidates)
            {
                AnalyzeDependencies(fullClassNameToClassDeclaration[classCandidate], dependencyInfo, fullClassNameToClassDeclaration, fullEnumNameToEnumDeclaration);
            }

            logger.LogInformation($"Found {dependencyInfo.Candidates.Count} candidates and {dependencyInfo.AllDependencies.Count} dependencies for class {className}");
            return dependencyInfo;
        }

        private void AnalyzeDependencies(ClassDeclarationSyntax classDeclaration, DependencyInfo dependencyInfo,
            Dictionary<string, ClassDeclarationSyntax> fullClassNameToClassDeclaration,
            Dictionary<string, EnumDeclarationSyntax> fullEnumNameToEnumDeclaration)
        {
            var compilation = CSharpCompilation.Create("TempAssembly")
                .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                .AddSyntaxTrees(classDeclaration.SyntaxTree);

            var semanticModel = compilation.GetSemanticModel(classDeclaration.SyntaxTree);

            // Analyze parent classes
            if (classDeclaration.BaseList != null)
            {
                foreach (var baseType in classDeclaration.BaseList.Types)
                {
                    var symbol = semanticModel.GetSymbolInfo(baseType.Type).Symbol;
                    string fullTypeName = GetFullTypeName(symbol, baseType.Type);

                    AddDependencyIfPresent(fullTypeName, dependencyInfo.ParentClasses, dependencyInfo.AllDependencies,
                        fullClassNameToClassDeclaration, fullEnumNameToEnumDeclaration);

                    // Recursively analyze parent class dependencies
                    if (fullClassNameToClassDeclaration.TryGetValue(fullTypeName, out var parentClassDeclaration))
                    {
                        AnalyzeDependencies(parentClassDeclaration, dependencyInfo, fullClassNameToClassDeclaration, fullEnumNameToEnumDeclaration);
                    }
                }
            }

            // Analyze derived classes
            foreach (var potentialDerivedClass in fullClassNameToClassDeclaration.Values)
            {
                if (potentialDerivedClass.BaseList != null)
                {
                    foreach (var baseType in potentialDerivedClass.BaseList.Types)
                    {
                        string derivedClassName = GetFullClassName(potentialDerivedClass);
                        AddDependencyIfPresent(derivedClassName, dependencyInfo.DerivedClasses, dependencyInfo.AllDependencies,
                            fullClassNameToClassDeclaration, fullEnumNameToEnumDeclaration);
                    }
                }
            }

            // Analyze fields
            foreach (var field in classDeclaration.DescendantNodes().OfType<FieldDeclarationSyntax>())
            {
                AnalyzeTypeSyntax(field.Declaration.Type, semanticModel, dependencyInfo.FieldDependencies,
                    dependencyInfo.AllDependencies, fullClassNameToClassDeclaration, fullEnumNameToEnumDeclaration);
            }

            // Analyze properties
            foreach (var property in classDeclaration.DescendantNodes().OfType<PropertyDeclarationSyntax>())
            {
                AnalyzeTypeSyntax(property.Type, semanticModel, dependencyInfo.PropertyDependencies,
                    dependencyInfo.AllDependencies, fullClassNameToClassDeclaration, fullEnumNameToEnumDeclaration);
            }

            // Analyze methods
            foreach (var method in classDeclaration.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                AnalyzeTypeSyntax(method.ReturnType, semanticModel, dependencyInfo.MethodDependencies,
                    dependencyInfo.AllDependencies, fullClassNameToClassDeclaration, fullEnumNameToEnumDeclaration);

                foreach (var parameter in method.ParameterList.Parameters)
                {
                    AnalyzeTypeSyntax(parameter.Type, semanticModel, dependencyInfo.MethodDependencies,
                        dependencyInfo.AllDependencies, fullClassNameToClassDeclaration, fullEnumNameToEnumDeclaration);
                }

                var localVariables = method.DescendantNodes().OfType<VariableDeclarationSyntax>();
                foreach (var variable in localVariables)
                {
                    AnalyzeTypeSyntax(variable.Type, semanticModel, dependencyInfo.MethodDependencies,
                        dependencyInfo.AllDependencies, fullClassNameToClassDeclaration, fullEnumNameToEnumDeclaration);
                }
            }

            // Analyze enums
            foreach (var enumDeclaration in classDeclaration.DescendantNodes().OfType<EnumDeclarationSyntax>())
            {
                string fullEnumName = GetFullEnumName(enumDeclaration);
                AddDependencyIfPresent(fullEnumName, dependencyInfo.EnumDependencies, dependencyInfo.AllDependencies,
                    fullClassNameToClassDeclaration, fullEnumNameToEnumDeclaration);
            }
        }


        private void AnalyzeTypeSyntax(TypeSyntax typeSyntax, SemanticModel semanticModel, HashSet<string> specificDependencies, HashSet<string> allDependencies, Dictionary<string, ClassDeclarationSyntax> fullClassNameToClassDeclaration, Dictionary<string, EnumDeclarationSyntax> fullEnumNameToEnumDeclaration)
        {

            var typeInfo = semanticModel.GetTypeInfo(typeSyntax);
            if (typeInfo.Type?.SpecialType == SpecialType.System_Void)
            {
                return;
            }
            
            string fullTypeName;
            if (typeInfo.Type != null)
            {
                fullTypeName = typeInfo.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                AddDependencyIfPresent(fullTypeName, specificDependencies, allDependencies, fullClassNameToClassDeclaration, fullEnumNameToEnumDeclaration);
                // Handle generic types
                if (typeInfo.Type is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.IsGenericType)
                {
                    foreach (var typeArgument in namedTypeSymbol.TypeArguments)
                    {
                        AddDependencyIfPresent(typeArgument.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), specificDependencies, allDependencies, fullClassNameToClassDeclaration, fullEnumNameToEnumDeclaration);
                    }
                }
            }
            else
            {
                // If typeInfo.Type is null, try to get the symbol info
                var symbolInfo = semanticModel.GetSymbolInfo(typeSyntax);
                if (symbolInfo.Symbol != null)
                {
                    fullTypeName = symbolInfo.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                }
                else
                {
                    // If we can't get the symbol, resolve the type name manually
                    fullTypeName = ResolveFullTypeName(typeSyntax, semanticModel);
                }
                AddDependencyIfPresent(fullTypeName, specificDependencies, allDependencies, fullClassNameToClassDeclaration, fullEnumNameToEnumDeclaration);
            }
        }

        private string ResolveFullTypeName(TypeSyntax typeSyntax, SemanticModel semanticModel)
        {
            // Start with the type name from the syntax
            string typeName = typeSyntax.ToString();

            // Get the containing symbol (could be a namespace, class, or method)
            var containingSymbol = semanticModel.GetEnclosingSymbol(typeSyntax.SpanStart);

            while (containingSymbol != null)
            {
                if (containingSymbol is INamespaceSymbol || containingSymbol is INamedTypeSymbol)
                {
                    // Prepend the containing namespace or type name
                    typeName = $"{containingSymbol.Name}.{typeName}";
                }

                // Move up to the parent symbol
                containingSymbol = containingSymbol.ContainingSymbol;
            }

            return typeName;
        }
        private void AddDependencyIfPresent(string fullTypeName, HashSet<string> specificDependencies, HashSet<string> allDependencies, Dictionary<string, ClassDeclarationSyntax> fullClassNameToClassDeclaration, Dictionary<string, EnumDeclarationSyntax> fullEnumNameToEnumDeclaration)
        {
            // Remove any generic type arguments if present
            int indexOfOpenBracket = fullTypeName.IndexOf('<');
            if (indexOfOpenBracket != -1)
            {
                fullTypeName = fullTypeName.Substring(0, indexOfOpenBracket);
            }

            if (fullClassNameToClassDeclaration.ContainsKey(fullTypeName) || fullEnumNameToEnumDeclaration.ContainsKey(fullTypeName))
            {
                specificDependencies.Add(fullTypeName);
                allDependencies.Add(fullTypeName);
            }
        }

        private static string GetNamespace(SyntaxNode node)
        {
            var namespaceDeclaration = node.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
            if (namespaceDeclaration != null) return namespaceDeclaration?.Name.ToString() ?? string.Empty;
            var fileScopedNamespaceDeclaration = node.Ancestors().OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault();
            return fileScopedNamespaceDeclaration?.Name.ToString() ?? string.Empty;
        }

        private static string GetFullClassName(ClassDeclarationSyntax classDeclaration)
        {
            var namespaceName = GetNamespace(classDeclaration);
            return string.IsNullOrEmpty(namespaceName) ? classDeclaration.Identifier.Text : $"{namespaceName}.{classDeclaration.Identifier.Text}";
        }

        private static string GetFullEnumName(EnumDeclarationSyntax enumDeclaration)
        {
            var namespaceName = GetNamespace(enumDeclaration);
            return string.IsNullOrEmpty(namespaceName) ? enumDeclaration.Identifier.Text : $"{namespaceName}.{enumDeclaration.Identifier.Text}";
        }

        private static string GetFullTypeName(ISymbol symbol, TypeSyntax typeSyntax)
        {
            if (symbol != null && symbol is INamedTypeSymbol namedTypeSymbol)
            {
                return namedTypeSymbol.ToDisplayString();
            }
            else
            {
                var namespaceName = GetNamespace(typeSyntax);
                // If symbol is null, try to get the name directly from the syntax
                return string.IsNullOrEmpty(namespaceName) ? typeSyntax.ToString() : $"{namespaceName}.{typeSyntax.ToString()}";
            }
        }
    }
}