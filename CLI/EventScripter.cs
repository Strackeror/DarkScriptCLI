using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8;
using SoulsFormats;
using static SoulsFormats.EMEVD;

namespace DarkScript3
{
    /// <summary>
    /// Packs and unpacks EMEVD files for a given game in a simple format.
    /// 
    /// To pack or unpack in the fancier format (can be detected by presence of $Event), use FancyEventScripter.
    /// </summary>
    public class EventScripter
    {
        private InstructionDocs docs;

        public readonly string EMEVDPath;
        public string JsFileName => $"{Path.GetFileName(EMEVDPath)}.js";
        public string EmevdFileName => $"{Path.GetFileName(EMEVDPath)}";
        public string EmevdFileDir => $"{Path.GetDirectoryName(EMEVDPath)}";

        public EMEVD EVD = new EMEVD();

        public EMELD ELD = new EMELD();

#if DEBUG
        private V8ScriptEngine v8 = new V8ScriptEngine(
            V8ScriptEngineFlags.EnableRemoteDebugging
            | V8ScriptEngineFlags.EnableDebugging
            //| V8ScriptEngineFlags.AwaitDebuggerAndPauseOnStart
        );
#else
        private V8ScriptEngine v8 = new V8ScriptEngine();
#endif

        // These are accessed from JS, in code below.
        // Also used for automatic skip amount calculation
        public int CurrentEventID = -1;
        public int CurrentInsIndex = -1;
        public string CurrentInsName = "";

        private List<string> LinkedFiles = new List<string>();

        public EventScripter(string file, InstructionDocs docs, EMEVD evd = null, string loadPath = null)
        {
            EMEVDPath = file;
            this.docs = docs;
            loadPath ??= file;
            EVD = evd ?? EMEVD.Read(loadPath);
            string emeldPath = loadPath.Replace(".emevd", ".emeld");
            if (File.Exists(emeldPath))
            {
                try
                {
                    ELD = EMELD.Read(emeldPath);
                }
                catch
                {
                }
            }
            InitAll();
        }


        /// <summary>
        /// Called by JS to add instructions to the event currently being edited.
        /// </summary>
        public Instruction MakeInstruction(Event evt, int bank, int index, int? at, object[] args)
        {
            CurrentEventID = (int)evt.ID;
            // TODO: Why is this done at the start? Nothing seems to use it, at least.
            CurrentInsIndex = evt.Instructions.Count + 1;

            try
            {
                EMEDF.InstrDoc doc = docs.DOC[bank][index];
                bool isVar = docs.IsVariableLength(doc);
                if (args.Length < doc.Arguments.Length)
                {
                    throw new Exception($"Instruction {bank}[{index}] ({doc.Name}) requires {doc.Arguments.Length} arguments, given {args.Length}.");
                }
                if (!isVar && args.Length > doc.Arguments.Length)
                {
                    throw new Exception($"Instruction {bank}[{index}] ({doc.Name}) given {doc.Arguments.Length} arguments, only permits {args.Length}.");
                }

                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] is bool)
                        args[i] = (bool)args[i] ? 1 : 0;
                    else if (args[i] is string argStr)
                    {
                        if (isVar)
                            throw new Exception("Event initializers cannot be dependent on parameters.");

                        var param = ScriptAst.EventParam.Parse(argStr);
                        if (param is null)
                            throw new Exception("Invalid parameter string: {" + args[i] + "}");

                        var (sourceStartByte, length) = param.Bytes;
                        int targetStartByte = docs.FuncBytePositions[doc][i];

                        Parameter p = new Parameter(at ?? evt.Instructions.Count, targetStartByte, sourceStartByte, length);
                        evt.Parameters.Add(p);
                        evt.Parameters = evt.Parameters.OrderBy(prm => prm.SourceStartByte).ToList();

                        args[i] = doc.Arguments[i].Default;
                    }
                }

