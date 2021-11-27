using System;
using System.Globalization;
using System.IO;
using GuitarProToMidi;
using Xunit;

namespace GuitarProToMidi_UnitTests;

[Collection("GpFileParserUnitTests")]
public class GpFileParserUnitTests
{
    [Theory]
    [InlineData("data/test_gp3.gp3", "data/test_gp3.mid")]
    [InlineData("data/test_gp4.gp4", "data/test_gp4.mid")]
    [InlineData("data/test_gp5.gp5", "data/test_gp5.mid")]
    [InlineData("data/test_gp6.gpx", "data/test_gp6.mid")]
    [InlineData("data/test_gp7.gp", "data/test_gp7.mid")]
    public void CreateMidiFile_ValidGpFile_ValidMidiOutput(string inputFile, string expectedOutputFile)
    {
        var parser = new GpFileParser(inputFile);
        var expectedBytes = File.ReadAllBytes(expectedOutputFile);

        var outputBytes = parser.CreateMidiFile();

        Assert.Equal(expectedBytes, outputBytes);
    }
}

[Collection("GpFileParserUnitTests")]
public sealed class GpFileParserUnitTestsRussianLocale : IDisposable
{
    private readonly CultureInfo _oldCurrentCulture;

    public GpFileParserUnitTestsRussianLocale()
    {
        _oldCurrentCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("ru-RU");
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _oldCurrentCulture;
    }

    [Theory]
    [InlineData("data/test_gp3.gp3", "data/test_gp3.mid")]
    [InlineData("data/test_gp4.gp4", "data/test_gp4.mid")]
    [InlineData("data/test_gp5.gp5", "data/test_gp5.mid")]
    [InlineData("data/test_gp6.gpx", "data/test_gp6.mid")]
    [InlineData("data/test_gp7.gp", "data/test_gp7.mid")]
    public void CreateMidiFile_ValidGpFile_ValidMidiOutput(string inputFile, string expectedOutputFile)
    {
        var parser = new GpFileParser(inputFile);
        var expectedBytes = File.ReadAllBytes(expectedOutputFile);

        var outputBytes = parser.CreateMidiFile();

        Assert.Equal(expectedBytes, outputBytes);
    }
}
