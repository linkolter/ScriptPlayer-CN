using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace ScriptPlayer.Shared.Scripts
{
    public class FunScriptLoader : ScriptLoader
    {
        public override List<ScriptAction> Load(Stream stream, FunScriptMetaData metaData)
        {
            using (StreamReader reader = new StreamReader(stream, new UTF8Encoding(false)))
            {
                string content = reader.ReadToEnd();
                var file = JsonConvert.DeserializeObject<FunScriptFile>(content);

                NormalizeActions(file.Actions, file.Inverted);

                if (file.MetaData != null && metaData != null)
                    file.MetaData.CopyTo(metaData);

                var actions = file.Actions.Cast<ScriptAction>().Where(a => a.TimeStamp >= TimeSpan.Zero).ToList();
                return actions;
            }
        }

        public override List<ScriptAction> Load(Stream stream)
        {
            return Load(stream, null);
        }

        public Dictionary<string, List<FunScriptAction>> LoadMultiAxis(string filename)
        {
            string content = File.ReadAllText(filename, new UTF8Encoding(false));
            FunScriptFile file = JsonConvert.DeserializeObject<FunScriptFile>(content);
            Dictionary<string, List<FunScriptAction>> result =
                new Dictionary<string, List<FunScriptAction>>(StringComparer.OrdinalIgnoreCase);

            AddAxis(result, "L0", file?.Actions, file?.Inverted ?? false);

            if (file?.Axes != null)
            {
                foreach (FunScriptAxis axis in file.Axes)
                    AddAxis(result, NormalizeAxisName(axis.Id ?? axis.Name), axis.Actions, file.Inverted);
            }

            if (file?.Channels != null)
            {
                foreach (KeyValuePair<string, FunScriptChannel> channel in file.Channels)
                    AddAxis(result, NormalizeAxisName(channel.Key), channel.Value?.Actions, file.Inverted);
            }

            return result;
        }

        private static void AddAxis(IDictionary<string, List<FunScriptAction>> result, string axis,
            IEnumerable<FunScriptAction> source, bool inverted)
        {
            if (string.IsNullOrWhiteSpace(axis) || source == null)
                return;

            List<FunScriptAction> actions = source
                .Where(action => action != null && action.TimeStamp >= TimeSpan.Zero)
                .Select(action => action.Duplicate())
                .OrderBy(action => action.TimeStamp)
                .ToList();
            NormalizeActions(actions, inverted);
            if (actions.Count > 0)
                result[axis] = actions;
        }

        private static void NormalizeActions(IEnumerable<FunScriptAction> actions, bool inverted)
        {
            if (actions == null)
                return;

            foreach (FunScriptAction action in actions)
            {
                byte position = (byte)Math.Min(99, (int)action.Position);
                action.Position = inverted ? (byte)(99 - position) : position;
            }
        }

        private static string NormalizeAxisName(string axis)
        {
            switch ((axis ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "l0":
                case "stroke": return "L0";
                case "l1":
                case "surge": return "L1";
                case "l2":
                case "sway": return "L2";
                case "r0":
                case "twist": return "R0";
                case "r1":
                case "roll": return "R1";
                case "r2":
                case "pitch": return "R2";
                case "a0":
                case "valve": return "A0";
                case "a1":
                case "suck": return "A1";
                case "a2":
                case "lube": return "A2";
                case "v0":
                case "vib":
                case "vibrate": return "V0";
                default: return null;
            }
        }

        public override List<ScriptFileFormat> GetSupportedFormats()
        {
            return new List<ScriptFileFormat>
            {
                new ScriptFileFormat("Fun Script", "funscript", "json")
            };
        }
    }
}
