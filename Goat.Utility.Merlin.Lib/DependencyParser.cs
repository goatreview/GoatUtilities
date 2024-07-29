using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

namespace Goat.Utility.Merlin.Lib
{
    public class DependencyParser(ILogger logger)
    {
        public class DependencyInfo
        {
            public HashSet<string> Candidates { get; } = new HashSet<string>();
            public HashSet<string> AllDependencies { get; } = new HashSet<string>();
            public HashSet<string> ParentClasses { get; } = new HashSet<string>();
            public HashSet<string> DerivedClasses { get; } = new HashSet<string>();
            public HashSet<string> FieldDependencies { get; } = new HashSet<string>();
            public HashSet<string> PropertyDependencies { get; } = new HashSet<string>();
            public HashSet<string> MethodDependencies { get; } = new HashSet<string>();
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

                    if (string.Equals(classDeclaration.Identifier.Text, className, StringComparison.InvariantCultureIgnoreCase) ||
                        string.Equals(fullClassName, className, StringComparison.InvariantCultureIgnoreCase))
                    {
                        dependencyInfo.Candidates.Add(fullClassName);
                    }

                    fullClassNameToClassDeclaration[fullClassName] = classDeclaration;
                    dependencyInfo.FullClassNameToFile.Add(fullClassName,file);
                }
            }

            if (dependencyInfo.Candidates.Count == 0)
            {
                logger.LogWarning($"Class {className} not found in the provided files");
                return dependencyInfo;
            }

            foreach (var classCandidate in dependencyInfo.Candidates)
            {
                AnalyzeDependencies(fullClassNameToClassDeclaration[classCandidate], dependencyInfo, fullClassNameToClassDeclaration);
            }

