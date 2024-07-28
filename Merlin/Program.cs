using CommandLine;
using Goat.Utility.Merlin.Lib;
using Merlin;
using Microsoft.Extensions.Logging;
using System.Text;

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddFilter("Microsoft", LogLevel.Warning)
        .AddFilter("System", LogLevel.Warning)
        //.AddFilter("CSharpFileManagerUtility", LogLevel.Debug)
        .AddConsole(options =>
        {
            options.FormatterName = "CustomFormatter";
            
        })
        .AddConsoleFormatter<CustomFormatter, CustomFormatterOptions>();
});
var logger = loggerFactory.CreateLogger("CSharpFileManagerUtility");

// Point d'entrée principal
var  parserResult  = await Parser.Default.ParseArguments<MergeOptions, ExtractOptions,RegisterOptions>(args)
    .MapResult(
        (MergeOptions opts) => MergeFilesAsync(opts, logger),
        (ExtractOptions opts) => ExtractFilesAsync(opts, logger),
        (RegisterOptions opts) => RegisterApplication(opts),
        errs => Task.FromResult(-1));

return parserResult;



static Task<int> RegisterApplication(RegisterOptions opts)
{
    try
    {
        if (opts.Unregister)
        {
            SelfRegistration.UnregisterFromPath();
            Console.WriteLine("Application unregistered from PATH");
        }
        else
        {
            SelfRegistration.RegisterInPath();
            Console.WriteLine("Application registered in PATH");
        }
        return Task.FromResult(0);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error during registration: {ex.Message}");
        return Task.FromResult(1);
    }
}


static async Task<int> MergeFilesAsync(MergeOptions opts,ILogger logger)
{
    var fileManager = new FileManager(logger);
    try
    {
        var files = FileManager.GetFilesToMerge(opts.SourceDirectory, opts.Patterns ?? []);
        
        var outputFile = opts.OutputFile;
        bool hasNoExtension = string.IsNullOrEmpty(Path.GetExtension(outputFile));
        if (hasNoExtension)
        {
            outputFile += ".miz";
        }

        await fileManager.MergeFilesAsync(files, outputFile, opts.Encoding ?? Encoding.UTF8.EncodingName);
        logger.LogInformation($"Merged {files.Count} files into {outputFile}");
        return 0;
    }
    catch (Exception ex)
    {
        logger.LogError($"An error occurred during the merge operation {ex.Message}");
        return 1;
    }
}

static async Task<int> ExtractFilesAsync(ExtractOptions opts,ILogger logger)
{
    var fileManager = new FileManager(logger);

    try
    {
        await fileManager.ExtractFilesAsync(opts.InputFile, opts.OutputDirectory, opts.Encoding ?? Encoding.UTF8.EncodingName);
        logger.LogInformation($"Extracted files from {opts.InputFile} to {opts.OutputDirectory}");
        return 0;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred during the extract operation");
        return 1;
    }
}
