using System.CommandLine;
using System.Reflection;
using System.Security.Cryptography;
using DotMake.CommandLine;
using Chronofoil.CaptureFile;
using Chronofoil.CaptureFile.Binary.Capture;
using Chronofoil.CaptureFile.Binary.Packet;
using Chronofoil.CaptureFile.Generated;
using Chronofoil.CLI.ProtoLifestream;
using Google.Protobuf;
using CaptureFrame = Chronofoil.CLI.ProtoLifestream.Capture.CaptureFrame;
using Direction = Chronofoil.CaptureFile.Binary.Packet.Direction;
using RawCaptureFrame = Chronofoil.CLI.ProtoLifestream.Capture.RawCaptureFrame;
using RawCaptureReader = Chronofoil.CLI.ProtoLifestream.Capture.RawCaptureReader;

namespace Chronofoil.CLI;

[CliCommand(Description = "cfcli - Tools for working with Chronofoil capture files and similar formats")]
public class RootCommand
{
}

[CliCommand(Alias = "cf", Description = "Commands for working with Chronofoil capture files", Parent = typeof(RootCommand))]
public class ChronofoilCommand
{
    
}

[CliCommand(Alias = "raw", Description = "Output a c/cfcap file to a rawcfcap file (unsupported flat binary representation)", Parent = typeof(ChronofoilCommand))]
public class CfRawCommand
{
    [CliOption(Name = "capture", Description = "Input dat file path")]
    public string InputFile { get; set; } = "";

    [CliOption(Name = "directory", Description = "Path to a folder containing captures")]
    public string InputDirectory { get; set; } = "";

    [CliOption(Alias = "v", Description = "Output info as writing occurs")]
    public bool? Verbose { get; set; } = false;

    public async Task RunAsync()
    {
        var fileSet = InputFile != "";
        var directorySet = InputDirectory != "";
        
        var noneSet = !fileSet && !directorySet;
        var bothSet = fileSet && directorySet;
        
        if (noneSet || bothSet)
        {
            await Console.Error.WriteLineAsync("Exactly one of --capture or --directory must be specified.");
            return;
        }
        
        var fileExists = File.Exists(InputFile);
        var directoryExists = Directory.Exists(InputDirectory);
        var verbose = Verbose ?? false;

        if (fileSet && !fileExists)
        {
            await Console.Error.WriteLineAsync($"Input file '{InputFile}' does not exist.");
            return;
        }

        if (directorySet && !directoryExists)
        {
            await Console.Error.WriteLineAsync($"Input directory '{InputDirectory}' does not exist.");
            return;
        }
        
        if (fileSet && fileExists) 
        {
            HandleSingle(new FileInfo(InputFile), verbose);
        }
        else if (directorySet && directoryExists) 
        {
            Console.WriteLine($"Converting all captures in input directory '{InputDirectory}' to raw format...");

            var cfcaps = Directory.GetFiles(InputDirectory, "*.cfcap");
            var ccfcaps = Directory.GetFiles(InputDirectory, "*.ccfcap");
            var files = cfcaps.Union(ccfcaps).ToArray();
            
            Console.WriteLine($"Found {files.Length} captures...");

            foreach (var file in files)
            {
                HandleSingle(new FileInfo(file), verbose);
            }
        }
    }

    private void HandleSingle(FileInfo input, bool verbose)
    {
        var outputPath = input.FullName.Replace(".cfcap", ".rawcfcap").Replace(".ccfcap", ".rawccfcap");
        var output = new FileInfo(outputPath);
        
        Console.WriteLine($"Converting Chronofoil capture file '{input.FullName}' to raw format '{output.FullName}'");
        
        if (output.Exists)
        {
            Console.WriteLine("Output file already exists. Overwrite? [y/N]");
            var response = Console.ReadLine();
            if (response != "y") return;
        }
        
        try
        {
            var reader = new CaptureReader(input.FullName);
            
            if (verbose)
            {
                Console.WriteLine($"Capture Version: {reader.VersionInfo.CaptureVersion}\n");
                Console.WriteLine($"Game Versions: {string.Join(", ", reader.VersionInfo.GameVer)}\n");
                Console.WriteLine($"Writer: {reader.VersionInfo.WriterIdentifier} v{reader.VersionInfo.WriterVersion}\n");
                Console.WriteLine($"Capture ID: {reader.CaptureInfo.CaptureId}\n");
                Console.WriteLine($"Start Time: {reader.CaptureInfo.CaptureStartTime}\n");
                Console.WriteLine($"End Time: {reader.CaptureInfo.CaptureEndTime}\n");
            }
            
            RawCaptureWriter.Write(reader, output);
            
            Console.WriteLine("Conversion completed successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during conversion: {ex.Message}");
            throw;
        }
    }
}

