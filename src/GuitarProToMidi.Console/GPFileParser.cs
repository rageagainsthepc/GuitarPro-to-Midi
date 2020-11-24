using System.Collections.Generic;
using System.IO;
using NLog;

namespace GuitarProToMidi
{
    public class GpFileParser {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly string _title;
        private readonly string _filePath;
        private readonly string _extension;
        private GPFile _gpfile;

        public GpFileParser(string filePath)
        {
            _title = Path.GetFileNameWithoutExtension(filePath);
            _filePath = filePath;
            _extension = Path.GetExtension(filePath);
        }

        public void CreateMidiFile()
        {
            var loader = File.ReadAllBytes(_filePath);
            //Detect Version by Filename
            int version = 7;
            string fileEnding = _extension;
            if (fileEnding.Equals(".gp3")) version = 3;
            if (fileEnding.Equals(".gp4")) version = 4;
            if (fileEnding.Equals(".gp5")) version = 5;
            if (fileEnding.Equals(".gpx")) version = 6;
            if (fileEnding.Equals(".gp")) version = 7;

            switch (version)
            {
                case 3:
                    _gpfile = new GP3File(loader);
                    _gpfile.readSong();
                    break;
                case 4:
                    _gpfile = new GP4File(loader);
                    _gpfile.readSong();
                    break;
                case 5:
                    _gpfile = new GP5File(loader);
                    _gpfile.readSong();
                    break;
                case 6:
                    _gpfile = new GP6File(loader);
                    _gpfile.readSong();
                    _gpfile = _gpfile.self; //Replace with transferred GP5 file
                    break;
                case 7:
                    byte[] buffer = new byte[8200000];
                    MemoryStream stream = new MemoryStream(buffer);
                    using (var unzip = new Unzip(_filePath))
                    {
                        unzip.Extract("Content/score.gpif", stream);
                        stream.Position = 0;
                        var sr = new StreamReader(stream);
                        string gp7xml = sr.ReadToEnd();

                        _gpfile = new GP7File(gp7xml);
                        _gpfile.readSong();
                        _gpfile = _gpfile.self; //Replace with transferred GP5 file

                    }
                    break;
                default:
                    Logger.Error("Unknown File Format");
                    break;
            }
            Logger.Debug("Done");

            var song = new NativeFormat(_gpfile);
            var midi = song.toMidi();
            List<byte> data = midi.createBytes();
            var dataArray = data.ToArray();
            using (var fs = new FileStream(Path.Join(Path.GetDirectoryName(_filePath), $"{_title}.mid"), FileMode.OpenOrCreate, FileAccess.Write))
            {
                fs.Write(dataArray, 0, dataArray.Length);
            }
        }
    }
}