            logger.LogInformation($"Found {dependencyInfo.Candidates.Count} candidates and {dependencyInfo.AllDependencies.Count} dependencies for class {className}");
            return dependencyInfo;
        }

        private static void AnalyzeDependencies(ClassDeclarationSyntax classDeclaration, DependencyInfo dependencyInfo,
            Dictionary<string, ClassDeclarationSyntax> fullClassNameToClassDeclaration)
        {
            var compilation = CSharpCompilation.Create("TempAssembly")
                .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                .AddSyntaxTrees(classDeclaration.SyntaxTree);

            var semanticModel = compilation.GetSemanticModel(classDeclaration.SyntaxTree);
            //var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);

            // Analyze parent classes
            if (classDeclaration.BaseList != null)
            {
                foreach (var baseType in classDeclaration.BaseList.Types)
                {
                    var symbol = semanticModel.GetSymbolInfo(baseType.Type).Symbol;
                    string fullTypeName = GetFullTypeName(symbol, baseType.Type);

                    AddDependencyIfPresent(fullTypeName, dependencyInfo.ParentClasses, dependencyInfo.AllDependencies,
                        fullClassNameToClassDeclaration);

                    // Recursively analyze parent class dependencies
                    if (fullClassNameToClassDeclaration.TryGetValue(fullTypeName, out var parentClassDeclaration))
                    {
                        AnalyzeDependencies(parentClassDeclaration, dependencyInfo, fullClassNameToClassDeclaration);
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
                        //var symbol = semanticModel.GetSymbolInfo(baseType.Type);
                        ////string fullTypeName = GetFullTypeName(symbol, baseType.Type);

                        //if (fullTypeName == GetFullClassName(classDeclaration))
                        //{
                            string derivedClassName = GetFullClassName(potentialDerivedClass);
                            AddDependencyIfPresent(derivedClassName, dependencyInfo.DerivedClasses, dependencyInfo.AllDependencies,
                                fullClassNameToClassDeclaration);
                        //}
                    }
                }
            }

            // Analyze fields
            foreach (var field in classDeclaration.DescendantNodes().OfType<FieldDeclarationSyntax>())
            {
                AnalyzeTypeSyntax(field.Declaration.Type, semanticModel, dependencyInfo.FieldDependencies,
                    dependencyInfo.AllDependencies, fullClassNameToClassDeclaration);
            }

            // Analyze properties
            foreach (var property in classDeclaration.DescendantNodes().OfType<PropertyDeclarationSyntax>())
            {
                AnalyzeTypeSyntax(property.Type, semanticModel, dependencyInfo.PropertyDependencies,
                    dependencyInfo.AllDependencies, fullClassNameToClassDeclaration);
            }

            // Analyze methods
            foreach (var method in classDeclaration.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                AnalyzeTypeSyntax(method.ReturnType, semanticModel, dependencyInfo.MethodDependencies,
                    dependencyInfo.AllDependencies, fullClassNameToClassDeclaration);

                foreach (var parameter in method.ParameterList.Parameters)
                {
                    AnalyzeTypeSyntax(parameter.Type, semanticModel, dependencyInfo.MethodDependencies,
                        dependencyInfo.AllDependencies, fullClassNameToClassDeclaration);
                }

                var localVariables = method.DescendantNodes().OfType<VariableDeclarationSyntax>();
                foreach (var variable in localVariables)
                {
                    AnalyzeTypeSyntax(variable.Type, semanticModel, dependencyInfo.MethodDependencies,
                        dependencyInfo.AllDependencies, fullClassNameToClassDeclaration);
                }
            }
        }

        private static void AnalyzeTypeSyntax(TypeSyntax typeSyntax, SemanticModel semanticModel, HashSet<string> specificDependencies, HashSet<string> allDependencies, Dictionary<string, ClassDeclarationSyntax> fullClassNameToClassDeclaration)
        {
            var typeInfo = semanticModel.GetTypeInfo(typeSyntax);
            string fullTypeName;

            if (typeInfo.Type != null)
            {
                fullTypeName = typeInfo.Type.ToDisplayString();
                AddDependencyIfPresent(fullTypeName, specificDependencies, allDependencies, fullClassNameToClassDeclaration);

                // Handle generic types
                if (typeInfo.Type is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.IsGenericType)
                {
                    foreach (var typeArgument in namedTypeSymbol.TypeArguments)
                    {
                        AddDependencyIfPresent(typeArgument.ToDisplayString(), specificDependencies, allDependencies, fullClassNameToClassDeclaration);
                    }
                }
            }
            else
            {
                // If typeInfo.Type is null, try to get the name directly from the syntax
                fullTypeName = typeSyntax.ToString();
                AddDependencyIfPresent(fullTypeName, specificDependencies, allDependencies, fullClassNameToClassDeclaration);
            }
        }

        private static void AddDependencyIfPresent(string fullTypeName, HashSet<string> specificDependencies, HashSet<string> allDependencies, Dictionary<string, ClassDeclarationSyntax> fullClassNameToClassDeclaration)
        {
            // Remove any generic type arguments if present
            int indexOfOpenBracket = fullTypeName.IndexOf('<');
            if (indexOfOpenBracket != -1)
            {
                fullTypeName = fullTypeName.Substring(0, indexOfOpenBracket);
            }

            if (fullClassNameToClassDeclaration.ContainsKey(fullTypeName))
            {
                specificDependencies.Add(fullTypeName);
                allDependencies.Add(fullTypeName);
            }
        }

        private static string GetFullClassName(ClassDeclarationSyntax classDeclaration)
        {
            var namespaceDeclaration = classDeclaration.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
            string namespaceName = namespaceDeclaration?.Name.ToString() ?? string.Empty;
            return string.IsNullOrEmpty(namespaceName) ? classDeclaration.Identifier.Text : $"{namespaceName}.{classDeclaration.Identifier.Text}";
        }

        private static string GetFullTypeName(ISymbol symbol, TypeSyntax typeSyntax)
        {
            if (symbol != null && symbol is INamedTypeSymbol namedTypeSymbol)
            {
                return namedTypeSymbol.ToDisplayString();
            }
            else
            {
                var namespaceDeclaration = typeSyntax.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
                string namespaceName = namespaceDeclaration?.Name.ToString() ?? string.Empty;
                // If symbol is null, try to get the name directly from the syntax
                return string.IsNullOrEmpty(namespaceName) ? typeSyntax.ToString() : $"{namespaceName}.{typeSyntax.ToString()}";
            }
        }
    }
}
