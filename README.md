# Chronofoil.CLI

Chronofoil.CLI is a binary providing tools for working with Chronofoil capture files and similar formats.

## Basic Information

Prior to full release, Chronofoil was in a private release stage and the plugin itself was called ProtoLifestream. The
original name for Chronofoil was originally Lifestream, but by the time I began developing the final version of the
project, that name was already well-known and used by an existing plugin. The file format was not extensible or easy
to read in different programming languages, which prompted me to redesign the format and store the information
differently. The original capture file had no compression and was a flat sequential binary structure. There was also a
bug where every frame received by the game was written to the capture file multiple times based on how many packets 
were in that frame.

## Usage

`cfcli.exe [command] [options]`

### Chronofoil Commands (`cf`)

Commands for working with Chronofoil capture files (`.cfcap`, `.ccfcap`).

#### `raw`

Converts a `.cfcap` or `.ccfcap` file to a raw, flat binary format (`.rawcfcap`, `.rawccfcap`).

**Options:**

*   `--capture <file>`: Path to a single capture file.
*   `--directory <path>`: Path to a directory containing multiple capture files.
*   `-v`: Verbose output.

**Examples:**

*   `cfcli.exe cf raw --capture "C:\captures\my_capture.cfcap"`
*   `cfcli.exe cf raw --directory "C:\captures"`

#### `template`

Generates a template file for the raw capture format, for use with hex editors.

**Options:**

*   `-ksy/--kaitai`: Output a Kaitai Struct template (`.ksy`).
*   `-bt/--010editor`: Output a 010 Editor template (`.bt`).
*   `-hexpat/--imhex`: Output an ImHex template (`.hexpat`).
*   `<file>`: The name of the output file.

**Examples:**

*   `cfcli.exe cf template --kaitai "my_template.ksy"`
*   `cfcli.exe cf template --010editor "my_template.bt"`

### ProtoLifestream Commands (`pl`)

Commands for working with ProtoLifestream capture files (`.dat`).

#### `update`

Converts a `.dat` file to a `.cfcap` file. Since this capture format did not stop on logout, this command may produce
multiple capture files per `.dat` file. Please be *absolutely certain* that you do not run this command on the
same capture file multiple times, as this will generate duplicate output captures with different capture IDs.

**Options:**

*   `--capture <file>`: Path to a single `.dat` file.
*   `--directory <path>`: Path to a directory containing multiple `.dat` files.

**Examples:**

*   `cfcli.exe pl update --capture "C:\captures\my_capture.dat"`
*   `cfcli.exe pl update --directory "C:\captures"`