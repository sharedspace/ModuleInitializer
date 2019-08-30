using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Pdb;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Reflection;
using MethodAttributes = Mono.Cecil.MethodAttributes;

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

                InjectModuleConstructorBody(assemblyDef, cctor.Body.GetILProcessor());

                var writerParameters = new WriterParameters()
                {
                    WriteSymbols = pdbPath != null,
                    SymbolWriterProvider = pdbPath != null ? new PdbWriterProvider() : null
                };

                assemblyDef.Write(filePath, writerParameters);
            }
        }

        private static void InjectModuleConstructorBody(AssemblyDefinition assemblyDef, ILProcessor ilGenerator)
        {
            ilGenerator.Body.Variables.Clear();
            ilGenerator.Body.Instructions.Clear();

            //.locals init (
            //    [0] class [mscorlib]System.Type V_0,
            //    [1] bool V_1,
            //    [2] class [mscorlib]System.Reflection.MethodInfo V_2,
            //    [3] bool V_3
            //)
            ilGenerator.Body.InitLocals = true;
            
            var local_v0 = new VariableDefinition(assemblyDef.MainModule.ImportReference(typeof(Type)));
            var local_v1 = new VariableDefinition(assemblyDef.MainModule.TypeSystem.Boolean);
            var local_v2 = new VariableDefinition(assemblyDef.MainModule.ImportReference(typeof(MethodInfo)));
            var local_v3 = new VariableDefinition(assemblyDef.MainModule.TypeSystem.Boolean);

            ilGenerator.Body.Variables.Add(local_v0);
            ilGenerator.Body.Variables.Add(local_v1);
            ilGenerator.Body.Variables.Add(local_v2);
            ilGenerator.Body.Variables.Add(local_v3);

            //IL_0000: nop
            //IL_0001: call class [mscorlib]System.Reflection.Assembly [mscorlib]System.Reflection.Assembly::GetExecutingAssembly()
            //IL_0006: ldstr "ModuleInitializer"
            //IL_000b: callvirt instance class [mscorlib]System.Type [mscorlib]System.Reflection.Assembly::GetType(string)
            //IL_0010: stloc.0
            //IL_0011: ldloc.0
            //IL_0012: ldnull
            //IL_0013: call bool [mscorlib]System.Type::op_Inequality(class [mscorlib]System.Type,  class [mscorlib]System.Type)
            //IL_0018: stloc.1
            //IL_0019: ldloc.1
            //IL_001a: brfalse.s IL_004b

            var typeRef_System_Reflection_Assembly = assemblyDef.MainModule.ImportReference(typeof(Assembly));
            var method_Assembly_GetExecutingAssembly = new MethodReference("GetExecutingAssembly", typeRef_System_Reflection_Assembly, typeRef_System_Reflection_Assembly);
            var method_Assembly_GetType = new MethodReference("GetType", assemblyDef.MainModule.ImportReference(typeof(Type)), typeRef_System_Reflection_Assembly);

            //ilGenerator.Emit(OpCodes.Nop);
            //ilGenerator.Emit(OpCodes.Call, method_Assembly_GetExecutingAssembly);
            //ilGenerator.Emit(OpCodes.Ldstr, "ModuleInitializer");
            //ilGenerator.Emit(ilGenerator.Create(OpCodes.Callvirt, method_Assembly_GetType, ))
            //ilGenerator.EmitCall(OpCodes.Callvirt, typeof(Assembly).GetMethod("GetType", new Type[] { typeof(string) }), null);
            //ilGenerator.Emit(OpCodes.Stloc_0);
            //ilGenerator.Emit(OpCodes.Ldloc_0);
            //ilGenerator.Emit(OpCodes.Ldnull);
            //ilGenerator.EmitCall(OpCodes.Call, typeof(Type).GetMethod("op_Inequality", new Type[] { typeof(Type), typeof(Type) }), null);
            //ilGenerator.Emit(OpCodes.Stloc_1);
            //ilGenerator.Emit(OpCodes.Ldloc_1);
            //ilGenerator.Emit(OpCodes.Brfalse_S, endLabel);

        }
    }
}
