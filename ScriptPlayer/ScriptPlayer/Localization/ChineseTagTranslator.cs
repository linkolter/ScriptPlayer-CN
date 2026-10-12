using System;
using System.Collections.Generic;

namespace ScriptPlayer.Localization
{
    /// <summary>Keeps NFO tags compatible while presenting Chinese labels.</summary>
    public static class ChineseTagTranslator
    {
        private static readonly Dictionary<string, string> Translations =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "2b", "2B" }, { "2d", "2D" }, { "3d", "3D" }, { "4k", "4K" },
                { "action-based", "动作同步" }, { "ahegao", "高潮脸" }, { "ai", "AI 生成" },
                { "ai-video", "AI 视频" }, { "amateur", "素人" }, { "anal", "肛交" },
                { "anal-creampie", "肛内射精" }, { "animation", "动画" }, { "asian", "亚洲" },
                { "asmr", "ASMR" }, { "audio-based", "音频同步" }, { "audio-only", "仅音频" },
                { "bbc", "黑粗巨根" }, { "bdsm", "BDSM" }, { "beat-based", "节拍同步" },
                { "belle-delphine", "Belle Delphine" }, { "big-ass", "丰臀" },
                { "big-cock", "巨根" }, { "big-dick", "巨根" }, { "big-tits", "巨乳" },
                { "blonde", "金发" }, { "blowjob", "口交" }, { "blue-archive", "碧蓝档案" },
                { "bondage", "束缚" }, { "cartoon", "卡通" }, { "censored", "有码" },
                { "cgi", "CG 动画" }, { "challenge", "挑战" }, { "cheerleader", "拉拉队" },
                { "clothes-on", "穿衣" }, { "compilation", "合集" }, { "cosplay", "角色扮演" },
                { "cowgirl", "女上位" }, { "creampie", "内射" }, { "cum-in-mouth", "口内射精" },
                { "cum-on-tits", "乳房射精" }, { "cumshot", "射精" }, { "cumshots", "射精" },
                { "dance", "舞蹈" }, { "deepthroat", "深喉" }, { "dildo", "假阳具" },
                { "doggy", "后入" }, { "double-penetration", "双插" }, { "edging", "寸止" },
                { "egirl", "网络少女" }, { "facefuck", "强制口交" }, { "facial", "颜射" },
                { "fap-hero", "节奏挑战" }, { "fast", "高速" }, { "femdom", "女主导" },
                { "fleshlight", "飞机杯" }, { "footjob", "足交" }, { "foursome", "四人" },
                { "funny", "搞笑" }, { "funsr-pro", "FunSR Pro" }, { "furry", "兽人" },
                { "futanari", "扶她" }, { "game", "游戏" }, { "gangbang", "轮交" },
                { "genshin-impact", "原神" }, { "goon", "持续刺激" }, { "halloween", "万圣节" },
                { "handjob", "手交" }, { "harem", "后宫" }, { "hentai", "成人动画" },
                { "hmv", "动画音乐剪辑" }, { "honkai-star-rail", "崩坏：星穹铁道" },
                { "hybrid-based", "混合同步" }, { "interracial", "跨种族" }, { "ive", "IVE" },
                { "japanese", "日本" }, { "jav", "日本成人视频" }, { "joi", "指令互动" },
                { "league-of-legends", "英雄联盟" }, { "len-0-2", "0–2 分钟" },
                { "len-2-5", "2–5 分钟" }, { "len-5-10", "5–10 分钟" },
                { "len-10-25", "10–25 分钟" }, { "len-25-60", "25–60 分钟" },
                { "len-60-plus", "60 分钟以上" }, { "len-multiple", "多段时长" },
                { "live2d", "Live2D" }, { "loli", "萝莉" }, { "loop", "循环" },
                { "machine", "机器" }, { "masturbation", "自慰" }, { "mating-press", "交配式" },
                { "missionary", "传教士体位" }, { "mmd", "MMD" }, { "monstergirls", "魔物娘" },
                { "multi-axis", "多轴" }, { "music", "音乐" }, { "music-based", "音乐同步" },
                { "no-free-links", "无免费链接" }, { "non-porn", "非色情" }, { "non-vr", "非 VR" },
                { "oral-creampie", "口内射精" }, { "orgy", "群交" }, { "osr2", "OSR2" },
                { "overclocked", "超频" }, { "overwatch", "守望先锋" }, { "passthrough", "透视" },
                { "patreon", "Patreon" }, { "pmv", "真人音乐剪辑" }, { "pokemon", "宝可梦" },
                { "popper", "Poppers" }, { "poppers", "Poppers" }, { "pornstar", "成人影星" },
                { "pov", "第一视角" }, { "pregnant", "怀孕" }, { "rape", "强制" },
                { "real", "真人" }, { "reverse-cowgirl", "反向女上位" }, { "ride", "骑乘" },
                { "riding", "骑乘" }, { "rough-sex", "粗暴性爱" }, { "sfm", "SFM 动画" },
                { "shefinishesthejob", "女方收尾" }, { "shortstack", "矮小丰满" },
                { "small-tits", "小胸" }, { "softcore", "软色情" }, { "solo-female", "单女" },
                { "split-screen", "分屏" }, { "squirt", "潮吹" }, { "sr6", "SR6" },
                { "stockings", "丝袜" }, { "stomach-bulge", "腹部凸起" }, { "straight", "异性" },
                { "street-fighter", "街头霸王" }, { "subtitles", "字幕" }, { "succubus", "魅魔" },
                { "syncbot", "SyncBot" }, { "teen", "少女" }, { "tentacle", "触手" },
                { "the-handy", "The Handy" }, { "the-handy-2-pro", "Handy 2 Pro" },
                { "thicc", "丰满" }, { "thighjob", "腿交" }, { "threesome", "三人" },
                { "throatfuck", "喉交" }, { "throatpie", "深喉内射" }, { "tiktok", "抖音" },
                { "titfuck", "乳交" }, { "twist", "扭转轴" }, { "uncensored", "无码" },
                { "vaginal", "阴道" }, { "vam", "VAM" }, { "vertical-video", "竖屏" },
                { "vr", "VR" }, { "vr-stereo", "VR 立体" }, { "vtuber", "虚拟主播" },
                { "zenless-zone-zero", "绝区零" }
            };

        public static string Translate(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return string.Empty;
            string normalized = tag.Trim();
            return Translations.TryGetValue(normalized, out string translated) ? translated : normalized;
        }
    }
}
