#nullable enable

using System.Text.RegularExpressions;
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

        [CommandOption("--no-fancy")]
        public bool NoFancy { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var options = new EventCFG.CFGOptions();
        var docs = new InstructionDocs("er-common.emedf.json");
        var eventScripter = new EventScripter(settings.FilePath, docs);
        var fancyScripter = new FancyEventScripter(eventScripter, docs, options);
        var decompiled = settings.NoFancy
            ? eventScripter.Unpack()
            : fancyScripter.Unpack();
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
        if (!settings.NoFancy)
        {
            Console.Write(fancyEventScripter.Unpack());
        }
        else
        {
            Console.Write(eventScripter.Unpack());
        }
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
        var options = new EventCFG.CFGOptions();
        var docs = new InstructionDocs("er-common.emedf.json");
        var writer = new StringWriter();

        EMEDF DOC = docs.DOC;

        foreach (var enum_ in DOC.Enums)
        {
            if (enum_.DisplayName == "BOOL" || enum_.DisplayName == "ONOFF")
                continue;
            if (docs.EnumNamesForGlobalization.Contains(enum_.Name))
            {
                writer.WriteLine($"declare type {enum_.DisplayName} = number;");
                foreach (var (_, value) in enum_.DisplayValues)
                    writer.WriteLine($"declare const {value}: {enum_.DisplayName};");
                continue;
            };

            writer.WriteLine($"declare enum {enum_.DisplayName} {{");
            foreach (var (_, value) in enum_.Values) {
                string name = Regex.Replace(value, @"[^\w]", "");
                writer.WriteLine($"{name},");
            }
            if (enum_.ExtraValues is not null)
            {
                foreach (var (key, value) in enum_.ExtraValues)
                    writer.WriteLine($"{key},");
            }
            writer.WriteLine("}");
        }

        foreach (EMEDF.ClassDoc bank in DOC.Classes)
        {
            foreach (EMEDF.InstrDoc instr in bank.Instructions)
            {
                string funcName = instr.DisplayName;

                // TODO: Consider requiring all arg docs to be uniquely named in InstructionDocs.
                List<string> args = new();
                HashSet<string> argNames = new();

                foreach (var (argDoc, index) in instr.Arguments.Select((a, i) => (a, i)))
                {
                    var name = argDoc.DisplayName;
                    while (argNames.Contains(name)) name += "_";
                    argNames.Add(name);

                    var type = "number";
                    if (argDoc.EnumDoc is not null)
                        type = argDoc.EnumDoc.DisplayName;
                    if (argDoc.EnumName == "BOOL")
                        type = "boolean";
                    if (argDoc.Vararg)
                        args.Add($"...{name}: {type}[]");
                    else if (index > instr.Arguments.Count() - instr.OptionalArgs)
                        args.Add($"{name}?: {type}");
                    else
                        args.Add($"{name}: {type}");
                }
                string argList = string.Join(", ", args);
                writer.WriteLine($"declare function {funcName}({argList});");
            }

        }
        foreach (KeyValuePair<string, string> alias in docs.DisplayAliases)
        {
            writer.WriteLine($"declare const {alias.Key}: typeof {alias.Value};");
        }
        writer.Write(Resource.Text("script.d.ts"));
        Console.WriteLine(writer.ToString());
        return 0;
    }
}