using CUE4Parse_Replay;
using CUE4Parse.UE4.Readers;
using Newtonsoft.Json;
using Serilog;
using Spectre.Console;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Verbose()
    .WriteTo.Console()
    .CreateLogger();
    
var replayFile = AnsiConsole.Ask<string>(@"Path to [deepskyblue1].replay[/] file to dump. [dim]ex. C:\Users\xxx\AppData\Local\xxx\Saved\Demos\ReplayFile.replay[/]").Replace("\"", string.Empty).TrimEnd();

while (string.IsNullOrWhiteSpace(replayFile) || !File.Exists(replayFile))
{
    replayFile = AnsiConsole.Ask<string>("The provided file path is invalid. Please enter a valid path to the [dodgerblue2].replay[/] file. [dim]e.g., C:\\Users\\xxx\\AppData\\Local\\xxx\\Saved\\Demos\\ReplayFile.replay[/]").Replace("\"", string.Empty).TrimEnd();
}

var data = File.ReadAllBytes(replayFile);
var archive = new FByteArchive("Replay Data", data);

var replay = new FLocalFileNetworkReplayStreamer(archive);
var serializedData = JsonConvert.SerializeObject(replay, Formatting.Indented);

var exportPath = Path.Combine(Environment.CurrentDirectory, "Replay.json");
File.WriteAllText(exportPath, serializedData);

Log.Information("Replay data has been exported to: {0}", exportPath);