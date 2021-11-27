using System;
using System.Collections.Generic;
using System.Globalization;

namespace GuitarProToMidi;

public class MidiExport
{
    public static System.Text.Encoding ascii = System.Text.Encoding.ASCII;

    public int fileType = 1;
    public int ticksPerBeat = 960;

    public List<MidiTrack> midiTracks = new List<MidiTrack>();
    public MidiExport(int fileType = 1, int ticksPerBeat = 960)
    {
        this.fileType = fileType;
        this.ticksPerBeat = ticksPerBeat;
    }

    public List<byte> createBytes()
    {
        List<byte> data = new List<byte>();
        data.AddRange(createHeader());
        foreach (MidiTrack track in midiTracks)
        {
            data.AddRange(track.createBytes());
        }

        return data;
    }

    public List<byte> createHeader()
    {
        List<byte> data = new List<byte>();

        List<byte> header = new List<byte>();
        header.AddRange(toBEShort(fileType));
        header.AddRange(toBEShort(midiTracks.Count));
        header.AddRange(toBEShort(ticksPerBeat));

        data.AddRange(writeChunk("MThd", header));

        return data;
    }

    public static List<byte> writeChunk(string name, List<byte> data)
    {
        List<byte> _data = new List<byte>();

        _data.AddRange(ascii.GetBytes(name));
        _data.AddRange(toBEULong(data.Count));
        _data.AddRange(data);
        return _data;
    }

    public static List<byte> toBEULong(int val)
    {
        List<byte> data = new List<byte>();
        byte[] LEdata = BitConverter.GetBytes((System.UInt32)val);

        for (int x = LEdata.Length - 1; x >= 0; x--)
        {
            data.Add(LEdata[x]);
        }

        return data;
    }

    public static List<byte> toBEShort(int val)
    {
        List<byte> data = new List<byte>();
        byte[] LEdata = BitConverter.GetBytes((System.Int16)val);

        for (int x = LEdata.Length - 1; x >= 0; x--)
        {
            data.Add(LEdata[x]);
        }

        return data;
    }

    public static List<byte> encodeVariableInt(int val)
    {
        if (val < 0) throw new FormatException("Variable int must be positive.");

        List<byte> data = new List<byte>();
        while (val > 0)
        {
            data.Add((byte)(val & 0x7f));
            val >>= 7;
        }

        if (data.Count > 0)
        {
            data.Reverse();
            for (int x = 0; x < data.Count - 1; x++)
            {
                data[x] |= 0x80;
            }
        }
        else
        {
            data.Add(0x00);
        }

        return data;
    }
}

public class MidiTrack
{
    public List<MidiMessage> messages = new List<MidiMessage>();
    public List<byte> createBytes()
    {
        List<byte> data = new List<byte>();
        byte runningStatusByte = 0x00;
        bool statusByteSet = false;
        foreach (MidiMessage message in messages)
        {
            if (message.time < 0)
            {
                message.time = 0;
            }
            data.AddRange(MidiExport.encodeVariableInt(message.time));
            if (message.type.Equals("sysex"))
            {
                statusByteSet = false;
                data.Add(0xf0);
                data.AddRange(MidiExport.encodeVariableInt(message.data.Length + 1));
                data.AddRange(message.data);
                data.Add(0xf7);
            }
            else
            {
                List<byte> raw = new List<byte>();
                raw = message.createBytes();

                byte temp = raw[0];
                if (statusByteSet && !message.is_meta && raw[0] < 0xf0 && raw[0] == runningStatusByte)
                {
                    raw.RemoveAt(0);
                    data.AddRange(raw);
                }
                else
                {
                    data.AddRange(raw);
                }
                runningStatusByte = temp;
                statusByteSet = true;
            }
        }

        return MidiExport.writeChunk("MTrk", data);
    }
}

public class MidiMessage
{
    public string type = "";
    public int time = 0;
    public bool is_meta = false;
    private byte code = 0x00;
    //MetaMessages:
    //#############
    //sequence_number 0x00
    public int number = 0;
    //text 0x01
    public string text = "";
    //copyright 0x02 -> text
    //track_name 0x03
    public string name = "";
    //instrument_name 0x04 -> track_name
    //lyrics 0x05 -> text
    //marker 0x06 -> text
    //cue_marker 0x07 -> text
    //device_name 0x08 -> track_name
    //channel_prefix 0x20
    public int channel = 0;
    //midi_port 0x21
    public int port = 0;
    //end_of_track 0x2f
    //set_tempo 0x51
    public int tempo = 500000;
    //smpte_offset 0x54 (Ignore)
    //public int frame_rate = 24; public int hours = 0; public int minutes = 0; public int seconds = 0; public int frames = 0; public int sub_frames = 0;
    //time_signature 0x58
    public int numerator = 4; public int denominator = 2; public int clocks_per_click = 24; public int notated_32nd_notes_per_beat = 8;
    //key_signature 0x59
    public int key = 0; bool is_major = true;

