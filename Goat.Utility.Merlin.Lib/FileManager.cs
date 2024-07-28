using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Goat.Utility.Merlin.Lib
{
    public class FileManager(ILogger logger)
    {
        public static List<string> GetFilesToMerge(string sourceDirectory, IEnumerable<string> patterns)
        {
            var includePatterns = new List<string>();
            var excludePatterns = new List<string>();

            foreach (var pattern in patterns)
            {
                if (pattern.StartsWith("!"))
                    excludePatterns.Add(pattern.Substring(1));
                else
                    includePatterns.Add(pattern);
            }

            return Directory
                    .GetFiles(sourceDirectory, "*.*", SearchOption.AllDirectories)
                    .Where(file => ShouldIncludeFile(file, includePatterns, excludePatterns))
                    .ToList();
        }

        private static bool ShouldIncludeFile(string file, List<string> includePatterns, List<string> excludePatterns)
        {
            var fileName = Path.GetFileName(file);

            if (includePatterns.Count == 0 || includePatterns.Any(p => MatchesPattern(fileName, p)))
            {
                return !excludePatterns.Any(p => MatchesPattern(fileName, p));
            }

            return false;
        }

        private static bool MatchesPattern(string fileName, string pattern)
        {
            return Regex.IsMatch(fileName, "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$");
        }

        public async Task MergeFilesAsync(List<string> files, string outputFile, string encoding)
        {
            await using var writer = new StreamWriter(outputFile, false, System.Text.Encoding.GetEncoding(encoding));

            foreach (var file in files)
            {
                var relativePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), file);
                var segments = relativePath.Split(new[] { @"\", @"/" }, StringSplitOptions.None);
                await writer.WriteLineAsync($"//@FileName {string.Join(@"/",segments)}");
                await writer.WriteLineAsync(await File.ReadAllTextAsync(file, System.Text.Encoding.GetEncoding(encoding)));
                //await writer.WriteLineAsync();
                logger.LogInformation($"Merged file: {relativePath}");
            }
        }

        public async Task ExtractFilesAsync(string inputFile, string outputDirectory, string encoding)
        {
            var currentFile = "";
            var fileContent = new List<string>();

            using var reader = new StreamReader(inputFile, System.Text.Encoding.GetEncoding(encoding));
            string? line=null;

            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (line.StartsWith("//@FileName"))
                {
                    if (currentFile != "")
                    {
                        await WriteExtractedFileAsync(currentFile, fileContent, outputDirectory, encoding);
                        fileContent.Clear();
                    }
                    currentFile = line.Substring(11).Trim();
                }
                else
                {
                    fileContent.Add(line);
                }
            }

            if (currentFile != "")
            {
                await WriteExtractedFileAsync(currentFile, fileContent, outputDirectory, encoding);
            }
        }

        private async Task WriteExtractedFileAsync(string filePath, List<string> content, string outputDirectory, string encoding)
        {
            var fullPath = Path.Combine(outputDirectory, filePath);
            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("Invalid output directory");

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            await File.WriteAllLinesAsync(fullPath, content, System.Text.Encoding.GetEncoding(encoding));
            logger.LogInformation($"Extracted file: {fullPath}");
        }
    }
}
