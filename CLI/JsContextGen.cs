using System.Text.RegularExpressions;
using DarkScript3;
using Esprima.Ast;
using static System.Linq.Enumerable;

#nullable enable


public static class SelectWhereSelector
{
    public static IEnumerable<U> SelectWhere<T, U>(this IEnumerable<T> from, Func<T, U?> func) where U : struct =>
            from.Select(t => func(t))
                .Where(u => u is not null)
                .Select(u => u!.Value);
    public static IEnumerable<U> SelectWhere<T, U>(this IEnumerable<T> from, Func<T, U?> func) =>
            from.Select(t => func(t))
                .Where(u => u is not null)
                .Select(u => u!);
}

public static class JsContextGen
{
    static (string, string) NegatedValues(Arg arg, string? on, string? off)
    {
        if (on is null && off is null) return ("true", "false");
        var enumVals = arg.doc.EnumDoc.Values;
        string? KeyByVal(Func<string, bool> func) => enumVals.Where((pair) => func(pair.Value)).FirstOrDefault().Key;

        var onVal = KeyByVal(v => v == on);
        var offVal = off is null ? KeyByVal(v => v != on) : KeyByVal(v => v == off);
        if (onVal is null || offVal is null) throw new Exception("Failed to generate enum values for arg");
        return (onVal, offVal);
    }

    public record struct Arg(EMEDF.ArgDoc doc, string name, string? @default = null)
    {
        public string Declare()
        {

            var type = "number";
            if (doc.EnumDoc is not null)
                type = doc.EnumDoc.DisplayName;
            if (doc.EnumName == "BOOL")
                type = "boolean | 0 | 1";
            if (doc.Vararg)
                return $"...{name}: Arg<{type}>[]";
            else if (@default is not null)
                return $"{name}?: Arg<{type}>";
            else
                return $"{name}: Arg<{type}>";
        }

        public string DefaultValue() => doc.GetDisplayValue(doc.Default).ToString() ?? "";

        public string JsArg()
        {
            if (doc.Vararg) return $"...{name}";
            if (@default is not null) return $"{name} = {@default}";
            return $"{name}";
        }

        public string CallArg()
        {
            if (doc.Vararg) return $"...{name}";
            return name;
        }

        public string Val()
        {
            if (doc.Vararg) return $"...{name}";
            if (@default is not null) return @default;
            return $"{name}";
        }
    }
    public record struct Func(string name, Arg[] args)
    {

        public Func Map(IEnumerable<(string name, Func<Arg, Arg?> func)>? mappings)
        {
            var mapList = mappings?.ToList();
            if (mapList is null) return this;
            return this with
            {
                args = args
                    .SelectWhere(arg =>
                    {
                        if (mapList.FindIndex(map => map.name == arg.doc.Name) is int index and not -1)
                        {
                            var (_, func) = mapList.ElementAt(index);
                            mapList.RemoveAt(index);
                            return func(arg);
                        }
                        return arg;
                    })
                    .ToArray()
            };

        }

        public Func Map(IEnumerable<string>? fields, Func<Arg, Arg?> func) => Map(fields?.Select(f => (f, func)));
        public Func Map(string field, Func<Arg, Arg?> func) => Map([field], func);

        public Func Map(Func<Arg, Arg?> func) => this with { args = args.SelectWhere(a => func(a)).ToArray() };

        public Func Remove(int before = 0, int after = 0) => this with { args = args.Skip(before).SkipLast(after).ToArray() };



        public string Declare()
        {
            string argList = string.Join(", ", args.Select(a => a.Declare()));
            return $"declare function {name}({argList})";
        }

        public string JsFunc()
        {
            string argList = string.Join(", ", args.Select(a => a.JsArg()));
            return $"function {name}({argList})";
        }

        public string JsCall()
        {
            string argList = string.Join(", ", args.Select(a => a.Val()));
            return $"{name}({argList})";
        }
    }


