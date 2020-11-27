using System.IO;
using NLog;

namespace GuitarProToMidi
{
    public class GpFileParser
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly string _filePath;
        private readonly string _extension;
        private GPFile _gpfile;

        public GpFileParser(string filePath)
        {
            _filePath = filePath;
            _extension = Path.GetExtension(filePath);
        }

        public byte[] CreateMidiFile()
        {
            var loader = File.ReadAllBytes(_filePath);

            switch (_extension)
            {
                case ".gp3":
                    _gpfile = new GP3File(loader);
                    _gpfile.readSong();
                    break;
                case ".gp4":
                    _gpfile = new GP4File(loader);
                    _gpfile.readSong();
                    break;
                case ".gp5":
                    _gpfile = new GP5File(loader);
                    _gpfile.readSong();
                    break;
                case ".gpx":
                    _gpfile = new GP6File(loader);
                    _gpfile.readSong();
                    _gpfile = _gpfile.self; //Replace with transferred GP5 file
                    break;
                case ".gp":
                    var stream = new MemoryStream();
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
            return song.toMidi().createBytes().ToArray();
        }
    }
}
