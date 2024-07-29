using CommandLine;

namespace Merlin
{

    [Verb("register", HelpText = "Register the application in the system PATH")]
    public class RegisterOptions
    {
        [Option('u', "unregister", Required = false, HelpText = "Unregister the application from the system PATH")]
        public bool Unregister { get; set; }
    }

    [Verb("merge", HelpText = "Merge multiple files into a single file")]
    public class MergeOptions
    {
        [Option('s', "source", Required = true, HelpText = "Source directory containing the files to merge")]
        public required string SourceDirectory { get; set; }

        [Option('o', "output", Required = true, HelpText = "Output file path for the merged content")]
        public required string OutputFile { get; set; }

        [Option('c', "class", Required = false, HelpText = "Class name to retrieve")]
        public string? BaseClass { get; set; }

        [Option('p', "patterns", Required = false, HelpText = "File patterns to include/exclude (e.g. *.cs, !*Test.cs)")]
        public IEnumerable<string>? Patterns { get; set; }

        [Option('e', "encoding", Required = false, Default = "utf-8", HelpText = "Encoding to use for reading/writing files")]
        public string? Encoding { get; set; }
    }

    [Verb("extract", HelpText = "Extract content from a merged file into separate files")]
    public class ExtractOptions
    {
        [Option('i', "input", Required = true, HelpText = "Input file to extract")]
        public required string InputFile { get; set; }

        [Option('o', "output", Required = true, HelpText = "Output directory for extracted files")]
        public required string OutputDirectory { get; set; }

        [Option('e', "encoding", Required = false, Default = "utf-8", HelpText = "Encoding to use for reading/writing files")]
        public string? Encoding { get; set; }
    }
}
