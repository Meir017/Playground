using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Cake.Core;
using Cake.Core.Annotations;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;

namespace CakeIdeas
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // GenereteIntellisense();

            ConvertCsToCake();
        }

        static void GenereteIntellisense()
        {
            const string cakeFileIntellisensePath = @"..\..\CakeFileIntellisense.cs";

            var assemblies = Directory.GetFiles(".", "**.dll").Select(Path.GetFullPath).Select(Assembly.LoadFile).ToArray();

            StringBuilder classBuilder = new StringBuilder()
                .AppendLine("//------------------------------------------------------------------------------")
                .AppendLine("// <auto-generated>")
                .AppendLine("//     This code was generated by a tool.")
                .AppendLine("//")
                .AppendLine("//     Changes to this file may cause incorrect behavior and will be lost if")
                .AppendLine("//     the code is regenerated. ")
                .AppendLine("// </auto-generated>")
                .AppendLine("//------------------------------------------------------------------------------")
                .AppendLine()
                .AppendLine($"namespace CakeIdeas")
                .AppendLine("{")
                .AppendLine("\tpublic abstract class CakeFileIntellisense : CakeFile")
                .AppendLine("\t{");
            foreach (var assembly in assemblies)
            {
                foreach (var type in assembly.DefinedTypes.Where(type => type.IsStatic()))
                {
                    if (type.DeclaredMethods.Any(IsCakeAliasMethod)) classBuilder.AppendLine($"#region {type.Name}");
                    foreach (var alias in type.DeclaredMethods.Where(IsCakeAliasMethod))
                    {
                        if (alias.GetCustomAttribute<CakeMethodAliasAttribute>() != null)
                        {
                            classBuilder.Append("\t\tprotected ");

                            if (alias.ReturnType == typeof(void)) classBuilder.Append($"void ");
                            else if (alias.ReturnType.IsGenericParameter) classBuilder.Append($"{alias.ReturnType} ");
                            else classBuilder.Append($"{GetTypeRepresentation(alias.ReturnType)} ");

                            classBuilder.Append(alias.Name);
                            if (alias.IsGenericMethod) classBuilder.Append("<T>");

                            classBuilder
                                .Append("(")
                                .Append(string.Join(", ", alias.GetParameters().Skip(1)
                                .Select(parameter => $"{GetParameterRepresentation(parameter)} {parameter.Name}")))
                                .Append(")")
                                .Append(" { throw new System.NotSupportedException(); } ")
                                .AppendLine();
                        }
                        else if (alias.GetCustomAttribute<CakePropertyAliasAttribute>() != null)
                        {
                            classBuilder
                                .Append("\t\tprotected ")
                                .Append(GetTypeRepresentation(alias.ReturnType))
                                .Append(" ")
                                .Append(alias.Name)
                                .Append(" { get { throw new System.NotSupportedException(); } }")
                                .AppendLine();
                        }
                    }
                    if (type.DeclaredMethods.Any(IsCakeAliasMethod)) classBuilder.AppendLine("#endregion");
                }
            }
            classBuilder
                .AppendLine("\t}")
                .AppendLine("}");

            File.WriteAllText(cakeFileIntellisensePath, classBuilder.ToString());
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

            if (parameter.ParameterType.FullName == null)
            {
                return $"{prefix}{parameter.ParameterType.Name}";
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
                return $"{prefix}{type.Namespace}.{type.Name.Substring(0, type.Name.Length - 2)}<{string.Join(",", type.GetGenericArguments().Select(arg => $"{arg.Namespace}.{arg.Name}"))}>";
            }

            return $"{prefix}{type.Namespace}.{type.Name}";
        }

        static void ConvertCsToCake()
        {
            string output = @"..\..\dist";
            string[] files = Directory.GetFiles(@"..\..\", "*.cake.cs");

            if (!Directory.Exists(output))
            {
                Directory.CreateDirectory(output);
            }

            foreach (var info in files.Select(file => new FileInfo(file)))
            {
                string outputFilename = info.Name.Replace(".cs", string.Empty);
                string cakeFile = ToCakeFile(File.ReadAllText(info.FullName));
                File.WriteAllText(Path.Combine(output, outputFilename), cakeFile);
            }
        }
        public static string ToCakeFile(string content)
        {
            const int classMethodStatementIndentationSpaces = 12;
            CompilationUnitSyntax compilation = SyntaxFactory.ParseCompilationUnit(content);

            StringBuilder builder = new StringBuilder();
            var script = GetExecuteMethodBody(compilation);

            foreach (string line in script)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    builder.AppendLine();
                    continue;
                }
                string trimmedLine = line.Substring(classMethodStatementIndentationSpaces);

                if (trimmedLine.StartsWith("Addin("))
                {
                    builder.AppendLine($"#addin {trimmedLine.Substring("Addin(".Length, trimmedLine.Length - "Addin(".Length - ");".Length)}");
                }
                else if (trimmedLine.StartsWith("Tool("))
                {
                    builder.AppendLine($"#tool {trimmedLine.Substring("Tool(".Length, trimmedLine.Length - "Tool(".Length - ");".Length)}");
                }
                else if (trimmedLine.StartsWith("Load("))
                {
                    builder.AppendLine($"#load {trimmedLine.Substring("Load(".Length, trimmedLine.Length - "Load(".Length - ");".Length)}");
                }
                else
                {
                    builder.AppendLine(trimmedLine);
                }
            }

            return builder.ToString();
        }

        private static string[] GetExecuteMethodBody(CompilationUnitSyntax compilation)
        {
            var @namespace = (NamespaceDeclarationSyntax)compilation.Members[0];
            var classes = @namespace.Members.OfType<ClassDeclarationSyntax>();

            var cakeFile = classes.FirstOrDefault(@class => @class.BaseList
                .Types.OfType<SimpleBaseTypeSyntax>()
                .Any(type => (type.Type as IdentifierNameSyntax).Identifier.ValueText == nameof(CakeFileIntellisense)));

            var execute = cakeFile.Members.OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(method => method.Identifier.ValueText == nameof(CakeFile.Execute));

            return execute.Body.Statements.ToFullString().SplitLines();
        }
    }
}
