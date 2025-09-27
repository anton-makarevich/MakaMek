using System.CommandLine;
using System.Text.Json;
using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Serialization.Converters;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;

namespace MakaMek.Tools.MtfConverter;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var inputOption = new Option<string>(
            aliases: ["-i", "--input"],
            description: "Path to the source MTF file or directory containing MTF files")
        {
            IsRequired = true
        };

        var outputOption = new Option<string>(
            aliases: ["-o", "--output"],
            description: "Directory where the converted JSON files should be saved")
        {
            IsRequired = true
        };

        var rootCommand = new RootCommand("MTF to JSON Converter")
        {
            inputOption,
            outputOption
        };

        rootCommand.SetHandler(async (inputPath, outputPath) =>
        {
            try
            {
                await ConvertMtfToJson(inputPath, outputPath);
            }
            catch (ArgumentException ex)
            {
                await Console.Error.WriteLineAsync($"Invalid argument: {ex.Message}");
                Environment.Exit(2);
            }
            catch (FileNotFoundException ex)
            {
                await Console.Error.WriteLineAsync($"File not found: {ex.Message}");
                Environment.Exit(3);
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Error: {ex.Message}");
                Environment.Exit(1);
            }
        }, inputOption, outputOption);

        return await rootCommand.InvokeAsync(args);
    }

    static async Task ConvertMtfToJson(string inputPath, string outputPath)
    {
        // Validate input path
        if (!File.Exists(inputPath) && !Directory.Exists(inputPath))
        {
            throw new ArgumentException($"Input path '{inputPath}' does not exist.");
        }

        // Validate/create output directory
        if (!Directory.Exists(outputPath))
        {
            Console.WriteLine($"Created output directory: {outputPath}");
        }

        var componentProvider = new ClassicBattletechComponentProvider();
        var mtfDataProvider = new MtfDataProvider(componentProvider);
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters =
            {
                new EnumConverter<MakaMekComponent>(),
                new EnumConverter<PartLocation>(),
                new EnumConverter<MovementType>(),
                new EnumConverter<UnitStatus>(),
                new EnumConverter<WeightClass>()
            }
        };

        // Determine if input is a file or directory
        if (File.Exists(inputPath))
        {
            await ConvertSingleFile(inputPath, outputPath, mtfDataProvider, jsonOptions);
        }
        else if (Directory.Exists(inputPath))
        {
            await ConvertDirectory(inputPath, outputPath, mtfDataProvider, jsonOptions);
        }
    }

    static async Task ConvertSingleFile(string filePath, string outputPath, MtfDataProvider mtfDataProvider, JsonSerializerOptions jsonOptions)
    {
        if (!Path.GetExtension(filePath).Equals(".mtf", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"File '{filePath}' is not an MTF file.");
        }
        
        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        Console.WriteLine($"Converting: {filePath}");

        try
        {
            var lines = await File.ReadAllLinesAsync(filePath);
            var unitData = mtfDataProvider.LoadMechFromTextData(lines);
            
            var fileName = Path.GetFileNameWithoutExtension(filePath) + ".json";
            var outputFilePath = Path.Combine(outputPath, fileName);
            
            var json = JsonSerializer.Serialize(unitData, jsonOptions);
            await File.WriteAllTextAsync(outputFilePath, json);
            
            Console.WriteLine($"Successfully converted to: {outputFilePath}");
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error converting '{filePath}': {ex.Message}");
            throw new InvalidOperationException($"Failed to convert '{filePath}': {ex.Message}", ex);
        }
    }

    static async Task ConvertDirectory(string directoryPath, string outputPath, MtfDataProvider mtfDataProvider, JsonSerializerOptions jsonOptions)
    {
        var mtfFiles = Directory.GetFiles(directoryPath, "*.mtf", SearchOption.TopDirectoryOnly);
        
        if (mtfFiles.Length == 0)
        {
            Console.WriteLine($"No MTF files found in directory: {directoryPath}");
            return;
        }

        Console.WriteLine($"Found {mtfFiles.Length} MTF file(s) to convert.");

        var successCount = 0;
        var errorCount = 0;

        foreach (var filePath in mtfFiles)
        {
            try
            {
                await ConvertSingleFile(filePath, outputPath, mtfDataProvider, jsonOptions);
                successCount++;
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Error converting '{filePath}': {ex.Message}");
                errorCount++;
            }
        }

        Console.WriteLine($"Conversion completed. Success: {successCount}, Errors: {errorCount}");
        
        if (errorCount > 0)
        {
            Environment.Exit(1);
        }
    }
}