using Chronofoil.CaptureFile;

namespace Chronofoil.CLI;

public static class RawCaptureWriter
{
    public static void Write(CaptureReader reader, FileInfo output)
    {
        var outStream = output.OpenWrite();
        foreach (var frame in reader.GetFrames())
        {
            outStream.WriteByte((byte)frame.Header.Protocol);
            outStream.WriteByte((byte)frame.Header.Direction);
            outStream.Write(frame.Frame.ToByteArray(), 0, frame.Frame.Length);
            outStream.Flush();
        }

        outStream.Close();
    }
}