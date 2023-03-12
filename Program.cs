using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ListReferencesInCsFile
{
    class Program
    {

        public static IEnumerable<FileInfo> FindCSharpFiles(string folderPath)
        {
            DirectoryInfo directory = new DirectoryInfo(folderPath);
            FileInfo[] files = directory.GetFiles("*.cs", SearchOption.AllDirectories);
            return files;
        }

        static void Main(string[] args)
        {
            // Project path
            var projectPath = @"D:\Dev\Ninject\src";

            // Entry class
            var className = "IActivationBlock";

            // Parse the C# code of all files in the project into SyntaxTrees
            var allFilesSyntaxTrees = FindCSharpFiles(projectPath)
                .Select(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file.FullName), path: file.FullName))
                .ToList();

            // Create a Compilation object with references to the required assemblies
            var compilation = CSharpCompilation.Create("MyCompilation",
                syntaxTrees: allFilesSyntaxTrees,
                references: new[]
                {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location), // mscorlib
                    MetadataReference.CreateFromFile(typeof(Console).Assembly.Location), // System.Console
                });

            var syntaxTreesProcessed = new HashSet<SyntaxTree>();

            // Find all source trees related to the class
            var syntaxTreesToProcess = new Queue<SyntaxTree>(
                compilation.GetSymbolsWithName(className)
                    .OfType<INamedTypeSymbol>()
                    .SelectMany(e => e.Locations)
                    .Select(e => e.SourceTree)
                    .Where(e => e != null)
                    .OfType<SyntaxTree>());
            
            // Will contain all direct and indirect dependencies of the class
            var dependenciesFound = new Dictionary<string, INamedTypeSymbol>();

            while (syntaxTreesToProcess.Any())
            {
                var currSyntaxTree = syntaxTreesToProcess.Dequeue();

                // Get the semantic model for the SyntaxTree
                var semanticModel = compilation.GetSemanticModel(currSyntaxTree);

                // Traverse the SyntaxTree and find all the class references that are in source
                var symbols = currSyntaxTree.GetRoot()
                    .DescendantNodes()
                    .OfType<SimpleNameSyntax>()
                    .Select(e => semanticModel.GetSymbolInfo(e))
                    .Select(e => e.Symbol)
                    .OfType<INamedTypeSymbol>()
                    .Select(e => e.IsGenericType ? e.ConstructUnboundGenericType() : e)
                    .ToList();

                foreach (var symbol in symbols)
                {
                    if (!dependenciesFound.ContainsKey(GetSymbolKey(symbol)))
                    {
                        // Find source trees related to a symbol
                        var nextSyntaxTrees = symbol.Locations
                            .Where(e => e.IsInSource)
                            .Select(e => e.SourceTree)
                            .Where(e => e != null)
                            .OfType<SyntaxTree>()
                            .ToList();

                        // Add them to queue for processing
                        foreach (var nextSyntaxTree in nextSyntaxTrees)
                        {
                            if (!syntaxTreesProcessed.Contains(nextSyntaxTree))
                            {
                                syntaxTreesToProcess.Enqueue(nextSyntaxTree);
                            }
                        }

                        dependenciesFound[GetSymbolKey(symbol)] = symbol;
                    }
                }

                syntaxTreesProcessed.Add(currSyntaxTree);
            }
            
            // Print the names of the classes that are referenced
            foreach (var name in dependenciesFound.Keys.OrderBy(e => e))
            {
                Console.WriteLine(name);
            }

        }

        private static string GetSymbolKey(INamedTypeSymbol symbol)
        {
            return symbol.ToString();
        }
    }
}