    //Messages:
    //#########
    //note_off 0x80 (channel, note, velocity)
    public int note = 0; public int velocity = 0;
    //note_on 0x90 (channel, note, velocity)
    //polytouch 0xa0 (channel, note, value)
    public int value = 0;
    //control_change 0xb0 (channel, control, value)
    public int control = 0;
    //program_change 0xc0 (channel, program)
    public int program = 0;
    //aftertouch 0xd0 (channel, value)
    //pitchwheel 0xe0 (channel, pitch)
    public int pitch = 0;
    //sysex 0xf0 (data)
    public byte[] data;

    //Others not needed..
    public MidiMessage(string type, string[] args, int time, byte[] data = null)
    {
        is_meta = false;
        this.type = type;
        this.time = time;

        //Meta Messages
        if (type.Equals("sequence_number")) { is_meta = true; code = 0x00; number = int.Parse(args[0], CultureInfo.InvariantCulture); }
        if (type.Equals("text") || type.Equals("copyright") || type.Equals("lyrics") || type.Equals("marker") || type.Equals("cue_marker"))
        {
            is_meta = true;
            text = args[0];
        }
        if (type.Equals("text")) code = 0x01;
        if (type.Equals("copyright")) code = 0x02;
        if (type.Equals("lyrics")) code = 0x05;
        if (type.Equals("marker")) code = 0x06;
        if (type.Equals("cue_marker")) code = 0x07;

        if (type.Equals("track_name") || type.Equals("instrument_name") || type.Equals("device_name"))
        {
            is_meta = true; code = 0x03;
            name = args[0];
        }
        if (type.Equals("instrument_name")) code = 0x04;
        if (type.Equals("device_name")) code = 0x08;

        if (type.Equals("channel_prefix")) { code = 0x20; channel = int.Parse(args[0], CultureInfo.InvariantCulture); is_meta = true; }
        if (type.Equals("midi_port")) { code = 0x21; port = int.Parse(args[0], CultureInfo.InvariantCulture); is_meta = true; }
        if (type.Equals("end_of_track")) { code = 0x2f; is_meta = true; }
        if (type.Equals("set_tempo")) { code = 0x51; tempo = int.Parse(args[0], CultureInfo.InvariantCulture); is_meta = true; }

        if (type.Equals("time_signature"))
        {
            is_meta = true; code = 0x58;
            numerator = int.Parse(args[0], CultureInfo.InvariantCulture);  //4
            denominator = int.Parse(args[1], CultureInfo.InvariantCulture); //4
            clocks_per_click = int.Parse(args[2], CultureInfo.InvariantCulture); //24
            notated_32nd_notes_per_beat = int.Parse(args[3], CultureInfo.InvariantCulture); //8
        }

        if (type.Equals("key_signature"))
        {
            is_meta = true; code = 0x59;
            key = int.Parse(args[0], CultureInfo.InvariantCulture);
            is_major = args[1].Equals("0"); //"0" or "1"
        }


        //Normal Messages
        if (type.Equals("note_off"))
        {
            code = 0x80;
            channel = int.Parse(args[0], CultureInfo.InvariantCulture);
            note = int.Parse(args[1], CultureInfo.InvariantCulture);
            velocity = int.Parse(args[2], CultureInfo.InvariantCulture);
        }

        if (type.Equals("note_on"))
        {
            code = 0x90;
            channel = int.Parse(args[0], CultureInfo.InvariantCulture);
            note = int.Parse(args[1], CultureInfo.InvariantCulture);
            velocity = int.Parse(args[2], CultureInfo.InvariantCulture);
        }

        if (type.Equals("polytouch"))
        {
            code = 0xa0;
            channel = int.Parse(args[0], CultureInfo.InvariantCulture);
            note = int.Parse(args[1], CultureInfo.InvariantCulture);
            value = int.Parse(args[2], CultureInfo.InvariantCulture);
        }

        if (type.Equals("control_change"))
        {
            code = 0xb0;
            channel = int.Parse(args[0], CultureInfo.InvariantCulture);
            control = int.Parse(args[1], CultureInfo.InvariantCulture);
            value = int.Parse(args[2], CultureInfo.InvariantCulture);
        }

        if (type.Equals("program_change"))
        {
            code = 0xc0;
            channel = int.Parse(args[0], CultureInfo.InvariantCulture);
            program = int.Parse(args[1], CultureInfo.InvariantCulture);
        }

        if (type.Equals("aftertouch"))
        {
            code = 0xd0;
            channel = int.Parse(args[0], CultureInfo.InvariantCulture);
            value = int.Parse(args[1], CultureInfo.InvariantCulture);
        }

        if (type.Equals("pitchwheel"))
        {
            code = 0xe0;
            channel = int.Parse(args[0], CultureInfo.InvariantCulture);
            pitch = int.Parse(args[1], CultureInfo.InvariantCulture);
        }

        if (type.Equals("sysex"))
        {
            code = 0xf0;
            this.data = data;
        }

    }

