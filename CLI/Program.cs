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

    public record struct Arg(EMEDF.ArgDoc doc, string name, bool optional);
    public record struct Func(EMEDF.InstrDoc instr, string name, Arg[] args)
    {
        public Func Update(int skip = 0, int skipLast = 0, IEnumerable<string>? optionals = null, IEnumerable<string>? remove = null)
        {
            var optionalList = optionals?.ToList() ?? new();
            var removedList = remove?.ToList() ?? new();
            return this with
            {
                args = args
                    .Skip(skip)
                    .SkipLast(skipLast)
                    .Reverse()
                    .Where(arg =>
                    {
                        if (removedList.Remove(arg.doc.Name))
                            return false;
                        return true;
                    })
                    .Select(arg =>
                    {
                        if (optionalList.Remove(arg.doc.Name))
                            return arg with { optional = true };
                        return arg;
                    })
                    .Reverse()
                    .ToArray()
            };
        }

        string GetArgString(Arg arg)
        {
            var argDoc = arg.doc;
            var name = arg.name;

            var type = "number";
            if (argDoc.EnumDoc is not null)
                type = argDoc.EnumDoc.DisplayName;
            if (argDoc.EnumName == "BOOL")
                type = "boolean";
            if (argDoc.Vararg)
                return $"...{name}: {type}[]";
            else if (arg.optional)
                return $"{name}?: {type}";
            else
                return $"{name}: {type}";
        }

        public string Declare()
        {
            string argList = string.Join(", ", args.Select(GetArgString));
            return $"declare function {name}({argList})";
        }
    }

    Func GetFunc(EMEDF.InstrDoc instr)
    {
        HashSet<string> names = new();
        var args = instr.Arguments.Select((arg, index) =>
            {
                var name = arg.DisplayName;
                while (names.Contains(name)) name += "_";
                names.Add(name);
                return new Arg(arg, name, index >= instr.Arguments.Length - instr.OptionalArgs);
            }
        );
        return new Func(instr, instr.DisplayName, args.ToArray());
    }


    (int cls, int index) ParseInstrIndex(string str)
    {
        var match = Regex.Match(str, @"(\d+)\[(\d+)\]");
        if (!match.Success) throw new Exception($"Invalid instr {str}");
        var cls = int.Parse(match.Groups[1].Value);
        var index = int.Parse(match.Groups[2].Value);
        return (cls, index);
    }

    EMEDF.InstrDoc? GetInstr(EMEDF doc, int cls, int index) =>
        doc
        .Classes.Find(c => c.Index == cls)?
        .Instructions.Find(i => i.Index == index);

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
            foreach (var (_, value) in enum_.Values)
            {
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

        foreach (var bank in DOC.Classes)
            foreach (var instr in bank.Instructions)
                writer.WriteLine(GetFunc(instr).Declare());

        foreach (var alias in docs.DisplayAliases)
        {
            writer.WriteLine($"declare const {alias.Key}: typeof {alias.Value};");
        }

        writer.WriteLine("declare class Condition {}");
        var conditionData = ConditionData.ReadStream("conditions.json");
        foreach (var cond in conditionData.Conditions)
        {
            if (cond.Games is not null && !cond.Games.Contains("er")) continue;
            if (cond.Cond is null) continue;

            var (cls, index) = ParseInstrIndex(cond.Cond);
            var instr = GetInstr(docs.DOC, cls, index);
            if (instr is null) continue;

            var func = GetFunc(instr).Update(1, 0, cond.OptFields ?? new(), [cond.NegateField ?? ""]) with { name = cond.Name };
            writer.WriteLine(func.Declare());

            foreach (var sbool in cond.AllBools)
            {
                var boolFunc = func with { name = sbool.Name };
                if (sbool.Required is not null)
                    boolFunc = boolFunc.Update(remove: sbool.Required.Select(r => r.Field));
                writer.WriteLine(boolFunc.Declare() + ": Condition");
            }
        }

        var shorts = conditionData.Shorts;
        foreach (var @short in shorts)
        {
            if (@short.Games is not null && !@short.Games.Contains("er")) continue;

            var (cls, index) = ParseInstrIndex(@short.Cmd);
            var instr = GetInstr(docs.DOC, cls, index);
            if (instr is null) continue;


            var func = GetFunc(instr).Update(optionals: @short.OptFields);
            if (@short.Shorts is not null)
            {
                foreach (var shortVersion in @short.Shorts)
                {
                    var shortfunc = func.Update(remove: shortVersion.Required.Select(r => r.Field)) with
                    { name = shortVersion.Name };
                    writer.WriteLine(shortfunc.Declare());
                }
            }


            if (@short.Enable is not null)
            {
                var shortfunc = func.Update(skipLast: 1);
                writer.WriteLine((shortfunc with { name = "Enable" + @short.Enable }).Declare());
                writer.WriteLine((shortfunc with { name = "Disable" + @short.Enable }).Declare());
            }
        }


        writer.Write(Resource.Text("script.d.ts"));
        Console.WriteLine(writer.ToString());
        return 0;
    }
}