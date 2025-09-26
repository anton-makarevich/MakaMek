using System.CommandLine;
using System.Text.Json;
using Sanet.MakaMek.Core.Data.Serialization.Converters;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Units;
using System.IO.Compression;

namespace MakaMek.Tools.MmuxPackager;

public class Program
{
    private const string ManifestVersion = "1.0";
    private const string RequiredMakaMekVersion = "0.43.2";
    
    public static async Task<int> Main(string[] args)
    {
        var dataSourceOption = new Option<string>(
            aliases: ["-d", "--data-source"],
            description: "Path to folder containing unit JSON files")
        {
            IsRequired = true
        };

        var imageSourceOption = new Option<string>(
            aliases: ["-i", "--image-source"],
            description: "Path to folder containing unit PNG images")
        {
            IsRequired = true
        };

        var outputOption = new Option<string>(
            aliases: ["-o", "--output"],
            description: "Path to output folder for generated .mmux files")
        {
            IsRequired = true
        };

        var rootCommand = new RootCommand("MakaMek Unit Packager - Creates .mmux packages from unit data and images")
        {
            dataSourceOption,
            imageSourceOption,
            outputOption
        };

        rootCommand.SetHandler(async (dataSourcePath, imageSourcePath, outputPath) =>
        {
            try
            {
                await PackageUnits(dataSourcePath, imageSourcePath, outputPath);
            }
            catch (ArgumentException ex)
            {
                await Console.Error.WriteLineAsync($"Invalid argument: {ex.Message}");
                Environment.Exit(2);
            }
            catch (DirectoryNotFoundException ex)
            {
                await Console.Error.WriteLineAsync($"Directory not found: {ex.Message}");
                Environment.Exit(3);
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Error: {ex.Message}");
                Environment.Exit(1);
            }
        }, dataSourceOption, imageSourceOption, outputOption);

        return await rootCommand.InvokeAsync(args);
    }

    static async Task PackageUnits(string dataSourcePath, string imageSourcePath, string outputPath)
    {
        // Validate input paths
        if (!Directory.Exists(dataSourcePath))
        {
            throw new DirectoryNotFoundException($"Data source directory '{dataSourcePath}' does not exist.");
        }

        if (!Directory.Exists(imageSourcePath))
        {
            throw new DirectoryNotFoundException($"Image source directory '{imageSourcePath}' does not exist.");
        }

        // Create an output directory if it doesn't exist
        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
            Console.WriteLine($"Created output directory: {outputPath}");
        }

