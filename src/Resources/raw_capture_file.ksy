meta:
  id: cfcap
  title: Chronofoil Capture File (Raw)
  file-extension: [rawcfcap, rawccfcap]
  endian: le
seq:
  - id: frames
    type: capture_frame
    repeat: eos
types:
  packet_header:
    seq:
      - id: size
        type: u4
      - id: src
        type: u4
      - id: dst
        type: u4
      - id: type
        type: u2
      - id: pad
        type: u2
  ipc_header:
    seq:
      - id: unknown1
        type: u2
      - id: type
        type: u2
        doc: Also known as "opcode"
      - id: pad1
        type: u2
      - id: server_id
        type: u2
      - id: timestamp
        type: u4
      - id: pad2
        type: u4
  packet:
    seq:
      - id: header
        type: packet_header
      - id: ipc_header
        type: ipc_header
        if: header.type == 3
      - id: data_no_ipc
        size: header.size - 16
        if: header.type != 3
      - id: data_ipc
        size: header.size - 32
        if: header.type == 3
  frame:
    seq:
      - id: prefix
        size: 16
      - id: time
        type: u8
      - id: size
        type: u4
      - id: protocol
        type: u2
      - id: count
        type: u2
      - id: version
        type: u1
      - id: compression
        type: u1
      - id: unknown
        type: u2
      - id: decompressed_length
        type: u4
      - id: packets
        type: packet
        repeat: expr
        repeat-expr: count
  capture_frame_header:
    seq:
      - id: protocol
        type: u1
        enum: protocol
      - id: direction
        type: u1
        enum: direction
  capture_frame:
    seq:
      - id: header
        type: capture_frame_header
      - id: data
        type: frame
enums:
  protocol:
    0: none
    1: zone
    2: chat
    3: lobby
  direction:
    0: none
    1: rx
    2: tx