    public List<byte> createBytes()
    {
        List<byte> data;
        if (is_meta) data = createMetaBytes();
        else data = createMessageBytes();

        return data;
    }

    public List<byte> createMetaBytes()
    {
        List<byte> data = new List<byte>();

        if (type.Equals("sequence_number"))
        {
            data.Add((byte)(number >> 8));
            data.Add((byte)(number & 0xff));
        }
        if (type.Equals("text") || type.Equals("copyright") || type.Equals("lyrics") || type.Equals("marker") || type.Equals("cue_marker"))
        {
            if (text == null) text = "";
            data.AddRange(MidiExport.ascii.GetBytes(text));
        }

        if (type.Equals("track_name") || type.Equals("instrument_name") || type.Equals("device_name"))
        {
            data.AddRange(MidiExport.ascii.GetBytes(name));
        }
        if (type.Equals("channel_prefix"))
        {
            data.Add((byte)channel);
        }
        if (type.Equals("midi_port"))
        {
            data.Add((byte)port);
        }
        if (type.Equals("set_tempo"))
        {
            //return [tempo >> 16, tempo >> 8 & 0xff, tempo & 0xff]
            data.Add((byte)(tempo >> 16));
            data.Add((byte)((tempo >> 8) & 0xff));
            data.Add((byte)(tempo & 0xff));
        }

        if (type.Equals("time_signature"))
        {
            data.Add((byte)numerator);
            data.Add((byte)Math.Log(denominator, 2));
            data.Add((byte)clocks_per_click);
            data.Add((byte)notated_32nd_notes_per_beat);
        }

        if (type.Equals("key_signature"))
        {
            data.Add((byte)(key & 0xff));
            data.Add(is_major ? (byte)0x00 : (byte)0x01);
        }

        int dataLength = data.Count;
        data.InsertRange(0, MidiExport.encodeVariableInt(dataLength));
        data.Insert(0, code);
        data.Insert(0, 0xff);

        return data;
    }


    public List<byte> createMessageBytes()
    {

        List<byte> data = new List<byte>();
        /* if (type.Equals("note_off")) { code = 0x80; channel = int.Parse(args[0]); note = int.Parse(args[1]); velocity = int.Parse(args[2]); }
        if (type.Equals("note_on")) { code = 0x90; channel = int.Parse(args[0]); note = int.Parse(args[1]); velocity = int.Parse(args[2]); }
        if (type.Equals("polytouch")) { code = 0xa0; channel = int.Parse(args[0]); note = int.Parse(args[1]); value = int.Parse(args[2]); }
        if (type.Equals("control_change")) { code = 0xb0; channel = int.Parse(args[0]); control = int.Parse(args[1]); value = int.Parse(args[2]); }
        if (type.Equals("program_change")) { code = 0xc0; channel = int.Parse(args[0]); program = int.Parse(args[1]); }
        if (type.Equals("aftertouch")) { code = 0xd0; channel = int.Parse(args[0]); value = int.Parse(args[1]); }
        if (type.Equals("pitchwheel")) { code = 0xe0; channel = int.Parse(args[0]); pitch = int.Parse(args[1]); }
        if (type.Equals("sysex")) { code = 0xf0; this.data = data; }

         */
        if (type.Equals("note_off") || type.Equals("note_on"))
        {
            data.Add((byte)(code | (byte)channel));
            data.Add((byte)note); data.Add((byte)velocity);
        }

        if (type.Equals("polytouch"))
        {
            data.Add((byte)(code | (byte)channel));
            data.Add((byte)note); data.Add((byte)value);
        }
        if (type.Equals("control_change"))
        {
            data.Add((byte)(code | (byte)channel));
            data.Add((byte)control); data.Add((byte)value);
        }
        if (type.Equals("program_change"))
        {
            data.Add((byte)(code | (byte)channel));
            data.Add((byte)program);
        }
        if (type.Equals("aftertouch"))
        {
            data.Add((byte)(code | (byte)channel));
            data.Add((byte)value);
        }
        if (type.Equals("pitchwheel"))  //14 bit signed integer
        {
            data.Add((byte)(code | (byte)channel));
            //data.Add((byte)pitch);
            pitch -= -8192;
            data.Add((byte)(pitch & 0x7f));
            data.Add((byte)(pitch >> 7));
        }
        if (type.Equals("sysex"))
        {
            data.AddRange(this.data);
        }


        return data;
    }
}
