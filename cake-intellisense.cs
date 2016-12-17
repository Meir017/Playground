using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Cake.Common.Tools.Cake;
using Cake.Core;
using Cake.Core.Annotations;
using Cake.Coveralls;

namespace ConsoleApp1
{
    internal class Program
    {
        static void Main(string[] args)
        {
            GenereteIntellisense();
        }

        static void GenereteIntellisense()
        {
            const string cakeFileIntellisensePath = @"..\..\CakeFileIntellisense.cs";

            var assemblies = new[] {
             //   typeof(ICakeContext).Assembly,
                typeof(CakeAliases).Assembly,
                typeof(CoverallsAliases).Assembly
            };

            var namespaces = new HashSet<string>();

            StringBuilder fileBuilder = new StringBuilder();
            StringBuilder classBuilder = new StringBuilder()
                .AppendLine($"namespace ConsoleApp1")
                .AppendLine("{")
                .AppendLine("\tpublic abstract class CakeFileIntellisense : CakeFile")
                .AppendLine("\t{");
            foreach (var assembly in assemblies)
            {
                foreach (var type in assembly.DefinedTypes.Where(type => type.IsStatic()))
                {
                    namespaces.Add(type.Namespace);
                    foreach (var alias in type.DeclaredMethods.Where(IsCakeAliasMethod))
                    {
                        foreach (var import in alias.GetCustomAttributes<CakeNamespaceImportAttribute>())
                        {
                            namespaces.Add(import.Namespace);
                        }
                        if (alias.GetCustomAttribute<CakeMethodAliasAttribute>() != null)
                        {
                            classBuilder.Append("\t\tprotected ");
                            if (alias.ReturnType == typeof(void))
                            {
                                classBuilder.Append($"void {alias.Name}");
                            }
                            else if (alias.ReturnType.FullName == null)
                            {
                                classBuilder.Append($"T {alias.Name}<T>");
                            }
                            else
                            {
                                classBuilder.Append($"{GetTypeRepresentation(alias.ReturnType)} {alias.Name}");
                            }
                            classBuilder
                                .Append("(")
                                .Append(string.Join(", ", alias.GetParameters().Skip(1)
                                .Select(parameter => $"{GetParameterRepresentation(parameter)} {parameter.Name}")))
                                .Append(")")
                                .Append(" { throw new NotSupportedException(); } ")
                                .AppendLine();
                        }
                        else if (alias.GetCustomAttribute<CakePropertyAliasAttribute>() != null)
                        {
                            classBuilder
                                .Append("\t\tprotected ")
                                .Append(GetTypeRepresentation(alias.ReturnType))
                                .Append(" ")
                                .Append(alias.Name)
                                .Append(" { get { throw new NotSupportedException(); } }")
                                .AppendLine();
                        }
                    }
                }
            }
            classBuilder
                .AppendLine("\t}")
                .AppendLine("}");

            foreach (var @namespace in typeof(ICakeContext).Assembly.DefinedTypes.Select(type => type.Namespace).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct())
            {
                namespaces.Add(@namespace);
            }
            namespaces.Add("System");
            namespaces.Add("System.Collections.Generic");
            // workaround
            namespaces.Add("Cake.Common.Build.Bamboo");
            namespaces.Add("Cake.Common.Build.Bamboo.Data");
            namespaces.Add("Cake.Common.Build.MyGet");

            foreach (var @namespace in namespaces)
            {
                fileBuilder.AppendLine($"using {@namespace};");
            }
            fileBuilder
                .AppendLine()
                .Append(classBuilder);

            File.WriteAllText(cakeFileIntellisensePath, fileBuilder.ToString());
        }

        static bool IsCakeAliasMethod(MethodInfo method)
        {
            if (!method.IsStatic) return false;

            if (method.GetCustomAttribute<CakeMethodAliasAttribute>() == null && method.GetCustomAttribute<CakePropertyAliasAttribute>() == null)
            {
                return false;
            }

            var parameters = method.GetParameters();
            if (!parameters.Any()) return false;

            if (parameters[0].ParameterType != typeof(ICakeContext)) return false;

            return true;
        }

        static string GetParameterRepresentation(ParameterInfo parameter)
        {
            string prefix = string.Empty;
            if (parameter.GetCustomAttribute<ParamArrayAttribute>() != null)
            {
                prefix = "params ";
            }
            return $"{prefix}{GetTypeRepresentation(parameter.ParameterType)}";
        }

        static string GetTypeRepresentation(Type type)
        {
            string prefix = string.Empty;
            if (type.IsByRef)
            {
                prefix = "out ";
                type = type.GetElementType();
            }

            if (type.IsGenericType)
            {
                return $"{prefix}{type.Name.Substring(0, type.Name.Length - 2)}<{string.Join(",", type.GetGenericArguments().Select(arg => arg.Name))}>";
            }

            return $"{prefix}{type.Name}";
        }

        static void ConvertCsToCake()
        {
            string output = "dist";
            string[] files = Directory.GetFiles(@"D:\Visual Studio Projects\ConsoleApp1\ConsoleApp1\cake", "*.cake.cs", SearchOption.AllDirectories);

            if (!Directory.Exists(output))
            {
                Directory.CreateDirectory(output);
            }

            foreach (var info in files.Select(file => new FileInfo(file)))
            {
                string outputFilename = info.Name.Replace(".cs", string.Empty);
                string cakeFile = CakeFileConverter.ToCakeFile(File.ReadAllText(info.FullName));
                File.WriteAllText(Path.Combine(output, outputFilename), cakeFile);

                return;
            }
        }
    }
}