    static Func GetFunc(EMEDF.InstrDoc instr, IEnumerable<EMEDF.ArgDoc>? args = null)
    {
        HashSet<string> names = new();
        args = args ?? instr.Arguments;
        var arglist = args.Select((arg, index) =>
            {
                var name = arg.DisplayName;
                while (names.Contains(name)) name += "_";
                names.Add(name);
                return new Arg(arg, name, index >= instr.Arguments.Length - instr.OptionalArgs ? arg.Default.ToString() : null);
            }
        );
        return new Func(instr.DisplayName, arglist.ToArray());
    }

    static Func? GetFunc(EMEDF doc, string? str)
    {
        if (str is null) return null;
        var (cls, index) = ParseInstrIndex(str);
        var instr = doc[cls]?[index];
        if (instr is null) return null;

        return GetFunc(instr);
    }


    static (int cls, int index) ParseInstrIndex(string str)
    {
        var match = Regex.Match(str, @"(\d+)\[(\d+)\]");
        if (!match.Success) throw new Exception($"Invalid instr {str}");
        var cls = int.Parse(match.Groups[1].Value);
        var index = int.Parse(match.Groups[2].Value);
        return (cls, index);
    }


    static string Join<T>(IEnumerable<T> values, Func<T, string> func, string join = "") =>
        string.Join(join, values.Select(func));

    public record struct Instruction(Func func, EMEDF.ClassDoc cls, EMEDF.InstrDoc ins);
    public record struct CondBool(Func func, Func call, string on, string off);
    public record struct CondCompare(Func func, Func call, int comparison, int rhs);
    public record struct Cond(
        Func baseFunc,
        Func?[] condFuncs,
        int? negateIndex,
        List<CondBool> bools,
        List<CondCompare> compares
    );

    public record struct Enum(string name, List<(string name, string value)> values, bool global);
    public record struct Context(
        List<Enum> enums,
        List<Instruction> instructions,
        List<(Func shorter, Func call)> shorts,
        List<Cond> conditions,
        List<(string, string)> aliases
    );

    static Func HandleOptFields(Func func, List<string>? optFields)
    {
        if (optFields is null) return func;
        var nfunc = func;
        nfunc.args = nfunc.args.ToArray();
        foreach (var i in Range(0, optFields.Count()))
        {
            var index = func.args.Count() - optFields.Count() + i;
            if (nfunc.args[index].doc.Name != optFields[i]) return func;
            nfunc.args[index].@default = func.args[index].DefaultValue();
        }
        return nfunc;
    }