        await ProcessJsonFiles(dataSourcePath, imageSourcePath, outputPath);
    }

    static async Task ProcessJsonFiles(string dataSourcePath, string imageSourcePath, string outputPath)
    {
        var jsonFiles = Directory.GetFiles(dataSourcePath, "*.json", SearchOption.TopDirectoryOnly);
        
        if (jsonFiles.Length == 0)
        {
            Console.WriteLine($"No JSON files found in directory: {dataSourcePath}");
            return;
        }

        Console.WriteLine($"Found {jsonFiles.Length} JSON file(s) to process.");

        var successCount = 0;
        var errorCount = 0;

        foreach (var jsonFilePath in jsonFiles)
        {
            try
            {
                await ProcessSingleUnit(jsonFilePath, imageSourcePath, outputPath);
                successCount++;
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Error processing '{jsonFilePath}': {ex.Message}");
                errorCount++;
            }
        }

        Console.WriteLine($"Packaging completed. Success: {successCount}, Errors: {errorCount}");
        
        if (errorCount > 0)
        {
            Environment.Exit(1);
        }
    }

    static async Task ProcessSingleUnit(string jsonFilePath, string imageSourcePath, string outputPath)
    {
        Console.WriteLine($"Processing: {jsonFilePath}");
        
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

        try
        {
            // Read and parse the JSON file
            var jsonContent = await File.ReadAllTextAsync(jsonFilePath);

            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                throw new InvalidOperationException("JSON file is empty or contains only whitespace");
            }

            UnitData unitData;
            try
            {
                unitData = JsonSerializer.Deserialize<UnitData>(jsonContent, jsonOptions);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Invalid JSON format: {ex.Message}", ex);
            }

            // Validate required fields
            if (string.IsNullOrWhiteSpace(unitData.Chassis))
            {
                throw new InvalidOperationException("Unit data is missing required 'Chassis' field");
            }

            if (string.IsNullOrWhiteSpace(unitData.Model))
            {
                throw new InvalidOperationException("Unit data is missing required 'Model' field");
            }
            
            // Find corresponding image file
            var imageFilePath = FindImageFile(imageSourcePath, unitData.Chassis, unitData.Model);
            if (imageFilePath == null)
            {
                throw new FileNotFoundException($"No corresponding image file found for unit '{unitData.Chassis}' (Model: {unitData.Model})");
            }

            Console.WriteLine($"Found image: {Path.GetFileName(imageFilePath)}");

            // Create manifest
            var manifest = new ManifestData(
                version: ManifestVersion,
                unitId: unitData.Model,
                requiredMakaMekVersion: RequiredMakaMekVersion,
                author: "", // Should be provided in the future
                source: "" // Should be provided in the future
            );

            // Create the .mmux package
            await CreateMmuxPackage(unitData, imageFilePath, manifest, outputPath, jsonOptions);

            Console.WriteLine($"Successfully created: {unitData.Model}.mmux");
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException($"Access denied when processing file: {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"I/O error when processing file: {ex.Message}", ex);
        }
    }

    static string? FindImageFile(string imageSourcePath, string unitName, string model)
    {
        var pngFiles = Directory.GetFiles(imageSourcePath, "*.png", SearchOption.TopDirectoryOnly);

        if (pngFiles.Length == 0)
        {
            return null;
        }
        
        // Priority 0: Exact match - {Name}_{Model}.png
        var modelMatchPattern = $"{model}.png";
        var modelMatch = pngFiles.FirstOrDefault(file =>
            Path.GetFileName(file).Equals(modelMatchPattern, StringComparison.OrdinalIgnoreCase));
        if (modelMatch != null)
        {
            return modelMatch;
        }

        // Priority 1: Exact match - {Name}_{Model}.png
        var exactMatchPattern = $"{unitName}_{model}.png";
        var exactMatch = pngFiles.FirstOrDefault(file =>
            Path.GetFileName(file).Equals(exactMatchPattern, StringComparison.OrdinalIgnoreCase));
        if (exactMatch != null)
        {
            return exactMatch;
        }

        // Priority 2: Name only match - {Name}.png
        var nameMatchPattern = $"{unitName}.png";
        var nameMatch = pngFiles.FirstOrDefault(file =>
            Path.GetFileName(file).Equals(nameMatchPattern, StringComparison.OrdinalIgnoreCase));
        if (nameMatch != null)
        {
            return nameMatch;
        }

        // Priority 3: Prefix match - any PNG file starting with {Name}
        var prefixMatch = pngFiles.FirstOrDefault(file =>
            Path.GetFileName(file).StartsWith(unitName, StringComparison.OrdinalIgnoreCase) &&
            Path.GetFileName(file).EndsWith(".png", StringComparison.OrdinalIgnoreCase));

        return prefixMatch;
    }

    static async Task CreateMmuxPackage(UnitData unitData, string imageFilePath, ManifestData manifest, string outputPath, JsonSerializerOptions jsonOptions)
    {
        var mmuxFileName = $"{unitData.Model}.mmux";
        var mmuxFilePath = Path.Combine(outputPath, mmuxFileName);

        // Validate image file exists and is accessible
        if (!File.Exists(imageFilePath))
        {
            throw new FileNotFoundException($"Image file not found: {imageFilePath}");
        }

        // Create a temporary ZIP file first
        var tempZipPath = Path.Combine(Path.GetTempPath(), $"mmux_{Guid.NewGuid()}.tmp");

        try
        {
            using (var archive = ZipFile.Open(tempZipPath, ZipArchiveMode.Create))
            {
                // Add unit.json
                var unitJsonEntry = archive.CreateEntry("unit.json");
                await using (var entryStream = unitJsonEntry.Open())
                await using (var writer = new StreamWriter(entryStream))
                {
                    var unitJson = JsonSerializer.Serialize(unitData, jsonOptions);
                    await writer.WriteAsync(unitJson);
                }

                // Add unit.png
                var unitPngEntry = archive.CreateEntry("unit.png");
                await using (var entryStream = unitPngEntry.Open())
                {
                    try
                    {
                        await using var imageStream = File.OpenRead(imageFilePath);
                        await imageStream.CopyToAsync(entryStream);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        throw new InvalidOperationException(
                            $"Access denied reading image file '{imageFilePath}': {ex.Message}", ex);
                    }
                    catch (IOException ex)
                    {
                        throw new InvalidOperationException($"Error reading image file '{imageFilePath}': {ex.Message}",
                            ex);
                    }
                }

                // Add manifest.json
                var manifestEntry = archive.CreateEntry("manifest.json");
                await using (var entryStream = manifestEntry.Open())
                await using (var writer = new StreamWriter(entryStream))
                {
                    var manifestJson = JsonSerializer.Serialize(manifest, jsonOptions);
                    await writer.WriteAsync(manifestJson);
                }
            }

            // Move the temporary ZIP file to the final .mmux location
            if (File.Exists(mmuxFilePath))
            {
                try
                {
                    File.Delete(mmuxFilePath);
                }
                catch (UnauthorizedAccessException ex)
                {
                    throw new InvalidOperationException(
                        $"Cannot overwrite existing file '{mmuxFilePath}': {ex.Message}", ex);
                }
            }

            try
            {
                File.Move(tempZipPath, mmuxFilePath);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new InvalidOperationException($"Cannot create output file '{mmuxFilePath}': {ex.Message}", ex);
            }
            catch (IOException ex)
            {
                throw new InvalidOperationException($"Error creating output file '{mmuxFilePath}': {ex.Message}", ex);
            }
        }
        finally
        {
            // Clean up temporary file 
            if (File.Exists(tempZipPath))
            {
                try
                {
                    File.Delete(tempZipPath);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }
}
