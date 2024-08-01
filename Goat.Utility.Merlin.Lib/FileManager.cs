using Microsoft.Extensions.Logging;
using System.Text;

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
                    .Where(file => ShouldIncludeFile(sourceDirectory,file, includePatterns, excludePatterns))
                    .ToList();
        }

        public static IEnumerable<string> NormalizeFileNames(string sourceDirectory, params string[] files)
        {
            foreach (var file in files)
            {
                var segments = file.Split(new[] { @"\", @"/" }, StringSplitOptions.None);
                var currentDirectory = sourceDirectory.Split(new[] { @"\", @"/" }, StringSplitOptions.None);
                var minLen = int.Min(segments.Length, currentDirectory.Length);
                var idx = minLen;
                for (int i = 0; i < minLen; i++)
                {
                    var segment = segments[i];
                    var directory = currentDirectory[i];
                    if (segment != directory)
                    {
                        idx = i + 1;
                        break;
                    }
                }
                var fileName = string.Join(@"/", segments.Skip(idx));
                yield return fileName;
            }
        }
        private static bool ShouldIncludeFile(string sourceDirectory,string file, List<string> includePatterns, List<string> excludePatterns)
        {
            var fileName = NormalizeFileNames(sourceDirectory, file).Single();           

            return GitIgnoreStyleMatcher.ShouldIncludeFile(fileName, includePatterns, excludePatterns);
        }

        //private static bool MatchesPattern(string fileName, string pattern)
        //{
        //    var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        //    return Regex.IsMatch(fileName, regex);
        //}

        public async Task MergeFilesAsync(List<string> files, string outputFile, string encoding)
        {
            await using var writer = new StreamWriter(outputFile, false, Encoding.GetEncoding(encoding));

            await WriteFileHeader(writer, files);

            foreach (var file in files.OrderBy(x=>x))
            {

                var relativePath = NormalizeFileNames(Directory.GetCurrentDirectory(), file).Single();
                await writer.WriteLineAsync($"//@FileName {relativePath}");
                await writer.WriteLineAsync(await File.ReadAllTextAsync(file, Encoding.GetEncoding(encoding)));
                logger.LogInformation($"Merged file: {relativePath}");
            }
        }

        private async Task WriteFileHeader(StreamWriter writer, List<string> files)
        {
            #region header

            var headerFilename = "header.txt";
            if (!File.Exists(headerFilename))
            {
                headerFilename = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, headerFilename);
            }
            var headerRaw =  await File.ReadAllTextAsync(headerFilename);
            var normalizedFiles = NormalizeFileNames(Directory.GetCurrentDirectory(), files.ToArray()).ToArray();
            var headerContent = string.Format(headerRaw, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                files.Count,
                string.Join("\n", normalizedFiles)
            );

            foreach (var line in headerContent.Split('\n'))
            {
                await writer.WriteLineAsync($"//@Header {line.TrimEnd()}");
            }

            #endregion
            #region intructions

            var instructionsFilename = "instructions.txt";
            if (!File.Exists(instructionsFilename))
            {
                instructionsFilename = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, instructionsFilename);
            }
            var instructionsRaw = await File.ReadAllTextAsync(instructionsFilename);
            
            foreach (var line in instructionsRaw.Split('\n'))
            {
                await writer.WriteLineAsync($"//@Instruction {line.TrimEnd()}");
            }

            #endregion
        }

        public async Task ExtractFilesAsync(string inputFile, string outputDirectory, string encoding)
        {
            var currentFile = "";
            var fileContent = new List<string>();

            using var reader = new StreamReader(inputFile, Encoding.GetEncoding(encoding));
            string? line = null;

            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (line.StartsWith("//@Header"))
                {
                    continue; // Ignore header lines
                }

                if (line.StartsWith("//@Instruction"))
                {
                    continue; // Ignore header lines
                }

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

            await File.WriteAllLinesAsync(fullPath, content, Encoding.GetEncoding(encoding));
            logger.LogInformation($"Extracted file: {fullPath}");
        }
    }
}