[CliCommand(Alias = "template", Description = "Output a template file for the raw format for use with certain hex editors.", Parent = typeof(ChronofoilCommand))]
public class CfTemplateCommand
{
    [CliOption(Name = "kaitai", Alias = "ksy", Description = "Get a Kaitai Struct template file.")]
    public bool Kaitai { get; set; }

    [CliOption(Name = "010editor", Alias = "bt", Description = "Get a 010 Editor template file.")]
    public bool Editor010 { get; set; }

    [CliOption(Name = "imhex", Alias = "hexpat", Description = "Get an ImHex template file.")]
    public bool ImHex { get; set; }

    [CliArgument(Name = "output", Description = "Output file name")]
    public string OutputFile { get; set; } = "";

    public async Task RunAsync()
    {
        var flags = new[] { Kaitai, Editor010, ImHex };
        if (flags.Count(f => f) != 1)
        {
            await Console.Error.WriteLineAsync("Exactly one of --kaitai, --010editor, or --imhex must be specified.");
            return;
        }

        var resourceName = "";
        if (Kaitai) resourceName = "raw_capture_file.ksy";
        if (Editor010) resourceName = "RawCaptureFile.bt";
        if (ImHex) resourceName = "RawCaptureFile.hexpat";

        var assembly = Assembly.GetExecutingAssembly();
        var resourceStream = assembly.GetManifestResourceStream($"Chronofoil.CLI.Resources.{resourceName}");

        if (resourceStream == null)
        {
            await Console.Error.WriteLineAsync($"Could not find embedded resource: {resourceName}");
            return;
        }

        using var reader = new StreamReader(resourceStream);
        var content = await reader.ReadToEndAsync();
        await File.WriteAllTextAsync(OutputFile, content);

        Console.WriteLine($"Wrote template to {OutputFile}");
    }
}

[CliCommand(Alias = "pl", Description = "Commands for working with ProtoLifestream capture files", Parent = typeof(RootCommand))]
public class ProtoLifestreamCommand
{
}

[CliCommand(Alias = "update", Description = "Output a dat file to a cfcap file, fixing any duplicate frames as necessary", Parent = typeof(ProtoLifestreamCommand))]
public class PlUpdateCommand
{
    [CliOption(Name = "capture", Description = "Input dat file path")]
    public string InputFile { get; set; } = "";

    [CliOption(Name = "directory", Description = "Path to a folder containing captures")]
    public string InputDirectory { get; set; } = "";

    public async Task RunAsync()
    {
        var fileSet = InputFile != "";
        var directorySet = InputDirectory != "";
        
        var noneSet = !fileSet && !directorySet;
        var bothSet = fileSet && directorySet;
        
        if (noneSet || bothSet)
        {
            await Console.Error.WriteLineAsync("Exactly one of --capture or --directory must be specified.");
            return;
        }
        
        var fileExists = File.Exists(InputFile);
        var directoryExists = Directory.Exists(InputDirectory);

        if (fileSet && !fileExists)
        {
            await Console.Error.WriteLineAsync($"Input file '{InputFile}' does not exist.");
            return;
        }

        if (directorySet && !directoryExists)
        {
            await Console.Error.WriteLineAsync($"Input directory '{InputDirectory}' does not exist.");
            return;
        }

        var converter = new PLSConverter();
        
        if (fileSet && fileExists) 
        {
            Console.WriteLine($"Converting input file '{InputFile}' to cfcap...");
            converter.ConvertCapture(InputFile);
        }
        else if (directorySet && directoryExists) 
        {
            Console.WriteLine($"Converting all captures in input directory '{InputDirectory}' to cfcap...");
            
            var files = Directory.GetFiles(InputDirectory, "*.dat");
            Console.WriteLine($"Found {files.Length} captures...");

            foreach (var file in files)
            {
                Console.WriteLine($"Converting input file '{file}' to cfcap...");
                converter.ConvertCapture(file);
            }
        }
    }
}

public static class Program
{
    public static async Task<int> Main(string[] args) => await Cli.RunAsync<RootCommand>(args);
}
