using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Pdb;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;

/*
 * Courtesy https://www.coengoedegebure.com/module-initializers-in-dotnet/ 
 * & https://github.com/CoenGoedegebure/ModuleInitializer
 */
namespace ModuleInitializer
{
    class Program
    {
        static int Main(string[] args)
        {
            var rootCommand = new RootCommand
            {
                new Option("-a", "Assembly Path")
                {
                    Argument = new Argument<FileInfo>()
                }
            };

            rootCommand.Handler = CommandHandler.Create<FileInfo>((a) => 
            {
                AddModuleInitialzer(a);
            });

            return rootCommand.Invoke(args);
        }

        private static void AddModuleInitialzer(FileInfo assemblyPath)
        {
            string filePath = assemblyPath.FullName;
            string pdbPath = Path.ChangeExtension(filePath, ".pdb");
            if (string.IsNullOrEmpty(pdbPath) || !File.Exists(pdbPath))
            {
                pdbPath = null;
            }

            var readerParameters = new ReaderParameters(ReadingMode.Immediate)
            {
                ReadSymbols = pdbPath != null,
                SymbolReaderProvider = pdbPath != null ? new PdbReaderProvider() : null
            };

            using (var assemblyDef = AssemblyDefinition.ReadAssembly(filePath, readerParameters))
            {
                if (assemblyDef == null)
                {
                    throw new Exception();
                }

                var cctor =
                    new MethodDefinition(
                        ".cctor",
                        MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.HideBySig | MethodAttributes.Private,
                        assemblyDef.MainModule.ImportReference(typeof(void)));

                if (assemblyDef.MainModule.GetType(@namespace: "", name: "<Module>")
                    ?.Methods
                    .Any((m) => m.Name.Equals(cctor.Name, StringComparison.OrdinalIgnoreCase)
                                && m.Attributes.HasFlag(cctor.Attributes)) == true)
                {
                    throw new Exception("Assembly already contains a Module .cctor");
                }

                InjectModuleConstructorBody(cctor.Body.GetILProcessor());

                var writerParameters = new WriterParameters()
                {
                    WriteSymbols = pdbPath != null,
                    SymbolWriterProvider = pdbPath != null ? new PdbWriterProvider() : null
                };

                assemblyDef.Write(filePath, writerParameters);
            }
        }

        private static void InjectModuleConstructorBody(ILProcessor ilGenerator)
        {
            throw new NotImplementedException();
        }
    }
}
