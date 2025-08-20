using System.Reflection;
using System.Security.Cryptography;
using Chronofoil.CaptureFile;
using Chronofoil.CaptureFile.Binary;
using Chronofoil.CaptureFile.Binary.Packet;
using Chronofoil.CaptureFile.Generated;
using Chronofoil.CLI.ProtoLifestream.Capture;
using Google.Protobuf;
using CaptureFrame = Chronofoil.CLI.ProtoLifestream.Capture.CaptureFrame;
using Direction = Chronofoil.CaptureFile.Binary.Packet.Direction;
using RawCaptureReader = Chronofoil.CLI.ProtoLifestream.Capture.RawCaptureReader;

namespace Chronofoil.CLI.ProtoLifestream;

public class PLSConverter
{
    public bool ConvertCapture(string inputFullName)
    {
        var input = new FileInfo(inputFullName);
        var outputFolder = input.DirectoryName!;
        
        // Determine if the capture file starts at 256 + 28 or 254 + 28
        var startPos = GetFrameStartPos(input);
        
        // Determine how many captures are in this capture file
        var capturePositions = DetermineCapturePositions(input, startPos);
        
        Console.WriteLine($"file contains {capturePositions.Count} captures");
        
        var captureReader = new RawCaptureReader(input.OpenRead());
        Console.WriteLine($"first start time: {ToDateTime(captureReader.CaptureHeader.CaptureTime)}");
        
        var versionInfo = new VersionInfo
        {
            CaptureVersion = captureReader.FileHeader.CaptureVersion,
            Dx9Revision = (long) captureReader.FileHeader.Dx9GameRev,
            Dx11Revision = (long) captureReader.FileHeader.Dx11GameRev,
            Dx9Hash = ByteString.CopyFrom(captureReader.FileHeader.Dx9Hash),
            Dx11Hash = ByteString.CopyFrom(captureReader.FileHeader.Dx11Hash),
            GameVer =
            {
                captureReader.FileHeader.FfxivGameVer,
                captureReader.FileHeader.Ex1GameVer, 
                captureReader.FileHeader.Ex2GameVer,
                captureReader.FileHeader.Ex3GameVer, 
                captureReader.FileHeader.Ex4GameVer
            },
            WriterIdentifier = $"Chronofoil.CLI_{captureReader.FileHeader.PluginVersion}-{captureReader.FileHeader.CaptureVersion}",
            WriterVersion = Assembly.GetExecutingAssembly().GetName().Version!.ToString(),
        };

        var captureIndex = 0;
        ulong lastFrameTime = 0;

        for (captureIndex = 0; captureIndex < capturePositions.Count; captureIndex++)
        {
            var captureGuid = Guid.Empty;
            ulong captureStart = 0L;
            
            if (captureIndex == 0)
            {
                captureGuid = captureReader.CaptureHeader.CaptureId;
                captureStart = captureReader.CaptureHeader.CaptureTime;
            } else {
                captureGuid = Guid.NewGuid();
                captureStart = lastFrameTime;
            }
            
            var startDateTime = ToDateTime(captureStart);
            
            Console.WriteLine($"processing capture {captureIndex} with guid {captureGuid} start at {startDateTime} [{captureStart}]");
            var writer = new CaptureWriter(Path.Combine(outputFolder, captureGuid.ToString()));
            writer.WriteVersionInfo(versionInfo);
            writer.WriteCaptureStart(captureGuid, startDateTime);
            
            var stream = captureReader.BaseStream;
            stream.Seek(capturePositions[captureIndex], SeekOrigin.Begin);
            var reader = new BinaryReader(stream);
            
            var stopPos = capturePositions.Count > captureIndex + 1 ? capturePositions[captureIndex + 1] : stream.Length;

            var lastFrameHash = "";
            var duplicateCount = 0;
            var frameCount = 0;
            
            while (reader.BaseStream.Position < stopPos && reader.PeekInt16() != -1)
            {
                frameCount++;
                var prePosition = reader.BaseStream.Position;

                CaptureFrame frame;
                RawCaptureFrame rawFrame;
                try
                {
                    frame = new CaptureFrame(reader);
                    reader.BaseStream.Position = prePosition;
                    rawFrame = new RawCaptureFrame(reader);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"found a frame fragment, giving up. bytes left: {reader.BaseStream.Length - prePosition}");
                    break;
                }
                
                var frameHash = ComputeFrameHash(rawFrame);
                lastFrameTime = Math.Max(lastFrameTime, frame.Header.TimeValue);
                
                if (frameHash == lastFrameHash)
                {
                    duplicateCount++;
                    continue;
                }
                lastFrameHash = frameHash;
                
                var protocol = rawFrame.CaptureHeader.Protocol switch
                {
                    PacketProtocol.None => Protocol.None,
                    PacketProtocol.Zone => Protocol.Zone,
                    PacketProtocol.Chat => Protocol.Chat,
                    PacketProtocol.Lobby => Protocol.Lobby,
                    _ => throw new ArgumentOutOfRangeException()
                };
                
                var direction = rawFrame.CaptureHeader.Direction switch
                {
                    Direction.Rx => Chronofoil.CaptureFile.Generated.Direction.Rx,
                    Direction.Tx => Chronofoil.CaptureFile.Generated.Direction.Tx,
                    _ => Chronofoil.CaptureFile.Generated.Direction.None,
                };
                
                writer.AppendCaptureFrame(protocol, direction, rawFrame.Data.AsSpan());;
            }
            
            var endDateTime = ToDateTime(lastFrameTime);
            writer.WriteCaptureEnd(endDateTime, true);
            Console.WriteLine($"processed capture {captureIndex} with guid {captureGuid} end at {endDateTime} [{lastFrameTime}]. {duplicateCount} duplicates among {frameCount} frames");
        }
        
