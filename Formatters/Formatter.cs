using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace CodeGenerator.Formatters
{
    public class Employee
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public int GetData(int value)
        {
            throw new NotImplementedException();
        }
    }

    public class Formatter
    {
        int parameterIndex = -2;
        public string GetAllPatterns(string configPath)
        {
            var config = JsonSerializer.Deserialize<Configuration>(File.ReadAllText(configPath));

            GetInternalTypes(out List<Pattern> internalPatterns, out List<Pattern> typePatterns);

            var values = internalPatterns
                            .SelectMany(x => x.GetPatterns())
                            .Concat(typePatterns.SelectMany(x => x.GetPatterns()))
                            .Concat(config.PropertyCollection.Select(x => x.Name))
                            .Concat(config.ParameterCollection.Select(x => x.Name))
                            .Concat(config.MethodCollection.Select(x => x.Name))
                            .OrderBy(x => x);
            return string.Join(Environment.NewLine, values);
        }

        public string Generate(string patternsFolderPath, string filePath, string configPath, Type type)
        {
            var config = JsonSerializer.Deserialize<Configuration>(File.ReadAllText(configPath));

            var content = File.ReadAllText(filePath);

            GetInternalTypes(out List<Pattern> internalPatterns, out List<Pattern> typePatterns);

            var formatters = new IPatternFormatter[]
            {
                new ExternalPatternFormatter(patternsFolderPath),
                new TypePropertyConfigurationPattern(config, internalPatterns, type),
                new MethodConfigurationPattern(config,internalPatterns,type),
                new TypePatternFormatter(typePatterns, type),
                new NamespaceCleanupPatternFormatter(config, type)
            };

            content = ReplaceContent(content, formatters);

            return content;
        }

        private string ReplaceContent(string content, IPatternFormatter[] formatters)
        {
            foreach (var formatter in formatters)
            {
                content = formatter.Replace(content);
            }

            int index = content.IndexOf("[$");

            if (index > 0 && index != parameterIndex)
            {
                parameterIndex = index;
                ReplaceContent(content, formatters);
            }

            return content;
        }

        private void GetInternalTypes(out List<Pattern> allPatterns, out List<Pattern> typePatterns)
        {
            var patternType = typeof(Pattern);

            List<Pattern> patterns = patternType.Assembly.GetTypes().Where(x =>
            {
                if (x.BaseType == patternType && x.GetCustomAttribute<TopinAttribute>() != null)
                {
                    var constructors = x.GetConstructors();
                    if (constructors.Length == 1)
                    {
                        return true;
                    }
                }
                return false;
            })
             .Select(x => (Pattern)Activator.CreateInstance(x))
             .ToList();

            allPatterns = new List<Pattern>(patterns);

            typePatterns = new List<Pattern>()
            {
                new TypeModelNamePattern() { Topins = patterns },
                new TypeModelFullNamePattern(),
                new NamespaceListPattern()
            };

            CreateAttributeTypeInstances<MethodPattern>(allPatterns, typeof(MethodItem), patterns);
            CreateAttributeTypeInstances<PropertyPattern>(allPatterns, typeof(PropertyAttributeItem), patterns);
            CreateAttributeTypeInstances<ParameterPattern>(allPatterns, typeof(ParameterAttributeItem), patterns);


            static void CreateAttributeTypeInstances<T>(List<Pattern> patterns, Type propertyType, List<Pattern> toppins) where T : Pattern
            {
                var properties = propertyType.GetProperties();
                foreach (var propertyInfo in properties)
                {
                    if (propertyInfo.GetCustomAttribute<IgnoreAttribute>() is null)
                    {
                        T pattern = (T)Activator.CreateInstance(typeof(T), propertyInfo.Name);
                        pattern.Topins = toppins;
                        patterns.Add(pattern);
                    }
                }
            }


        }



        private List<Pattern> GetInternalTypes1()
        {
            var patternType = typeof(Pattern);

            List<Pattern> patterns = patternType.Assembly.GetTypes().Where(x =>
            {
                if (x.BaseType == patternType)
                {
                    var constructors = x.GetConstructors(BindingFlags.Public | BindingFlags.Default);
                    if (constructors.Length == 1)
                    {
                        return true;
                    }
                }
                return false;
            })
             .Select(x => (Pattern)Activator.CreateInstance(x))
             .ToList();

            //var patterns = new List<Pattern>
            //{
            //    new LowerCasePattern(),
            //    new UpperCasePattern(),
            //    new CsharpPattern(),
            //    new DashedPattern(),
            //    new CamelCasePattern(),
            //    new ProperCasePattern(),
            //    new TypeModelNamePattern(),
            //};

            var additionalPatterns = new List<Pattern>(patterns);

            CreateAttributeTypeInstances<MethodPattern>(additionalPatterns, typeof(MethodItem), patterns);
            CreateAttributeTypeInstances<PropertyPattern>(additionalPatterns, typeof(PropertyAttributeItem), patterns);
            CreateAttributeTypeInstances<ParameterPattern>(additionalPatterns, typeof(ParameterAttributeItem), patterns);


            static void CreateAttributeTypeInstances<T>(List<Pattern> patterns, Type propertyType, List<Pattern> toppins) where T : Pattern
            {
                var properties = propertyType.GetProperties();
                // List<T> items = new List<T>(properties.Length);
                foreach (var propertyInfo in properties)
                {
                    T pattern = (T)Activator.CreateInstance(typeof(T), propertyInfo.Name);
                    pattern.Topins = toppins;
                    patterns.Add(pattern);
                }

                //return items;
            }

            return new List<Pattern>(patterns) { };
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
    public sealed class IgnoreAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class TopinAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class InjectTopinsAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class PlainAttribute : Attribute
    {
    }

    public interface IPatternFormatter
    {
        string Replace(string builder);
    }


    public class NamespaceCleanupPatternFormatter : IPatternFormatter
    {
        readonly SortedSet<string> _namespaceCollection;
        private readonly Configuration _config;

        public NamespaceCleanupPatternFormatter(Configuration config, Type type)
        {
            _namespaceCollection = GetNamespacesCollection(type);
            this._config = config;
        }

        public string Replace(string builder)
        {
            if (_config.CleanUpNamespaces)
            {
                StringBuilder signature = new StringBuilder(builder);
                foreach (var namespaceValue in _namespaceCollection.OrderByDescending(x => x.Length))
                {
                    signature.Replace(namespaceValue + '.', "");
                }
                return signature.ToString();
            }
            return builder;
        }

        SortedSet<string> GetNamespacesCollection(Type type)
        {
            SortedSet<string> namespaceCollection = new SortedSet<string>();

            MethodInfo[] methods = type.GetMethods();

            foreach (var method in methods)
            {
                foreach (var parameter in method.GetParameters())
                {
                    namespaceCollection.Add(parameter.ParameterType.Namespace);
                }

                if (method.ReturnType.Name != "void")
                {
                    namespaceCollection.Add(method.ReturnType.Namespace);
                }
            }

            return namespaceCollection;

        }
    }

    public class ExternalPatternFormatter : IPatternFormatter
    {
        private readonly string[] _patterns;
        int parameterIndex = -2;

        public ExternalPatternFormatter(string folderPath)
        {
            _patterns = Directory.GetFiles(folderPath);
        }

        public string Replace(string builder)
        {
            foreach (var pattern in _patterns)
            {
                string name = Path.GetFileNameWithoutExtension(pattern);
                builder = builder.Replace("[$" + name + "]", GetContent(pattern));
            }

            int index = builder.IndexOf("[$");

            if (index>0 && index != parameterIndex)
            {
                parameterIndex = index;
                builder = Replace(builder);
            }

            return builder;
        }

        private string GetContent(string pattern)
        {
            return File.ReadAllText(pattern);
        }
    }

    public class TypePatternFormatter : IPatternFormatter
    {
        private readonly IEnumerable<Pattern> _patterns;
        private readonly Type _type;

        public TypePatternFormatter(IEnumerable<Pattern> patterns, Type type)
        {
            this._patterns = patterns;
            this._type = type;
        }

        public string Replace(string builder)
        {
            foreach (var pattern in _patterns)
            {
                builder = pattern.Replace(builder, _type);
            }

            return builder;
        }
    }

    public abstract class AttributeConfigurationPattern : IPatternFormatter
    {
        private readonly IEnumerable<Pattern> _patterns;
        private readonly Configuration _config;

        protected AttributeConfigurationPattern(Configuration config, IEnumerable<Pattern> patterns)
        {
            _config = config;
            this._patterns = patterns;
        }

        public string Replace(string builder)
        {
            var configs = GetConfigurationItems();

            foreach (var config in configs)
            {
                string format = config.Format;
                if (File.Exists(config.File))
                {
                    format = File.ReadAllText(config.File);
                }
                if (string.IsNullOrEmpty(config.Pattern))
                {
                    config.Pattern = ".";
                }

                StringBuilder output = new StringBuilder();

                var attributes = GetAttributeItems();

                foreach (var attribute in attributes)
                {
                    bool isMatched = !(config.IncludePattern ^ Regex.IsMatch(attribute.Name, config.Pattern));

                    if (isMatched)
                    {
                        string propertyOutput = format;

                        foreach (var pattern in _patterns)
                        {
                            propertyOutput = pattern.Replace(propertyOutput, attribute);

                            propertyOutput = PerformAttributeAction(propertyOutput, attribute);
                        }

                        output.Append(propertyOutput);
                    }
                }

                builder = builder.Replace("[$" + config.Name + "]", output.ToString());
            }

            return builder;
        }

        protected virtual string PerformAttributeAction(string propertyOutput, AttributeItem attribute)
        {
            return propertyOutput;
        }

        protected abstract IEnumerable<AttributeInfo> GetConfigurationItems();


        //private IEnumerable<AttributeInfo> GetConfigurationItems()
        //{
        //    var items = new List<AttributeInfo>();

        //    items.AddRange(_config.PropertyCollection);
        //    items.AddRange(_config.MethodCollection);
        //    items.AddRange(_config.ParameterCollection);

        //    return items;
        //}
        protected abstract IEnumerable<AttributeItem> GetAttributeItems();
    }

    public class TypePropertyConfigurationPattern : AttributeConfigurationPattern
    {
        private readonly Configuration _config;
        private readonly Type _type;

        public TypePropertyConfigurationPattern(Configuration config, IEnumerable<Pattern> patterns, Type type) : base(config, patterns)
        {
            this._config = config;
            this._type = type;
        }

        protected override IEnumerable<AttributeInfo> GetConfigurationItems()
        {
            return _config.PropertyCollection;
        }

        protected override IEnumerable<AttributeItem> GetAttributeItems()
        {
            return _type.GetProperties().Select(x => new PropertyAttributeItem() { Name = x.Name, Type = x.PropertyType }).ToArray();
        }
    }

    public class MethodConfigurationPattern : AttributeConfigurationPattern
    {
        private readonly Configuration _config;
        private readonly IEnumerable<Pattern> _patterns;
        private readonly Type _type;

        public MethodConfigurationPattern(Configuration config, IEnumerable<Pattern> patterns, Type type) : base(config, patterns)
        {
            this._config = config;
            this._patterns = patterns;
            this._type = type;
        }

        protected override IEnumerable<AttributeInfo> GetConfigurationItems()
        {
            return _config.MethodCollection;
        }

        protected override IEnumerable<AttributeItem> GetAttributeItems()
        {
            List<MethodItem> methodItems = new List<MethodItem>();
            MethodInfo[] methods = _type.GetMethods();

            foreach (var method in methods)
            {
                var isCompilerGenerated = method.GetCustomAttribute<System.Runtime.CompilerServices.CompilerGeneratedAttribute>() != null;
                if (isCompilerGenerated || method.DeclaringType != _type)
                {
                    continue;
                }

                MethodItem methodItem = new MethodItem()
                {
                    Signature = method.GetSignature(false),
                    Invoke = method.GetSignature(true),
                    ReturnType = method.ReturnType.Name,
                    Name = method.Name,
                    Parameters = method.GetParameters().Select(x => new PropertyAttributeItem() { Name = x.Name, Type = x.ParameterType }).ToArray(),
                    //Namespaces = string.Join("", _namespaceCollection.Select(x => "using " + x + ";" + Environment.NewLine)),
                    ParameterPattern = new MethodParameterConfigurationPattern(_config, _patterns, method),
                };

                methodItems.Add(methodItem);
            }

            return methodItems;
        }

        protected override string PerformAttributeAction(string propertyOutput, AttributeItem attribute)
        {
            if (attribute is MethodItem method)
            {
                var parameterOutput= method.ParameterPattern.Replace(propertyOutput);
                return parameterOutput;
            }

            return propertyOutput;
        }

        //SortedSet<string> GetNamespacesCollection()
        //{
        //    SortedSet<string> namespaceCollection = new SortedSet<string>();

        //    MethodInfo[] methods = _type.GetMethods();

        //    foreach (var method in methods)
        //    {
        //        foreach (var parameter in method.GetParameters())
        //        {
        //            namespaceCollection.Add(parameter.ParameterType.Namespace);
        //        }

        //        if (method.ReturnType.Name != "void")
        //        {
        //            namespaceCollection.Add(method.ReturnType.Namespace);
        //        }
        //    }

        //    return namespaceCollection;

        //}
    }

    public class MethodParameterConfigurationPattern : AttributeConfigurationPattern
    {
        private readonly Configuration _config;
        private readonly MethodInfo _method;

        public MethodParameterConfigurationPattern(Configuration config, IEnumerable<Pattern> patterns, MethodInfo method) : base(config, patterns)
        {
            this._config = config;
            this._method = method;
        }

        protected override IEnumerable<AttributeInfo> GetConfigurationItems()
        {
            return _config.ParameterCollection;
        }

        protected override IEnumerable<AttributeItem> GetAttributeItems()
        {
            return _method.GetParameters().Select(x => new ParameterAttributeItem() { Name = x.Name, Type = x.ParameterType }).ToArray();
        }
    }


    [DebuggerDisplay("{GetPatterns()}")]
    public abstract class Pattern
    {
        public abstract string Name { get; }

        public List<Pattern> Topins { get; set; }

        public IEnumerable<string> GetPatterns()
        {
            SortedSet<string> names;

            if (Topins == null)
            {
                names = new SortedSet<string>();
            }
            else
            {
                names = new SortedSet<string>(Topins.Select(x => Name + "_" + x.Name));
            }

            names.Add(Name);

            return names;
        }

        public string Replace(string builder, object value)
        {
            if (!IsValid(value))
            {
                return builder;
            }

            object content = GetFormatted(value);

            if (Topins != null)
            {
                foreach (var pattern in Topins)
                {
                    if (pattern.IsValid(content))
                    {
                        string formattedValue = pattern.GetFormatted(content).ToString();
                        string parameterName = "[$" + Name + "_" + pattern.Name + "]";
                        builder = builder.Replace(parameterName, formattedValue);
                    }
                }
            }

            builder = builder.Replace("[$" + Name + "]", content.ToString());

            return builder;

        }

        protected abstract bool IsValid(object value);
        protected abstract object GetFormatted(object value);
    }

    [Topin]
    public class LowerCasePattern : Pattern
    {
        public override string Name => "lower";

        protected override object GetFormatted(object value)
        {
            return value.ToString().ToLower();
        }

        protected override bool IsValid(object value)
        {
            return value is string;
        }
    }

    [Topin]
    public class UpperCasePattern : Pattern
    {
        public override string Name => "upper";

        protected override object GetFormatted(object value)
        {
            return value.ToString().ToUpper();
        }

        protected override bool IsValid(object value)
        {
            return value is string;
        }
    }

    [Topin]
    public class CsharpPattern : Pattern
    {
        public override string Name => "csharp_type";

        protected override object GetFormatted(object value)
        {
            if (value is string content)
            {
                var type = Type.GetType(content);
                if (type != null)
                {
                    return GetCsharpName(type);
                }
            }
            else if (value is Type type)
            {
                return GetCsharpName(type);
            }

            return value;

            static string GetCsharpName(Type type)
            {
                var nullNestedType = Nullable.GetUnderlyingType(type);
                if (nullNestedType != null)
                {
                    var nullName = GetCsharpName(nullNestedType);
                    return nullName;
                }

                string name = null;
                var code = Type.GetTypeCode(type);

                switch (code)
                {
                    case TypeCode.Empty:
                        break;
                    case TypeCode.Object:
                        name = type.Namespace + "." + type.Name;
                        break;
                    case TypeCode.DBNull:
                        break;
                    case TypeCode.Boolean:
                        name = "bool";
                        break;
                    case TypeCode.Char:
                        name = "char";
                        break;
                    case TypeCode.SByte:
                        name = "sbyte";
                        break;
                    case TypeCode.Byte:
                        name = "byte";
                        break;
                    case TypeCode.Int16:
                        name = "short";
                        break;
                    case TypeCode.UInt16:
                        name = "ushort";
                        break;
                    case TypeCode.Int32:
                        name = "int";
                        break;
                    case TypeCode.UInt32:
                        name = "uint";
                        break;
                    case TypeCode.Int64:
                        name = "long";
                        break;
                    case TypeCode.UInt64:
                        name = "ulong";
                        break;
                    case TypeCode.Single:
                        name = "float";
                        break;
                    case TypeCode.Double:
                        name = "double";
                        break;
                    case TypeCode.Decimal:
                        name = "decimal";
                        break;
                    case TypeCode.DateTime:
                        name = "System.DateTime";
                        break;
                    case TypeCode.String:
                        name = "string";
                        break;
                    default:
                        break;
                }

                return name;
            }
        }

        protected override bool IsValid(object value)
        {
            if (value is string content)
            {
                var type = Type.GetType(content);
                return type != null;
            }
            else if (value is Type)
            {
                return true;
            }

            return false;
        }
    }

    [Topin]
    public class DashedPattern : Pattern
    {
        public override string Name => "dashed";

        protected override object GetFormatted(object value)
        {
            string content = value as string;

            string output = content[0].ToString();

            for (int i = 1; i < content.Length; i++)
            {
                if (char.IsUpper(content[i]))
                {
                    output += '-';
                }

                output += content[i];
            }

            return output.ToLower();
        }

        protected override bool IsValid(object value)
        {
            return value is string;
        }
    }

    [Topin]
    public class CamelCasePattern : Pattern
    {
        public override string Name => "camel_case";

        protected override object GetFormatted(object value)
        {
            string content = value as string;
            return char.ToLower(content[0]) + content[1..];
        }

        protected override bool IsValid(object value)
        {
            return value is string;
        }
    }

    [Topin]
    public class ProperCasePattern : Pattern
    {
        public override string Name => "proper_case";

        protected override object GetFormatted(object value)
        {
            string content = value as string;
            return char.ToUpper(content[0]) + content[1..];
        }

        protected override bool IsValid(object value)
        {
            return value is string;
        }
    }

    [InjectTopins]
    public class TypeModelNamePattern : Pattern
    {
        public override string Name => "model";

        protected override object GetFormatted(object value)
        {
            return (value as Type).Name;

        }

        protected override bool IsValid(object value)
        {
            return value is Type;
        }
    }

    [InjectTopins]
    public class TypeModelFullNamePattern : Pattern
    {
        public override string Name => "model_full_name";

        protected override object GetFormatted(object value)
        {
            if (value is Type type)
            {
                return type.FullName;
            }

            return value;
        }

        protected override bool IsValid(object value)
        {
            return value is Type;
        }
    }

    public class NamespaceListPattern : Pattern
    {
        public override string Name => "namespace_list";

        protected override object GetFormatted(object value)
        {
            if (value is Type type)
            {
                return string.Join("", GetNamespacesCollection(type).Select(x => "using " + x + ";" + Environment.NewLine));
            }

            return value;
        }

        SortedSet<string> GetNamespacesCollection(Type type)
        {
            SortedSet<string> namespaceCollection = new SortedSet<string>();

            AddMethodParametersToNamespaceList(type, namespaceCollection);
            AddPropertyToNamespaceList(type, namespaceCollection);

            return namespaceCollection;
        }

        private static void AddPropertyToNamespaceList(Type type, SortedSet<string> namespaceCollection)
        {
            var properties = type.GetProperties();

            foreach (var property in properties)
            {
                namespaceCollection.Add(property.PropertyType.Namespace);
            }
        }

        private static void AddMethodParametersToNamespaceList(Type type, SortedSet<string> namespaceCollection)
        {
            MethodInfo[] methods = type.GetMethods();

            foreach (var method in methods)
            {
                foreach (var parameter in method.GetParameters())
                {
                    namespaceCollection.Add(parameter.ParameterType.Namespace);
                }

                if (method.ReturnType.Name != "void")
                {
                    namespaceCollection.Add(method.ReturnType.Namespace);
                }
            }
        }

        protected override bool IsValid(object value)
        {
            return value is Type;
        }
    }

    public abstract class AttributePattern<T> : Pattern
    {
        private readonly string _propertyName;
        private readonly PropertyInfo _property;

        public override string Name => AliasName + "_" + _propertyName;

        protected abstract string AliasName { get; }

        protected AttributePattern(string propertyName)
        {
            _property = typeof(T).GetProperty(propertyName) ?? throw new ArgumentNullException(propertyName + " is not valid");
            this._propertyName = propertyName.ToLower();
        }

        protected override object GetFormatted(object value) => _property.GetValue(value);
        protected override bool IsValid(object value)
        {
            var type = typeof(T);
            Debug.WriteLine(type.Name);
            bool isMatched = value is T;
            Debug.WriteLine($"{isMatched}");
            return isMatched;
        }

    }

    public class MethodPattern : AttributePattern<MethodItem>
    {
        protected override string AliasName => "method";

        public MethodPattern(string propertyName) : base(propertyName)
        {
        }
    }

    public class PropertyPattern : AttributePattern<PropertyAttributeItem>
    {
        protected override string AliasName => "property";

        public PropertyPattern(string propertyName) : base(propertyName)
        {
        }
    }

    public class ParameterPattern : AttributePattern<ParameterAttributeItem>
    {
        protected override string AliasName => "parameter";

        public ParameterPattern(string propertyName) : base(propertyName)
        {
        }
    }

    //public class PropertyNamePattern : Pattern
    //{
    //    public override string Name => "property_name";

    //    protected override object GetFormatted(object value)
    //    {
    //        if (value is PropertyAttributeItem property)
    //        {
    //            return property.Name;
    //        }

    //        return value;
    //    }
    //}


    //public class MethodNamePattern : Pattern
    //{
    //    public override string Name => "method_name";

    //    protected override object GetFormatted(object value) => (value as MethodItem).Name;
    //    protected override bool IsValidValue(object value) => value is MethodItem;

    //}

    //public class MethodSignaturePattern : Pattern
    //{
    //    public override string Name => "method_signature";

    //    protected override object GetFormatted(object value) => (value as MethodItem).Signature;
    //    protected override bool IsValidValue(object value) => value is MethodItem;
    //}

    //public class MethodInvokePattern : Pattern
    //{
    //    public override string Name => "method_invoke";

    //    protected override object GetFormatted(object value) => (value as MethodItem).Invoke;
    //    protected override bool IsValidValue(object value) => value is MethodItem;
    //}

    //public class MethodNamespacesPattern : Pattern
    //{
    //    public override string Name => "method_namespaces";

    //    protected override object GetFormatted(object value) => (value as MethodItem).Namespaces;
    //    protected override bool IsValidValue(object value) => value is MethodItem;
    //}

    //public class MethodReturnTypePattern : Pattern
    //{
    //    public override string Name => "method_returntype";

    //    protected override object GetFormatted(object value) => (value as MethodItem).Name;
    //    protected override bool IsValidValue(object value) => value is MethodItem;
    //}

    //public class PropertyNamePattern : Pattern
    //{
    //    public override string Name => "property_name";

    //    protected override object GetFormatted(object value)
    //    {
    //        if (value is PropertyAttributeItem property)
    //        {
    //            return property.Name;
    //        }

    //        return value;
    //    }
    //}

    //public class PropertyTypePattern : Pattern
    //{
    //    public override string Name => "property_type";

    //    protected override object GetFormatted(object value)
    //    {
    //        if (value is PropertyAttributeItem property)
    //        {
    //            return property.Type;
    //        }

    //        return value;
    //    }
    //}




    [DebuggerDisplay("{Name}")]
    public abstract class AttributeItem
    {
        public string Name { get; set; }
    }

    public class PropertyAttributeItem : AttributeItem
    {
        public Type Type { get; set; }
    }

    public class ParameterAttributeItem : AttributeItem
    {
        public Type Type { get; set; }
    }

    public class MethodItem : AttributeItem
    {
        public string Signature { get; set; }
        public string Invoke { get; set; }
        public string ReturnType { get; set; }

        [Ignore]
        public IEnumerable<PropertyAttributeItem> Parameters { get; set; }
        //public string Namespaces { get; set; }

        [Ignore]
        public MethodParameterConfigurationPattern ParameterPattern { get; set; }
    }

    [DebuggerDisplay("{Name}")]
    public class AttributeInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("file")]
        public string File { get; set; }

        [JsonPropertyName("format")]
        public string Format { get; set; }

        [JsonPropertyName("pattern")]
        public string Pattern { get; set; }

        [JsonPropertyName("includePattern")]
        public bool IncludePattern { get; set; }
    }

    public class Configuration
    {
        [JsonPropertyName("propertyCollection")]
        public List<AttributeInfo> PropertyCollection { get; set; }

        [JsonPropertyName("cleanUpNamespaces")]
        public bool CleanUpNamespaces { get; set; }

        [JsonPropertyName("parameterCollection")]
        public List<AttributeInfo> ParameterCollection { get; set; }

        [JsonPropertyName("methodCollection")]
        public List<AttributeInfo> MethodCollection { get; set; }
    }
}
