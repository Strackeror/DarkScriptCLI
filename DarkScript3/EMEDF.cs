using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkScript3
{
    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class EMEDF
    {
        public ClassDoc this[int classIndex] => Classes.FirstOrDefault(c => c.Index == classIndex);

        [JsonProperty(PropertyName = "unknown", Order = 1)]
        private long UNK;

        [JsonProperty(PropertyName = "main_classes", Order = 2)]
        public List<ClassDoc> Classes { get; set; }

        [JsonProperty(PropertyName = "enums", Order = 3)]
        public EnumDoc[] Enums { get; set; }

        [JsonProperty(PropertyName = "darkscript", Order = 4)]
        public DarkScriptDoc DarkScript { get; set; }

        public static EMEDF ReadText(string input)
        {
            return JsonConvert.DeserializeObject<EMEDF>(input);
        }

        public static EMEDF ReadFile(string path)
        {
            string input = File.ReadAllText(path);
            return ReadText(input);
        }

        public static EMEDF ReadStream(string resource)
        {
            string input = Resource.Text(resource);
            return ReadText(input);
        }

        public class ClassDoc
        {
            [JsonProperty(PropertyName = "name", Order = 1)]
            public string Name { get; set; }

            [JsonProperty(PropertyName = "index", Order = 2)]
            public long Index { get; set; }

            [JsonProperty(PropertyName = "instrs", Order = 3)]
            public List<InstrDoc> Instructions { get; set; }

            public InstrDoc this[int instructionIndex] => Instructions.Find(ins => ins.Index == instructionIndex);
        }

        public class InstrDoc
        {
            [JsonProperty(PropertyName = "name", Order = 1)]
            public string Name { get; set; }

            [JsonProperty(PropertyName = "index", Order = 2)]
            public long Index { get; set; }

            [JsonProperty(PropertyName = "args", Order = 3)]
            public ArgDoc[] Arguments { get; set; }

            public ArgDoc this[uint i] => Arguments[i];

            // Calculated values

            [JsonIgnore]
            public string DisplayName { get; set; }

            // Number of optional args at the end.
            // Currently only implemented in MattScript.
            [JsonIgnore]
            public int OptionalArgs { get; set; }
        }

        public class ArgDoc
        {
            [JsonProperty(PropertyName = "name", Order = 1)]
            public string Name { get; set; }

            [JsonProperty(PropertyName = "type", Order = 2)]
            public long Type { get; set; }

            [JsonProperty(PropertyName = "enum_name", Order = 3, NullValueHandling = NullValueHandling.Include)]
            public string EnumName { get; set; }

            [JsonProperty(PropertyName = "default", Order = 4)]
            public long Default { get; set; }

            [JsonProperty(PropertyName = "min", Order = 5)]
            public long Min { get; set; }

            [JsonProperty(PropertyName = "max", Order = 6)]
            public long Max { get; set; }

            [JsonProperty(PropertyName = "increment", Order = 7)]
            public long Increment { get; set; }

            [JsonProperty(PropertyName = "format_string", Order = 8)]
            public string FormatString { get; set; }

            [JsonProperty(PropertyName = "unk1", Order = 9)]
            private long UNK1;

            [JsonProperty(PropertyName = "unk2", Order = 10)]
            private long UNK2;

            [JsonProperty(PropertyName = "unk3", Order = 11)]
            private long UNK3;

            [JsonProperty(PropertyName = "unk4", Order = 12)]
            private long UNK4;

            // These fields are not present in the original EMEDF

            // If an argument may be repeated zero or multiple times. Only used for display/documentation for the moment.
            // TODO: Move to DarkScript section?
            [JsonProperty(PropertyName = "vararg", Order = 99, DefaultValueHandling = DefaultValueHandling.Ignore)]
            public bool Vararg { get; set; }

            // Calculated values
            [JsonIgnore]
            public string DisplayName { get; set; }

            [JsonIgnore]
            public EnumDoc EnumDoc { get; set; }

            public object GetDisplayValue(object val) => EnumDoc == null ? val : EnumDoc.GetDisplayValue(val);
        }

        public class EnumDoc
        {
            [JsonProperty(PropertyName = "name", Order = 1)]
            public string Name { get; set; }

            // Map from integer value to EST enum name
            [JsonProperty(PropertyName = "values", Order = 2)]
            public Dictionary<string, string> Values { get; set; }

            // Calculated values

            [JsonIgnore]
            public string DisplayName { get; set; }

            // Map from integer value to full enum name, including DarkScript3-ification and DisplayName prefix
            [JsonIgnore]
            public Dictionary<string, string> DisplayValues { get; set; }

            // Map from display name (excluding prefix) to integer value
            [JsonIgnore]
            public Dictionary<string, int> ExtraValues { get; set; }

            public object GetDisplayValue(object val) => DisplayValues.TryGetValue(val.ToString(), out string reval) ? reval : val;
        }

        public class DarkScriptDoc
        {
            // Mapping from DarkScript-style name to string like 3[00], for backwards compatibility
            // e.g. Unknown420 -> 4[20]
            [JsonProperty(PropertyName = "aliases", Order = 1)]
            public Dictionary<string, string> Aliases { get; set; }

            // Mapping from enum display name to integer values. e.g. TeamType.Unknown15 -> 15
            [JsonProperty(PropertyName = "enum_aliases", Order = 2)]
            public Dictionary<string, int> EnumAliases { get; set; }

            // Apply a bunch of rules to EST names, mainly removing redundant (De) prefixes and Enable/Disable names
            [JsonProperty(PropertyName = "replacements", Order = 3)]
            public Dictionary<string, string> Replacements { get; set; }

            // Custom enum globalization list.
            [JsonProperty(PropertyName = "global_enums", Order = 4)]
            public List<string> GlobalEnums { get; set; }
        }
    }
}
