using CodeGeneration.Core;
using CodeGeneration.Csv;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace SmartAutomationFolderStructure
{
    class Program
    {
        static void Main(string[] args)
        {
            //ReadFileContent();
            CreateFolderStructure();
        }

        private static void ReadFileContent()
        {
            var content = File.ReadAllText(@"C:\Library\Projects\Smart\Backup\SMART_Automation\instances\pages\non_production_time_tracker_instance.py");

        }

        public static void CreateFolderStructure()
        {
            Program program = new Program();
            string rootFolder = $@"./../../../\Resources\";
            string filePath = Path.Combine(rootFolder, "folder_structure.txt");
            var structure = program.CreateTreeStructure(filePath, "page");

            //program.LogFolderPath(structure);

            program.CreateFiles(structure, new TestPathFormatter());
            program.CreateFiles(structure, new FactoryPathFormatter());
            program.CreateFiles(structure, new ModelPathFormatter());
            program.CreateFiles(structure, new PagePathFormatter());

            //program.CreateFiles(structure, new ResourcePathFormatter());

            structure.Value.Data = "factory";
            program.CreateInstances(structure, new PageInstanceFormatter());
            program.CreateInstances(structure, new FactoryInstanceFormatter());

            //structure.Value.Data = "page";
            //program.LogReferencePath(structure, "self", "./references/page_ref.txt");
            //structure.Value.Data = "";
            //program.LogReferencePath(structure, "self.factory.page", "./references/factory_ref.txt");
        }

        
        private TreeNode<TreeDataNode> CreateTreeStructure(string filePath, string rootName)
        {
            var lines = File.ReadAllLines(filePath);

            TreeNode<TreeDataNode> rootNode = new TreeNode<TreeDataNode>(new TreeDataNode(0, rootName));
            Stack<TreeNode<TreeDataNode>> parents = new Stack<TreeNode<TreeDataNode>>();

            parents.Push(rootNode);

            TreeNode<TreeDataNode> currentNode = null;

            int preLevel = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int level = GetLevel(line);

                string data = line[level..].Trim();

                if (level > preLevel)
                {
                    //Console.WriteLine($"Level is increased from {preLevel} to {level} so current node {currentNode.Value.Data}");
                    parents.Push(currentNode);
                }

                if (level < preLevel)
                {
                    //parents.Pop();
                    for (int k = preLevel; k > level; k--)
                    {
                        parents.Pop();
                    }

                    //var removedNode = parents.Pop();
                    //Console.WriteLine($"Level is decreased from {preLevel} to {level} so current node {removedNode.Value.Data}");
                }

                var parent = parents.Peek();

                //Console.WriteLine($"Current Level {level} parent node is {parent.Value.Data} -> {data}");

                currentNode = parent.AddChild(new TreeDataNode(level + 1, data));

                preLevel = level;
            }

            //Console.WriteLine(new string('-', 50));
            //Console.WriteLine();
            //rootNode.Traverse(x => Console.WriteLine("{0}{1}", new string('\t', x.Level + 1), x.Data));

            return rootNode;

            static int GetLevel(string value)
            {
                int level = 0;
                foreach (var ch in value)
                {
                    if ('\t'.Equals(ch))
                    {
                        level++;
                    }
                    else
                    {
                        break;
                    }
                }

                return level;
            }
        }

        private interface IPathFormatter
        {
            string RootPath { get; }
            string GetFormattedValue(TreeDataNode value);
            void Work(string directoryPath, string value, TreeNode<TreeDataNode> node);
        }

        private abstract class PathFormatterBase : IPathFormatter
        {
            private readonly CsvCodeGenerator _generator = new CsvCodeGenerator();

            public abstract string RootPath { get; }

            public string GetFormattedValue(TreeDataNode value)
            {
                return _generator.GetPipesFormattedValue(value.Data, PipeType.Proper, PipeType.Clean, PipeType.Underscore, PipeType.Lower);
            }

            public abstract void Work(string directoryPath, string value, TreeNode<TreeDataNode> node);

            protected void CreateFile(string directoryPath, string fileName, string fileContent)
            {
                var fullFilePath = Path.Combine(directoryPath, fileName);
                CreateFile(fullFilePath, fileContent);
            }

            private void CreateFile(string fullFilePath, string fileContent)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fullFilePath));
                File.WriteAllText(fullFilePath, fileContent);
            }

            protected string GetImportLocation(TreeNode<TreeDataNode> node)
            {
                var backRef = node;

                Stack<TreeNode<TreeDataNode>> parents = new Stack<TreeNode<TreeDataNode>>();

                while (node.Value.Level != 0)
                {
                    parents.Push(node);
                    node = node.Parent;
                }

                string pageImportPath = string.Join(".", parents.Select(x => GetFormattedValue(x.Value)));
                return pageImportPath;
            }

            protected string GetProperCase(TreeNode<TreeDataNode> node)
            {
                return _generator.GetPipesFormattedValue(node.Value.Data, PipeType.Proper, PipeType.Clean);
            }
        }

        private class FactoryPathFormatter : PathFormatterBase
        {
            public override string RootPath => "./factories/pages";

            public override void Work(string directoryPath, string value, TreeNode<TreeDataNode> node)
            {
                var fileName = value + "_factory.py";

                string content =
$@"from faker import Faker
from models.pages.{GetImportLocation(node)}_model import {GetProperCase(node)}Model


class {GetProperCase(node)}Factory:
    _faker = Faker()
    pass
";
                CreateFile(directoryPath, fileName, content);
            }
        }

        private class ModelPathFormatter : PathFormatterBase
        {
            public override string RootPath => "./models/pages";

            public override void Work(string directoryPath, string value, TreeNode<TreeDataNode> node)
            {
                var fileName = value + "_model.py";
                string content =
$@"class {GetProperCase(node)}Model:
    pass
";
                CreateFile(directoryPath, fileName, content);
            }
        }

        private class TestPathFormatter : PathFormatterBase
        {
            public override string RootPath => "./test_cases/regression";

            public override void Work(string directoryPath, string value, TreeNode<TreeDataNode> node)
            {
                var fileName = "test_" + value + ".py";
                string content =
                $@"from test_cases.test_fixture_base import TestFixtureBase


class Test{GetProperCase(node)}(TestFixtureBase):
    # self.factory.page.{GetImportLocation(node)}
    # self.page.{GetImportLocation(node)}
    pass
";
                CreateFile(directoryPath, fileName, content);
            }
        }

        private class ResourcePathFormatter : PathFormatterBase
        {
            public override string RootPath => "./resources";

            public override void Work(string directoryPath, string value, TreeNode<TreeDataNode> node)
            {
                var fileName = "test_data_sample.csv";

                CreateFile(Path.Combine(directoryPath, value), fileName, string.Empty);
            }
        }

        private class PagePathFormatter : PathFormatterBase
        {
            public override string RootPath => "./pages";

            public override void Work(string directoryPath, string value, TreeNode<TreeDataNode> node)
            {
                var fileName = value + "_page.py";

                string content =
                $@"from core.driver_element_proxy import DriverElementProxy
from core.driver_proxy import DriverProxy
from pages.page_base import PageBase
from models.pages.{GetImportLocation(node)}_model import {GetProperCase(node)}Model


class {GetProperCase(node)}Page(PageBase):
 
    def __init__(self, driver: DriverProxy, converter: DriverElementProxy):
        super().__init__(driver, converter)

    pass
";
                CreateFile(directoryPath, fileName, content);
            }
        }


        private void CreateFiles(TreeNode<TreeDataNode> structure, IPathFormatter formatter)
        {
            foreach (var childNode in structure.Children)
            {
                CreateTestFiles(childNode, formatter.RootPath);
            }

            void CreateTestFiles(TreeNode<TreeDataNode> node, string path)
            {
                string formattedName = formatter.GetFormattedValue(node.Value);

                if (node.ChildrenCount == 0)
                {
                    formatter.Work(path, formattedName, node);
                }
                else
                {
                    string currentPath = path + Path.DirectorySeparatorChar + formattedName;

                    foreach (var childNode in node.Children)
                    {
                        CreateTestFiles(childNode, currentPath);
                    }
                }
            }
        }

        private interface IInstanceFormatter
        {
            string RootFolder { get; }
            string GetFileName(TreeNode<TreeDataNode> node);
            string GetInitDeclaration();
            StringBuilder CreateImportBuilder();
            string GetImportValue(TreeNode<TreeDataNode> node);
            string GetClassName(TreeNode<TreeDataNode> value);
            string GetFieldDeclareValue(TreeNode<TreeDataNode> node);
            string GetFieldInitValue(TreeNode<TreeDataNode> node);
        }

        public abstract class InstanceFormatterBase : IInstanceFormatter
        {
            private readonly string _nodeNameSuffixName;
            private readonly string _fileSuffixName;
            private readonly string _leafNodeNameSuffix;

            private readonly CsvCodeGenerator _generator = new CsvCodeGenerator();

            public InstanceFormatterBase(string nodeNameSuffixName, string fileSuffixName, string leafNodeNameSuffix)
            {
                _nodeNameSuffixName = nodeNameSuffixName;
                _fileSuffixName = fileSuffixName;
                _leafNodeNameSuffix = leafNodeNameSuffix;
            }

            public abstract string RootFolder { get; }

            public abstract StringBuilder CreateImportBuilder();

            public string GetClassName(TreeNode<TreeDataNode> node)
            {
                string clsName = GetProperName(node);
                string output = $"{clsName}{_nodeNameSuffixName}";
                return output;
            }

            public string GetFieldDeclareValue(TreeNode<TreeDataNode> node)
            {
                string fieldRef = GetFieldName(node);
                string pageName = GetProperName(node);

                string output = $"{fieldRef}: {pageName}{(node.ChildrenCount == 0 ? _leafNodeNameSuffix : _nodeNameSuffixName)}";
                return output;
            }

            public string GetFieldInitValue(TreeNode<TreeDataNode> node)
            {
                string fieldRef = GetFieldName(node);

                string pageName = GetProperName(node);
                string fieldInitSuffix = GetFieldInitParameter();

                string output = $"self.{fieldRef} = {pageName}{(node.ChildrenCount == 0 ? _leafNodeNameSuffix : _nodeNameSuffixName)}{fieldInitSuffix}";
                return output;
            }

            protected abstract string GetFieldInitParameter();

            public string GetFileName(TreeNode<TreeDataNode> node)
            {
                return GetFieldName(node) + $"_{_fileSuffixName}.py";
            }

            public string GetImportValue(TreeNode<TreeDataNode> node)
            {
                if (node.ChildrenCount == 0)
                {
                    return $"from {GetImportLeafNodeValue()}.{GetPageImportLocation(node)}_{_leafNodeNameSuffix.ToLower()} import {GetProperName(node)}{_leafNodeNameSuffix}";
                }
                else
                {
                    return $"from {GetImportNodeValue()}.{GetFieldName(node)}_{_fileSuffixName} import {GetProperName(node)}{_nodeNameSuffixName}";
                }
            }

            protected abstract string GetImportNodeValue();
            protected abstract string GetImportLeafNodeValue();

            private string GetPageImportLocation(TreeNode<TreeDataNode> node)
            {
                string path = GetFieldName(node);

                var backRef = node;

                Stack<TreeNode<TreeDataNode>> parents = new Stack<TreeNode<TreeDataNode>>();

                while (node.Value.Level != 0)
                {
                    parents.Push(node);
                    node = node.Parent;
                }

                string pageImportPath = string.Join(".", parents.Select(x => GetFieldName(x)));
                return pageImportPath;
            }

            private string GetProperName(TreeNode<TreeDataNode> node)
            {
                return _generator.GetPipesFormattedValue(node.Value.Data, PipeType.Proper, PipeType.Clean);
            }

            private string GetFieldName(TreeNode<TreeDataNode> node)
            {
                return _generator.GetPipesFormattedValue(node.Value.Data, PipeType.Proper, PipeType.Clean, PipeType.Underscore, PipeType.Lower);
            }

            public abstract string GetInitDeclaration();


        }

        public class PageInstanceFormatter : InstanceFormatterBase
        {
            public PageInstanceFormatter() : base("PageInstance", "page_instance", "Page")
            {
            }

            public override string RootFolder => "./instances/pages/";

            public override StringBuilder CreateImportBuilder()
            {
                return new StringBuilder(
@"from core.driver_element_proxy import DriverElementProxy
from core.driver_proxy import DriverProxy
");
            }

            protected override string GetImportLeafNodeValue()
            {
                return "pages";
            }

            protected override string GetImportNodeValue()
            {
                return "instances.pages";
            }

            public override string GetInitDeclaration()
            {
                return "def __init__(self, driver: DriverProxy, converter: DriverElementProxy):";
            }

            protected override string GetFieldInitParameter()
            {
                return "(driver, converter)";
            }


            /*
                        public string RootFolder => "./instances/pages/";

                        public StringBuilder CreateImportBuilder()
                        {
                            return new StringBuilder(
             @"from core.driver_element_proxy import DriverElementProxy
            from core.driver_proxy import DriverProxy
            ");
                        }

                        public string GetClassName(TreeNode<TreeDataNode> node)
                        {
                            string clsName = GetProperName(node);
                            string output = $"{clsName}{SUFFIX_NAME}";
                            return output;
                        }

                        public string GetFieldDeclareValue(TreeNode<TreeDataNode> node)
                        {
                            string fieldRef = GetFieldName(node);
                            string pageName = GetProperName(node);

                            string output = $"{fieldRef}: {pageName}{(node.ChildrenCount == 0 ? NAME_TYPE : SUFFIX_NAME)}";
                            return output;
                        }

                        public string GetFieldInitValue(TreeNode<TreeDataNode> node)
                        {
                            string fieldRef = GetFieldName(node);

                            string pageName = GetProperName(node);

                            string output = $"self.{fieldRef} = {pageName}{(node.ChildrenCount == 0 ? NAME_TYPE : SUFFIX_NAME)}(driver, converter)";
                            return output;
                        }

                        public string GetFileName(TreeNode<TreeDataNode> node)
                        {
                            return GetFieldName(node) + $"_{FILE_SUFFIX_NAME}.py";
                        }

                        public string GetImportValue(TreeNode<TreeDataNode> node)
                        {
                            if (node.ChildrenCount == 0)
                            {
                                return $"from pages.{GetPageImportLocation(node)}_{NAME_TYPE.ToLower()} import {GetProperName(node)}{NAME_TYPE}";
                            }
                            else
                            {
                                return $"from instances.pages.{GetFieldName(node)}_{FILE_SUFFIX_NAME} import {GetProperName(node)}{SUFFIX_NAME}";
                            }
                        }

                        private string GetPageImportLocation(TreeNode<TreeDataNode> node)
                        {
                            string path = GetFieldName(node);

                            var backRef = node;

                            Stack<TreeNode<TreeDataNode>> parents = new Stack<TreeNode<TreeDataNode>>();

                            while (node.Value.Level != 0)
                            {
                                parents.Push(node);
                                node = node.Parent;
                            }

                            string pageImportPath = string.Join(".", parents.Select(x => GetFieldName(x)));
                            return pageImportPath;
                        }

                        private string GetProperName(TreeNode<TreeDataNode> node)
                        {
                            return _generator.GetPipesFormattedValue(node.Value.Data, PipeType.Proper, PipeType.Clean);
                        }

                        private string GetFieldName(TreeNode<TreeDataNode> node)
                        {
                            return _generator.GetPipesFormattedValue(node.Value.Data, PipeType.Proper, PipeType.Clean, PipeType.Underscore, PipeType.Lower);
                        }

                        public string GetInitDeclaration()
                        {
                            return "def __init__(self, driver: DriverProxy, converter: DriverElementProxy):";
                        }
            */
        }

        public class FactoryInstanceFormatter : InstanceFormatterBase
        {
            public FactoryInstanceFormatter() : base("FactoryInstance", "factory_instance", "Factory")
            {
            }

            public override string RootFolder => "./instances/factories/pages/";

            public override StringBuilder CreateImportBuilder()
            {
                return new StringBuilder();
            }

            public override string GetInitDeclaration()
            {
                return "def __init__(self):";
            }

            protected override string GetFieldInitParameter()
            {
                return "()";
            }

            protected override string GetImportLeafNodeValue()
            {
                return "factories.pages";
            }

            protected override string GetImportNodeValue()
            {
                return "instances.factories.pages";
            }

            /*            private const string SUFFIX_NAME = "FactoryInstance";
                        private const string NAME_TYPE = "Factory";
                        private const string FILE_SUFFIX_NAME = "factory_instance";

                        private readonly CsvCodeGenerator _generator = new CsvCodeGenerator();

                        public string RootFolder => "./instances/factories/pages/";

                        public StringBuilder CreateImportBuilder()
                        {
                            return new StringBuilder();
                        }

                        public string GetClassName(TreeNode<TreeDataNode> node)
                        {
                            string clsName = GetProperName(node);
                            string output = $"{clsName}{SUFFIX_NAME}";
                            return output;
                        }

                        public string GetFieldDeclareValue(TreeNode<TreeDataNode> node)
                        {
                            string fieldRef = GetFieldName(node);
                            string pageName = GetProperName(node);

                            string output = $"{fieldRef}: {pageName}{(node.ChildrenCount == 0 ? NAME_TYPE : SUFFIX_NAME)}";
                            return output;
                        }

                        public string GetFieldInitValue(TreeNode<TreeDataNode> node)
                        {
                            string fieldRef = GetFieldName(node);

                            string pageName = GetProperName(node);

                            string output = $"self.{fieldRef} = {pageName}{(node.ChildrenCount == 0 ? NAME_TYPE : SUFFIX_NAME)}(driver, converter)";
                            return output;
                        }

                        public string GetFileName(TreeNode<TreeDataNode> node)
                        {
                            return GetFieldName(node) + $"_{FILE_SUFFIX_NAME}.py";
                        }

                        public string GetImportValue(TreeNode<TreeDataNode> node)
                        {
                            if (node.ChildrenCount == 0)
                            {
                                return $"from factories.pages.{GetImportLocation(node)}_{NAME_TYPE.ToLower()} import {GetProperName(node)}{NAME_TYPE}";
                            }
                            else
                            {
                                return $"from instances.factories.pages.{GetFieldName(node)}_{FILE_SUFFIX_NAME} import {GetProperName(node)}{SUFFIX_NAME}";
                            }
                        }

                        private string GetImportLocation(TreeNode<TreeDataNode> node)
                        {
                            var backRef = node;

                            Stack<TreeNode<TreeDataNode>> parents = new Stack<TreeNode<TreeDataNode>>();

                            while (node.Value.Level != 0)
                            {
                                parents.Push(node);
                                node = node.Parent;
                            }

                            string pageImportPath = string.Join(".", parents.Select(x => GetFieldName(x)));
                            return pageImportPath;
                        }

                        private string GetProperName(TreeNode<TreeDataNode> node)
                        {
                            return _generator.GetPipesFormattedValue(node.Value.Data, PipeType.Proper, PipeType.Clean);
                        }

                        private string GetFieldName(TreeNode<TreeDataNode> node)
                        {
                            return _generator.GetPipesFormattedValue(node.Value.Data, PipeType.Proper, PipeType.Clean, PipeType.Underscore, PipeType.Lower);
                        }

                        public string GetInitDeclaration()
                        {
                            return "def __init__():";
                        }
            */

        }

        private void CreateInstances(TreeNode<TreeDataNode> structure, IInstanceFormatter formatter)
        {
            CreateInstance(structure);

            void CreateInstance(TreeNode<TreeDataNode> node)
            {
                if (node.ChildrenCount == 0)
                {
                    return;
                }

                StringBuilder declareBuilder = new StringBuilder();
                StringBuilder initBuilder = new StringBuilder();

                StringBuilder importBuilder = formatter.CreateImportBuilder();

                foreach (var childNode in node.Children)
                {
                    importBuilder.AppendLine($"{formatter.GetImportValue(childNode)}");
                    declareBuilder.AppendLine($"{new string(' ', 4)}{formatter.GetFieldDeclareValue(childNode)}");
                    initBuilder.AppendLine($"{new string(' ', 8)}{formatter.GetFieldInitValue(childNode)}");
                    CreateInstance(childNode);
                }


                string output = $@"{importBuilder}

class {formatter.GetClassName(node)}:
{declareBuilder}
    {formatter.GetInitDeclaration()}
{initBuilder}";
                string fileName = formatter.GetFileName(node);

                var fullPath = Path.GetFullPath(Path.Combine(formatter.RootFolder, fileName));
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                File.WriteAllText(fullPath, output);
            }
        }

        private void LogReferencePath(TreeNode<TreeDataNode> node, string path, string filePath)
        {
            CsvCodeGenerator generator = new CsvCodeGenerator();

            StringBuilder builder = new StringBuilder();

            LogPath(node, path);

            void LogPath(TreeNode<TreeDataNode> node, string path)
            {
                var accessPath = path + (string.IsNullOrEmpty(node.Value.Data) ? "" :
                    ("." + generator.GetPipesFormattedValue(node.Value.Data, PipeType.Proper, PipeType.Clean, PipeType.Underscore, PipeType.Lower)));

                if (node.ChildrenCount == 0)
                {
                    //Console.WriteLine(accessPath);
                    builder.AppendLine(accessPath);
                }
                else
                {
                    foreach (var child in node.Children)
                    {
                        LogPath(child, accessPath);
                    }
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(filePath));

            File.WriteAllText(filePath, builder.ToString());

        }

        private void LogFolderPath(TreeNode<TreeDataNode> structure)
        {
            var generator = new CsvCodeGenerator();

            List<string> paths = new List<string>();

            foreach (var childNode in structure.Children)
            {
                CreatePaths(childNode, paths, null);
            }

            paths.ForEach(Console.WriteLine);

            void CreatePaths(TreeNode<TreeDataNode> node, List<string> paths, string path)
            {
                string currentPath = path + Path.DirectorySeparatorChar + generator.GetPipesFormattedValue(node.Value.Data,
                    PipeType.Proper, PipeType.Clean, PipeType.Underscore, PipeType.Lower);

                if (node.ChildrenCount == 0)
                {
                    paths.Add(currentPath);
                }
                else
                {
                    foreach (var childNode in node.Children)
                    {
                        CreatePaths(childNode, paths, currentPath);
                    }
                }
            }
        }

        public class TreeDataNode
        {
            public TreeDataNode(int level, string data)
            {
                if (string.IsNullOrEmpty(data))
                {
                    throw new ArgumentException($"'{nameof(data)}' cannot be null or empty.", nameof(data));
                }

                Level = level;
                Data = data;
            }

            public int Level { get; }
            public string Data { get; set; }
            public string FormattedData { get; set; }

            public override string ToString()
            {
                return $"{Level}:{Data}";
            }
        }

        [DebuggerDisplay("{Value}")]
        public class TreeNode<T>
        {
            private readonly T _value;
            private readonly List<TreeNode<T>> _children = new List<TreeNode<T>>();

            public TreeNode(T value)
            {
                _value = value;
            }

            public TreeNode<T> this[int i]
            {
                get { return _children[i]; }
            }

            public TreeNode<T> Parent { get; private set; }

            public T Value { get { return _value; } }

            public ReadOnlyCollection<TreeNode<T>> Children
            {
                get { return _children.AsReadOnly(); }
            }

            public int ChildrenCount
            {
                get { return _children.Count; }
            }

            public TreeNode<T> AddChild(T value)
            {
                var node = new TreeNode<T>(value) { Parent = this };
                _children.Add(node);
                return node;
            }

            public TreeNode<T>[] AddChildren(params T[] values)
            {
                return values.Select(AddChild).ToArray();
            }

            public bool RemoveChild(TreeNode<T> node)
            {
                return _children.Remove(node);
            }

            public void Traverse(Action<T> action)
            {
                action(Value);
                foreach (var child in _children)
                    child.Traverse(action);
            }

            public IEnumerable<T> Flatten()
            {
                return new[] { Value }.Concat(_children.SelectMany(x => x.Flatten()));
            }
        }
    }
}
