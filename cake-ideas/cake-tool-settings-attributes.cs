using System;
using System.Reflection;
using Cake.Core;
using Cake.Core.IO;
using Cake.Core.IO.NuGet;
using Cake.Core.Tooling;

namespace Cake.Core.Tooling
{
    /// <summary>
    /// Represents the base class for tools parameters
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public abstract class CakeParameterAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CakeParameterAttribute"/> class
        /// </summary>
        /// <param name="template">The template.</param>
        public CakeParameterAttribute(string template)
        {
            Template = template ?? throw new ArgumentNullException(nameof(template));
        }

        /// <summary>
        /// Gets the template
        /// </summary>
        public string Template { get; }

        /// <summary>
        /// Extracts the parameter from the settings object and appends in to the builder.
        /// </summary>
        /// <param name="builder">The builder to append to.</param>
        /// <param name="settings">The settings objects.</param>
        /// <param name="info">The property to extract.</param>
        /// <param name="environment">The environment object to use with paths.</param>
        public abstract void Append(ProcessArgumentBuilder builder, object settings, PropertyInfo info, ICakeEnvironment environment);
    }

    /// <summary>
    /// Used to annotate string and numeric parameters.
    /// </summary>
    public class CakeTextParameterAttribute : CakeParameterAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CakeTextParameterAttribute"/> class.
        /// </summary>
        /// <param name="template">The template used for the parameter. must contain the constant '{0}'</param>
        public CakeTextParameterAttribute(string template) : base(template)
        {
        }

        /// <summary>
        /// Gets or sets a value indicating whether the parameter is mandatory or optional
        /// </summary>
        public bool Mandatory { get; set; } = false;

        /// <inheritdoc/>
        public override void Append(ProcessArgumentBuilder builder, object settings, PropertyInfo info, ICakeEnvironment environment)
        {
            var value = info.GetValue(settings);
            if (value == null)
            {
                if (Mandatory)
                {
                    throw new ArgumentException("Value is mandatory and cannot be null");
                }
                return;
            }

            if (!Template.Contains("{0}"))
            {
                throw new FormatException("The template must contain a placeholder {0}");
            }

            builder.Append(Template, value.ToString());
        }
    }

    /// <summary>
    /// Use to annotate <see cref="bool"/> parameters that are represented as switches
    /// </summary>
    public class CakeSwitchParameterAttribute : CakeParameterAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CakeSwitchParameterAttribute"/> class.
        /// </summary>
        /// <param name="template">The template used for the parameter. must contain the constant '{0}'</param>
        public CakeSwitchParameterAttribute(string template) : base(template)
        {
        }

        /// <inheritdoc/>
        public override void Append(ProcessArgumentBuilder builder, object settings, PropertyInfo info, ICakeEnvironment environment)
        {
            if (info.GetValue(settings) is bool flag && flag)
            {
                builder.Append(Template);
            }
        }
    }

    /// <summary>
    /// Used to annotate <see cref="FilePath"/> and <see cref="DirectoryPath"/> parameters
    /// </summary>
    public class CakePathParameterAttribute : CakeParameterAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CakePathParameterAttribute"/> class.
        /// </summary>
        /// <param name="template">The template used for the parameter. must contain the constant '{0}'</param>
        public CakePathParameterAttribute(string template) : base(template)
        {
        }

        /// <summary>
        /// Gets or sets a value indicating whether the parameter is mandatory or optional
        /// </summary>
        public bool Mandatory { get; set; }

        /// <inheritdoc/>
        public override void Append(ProcessArgumentBuilder builder, object settings, PropertyInfo info, ICakeEnvironment environment)
        {
            var path = (Path)info.GetValue(settings);
            if (path == null)
            {
                if (Mandatory)
                {
                    throw new ArgumentException("Value is mandatory and cannot be null");
                }
                return;
            }

            if (!Template.Contains("{0}"))
            {
                throw new FormatException("The template must contain a placeholder {0}");
            }

            switch (path)
            {
                case FilePath file:
                    builder.Append(Template, file.MakeAbsolute(environment).FullPath.Quote());
                    break;
                case DirectoryPath directory:
                    builder.Append(Template, directory.MakeAbsolute(environment).FullPath.Quote());
                    break;
            }
        }
    }

    /// <summary>
    /// Used to annotate <see cref="Enum"/> parameters.
    /// </summary>
    public class CakeEnumParameterAttribute : CakeParameterAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CakeEnumParameterAttribute"/> class.
        /// </summary>
        /// <param name="template">The template used for the parameter. must contain the constant '{0}'</param>
        /// <param name="format">The format to use on the enum</param>
        public CakeEnumParameterAttribute(string template, EnumFormat format) : base(template)
        {
            Format = format;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the parameter is mandatory or optional
        /// </summary>
        public bool Mandatory { get; set; }

        /// <summary>
        /// Gets the format
        /// </summary>
        public EnumFormat Format { get; }

        /// <inheritdoc/>
        public override void Append(ProcessArgumentBuilder builder, object settings, PropertyInfo info, ICakeEnvironment environment)
        {
            var value = (Enum)info.GetValue(settings);
            if (value == null)
            {
                if (Mandatory)
                {
                    throw new ArgumentException("Value is mandatory and cannot be null");
                }
                return;
            }

            if (!Template.Contains("{0}"))
            {
                throw new FormatException("The template must contain a placeholder {0}");
            }
            switch (Format)
            {
                case EnumFormat.G:
                case EnumFormat.D:
                case EnumFormat.X:
                case EnumFormat.F:
                    builder.Append(Template, value.ToString(Format.ToString()));
                    break;
                case EnumFormat.LowerCaseInvariant:
                    builder.Append(Template, value.ToString().ToLowerInvariant());
                    break;
            }
        }
    }

    /// <summary>
    /// Represents differnet formattings for enums.
    /// </summary>
    public enum EnumFormat
    {
        /// <summary>
        /// The general format
        /// </summary>
        G,

        /// <summary>
        /// The decimal format
        /// </summary>
        D,

        /// <summary>
        /// The hex format
        /// </summary>
        X,

        /// <summary>
        /// The flags format
        /// </summary>
        F,

        /// <summary>
        /// The G format and then <see cref="string.ToLowerInvariant"/>
        /// </summary>
        LowerCaseInvariant
    }
}

