#nullable enable

using DarkScript3;
using SoulsFormats;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
    {
        config.AddCommand<CompileCommand>("compile");
        config.AddCommand<DecompileCommand>("decompile");
        config.AddCommand<PreviewCommand>("preview");
    });

app.Run(args);

class CompileCommand : Command<CompileCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[filePath]")]
        required public string FilePath { get; init; }

        [CommandArgument(1, "[outputPath]")]
        public string? OutputPath { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var options = new EventCFG.CFGOptions();
        var docs = new InstructionDocs("er-common.emedf.json");
        var eventScripter = new FancyEventScripter(new EventScripter(settings.OutputPath, docs), docs, options);
        var emevd = eventScripter.Pack(File.ReadAllText(settings.FilePath), settings.FilePath);
        emevd.Write(settings.OutputPath);
        return 0;
    }
}

class DecompileCommand : Command<DecompileCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[filePath]")]
        required public string FilePath { get; init; }

        [CommandArgument(1, "[outputPath]")]
        public string? OutputPath { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var options = new EventCFG.CFGOptions();
        var docs = new InstructionDocs("er-common.emedf.json");
        var eventScripter = new FancyEventScripter(new EventScripter(settings.FilePath, docs), docs, options);
        var decompiled = eventScripter.Unpack();
        if (settings.OutputPath is not null)
            File.WriteAllText(settings.OutputPath, decompiled);
        else
            Console.Write(decompiled);
        return 0;
    }
}


class PreviewCommand : Command<PreviewCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[filePath]")]
        required public string FilePath { get; init; }

        [CommandOption("--no-fancy")]
        public bool NoFancy { get; init; }
    }
    public override int Execute(CommandContext context, Settings settings)
    {
        var options = new EventCFG.CFGOptions();
        var docs = new InstructionDocs("er-common.emedf.json");
        var eventScripter = new EventScripter("dummy.emevd.dcx", docs, new EMEVD(EMEVD.Game.Sekiro));
        var fancyEventScripter = new FancyEventScripter(eventScripter, docs, options);
        fancyEventScripter.Pack(File.ReadAllText(settings.FilePath), settings.FilePath);
        if (!settings.NoFancy) {
            Console.Write(fancyEventScripter.Unpack());
        } else {
            Console.Write(eventScripter.Unpack());
        }
        return 0;
    }
}