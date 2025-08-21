using Chronofoil.CaptureFile.Binary.Capture;
using Chronofoil.CaptureFile.Binary.Packet;

namespace Chronofoil.CLI.ProtoLifestream.Capture;

public class CaptureFrame {
    public CaptureFrameHeader CaptureHeader;
    public FrameHeader Header;
    public CaptureFile.Binary.Packet.Packet[] Packets;
    
    public CaptureFrame(BinaryReader br) {
        CaptureHeader = new CaptureFrameHeader(br);
        Header = new FrameHeader(br);
        Packets = new CaptureFile.Binary.Packet.Packet[Header.Count];
        for (int i = 0; i < Packets.Length; i++)
            Packets[i] = new CaptureFile.Binary.Packet.Packet(br);
    }
}