namespace Cake.Common.Tools.NuGet.Init
{
    /// <summary>
    /// The NuGet package init tool copies all the packages from the source to the hierarchical destination.
    /// </summary>
    public sealed class NuGetIniter : NuGetTool<NuGetInitSettings>
    {
        private readonly ICakeEnvironment _environment;

        /// <summary>
        /// Initializes a new instance of the <see cref="NuGetIniter"/> class.
        /// </summary>
        /// <param name="fileSystem">The file system.</param>
        /// <param name="environment">The environment.</param>
        /// <param name="processRunner">The process runner.</param>
        /// <param name="tools">The tool locator.</param>
        /// <param name="resolver">The NuGet tool resolver.</param>
        public NuGetIniter(
            IFileSystem fileSystem,
            ICakeEnvironment environment,
            IProcessRunner processRunner,
            IToolLocator tools,
            INuGetToolResolver resolver) : base(fileSystem, environment, processRunner, tools, resolver)
        {
            _environment = environment;
        }

        /// <summary>
        /// Init adds all the packages from the source to the hierarchical destination.
        /// </summary>
        /// <param name="sourcePackageSourcePath">Package source to be copied from.</param>
        /// <param name="destinationPackageSourcePath">Package destination to be copied to.</param>
        /// <param name="settings">The settings.</param>
        public void Init(string sourcePackageSourcePath, string destinationPackageSourcePath,
            NuGetInitSettings settings)
        {
            if (string.IsNullOrWhiteSpace(sourcePackageSourcePath))
            {
                throw new ArgumentNullException(nameof(sourcePackageSourcePath));
            }
            if (string.IsNullOrWhiteSpace(destinationPackageSourcePath))
            {
                throw new ArgumentNullException(nameof(destinationPackageSourcePath));
            }
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var sourcePackagePath = sourcePackageSourcePath;
            var destinationPackagePath = destinationPackageSourcePath;

            Run(settings, GetArguments(sourcePackagePath, destinationPackagePath, settings));
        }

        private ProcessArgumentBuilder GetArguments(string sourcePackageSourcePath, string destinationPackageSourcePath, NuGetInitSettings settings)
        {
            var builder = new ProcessArgumentBuilder();

            builder.Append("init");
            builder.AppendQuoted(sourcePackageSourcePath);
            builder.AppendQuoted(destinationPackageSourcePath);

            settings.AppendArgumentsFromAttributes(builder, _environment);
            
            builder.Append("-NonInteractive");

            return builder;
        }
    }
    
    /// <summary>
    /// Contains settings used by <see cref="NuGetInitSettings"/>.
    /// </summary>
    public class NuGetInitSettings : ToolSettingsBase
    {
        /// <summary>
        /// Gets or sets a value indicating whether a package added to an offline feed is also expanded.
        /// </summary>
        /// <value><c>true</c> if package should also be expanded; otherwise, <c>false</c>.</value>
        [CakeSwitchParameter("-Expand")]
        public bool Expand { get; set; }

        /// <summary>
        /// Gets or sets the output verbosity.
        /// </summary>
        /// <value>The output verbosity.</value>
        [CakeEnumParameter("-Verbosity {0}", EnumFormat.LowerCaseInvariant)]
        public NuGetVerbosity? Verbosity { get; set; }

        /// <summary>
        /// Gets or sets the NuGet configuration file.
        /// If not specified, the file <c>%AppData%\NuGet\NuGet.config</c> is used as the configuration file.
        /// </summary>
        /// <value>The NuGet configuration file.</value>
        [CakePathParameter("-ConfigFile {0}")]
        public FilePath ConfigFile { get; set; }
    }
}

namespace Cake.Core.Tooling
{
    public class ToolSettingsBase : ToolSettings
    {
        public ProcessArgumentBuilder AppendArgumentsFromAttributes(ProcessArgumentBuilder builder, ICakeEnvironment environment)
        {
            foreach (var property in GetType().GetRuntimeProperties())
            {
                var attribute = property.GetCustomAttribute<CakeParameterAttribute>(inherit: true);
                attribute?.Append(builder, this, property, environment);
            }

            return builder;
        }
    }
}
