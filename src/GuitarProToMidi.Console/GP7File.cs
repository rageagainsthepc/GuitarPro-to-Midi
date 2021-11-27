using System.Collections.Generic;

namespace GuitarProToMidi;

public class GP7File : GPFile
{

    static string xml;
    public new List<Track> tracks;

    public GP7File(string data)
    {
        GPBase.pointer = 0;
        xml = data;
    }

    public override void readSong()
    {
        Node parsedXml = GP6File.ParseGP6(xml, 3);
        GP5File gp5file = GP6File.GP6NodeToGP5File(parsedXml.subnodes[0]);
        tracks = gp5file.tracks;
        self = gp5file;
        self.versionTuple = new int[] { 7, 0 };
    }
}
