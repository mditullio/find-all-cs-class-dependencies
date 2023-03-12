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
        static void Main(string[] args)
        {
            // Parse the C# code into a SyntaxTree
            string code = File.ReadAllText(@"D:\Dev\Ninject\src\Ninject\IKernel.cs");
            SyntaxTree tree = CSharpSyntaxTree.ParseText(code);

            // Create a Compilation object with references to the required assemblies
            Compilation compilation = CSharpCompilation.Create("MyCompilation",
                syntaxTrees: new[] { tree },
                references: new[]
                {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location), // mscorlib
                    MetadataReference.CreateFromFile(typeof(Console).Assembly.Location), // System.Console
                    MetadataReference.CreateFromFile(typeof(Ninject.IKernel).Assembly.Location) // Ninject.IKernel
                });

            // Get the semantic model for the SyntaxTree
            SemanticModel semanticModel = compilation.GetSemanticModel(tree);

            // Traverse the SyntaxTree and find all the class references
            List<(SimpleNameSyntax Node, SymbolInfo SymbolInfo)> classReferences = tree.GetRoot()
                .DescendantNodes()
                .OfType<SimpleNameSyntax>()
                .Select(node => (Node: node, SymbolInfo: semanticModel.GetSymbolInfo(node)))
                .Where(tuple => tuple.SymbolInfo.Symbol?.Kind == SymbolKind.NamedType)
                .ToList();

            // Print the names of the classes that are referenced
            foreach (var classReference in classReferences)
            {
                var symbol = classReference.SymbolInfo.Symbol;
                Console.WriteLine(symbol.ToString());
            }

        }
    }
}
