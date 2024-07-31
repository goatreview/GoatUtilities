using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace Goat.Utility.Merlin.Lib
{
    public class DependencyParser
    {

        private static readonly HashSet<string> BasicTypes = new HashSet<string>
        {
            "bool", "byte", "sbyte", "char", "decimal", "double", "float", "int", "uint",
            "long", "ulong", "object", "short", "ushort", "string", "DateTime", "TimeSpan",
            "Guid", "DateTimeOffset", "System.Boolean", "System.Byte", "System.SByte",
            "System.Char", "System.Decimal", "System.Double", "System.Single", "System.Int32",
            "System.UInt32", "System.Int64", "System.UInt64", "System.Object", "System.Int16",
            "System.UInt16", "System.String", "System.DateTime", "System.TimeSpan",
            "System.Guid", "System.DateTimeOffset"
        };


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
            public Dictionary<string, string> FullTypeNameToFile { get; } = new Dictionary<string, string>();
            public Dictionary<string, TypeDeclarationSyntax> FullTypeNameToDeclaration { get; } = new Dictionary<string, TypeDeclarationSyntax>();
            public Dictionary<string, EnumDeclarationSyntax> FullEnumNameToEnumDeclaration { get; } = new Dictionary<string, EnumDeclarationSyntax>();
            public Dictionary<string, HashSet<string>> FileToUsings { get; } = new Dictionary<string, HashSet<string>>();
            public HashSet<string> ParsedFiles { get; } = new HashSet<string>();
        }

        public DependencyInfo GetTypeDependencies(IReadOnlyList<string> files, string typeName)
        {
            var dependencyInfo = new DependencyInfo();
            return GetTypeDependenciesRecursive(files, typeName, dependencyInfo);
        }

        private DependencyInfo GetTypeDependenciesRecursive(IReadOnlyList<string> files, string typeName, DependencyInfo dependencyInfo)
        {
            foreach (var file in files)
            {
                if (string.IsNullOrWhiteSpace(file) || dependencyInfo.ParsedFiles.Contains(file))
                {
                    continue;
                }

                dependencyInfo.ParsedFiles.Add(file);

                var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file));
                var root = tree.GetCompilationUnitRoot();

                // Collect usings
                var usings = root.Usings.Select(u => u.Name.ToString()).ToHashSet();
                dependencyInfo.FileToUsings[file] = usings;

                foreach (var typeDeclaration in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
                {
                    var fullTypeName = GetFullTypeName(typeDeclaration);
                    dependencyInfo.FullTypeNameToDeclaration[fullTypeName] = typeDeclaration;
                    dependencyInfo.FullTypeNameToFile[fullTypeName] = file;

                    if (string.Equals(typeDeclaration.Identifier.Text, typeName, StringComparison.InvariantCultureIgnoreCase) ||
                        string.Equals(fullTypeName, typeName, StringComparison.InvariantCultureIgnoreCase))
                    {
                        AddDependencyIfPresent(fullTypeName, dependencyInfo.Candidates, dependencyInfo.AllDependencies, dependencyInfo);
                    }
                }

                foreach (var enumDeclaration in root.DescendantNodes().OfType<EnumDeclarationSyntax>())
                {
                    var fullEnumName = GetFullEnumName(enumDeclaration);
                    dependencyInfo.FullEnumNameToEnumDeclaration[fullEnumName] = enumDeclaration;
                    dependencyInfo.FullTypeNameToFile[fullEnumName] = file;

                    if (string.Equals(enumDeclaration.Identifier.Text, typeName, StringComparison.InvariantCultureIgnoreCase) ||
                        string.Equals(fullEnumName, typeName, StringComparison.InvariantCultureIgnoreCase))
                    {
                        AddDependencyIfPresent(fullEnumName, dependencyInfo.Candidates, dependencyInfo.AllDependencies, dependencyInfo);
                    }
                }
            }

            if (dependencyInfo.Candidates.Count == 0)
            {
                logger.LogWarning($"Type {typeName} not found in the provided files");
                return dependencyInfo;
            }

            foreach (var typeCandidate in dependencyInfo.Candidates)
            {
                AnalyzeDependencies(dependencyInfo.FullTypeNameToDeclaration[typeCandidate], dependencyInfo, files);
            }

            return dependencyInfo;
        }

        private void AnalyzeDependencies(TypeDeclarationSyntax typeDeclaration, DependencyInfo dependencyInfo, IReadOnlyList<string> allFiles)
        {
            var compilation = CSharpCompilation.Create("TempAssembly")
                .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                .AddSyntaxTrees(typeDeclaration.SyntaxTree);

            var semanticModel = compilation.GetSemanticModel(typeDeclaration.SyntaxTree);
            var currentFile = dependencyInfo.FullTypeNameToFile[GetFullTypeName(typeDeclaration)];

            // Analyze fields, properties, methods, etc.
            foreach (var field in typeDeclaration.DescendantNodes().OfType<FieldDeclarationSyntax>())
            {
                AnalyzeTypeSyntax(field.Declaration.Type, semanticModel, dependencyInfo.FieldDependencies,
                    dependencyInfo.AllDependencies, currentFile, dependencyInfo, allFiles);
            }

            foreach (var property in typeDeclaration.DescendantNodes().OfType<PropertyDeclarationSyntax>())
            {
                AnalyzeTypeSyntax(property.Type, semanticModel, dependencyInfo.PropertyDependencies,
                    dependencyInfo.AllDependencies, currentFile, dependencyInfo, allFiles);
            }

            foreach (var method in typeDeclaration.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                AnalyzeTypeSyntax(method.ReturnType, semanticModel, dependencyInfo.MethodDependencies,
                    dependencyInfo.AllDependencies, currentFile, dependencyInfo, allFiles);

                foreach (var parameter in method.ParameterList.Parameters)
                {
                    AnalyzeTypeSyntax(parameter.Type, semanticModel, dependencyInfo.MethodDependencies,
                        dependencyInfo.AllDependencies, currentFile, dependencyInfo, allFiles);
                }
            }

            // Analyze base types
            if (typeDeclaration.BaseList != null)
            {
                foreach (var baseType in typeDeclaration.BaseList.Types)
                {
                    AnalyzeTypeSyntax(baseType.Type, semanticModel, dependencyInfo.ParentClasses,
                        dependencyInfo.AllDependencies, currentFile, dependencyInfo, allFiles);
                }
            }

            // For records, analyze the primary constructor parameters
            if (typeDeclaration is RecordDeclarationSyntax recordDeclaration)
            {
                foreach (var parameter in recordDeclaration.ParameterList?.Parameters ?? Enumerable.Empty<ParameterSyntax>())
                {
                    AnalyzeTypeSyntax(parameter.Type, semanticModel, dependencyInfo.PropertyDependencies,
                        dependencyInfo.AllDependencies, currentFile, dependencyInfo, allFiles);
                }
            }
        }


        private void AnalyzeTypeSyntax(TypeSyntax typeSyntax, SemanticModel semanticModel, HashSet<string> specificDependencies,
            HashSet<string> allDependencies, string currentFile, DependencyInfo dependencyInfo, IReadOnlyList<string> allFiles)
        {
            if (typeSyntax == null)
            {
                return;
            }

            var typeInfo = semanticModel.GetTypeInfo(typeSyntax);
            if (typeInfo.Type?.SpecialType == SpecialType.System_Void)
            {
                return;
            }

            string fullTypeName = ResolveFullTypeName(typeSyntax, semanticModel, currentFile, dependencyInfo);

            if (!string.IsNullOrEmpty(fullTypeName))
            {
                if (!dependencyInfo.FullTypeNameToDeclaration.ContainsKey(fullTypeName) &&
                    !dependencyInfo.FullEnumNameToEnumDeclaration.ContainsKey(fullTypeName))
                {
                    // Type not found, try to parse it recursively
                    var unparsedFiles = allFiles.Where(f => !dependencyInfo.ParsedFiles.Contains(f)).ToList();
                    if (unparsedFiles.Any())
                    {
                        GetTypeDependenciesRecursive(unparsedFiles, fullTypeName, dependencyInfo);
                    }
                }

                AddDependencyIfPresent(fullTypeName, specificDependencies, allDependencies, dependencyInfo);
            }

            // Handle generic types
            if (typeSyntax is GenericNameSyntax genericNameSyntax)
            {
                foreach (var typeArgument in genericNameSyntax.TypeArgumentList.Arguments)
                {
                    AnalyzeTypeSyntax(typeArgument, semanticModel, specificDependencies, allDependencies, currentFile, dependencyInfo, allFiles);
                }
            }
            else if (typeInfo.Type is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.IsGenericType)
            {
                foreach (var typeArgument in namedTypeSymbol.TypeArguments)
                {
                    var typeArgumentSyntax = typeArgument.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as TypeSyntax;
                    if (typeArgumentSyntax != null)
                    {
                        AnalyzeTypeSyntax(typeArgumentSyntax, semanticModel, specificDependencies, allDependencies, currentFile, dependencyInfo, allFiles);
                    }
                }
            }
        }

        private bool IsBasicTypeOrNullable(string typeName)
        {
            if (BasicTypes.Contains(typeName))
            {
                return true;
            }

            // Check for nullable types
            if (typeName.StartsWith("System.Nullable<") && typeName.EndsWith(">"))
            {
                string innerType = typeName.Substring(16, typeName.Length - 17);
                return BasicTypes.Contains(innerType);
            }

            return false;
        }
        private string ResolveFullTypeName(TypeSyntax typeSyntax, SemanticModel semanticModel, string currentFile, DependencyInfo dependencyInfo)
        {
            var typeInfo = semanticModel.GetTypeInfo(typeSyntax);
            //if (typeInfo.Type != null)
            //{
                
            //    return typeInfo.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            //}

            string typeName = typeSyntax.ToString();

            if (IsBasicTypeOrNullable(typeName))
            {
                return string.Empty;
            }

            // Handle generic types
            if (typeSyntax is GenericNameSyntax genericNameSyntax)
            {
                var genericTypeName = genericNameSyntax.Identifier.Text;
                var typeArguments = string.Join(", ", genericNameSyntax.TypeArgumentList.Arguments.Select(arg => ResolveFullTypeName(arg, semanticModel, currentFile, dependencyInfo)));
                typeName = $"{genericTypeName}<{typeArguments}>";
            }

            var usings = dependencyInfo.FileToUsings[currentFile];
            var currentNamespace = GetNamespace(typeSyntax);
            var rootNamespace = string.Join(".", currentNamespace.Split(".", StringSplitOptions.None)[..^1]);
            
            usings.Add(currentNamespace);
            usings.Add(rootNamespace);

            foreach (var usingNamespace in usings)
            {
                string fullTypeName = $"{usingNamespace}.{typeName}";
                if (dependencyInfo.FullTypeNameToDeclaration.ContainsKey(fullTypeName) ||
                    dependencyInfo.FullEnumNameToEnumDeclaration.ContainsKey(fullTypeName))
                {
                    return fullTypeName;
                }
            }

            return typeName;
        }

        private string ResolveFullTypeName(ITypeSymbol typeSymbol, string currentFile, DependencyInfo dependencyInfo)
        {
            if (typeSymbol != null)
            {
                return typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }
            return string.Empty;
        }

        private void AddDependencyIfPresent(string fullTypeName, HashSet<string> specificDependencies, HashSet<string> allDependencies, DependencyInfo dependencyInfo)
        {
            if (dependencyInfo.FullTypeNameToDeclaration.ContainsKey(fullTypeName) ||
                dependencyInfo.FullEnumNameToEnumDeclaration.ContainsKey(fullTypeName))
            {
                specificDependencies.Add(fullTypeName);
                allDependencies.Add(fullTypeName);
            }
        }

        private static string GetFullTypeName(TypeDeclarationSyntax typeDeclaration)
        {
            var namespaceName = GetNamespace(typeDeclaration);
            return string.IsNullOrEmpty(namespaceName) ? typeDeclaration.Identifier.Text : $"{namespaceName}.{typeDeclaration.Identifier.Text}";
        }

        private static string GetFullEnumName(EnumDeclarationSyntax enumDeclaration)
        {
            var namespaceName = GetNamespace(enumDeclaration);
            return string.IsNullOrEmpty(namespaceName) ? enumDeclaration.Identifier.Text : $"{namespaceName}.{enumDeclaration.Identifier.Text}";
        }

        private static string GetNamespace(SyntaxNode node)
        {
            var namespaceDeclaration = node.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
            if (namespaceDeclaration != null) return namespaceDeclaration.Name.ToString();
            var fileScopedNamespaceDeclaration = node.Ancestors().OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault();
            return fileScopedNamespaceDeclaration?.Name.ToString() ?? string.Empty;
        }
    }
}