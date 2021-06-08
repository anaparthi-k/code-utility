using CodeGenerator.Formatters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace CodeGenerator
{
    public class FormatPatternTests
    {
        private readonly ITestOutputHelper _output;
        const string ROOT_FOLDER = @"./../../../\Resources\Formatters";
        const string PATTERNS_FOLDER = ROOT_FOLDER + @"\Pattern";

        public FormatPatternTests(ITestOutputHelper output)
        {
            this._output = output;
        }

        [Fact]
        public void PrintAllPatternsFortmatTest()
        {
            Formatter formatter = new Formatter();
            string configPath = Path.Combine(ROOT_FOLDER, "config.json");
            string message = formatter.GetAllPatterns(configPath);
            _output.WriteLine(message);
            Assert.NotNull(message);
        }

        [Fact]
        public void FortmatTest()
        {
            Formatter formatter = new Formatter();
            string filePath = Path.Combine(ROOT_FOLDER, "custom_template.txt");
            string configPath = Path.Combine(ROOT_FOLDER, "config.json");
            var output = formatter.Generate(PATTERNS_FOLDER, filePath, configPath, typeof(Employee));
            _output.WriteLine(output);
            Assert.NotNull(output);
        }
    }
}