                List<object> properArgs = new List<object>();
                if (isVar)
                {
                    foreach (object arg in args)
                    {
                        properArgs.Add(Convert.ToInt32(arg));
                    }
                }
                else
                {
                    for (int i = 0; i < doc.Arguments.Length; i++)
                    {
                        EMEDF.ArgDoc argDoc = doc.Arguments[i];
                        if (argDoc.Type == 0) properArgs.Add(Convert.ToByte(args[i])); //u8
                        else if (argDoc.Type == 1) properArgs.Add(Convert.ToUInt16(args[i])); //u16
                        else if (argDoc.Type == 2) properArgs.Add(Convert.ToUInt32(args[i])); //u32
                        else if (argDoc.Type == 3) properArgs.Add(Convert.ToSByte(args[i])); //s8
                        else if (argDoc.Type == 4) properArgs.Add(Convert.ToInt16(args[i])); //s16
                        else if (argDoc.Type == 5) properArgs.Add(Convert.ToInt32(args[i])); //s32
                        else if (argDoc.Type == 6) properArgs.Add(Convert.ToSingle(args[i])); //f32
                        else if (argDoc.Type == 8) properArgs.Add(Convert.ToUInt32(args[i])); //string position
                        else throw new Exception("Invalid type in argument definition.");
                    }
                }
                Instruction ins = new Instruction(bank, index, properArgs);
                if (at is not null && at < evt.Instructions.Count)
                    evt.Instructions[at.Value] = ins;
                else
                    evt.Instructions.Add(ins);
                CurrentEventID = -1;
                CurrentInsIndex = -1;
                return ins;
            }
            catch (Exception ex)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"EXCEPTION\nCould not write instruction at Event {CurrentEventID} at index {CurrentInsIndex}.\n");
                sb.AppendLine($"INSTRUCTION\n{CurrentInsName} | {bank}[{index}]\n");
                sb.AppendLine(ex.Message);
                throw new Exception(sb.ToString());
            }
        }

        /// <summary>
        /// Called by JS to edit an earlier instruction to skip to the current instruction's index.
        /// </summary>
        public void FillSkipPlaceholder(Event evt, int fillIndex)
        {
            int skipTarget = evt.Instructions.Count;
            if (evt == null || fillIndex < 0 || fillIndex >= skipTarget)
            {
                throw new Exception($"Invalid or unspecified skip placeholder index in Event {CurrentEventID} ({evt?.ID}) at index {CurrentInsIndex}");
            }
            // This is a bit fragile, we can't do much checking without maintaining more state.
            Instruction ins = evt.Instructions[fillIndex];
            // 99 as fill-in value in script.js. It is checked afterwards that all of these are filled.
            if (ins.ArgData.Length == 0 || ins.ArgData[0] != 99)
            {
                throw new Exception($"Unexpected instruction {InstructionDocs.InstrDebugString(ins)} in skip placeholder in Event {CurrentEventID}, from indices {fillIndex}->{skipTarget}");
            }
            // 0-line skip is from e.g. fillIndex = 5, to skipTarget = 6
            // 4-line skip (the entire event *after* the first instruction) is from fillIndex = 0 to skipTarget = 5
            int skipCount = skipTarget - fillIndex - 1;
            if (skipCount < 0 || skipCount > byte.MaxValue)
            {
                throw new Exception($"Skip too long in Event {CurrentEventID} from indices {fillIndex}->{skipTarget}, must be <256 lines. Use labels or split up the event.");
            }
            ins.ArgData[0] = (byte)skipCount;
        }

        public void FillGotoPlaceholder(Event evt, int fillIndex, byte labelIndex)
        {
            Instruction ins = evt.Instructions[fillIndex];
            if (ins.ArgData is not [99, ..])
                throw new Exception("Target instruction does not have expected placeholder");
            ins.ArgData[0] = labelIndex;
        }

        public int ConvertFloatToIntBytes(double input)
        {
            return BitConverter.ToInt32(BitConverter.GetBytes((float)input), 0);
        }

        /// <summary>
        /// Called by JS to add instructions to the event currently being edited.
        /// </summary>
        public Instruction MakeInstruction(Event evt, int bank, int index, int? at, uint layer, object[] args)
        {
            Instruction ins = MakeInstruction(evt, bank, index, at, args);
            ins.Layer = layer;
            return ins;
        }

        /// <summary>
        /// Sets up the JavaScript environment.
        /// </summary>
        private void InitAll()
        {
            v8.DocumentSettings.AccessFlags = DocumentAccessFlags.EnableFileLoading;
            v8.DocumentSettings.SearchPath = Path.GetDirectoryName(EMEVDPath);

            v8.AddHostObject("$$$_host", new HostFunctions());
            v8.AddHostObject("EVD", EVD);
            v8.AddHostType("EMEVD", typeof(EMEVD));
            v8.AddHostObject("Scripter", this);
            v8.AddHostType("EVENT", typeof(Event));
            v8.AddHostType("INSTRUCTION", typeof(Instruction));
            v8.AddHostType("PARAMETER", typeof(Parameter));
            v8.AddHostType("REST", typeof(Event.RestBehaviorType));
            v8.AddHostType("Console", typeof(Console));


            v8.Execute("script.js", Resource.Text("script.js"));
            v8.Execute("scriptconditions.js", Resource.Text("scriptconditions.js"));

            var context = JsContextGen.GenerateContext(docs.DOC, ConditionData.ReadStream("conditions.json"));
            var code = JsContextGen.GenerateContextJs(context);

#if DEBUG
            File.WriteAllText(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\script.generated.js", code);
#endif
            try
            {
                v8.Execute("script.generated.js", code.ToString());
            }
            catch (Exception ex) when (ex is IScriptEngineException scriptException)
            {
                throw new Exception($"Error processing js context: {scriptException.ErrorDetails}");
            }
        }

        public string EventName(long id)
        {
            var evt = ELD.Events.FirstOrDefault(e => e.ID == id);
            if (evt != null) return evt.Name;
            return null;
        }

        /// <summary>
        /// Executes the selected code to generate the EMEVD.
        /// 
        /// documentName should preferably be the simple name of a .js file, for reporting purposes.
        /// </summary>
        public EMEVD Pack(string code, string documentName)
        {
            EVD.Events.Clear();
            v8.DocumentSettings.Loader.DiscardCachedDocuments();
            try
            {
                DocumentInfo docInfo = new DocumentInfo(documentName) { Category = ModuleCategory.Standard };
                v8.Execute(docInfo, code);
            }
            catch (Exception ex) when (ex is IScriptEngineException scriptException)
            {
                throw JSScriptException.FromV8(scriptException);
            }
            return EVD;
        }

        /// <summary>
        /// Generates JS source code from the EMEVD.
        /// </summary>
        public string Unpack(bool compatibilityMode = false)
        {
            InitLinkedFiles();
            StringBuilder code = new StringBuilder();
            foreach (Event evt in EVD.Events)
            {
                UnpackEvent(evt, code, compatibilityMode);
            }
            return code.ToString();
        }

        public void UnpackEvent(Event evt, StringBuilder code, bool compatibilityMode = false)
        {
            CurrentEventID = (int)evt.ID;

            string id = evt.ID.ToString();
            string restBehavior = evt.RestBehavior.ToString();

            Dictionary<Parameter, string> paramNames = ParamNames(evt);
            IEnumerable<string> argNameList = paramNames.Values.Distinct();
            string evtArgs = string.Join(", ", argNameList);

            string eventName = EventName(evt.ID);
            if (eventName != null) code.AppendLine($"// {eventName}");
            code.AppendLine($"Event({id}, {restBehavior}, function({evtArgs}) {{");
            for (int insIndex = 0; insIndex < evt.Instructions.Count; insIndex++)
            {
                CurrentInsIndex = insIndex;
                Instruction ins = evt.Instructions[insIndex];
                EMEDF.InstrDoc doc = docs.DOC[ins.Bank]?[ins.ID];
                if (doc == null)
                {
#if DEBUG
                    // Partial mode
                    {
                        code.AppendLine(ScriptAst.SingleIndent + InstructionDocs.InstrDebugStringFull(ins, "Nodoc", insIndex, paramNames));
                        continue;
                    }
#endif
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine($@"Unable to read instruction at Event {CurrentEventID} at index {CurrentInsIndex}.");
                    sb.AppendLine($@"Unknown instruction id: {InstructionDocs.InstrDebugString(ins)}");
                    throw new Exception(sb.ToString());
                }
                string funcName = doc.DisplayName;

                List<object> args;
                try
                {
                    args = docs.UnpackArgsWithParams(ins, insIndex, doc, paramNames, (argDoc, val) => argDoc.GetDisplayValue(val), compatibilityMode);
                }
                catch (Exception ex)
                {
#if DEBUG
                    // Partial mode
                    {
                        code.AppendLine(ScriptAst.SingleIndent + InstructionDocs.InstrDebugStringFull(ins, "Baddoc", insIndex, paramNames));
                        continue;
                    }
#endif
                    var sb = new StringBuilder();
                    sb.AppendLine($@"Unable to unpack arguments for {funcName}({InstructionDocs.InstrDocDebugString(doc)}) at Event {CurrentEventID} at index {CurrentInsIndex}.");
                    sb.AppendLine($@"Instruction arg data: {InstructionDocs.InstrDebugString(ins)}");
                    sb.AppendLine(ex.Message);
                    throw new Exception(sb.ToString());
                }

                if (ins.Layer.HasValue)
                {
                    args.Add(InstructionDocs.LayerString(ins.Layer.Value));
                }

                string lineOfCode = $"{doc.DisplayName}({string.Join(", ", args)});";
                code.AppendLine(ScriptAst.SingleIndent + lineOfCode);
            }
            code.AppendLine("});");
            code.AppendLine("");

            CurrentInsIndex = -1;
            CurrentEventID = -1;
        }

        /// <summary>
        /// Sets up the list of linked files.
        /// </summary>
        private void InitLinkedFiles()
        {
            var reader = new BinaryReaderEx(false, EVD.StringData);
            if (docs.IsASCIIStringData)
            {
                foreach (long offset in EVD.LinkedFileOffsets)
                {
                    string linkedFile = reader.GetASCII(offset);
                    LinkedFiles.Add(linkedFile);
                }
            }
            else
            {
                foreach (long offset in EVD.LinkedFileOffsets)
                {
                    string linkedFile = reader.GetUTF16(offset);
                    LinkedFiles.Add(linkedFile);
                }
            }
        }

        /// <summary>
        /// Returns a dictionary containing the textual names of an event's parameters.
        /// </summary>
        public Dictionary<Parameter, string> ParamNames(Event evt)
        {
            Dictionary<long, List<Parameter>> paramValues = new Dictionary<long, List<Parameter>>();
            for (int i = 0; i < evt.Parameters.Count; i++)
            {
                Parameter prm = evt.Parameters[i];
                if (!paramValues.ContainsKey(prm.SourceStartByte))
                    paramValues[prm.SourceStartByte] = new List<Parameter>();

                paramValues[prm.SourceStartByte].Add(prm);
            }

            Dictionary<Parameter, string> paramNames = new Dictionary<Parameter, string>();
            int ind = 0;
            foreach (var kv in paramValues)
            {
                foreach (var p in kv.Value)
                {
                    paramNames[p] = $"X{p.SourceStartByte}_{p.ByteCount}";
                }
                ind++;
            }
            return paramNames;
        }
    }
}