    public static Context GenerateContext(EMEDF doc, ConditionData conditionData)
    {

        var instructions = doc.Classes
            .SelectMany(cls => cls.Instructions.Select(
                instr => new Instruction(GetFunc(instr), cls, instr)))
            .ToList();
        var globalEnums = doc.DarkScript.GlobalEnums ?? ["ON/OFF", "ON/OFF/CHANGE", "Condition Group", "Condition State", "Disabled/Enabled"];
        var enums = doc.Enums
            .Where(e => e.DisplayName is not "BOOL" and not "ONOFF")
            .Select(e => new Enum(
                    e.DisplayName,
                    e.Values.Select(doc => (Regex.Replace(doc.Value, @"[^\w]+", ""), doc.Key)).ToList(),
                    globalEnums.Contains(e.Name)
                )
            )
            .ToList();
        var aliases = doc.DarkScript.Aliases.Select(kv => (kv.Key, GetFunc(doc, kv.Value)!.Value.name)).ToList();

        Arg? MakePlaceholder(Arg arg) => arg with { @default = "void 0" };
        (string, Func<Arg, Arg?>) ReplaceArg(ConditionData.FieldValue field) =>
            (field.Field, arg => arg with { @default = field.Value.ToString() });

        var shorters = new List<(Func, Func)> { };
        foreach (var @short in conditionData.Shorts)
        {
            if (@short.Games is not null && !@short.Games.Contains("er")) continue;
            var maybeFunc = GetFunc(doc, @short.Cmd);
            if (maybeFunc is not Func func) continue;
            func = func.Map(arg => arg with { @default = null });

            foreach (var shortVersion in @short.Shorts ?? new())
            {
                var call = func;
                call = call.Map(shortVersion.Required.Select(ReplaceArg));
                var shortFunc = (func with { name = shortVersion.Name })
                    .Map(shortVersion.Required.Select(r => r.Field), arg => null);
                shortFunc = HandleOptFields(shortFunc, @short.OptFields);
                shorters.Add((shortFunc, call));
            }

            if (@short.Enable is not null)
                foreach (var state in new[] { "Enable", "Disable" })
                {
                    var shortFunc = func with { name = state + @short.Enable };
                    shortFunc.args = shortFunc.args.SkipLast(1).ToArray();

                    var call = func with { args = func.args.ToArray() };
                    var index = call.args.Count() - 1;
                    call.args[index].@default = state + "d";
                    shorters.Add((shortFunc, call));
                }
        }



        var conditions = new List<Cond>();
        foreach (var cond in conditionData.Conditions)
        {
            if (cond.Games is not null && !cond.Games.Contains("er")) continue;

            var condInstructions = new[] { cond.Cond, cond.Skip, cond.End, cond.Goto, cond.Wait };
            var funcs = condInstructions.Select(ins => GetFunc(doc, ins)).ToArray();

            int[] skips = [1, 1, 1, 1, 0];
            var maybeBaseFunc = funcs
                .Select((f, index) => (f, index))
                .Where(t => t.f is not null)
                .Select(t => t.f?.Remove(skips[t.index]))
                .FirstOrDefault();
            if (maybeBaseFunc is not Func func) continue;


            func = func.Map(arg => arg with { @default = null });
            func = func with { name = cond.Name };
            var condFunc = HandleOptFields(func, cond.OptFields);
            var negateIndex = Array.FindIndex(func.args, arg => arg.doc.Name == cond.NegateField);


            var bools = new List<CondBool>();
            foreach (var @bool in cond.AllBools)
            {
                if (cond.NegateField is null) throw new Exception("No negate field");
                var (on, off) = NegatedValues(func.args[negateIndex], @bool.True, @bool.False);

                var boolFunc = condFunc with { name = @bool.Name };
                boolFunc = boolFunc
                    .Map(@bool.Required?.Select(r => r.Field), arg => null)
                    .Map(@cond.NegateField!, arg => null);

                var call = func;
                call = call
                    .Map(@bool.Required?.Select(ReplaceArg))
                    .Map(cond.NegateField, arg => arg with { @default = on });

                bools.Add(new CondBool(boolFunc, call, on, off));
            }

            var comps = new List<CondCompare>();
            foreach (var comp in cond.AllCompares)
            {
                var compIndex = Array.FindIndex(func.args, arg => arg.doc.Name == "Comparison Type");
                var rhsIndex = Array.FindIndex(func.args, arg => arg.doc.Name == comp.Rhs);

                var compFunc = condFunc with { name = comp.Name };
                compFunc = compFunc
                    .Map("Comparison Type", arg => null)
                    .Map(comp.Rhs, arg => null);

                var call = func
                    .Map([comp.Rhs, "Comparison Type"], MakePlaceholder);
                comps.Add(new CondCompare(compFunc, call, compIndex, rhsIndex));
            }

            conditions.Add(new Cond(condFunc, funcs, negateIndex >= 0 ? negateIndex : null, bools, comps));
        }
        return new Context(enums, instructions, shorters, conditions, aliases);
    }