        return true;
    }
    
    private static string ComputeFrameHash(RawCaptureFrame frame)
    {
        var hash = SHA1.HashData(frame.Data);
        var str = Convert.ToHexString(hash);
        return str;
    }

    private long GetFrameStartPos(FileInfo input)
    {
        var reader = new RawCaptureReader(input.OpenRead());
        var stream = reader.BaseStream;
        var basePosition = stream.Position;
        var testFrame = new RawCaptureFrame(new BinaryReader(stream));
        if (testFrame.CaptureHeader.Protocol == PacketProtocol.None) {
            Console.WriteLine($"packet protocol was none!");
            return basePosition + 2;
        }

        if (testFrame.Data.Length > 17000) {
            Console.WriteLine($"packet was too large!");
            return basePosition + 2;
        }

        return basePosition;
    }
    
    private List<long> DetermineCapturePositions(FileInfo input, long startPos)
    {
        var capturePositions = new List<long> { startPos };

        var reader = new BinaryReader(input.OpenRead());
        reader.BaseStream.Seek(startPos, SeekOrigin.Begin);

        ulong recentTime = 0;
        var frameCount = 0;
        var lastFrameHash = "";
        
        while (reader.BaseStream.Position != reader.BaseStream.Length && reader.PeekInt16() != -1)
        {
            frameCount++;
            
            var prePosition = reader.BaseStream.Position;

            CaptureFrame frame;
            RawCaptureFrame rawFrame;
            try
            {
                frame = new CaptureFrame(reader);
                reader.BaseStream.Position = prePosition;
                rawFrame = new RawCaptureFrame(reader);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"found a frame fragment, giving up. bytes left: {reader.BaseStream.Length - prePosition}");
                break;
            }
            
            var frameHash = ComputeFrameHash(rawFrame);
            if (frameHash == lastFrameHash) continue;
            lastFrameHash = frameHash;

            // Ignore the one that happens 2 frames in
            if (frameCount < 3) continue;
            
            recentTime = Math.Max(recentTime, frame.Header.TimeValue);
            
            foreach (var packet in frame.Packets)
            {
                if (frame.CaptureHeader is { Protocol: PacketProtocol.Lobby, Direction: Direction.Rx } &&
                    packet.Header.Type == PacketType.KeepAlive)
                {
                    var time = ToDateTime(recentTime);
                    Console.WriteLine($"[{capturePositions.Count}] [{frameCount}] most recent time: {recentTime} {time}");
                    capturePositions.Add(prePosition);
                }
            }
        }
        
        var time1 = ToDateTime(recentTime);
        Console.WriteLine($"[{capturePositions.Count}] [{frameCount}] most recent time: {recentTime} {time1}");
        return capturePositions;
    }

    private static DateTime ToDateTime(ulong time)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds((long) time).UtcDateTime;
    }

    private static long FromDateTime(DateTime time)
    {
        return new DateTimeOffset(time, TimeSpan.Zero).ToUnixTimeMilliseconds();
    }
}
