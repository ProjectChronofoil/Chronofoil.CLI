#pragma author perchbird
#pragma description chronofoil raw capture file
#pragma pattern_limit 2147483647
#pragma array_limit 2147483647
#pragma endian little

import std.io;
import std.sys;
import std.mem;

// chronofoil raw capture file

enum Protocol : u8
{
    Protocol_None = 0,
    Zone = 1,
    Chat = 2,
    Lobby = 3,
};

enum Direction : u8
{
    Direction_None = 0,
    Rx = 1,
    Tx = 2,
};

struct PacketHeader
{
    u32 size;
    u32 src;
    u32 dst;
    u16 type;
    u16 pad;
};

struct IpcHeader
{
    u16 unknown1;
    u16 type; // opcode
    u16 padding1;
    u16 serverId;
    u32 timestamp;
    u32 padding2;
};

struct Packet
{
    PacketHeader packetHeader;

    u32 dataSize = packetHeader.size - 16;
    
    if (packetHeader.type == 3) {
        IpcHeader ipcHeader;
        dataSize -= 16;
    }

    if (dataSize > 0)
        u8 data[dataSize];
};

struct Frame
{
    u8 prefix[16];
    s64 time;
    u32 size;
    u16 proto;
    u16 count;
    u8 version;
    u8 compression;
    u16 unknown;
    u32 decompressedLength;
    
    Packet packets[count];
};

struct CaptureFrameHeader
{
    Protocol protocol;
    Direction direction;
};

struct CaptureFrame
{
    CaptureFrameHeader header;
    Frame data;
};

CaptureFrame frames[while(!std::mem::eof())] @ 0x0;