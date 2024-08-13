using System.Text.RegularExpressions;
using DarkScript3;

#nullable enable

public static class JsContextGen
{
    public record struct Arg(EMEDF.ArgDoc doc, string name, string? @default)
    {
        public string Declare()
        {

            var type = "number";
            if (doc.EnumDoc is not null)
                type = doc.EnumDoc.DisplayName;
            if (doc.EnumName == "BOOL")
                type = "boolean | 0 | 1";
            if (doc.Vararg)
                return $"...{name}: {type}[]";
            else if (@default is not null)
                return $"{name}?: {type}";
            else
                return $"{name}: {type}";
        }

        public string JsArg()
        {
            if (doc.Vararg) return $"...{name}";
            if (@default is not null) return $"{name} = {@default}";
            return $"{name}";
        }

        public string Val()
        {
            if (doc.Vararg) return $"...{name}";
            if (@default is not null) return $"{@default}";
            return $"{name}";
        }
    }
    public record struct Func(EMEDF.ClassDoc cls, EMEDF.InstrDoc instr, string name, Arg[] args)
    {
        public Func Update(int skip = 0, int skipLast = 0, IEnumerable<(string name, string? @default)>? optionals = null, IEnumerable<string>? remove = null)
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
                        if (optionalList.FirstOrDefault(opt => opt.name == arg.doc.Name) is not (null, null) and var (name, @default))
                        {
                            optionalList.Remove((name!, @default));
                            return arg with { @default = @default };
                        }
                        return arg;
                    })
                    .Reverse()
                    .ToArray()
            };
        }

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


        public string Instruction()
        {
            string name = this.name;
            return $$"""
                function {{name}}({{Join(args, a => a.JsArg(), ", ")}}) {
                    Scripter.CurrentInsName = "{{name}}";
                    for (let arg of [{{Join(args, a => a.name, ", ")}}]) {
                        if (arg == void 0) throw `!!! Argument ${arg} in instruction "{{name}}" is undefined or missing.`
                    }
                    var ins = _Instruction({{cls.Index}}, {{instr.Index}}, [{{Join(args, a => a.name, ", ")}}]);
                    Scripter.CurrentInsName = "";
                }
                """;
        }
    }

    static Func GetFunc(EMEDF.ClassDoc classDoc, EMEDF.InstrDoc instr)
    {
        HashSet<string> names = new();
        var args = instr.Arguments.Select((arg, index) =>
            {
                var name = arg.DisplayName;
                while (names.Contains(name)) name += "_";
                names.Add(name);
                return new Arg(arg, name, index >= instr.Arguments.Length - instr.OptionalArgs ? "0" : null);
            }
        );
        return new Func(classDoc, instr, instr.DisplayName, args.ToArray());
    }

    static Func? GetFunc(EMEDF doc, string? str)
    {
        if (str is null) return null;
        var (cls, index) = ParseInstrIndex(str);
        var instr = doc[cls]?[index];
        if (instr is null) return null;

        return GetFunc(doc[cls], instr);
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

    public static string GenerateContextJs(InstructionDocs docs, ConditionData condData)
    {
        StringWriter writer = new();
        foreach (var cls in docs.DOC.Classes)
        {
            foreach (var instruction in cls.Instructions)
            {
                writer.WriteLine(GetFunc(cls, instruction).Instruction());
            }
        }

        foreach (var @enum in docs.DOC.Enums)
        {
            if (@enum.DisplayName == "BOOL" || @enum.DisplayName == "ONOFF")
                continue;
            var name = @enum.DisplayName;
            if (docs.EnumNamesForGlobalization.Contains(@enum.Name))
            {
                foreach (var (key, value) in @enum.DisplayValues)
                    writer.WriteLine($"const {value} = {key};");
                continue;
            };

            writer.WriteLine($"const {name} = {{}}");
            foreach (var (key, value) in @enum.Values)
            {
                var valueName = Regex.Replace(value, @"[^\w]", "");
                writer.WriteLine($"{name}.{valueName} = {key};");
            }
            if (@enum.ExtraValues is not null)
            {
                foreach (var (key, value) in @enum.ExtraValues)
                    writer.WriteLine($"{name}.{value} = {key};");
            }
        }

        foreach (var (key, value) in docs.DisplayAliases)
        {
            writer.WriteLine($"const {key} = {value};");
        }


        foreach (var @short in condData.Shorts)
        {
            if (@short.Games is not null && !@short.Games.Contains("er")) continue;

            var mbFunc = GetFunc(docs.DOC, @short.Cmd);
            if (mbFunc is not Func func) continue;

            if (@short.OptFields is not null)
                foreach (var opt in @short.OptFields)
                    func = func.Update(optionals: [(opt, "0")]);

            if (@short.Shorts is not null)
                foreach (var shortVersion in @short.Shorts)
                {
                    var call = func;
                    foreach (var req in shortVersion.Required)
                        call = call.Update(optionals: [(req.Field, req.Value.ToString())]);

                    var shortFunc = func with { name = shortVersion.Name };
                    shortFunc = shortFunc.Update(remove: shortVersion.Required.Select(r => r.Field));

                    writer.WriteLine($@"{shortFunc.JsFunc()} {{ {call.JsCall()} }}");
                }

            if (@short.Enable is not null)
            {
                foreach (var prefix in new[] { "Enable", "Disable" })
                {
                    var call = func with
                    {
                        args = func.args.SkipLast(1).Append(func.args.Last() with { @default = prefix + "d" }).ToArray()
                    };

                    var shortFunc = func with { args = func.args.SkipLast(1).ToArray(), name = prefix + @short.Enable };
                    writer.WriteLine($$"""
                                {{shortFunc.JsFunc()}} {
                                    return {{call.JsCall()}}
                                }
                                """);
                }

            }
        }

        foreach (var cond in condData.Conditions)
        {
            if (cond.Games is not null && !cond.Games.Contains("er")) continue;

            var instructions = new[] { cond.Cond, cond.Skip, cond.End, cond.Goto, cond.Wait };
            var funcs = instructions.Select(ins => GetFunc(docs.DOC, ins)).ToArray();

            int[] skips = [1, 1, 0, 1, 0];
            var maybeBaseFunc = funcs
                .Select((f, index) => f?.Update(skips[index]))
                .Where(f => f is not null)
                .FirstOrDefault();
            if (maybeBaseFunc is not Func func) continue;
            func = func with { name = cond.Name };

            writer.WriteLine(
                $$"""

                const Cond{{func.name}} = new ConditionType(
                    {{string.Join(",", funcs.Select(f => f?.name ?? "null"))}}
                );
                function {{func.name}}( {{Join(func.args, arg => arg.JsArg(), ", ")}}) {
                    return new Condition(Cond{{func.name}}, {{Join(func.args, arg => arg.Val(), ", ")}});
                }
                """
            );

            foreach (var @bool in cond.AllBools)
            {
                var call = func;
                if (@bool.Required is not null)
                    foreach (var req in @bool.Required)
                        call = call.Update(optionals: [(req.Field, req.Value.ToString())]);

                var boolFunc = func with { name = @bool.Name };
                boolFunc = boolFunc.Update(remove: @bool.Required?.Select(r => r.Field));
                writer.WriteLine($$"""
                                {{boolFunc.JsFunc()}} {
                                    return {{call.JsCall()}}
                                }
                                """);
            }
        }
        return writer.ToString();

    }

}