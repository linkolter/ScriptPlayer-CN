using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ScriptPlayer.Metadata
{
    public static class EroScriptsMetadataClient
    {
        private static readonly Regex TopicIdPattern = new Regex(@"/t/(?:[^/]+/)?(?<id>\d+)(?:/|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static async Task<NfoMetadata> FetchAsync(string topicUrl)
        {
            Uri uri;
            if (!Uri.TryCreate(topicUrl, UriKind.Absolute, out uri) ||
                !uri.Host.EndsWith("eroscripts.com", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("请输入有效的 EroScripts 主题网址。", nameof(topicUrl));

            Match match = TopicIdPattern.Match(uri.AbsolutePath);
            if (!match.Success)
                throw new ArgumentException("无法从网址中识别主题编号。请使用 /t/主题名/编号 格式的网址。", nameof(topicUrl));

            string apiUrl = "https://discuss.eroscripts.com/t/" + match.Groups["id"].Value + ".json";
            using (HttpClient client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(20);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("ScriptPlayer-NFO/1.0");
                string json = await client.GetStringAsync(apiUrl).ConfigureAwait(false);
                JObject topic = JObject.Parse(json);

                List<string> tags = new List<string>();
                JArray tagArray = topic["tags"] as JArray;
                if (tagArray != null)
                {
                    foreach (JToken token in tagArray)
                    {
                        string tag = token.Type == JTokenType.String
                            ? token.Value<string>()
                            : token.Value<string>("name") ?? token.Value<string>("slug");
                        if (!string.IsNullOrWhiteSpace(tag))
                            tags.Add(tag.Trim());
                    }
                }

                return new NfoMetadata
                {
                    Title = topic.Value<string>("title") ?? topic.Value<string>("fancy_title"),
                    OriginalTitle = topic.Value<string>("title"),
                    SourceUrl = topicUrl,
                    Type = "EroScripts",
                    Tags = tags.Distinct(StringComparer.InvariantCultureIgnoreCase).ToList()
                };
            }
        }
    }
}
