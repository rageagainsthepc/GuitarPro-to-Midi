# GuitarPro-to-Midi

GuitarPro-to-Midi is a cross-platform command line application for converting GuitarPro files to MIDI files.

## Features

- Reading GuitarPro 3 - 5 Files (based on the open python pyGuitarPro project)
- Reading GuitarPro 6 Files (using a simple bitwise compression and an xml structure with dictionary and ids)
- Reading GuitarPro 7 Files (packed like a normal zip-file and using a very large xml structure)
- Transferring all files into a common native format that saves all (and only) the information that are interesting for midi files. I.e. a lot of information like fingering or guitar amp preferences are ignored.
- Splitting to a secondary channel for certain effects
- Exporting to Midi, trying to simulate the sound as best as possible:
  Simulating:
    - Different types of harmonics
    - Strum patterns
    - Bending - as far as the midi standard allows
    - Trembar - "
    - Volume knob effects
    - Muted notes
    - Vibratos
    - and perhaps more..
 
 (I must mention that GuitarPro's native Midi export lacks far behind in this functionality!)
    
 Please enjoy and create some great software with this!

## Getting started

You can download a self-contained binary for Windows, MacOS and Linux in the release
section of this repository on github.

Usage: `GuitarProToMidi path/to/GuitarProFile.gp`

The above command should create a file `GuitarProFile.mid` in the same directory as
the input file.

### Build and run from source

- Install dotnet core SDK (preferably at least v5.0)
- Run `dotnet build` in the project root folder
- Run `dotnet run --project src/GuitarProToMidi.Console/GuitarProToMidi.csproj -- path/to/GuitarProFile.gp`
