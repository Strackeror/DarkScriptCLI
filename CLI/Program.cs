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
        config.AddCommand<DeclareCommand>("declare");
        config.SetExceptionHandler((exception, _) =>
        {
            Console.WriteLine(exception.ToString());
            Environment.Exit(-1);
        });
    });

app.Run(args);

enum JsType
{
    Js,
    BasicJs,
    MattScript,
}

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

        [CommandOption("--js-type")]
        public JsType Type { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var options = new EventCFG.CFGOptions();
        var docs = new InstructionDocs("er-common.emedf.json");
        var eventScripter = new EventScripter(settings.FilePath, docs);
        var fancyScripter = new FancyEventScripter(eventScripter, docs, options);
        var decompiled = settings.Type switch
        {
            JsType.Js => fancyScripter.UnpackJs(),
            JsType.MattScript => fancyScripter.Unpack(),
            JsType.BasicJs => eventScripter.Unpack(),
            _ => throw new Exception("")
        };
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

        [CommandOption("--js-type")]
        public JsType Type { get; init; }
    }
    public override int Execute(CommandContext context, Settings settings)
    {
        var options = new EventCFG.CFGOptions();
        var docs = new InstructionDocs("er-common.emedf.json");
        var eventScripter = new EventScripter("dummy.emevd.dcx", docs, new EMEVD(EMEVD.Game.Sekiro));
        var fancyScripter = new FancyEventScripter(eventScripter, docs, options);
        fancyScripter.Pack(File.ReadAllText(settings.FilePath), settings.FilePath);

        var decompiled = settings.Type switch
        {
            JsType.MattScript => fancyScripter.Unpack(),
            JsType.Js => fancyScripter.UnpackJs(),
            JsType.BasicJs => eventScripter.Unpack(),
            _ => throw new Exception("")
        };
        Console.Write(decompiled);
        return 0;
    }
}
class DeclareCommand : Command<DeclareCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[filePath]")]
        required public string FilePath { get; init; }

    }
    public override int Execute(CommandContext context, Settings settings)
    {
        var docs = new InstructionDocs("er-common.emedf.json");
        var jsContext = JsContextGen.GenerateContext(docs.DOC, ConditionData.ReadStream("conditions.json"));
        var decls = JsContextGen.GenerateTsDecls(jsContext);
        decls += Resource.Text("declarations.d.ts");

        switch (settings.FilePath)
        {
            case string s:
                File.WriteAllText(s, decls);
                break;
            case null:
                Console.Write(decls);
                break;
        }
        return 0;
    }
}