using System.IO;
using NLog;

namespace GuitarProToMidi
{
    public class GpFileParser
    {
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

        public void CreateMidiFile(string outputPath, bool overwrite)
        {
            var loader = File.ReadAllBytes(_filePath);
            //Detect Version by Filename
            var version = 7;
            var fileEnding = _extension;
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
                    var buffer = new byte[8200000];
                    var stream = new MemoryStream(buffer);
                    using (var unzip = new Unzip(_filePath))
                    {
                        unzip.Extract("Content/score.gpif", stream);
                        stream.Position = 0;
                        var sr = new StreamReader(stream);
                        var gp7Xml = sr.ReadToEnd();

                        _gpfile = new GP7File(gp7Xml);
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
            var data = midi.createBytes();
            var dataArray = data.ToArray();
            using var fs = new FileStream(outputPath ?? Path.Join(Path.GetDirectoryName(_filePath), $"{_title}.mid"),
                overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write);
            fs.Write(dataArray, 0, dataArray.Length);
        }
    }
}