    public static string GenerateContextJs(Context context)
    {
        StringWriter writer = new();

        foreach (var @enum in context.enums)
        {
            var enumName = @enum.name;
            writer.WriteLine($$"""
                const {{enumName}} = {
                {{Join(@enum.values, v => $"  {v.name}: {v.value},\n")}}
                }
                """);
            if (@enum.global)
                foreach (var (name, value) in @enum.values)
                    writer.WriteLine($"const {name} = {enumName}.{name}");
        }

        foreach (var instruction in context.instructions)
        {
            var ((name, args), cls, instr) = instruction;
            writer.WriteLine($$"""
                 function {{name}}({{Join(args, a => a.JsArg(), ", ")}}) {
                     Scripter.CurrentInsName = "{{name}}";
                     for (let arg of [{{Join(args, a => a.name, ", ")}}]) {
                         if (arg == void 0) throw new Error(`!!! Argument ${arg} in instruction "{{name}}" is undefined or missing.`)
                     }
                     var ins = _Instruction({{cls.Index}}, {{instr.Index}}, [{{Join(args, a => a.CallArg(), ", ")}}]);
                     Scripter.CurrentInsName = "";
                 }
                 """);
        }

        foreach (var @short in context.shorts)
        {
            var (func, call) = @short;
            writer.WriteLine($@"{func.JsFunc()} {{ {call.JsCall()} }}");
        }

        foreach (var cond in context.conditions)
        {
            var condFunc = cond.baseFunc;
            var funcs = cond.condFuncs;
            writer.WriteLine(
                $$"""
                const Cond{{condFunc.name}} = new ConditionType(
                    {{string.Join(",", funcs.Select(f => f?.name ?? "null"))}}
                );
                function {{condFunc.name}}({{Join(condFunc.args, arg => arg.JsArg(), ", ")}}) {
                    return new Condition(
                        Cond{{condFunc.name}},
                        {{Join(condFunc.args, arg => arg.name + ", ")}}
                    )
                    {{(cond.negateIndex is null ? null
                        : $".withNegator(1, 0, {cond.negateIndex})"
                    )}}
                    ;
                    
                }
                """
            );

            foreach (var (boolFunc, call, on, off) in cond.bools)
            {
                writer.WriteLine($$"""
                {{boolFunc.JsFunc()}} {
                    return {{call.JsCall()}}
                        .withNegator({{on}}, {{off}}, {{cond.negateIndex}})
                }
                """);
            }

            foreach (var (compFunc, call, comp, rhs) in cond.compares)
            {
                writer.WriteLine($$"""
                    {{compFunc.JsFunc()}} {
                        return new Comparable({{call.JsCall()}}, {{comp}}, {{rhs}})
                    }
                    """);
            }
        }

        foreach (var (key, value) in context.aliases)
        {
            writer.WriteLine($"const {key} = {value};");
        }
        writer.WriteLine("const mainGroupAbuse = CondGroup(MAIN)");
        return writer.ToString();
    }

    public static string GenerateTsDecls(Context context)
    {
        StringWriter writer = new();
        foreach (var @enum in context.enums)
        {
            var enumName = @enum.name;
            writer.WriteLine($$"""
                declare enum {{enumName}} {
                {{Join(@enum.values, v => $"  {v.name} = {v.value},\n")}}
                }
                """);
            if (@enum.global)
                foreach (var (name, value) in @enum.values)
                    writer.WriteLine($"declare const {name}: {enumName}.{name}");
        }

        foreach (var instruction in context.instructions)
            writer.WriteLine(instruction.func.Declare());

        foreach (var shorter in context.shorts)
            writer.WriteLine(shorter.shorter.Declare());

        foreach (var cond in context.conditions)
        {
            writer.WriteLine(cond.baseFunc.Declare() + ": Condition");
            foreach (var @bool in cond.bools)
                writer.WriteLine(@bool.func.Declare() + ": Condition");
            foreach (var comp in cond.compares)
                writer.WriteLine(comp.func.Declare() + ": Comparable");
        }

        foreach (var (alias, aliased) in context.aliases)
            writer.WriteLine($"declare const {alias}: typeof {aliased}");

        return writer.ToString();
    }

}