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
            // Parse the C# code into a SyntaxTree

            var syntaxTrees = FindCSharpFiles(@"D:\Dev\Ninject\src")
                .Select(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file.FullName), path: file.FullName))
                .ToList();

            // Create a Compilation object with references to the required assemblies
            var compilation = CSharpCompilation.Create("MyCompilation",
                syntaxTrees: syntaxTrees,
                references: new[]
                {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location), // mscorlib
                    MetadataReference.CreateFromFile(typeof(Console).Assembly.Location), // System.Console
                });

            var tree = syntaxTrees.First(tree => tree.FilePath.EndsWith("\\KernelBase.cs"));
            
            // Get the semantic model for the SyntaxTree
            var semanticModel = compilation.GetSemanticModel(tree);

            // Traverse the SyntaxTree and find all the class references that are in source
            var typeReferences = tree.GetRoot()
                .DescendantNodes()
                .OfType<SimpleNameSyntax>()
                .Select(e => semanticModel.GetSymbolInfo(e))
                .Where(e => e.Symbol?.Kind == SymbolKind.NamedType)
                .ToHashSet();

            var typeReferencesInSource = typeReferences
                .Where(e => e.Symbol.Locations.Any(e => e.IsInSource))
                .ToHashSet();

            var typeReferencesNotInSource = typeReferences
                .Where(e => e.Symbol.Locations.Any(e => !e.IsInSource))
                .ToHashSet();

            // Print the names of the classes that are referenced
            foreach (var name in typeReferences.Select(e => e.Symbol.ToString()).OrderBy(e => e))
            {
                Console.WriteLine(name);
            }

        }
    }
}
