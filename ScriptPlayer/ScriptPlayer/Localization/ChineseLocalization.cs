using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Automation;
using System.Windows.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;

namespace ScriptPlayer.Localization
{
    /// <summary>
    /// Translates the existing WPF user interface without changing persisted values,
    /// file formats or device/API identifiers. This keeps the localization isolated
    /// from ScriptPlayer's playback logic and also covers dialogs supplied by controls.
    /// </summary>
    public static class ChineseLocalization
    {
        private static readonly Dictionary<string, string> Strings =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Command names are generated at runtime for the input-mapping page.
                { "Add E-Stim Audio Device", "添加电刺激音频设备" },
                { "Add File To Playlist (first)", "添加文件到播放列表（最前）" },
                { "Add File To Playlist (last)", "添加文件到播放列表（最后）" },
                { "Add Folder To Playlist (first)", "添加文件夹到播放列表（最前）" },
                { "Add Folder To Playlist (last)", "添加文件夹到播放列表（最后）" },
                { "Connect Buttplug", "连接 Buttplug" },
                { "Connect Launch Directly", "直接连接 Launch" },
                { "Decrease Audio Delay", "减少音频延迟" },
                { "Decrease Filter Range", "缩小过滤范围" },
                { "Decrease Lower Range", "降低范围下限" },
                { "Decrease Max Speed", "降低最大速度" },
                { "Decrease Min Speed", "降低最小速度" },
                { "Decrease Pattern Speed", "降低模式速度" },
                { "Decrease Playback Rate", "降低播放速度" },
                { "Decrease Range Extender", "减小范围扩展" },
                { "Decrease Script Delay", "减少脚本延迟" },
                { "Decrease Upper Range", "降低范围上限" },
                { "Disconnect Buttplug", "断开 Buttplug" },
                { "Enter Fullscreen", "进入全屏" },
                { "Exit Fullscreen", "退出全屏" },
                { "Generate All for loaded Video", "为已加载视频生成全部内容" },
                { "Generate Heatmap for loaded Video", "为已加载视频生成热力图" },
                { "Generate Preview GIF for loaded Video", "为已加载视频生成预览 GIF" },
                { "Generate Thumbnail Banner for loaded Video", "为已加载视频生成缩略图横幅" },
                { "Generate Thumbnails for loaded Video", "为已加载视频生成缩略图" },
                { "Go back 5 seconds", "后退 5 秒" },
                { "Go forwards 5 seconds", "前进 5 秒" },
                { "Increase Audio Delay", "增加音频延迟" },
                { "Increase Filter Range", "扩大过滤范围" },
                { "Increase Lower Range", "提高范围下限" },
                { "Increase Max Speed", "提高最大速度" },
                { "Increase Min Speed", "提高最小速度" },
                { "Increase Pattern Speed", "提高模式速度" },
                { "Increase Playback Rate", "提高播放速度" },
                { "Increase Range Extender", "增大范围扩展" },
                { "Increase Script Delay", "增加脚本延迟" },
                { "Increase Upper Range", "提高范围上限" },
                { "Move to default folder", "移动到默认文件夹" },
                { "Move to default folder, remove from playlist", "移动到默认文件夹并从播放列表移除" },
                { "Move to default folder, remove from playlist, play next", "移动到默认文件夹、从播放列表移除并播放下一项" },
                { "Move to recycle bin, play next", "移至回收站并播放下一项" },
                { "Mute", "静音" },
                { "Next Pattern", "下一个模式" },
                { "Pause", "暂停" },
                { "Play", "播放" },
                { "Play next playlist entry", "播放列表下一项" },
                { "Play previous playlist entry", "播放列表上一项" },
                { "Previous Pattern", "上一个模式" },
                { "Save Script As", "脚本另存为" },
                { "Select Next Saved Range", "选择下一个已保存范围" },
                { "Select Previous Saved Range", "选择上一个已保存范围" },
                { "Set Command Source None", "设置命令源：无" },
                { "Set Command Source Pattern", "设置命令源：模式" },
                { "Set Command Source Random", "设置命令源：随机" },
                { "Set Command Source Video", "设置命令源：视频" },
                { "Set Playback Mode Blind", "设置播放模式：盲操" },
                { "Set Playback Mode GoPro VR", "设置播放模式：GoPro VR" },
                { "Set Playback Mode HereSphere", "设置播放模式：HereSphere" },
                { "Set Playback Mode Kodi", "设置播放模式：Kodi" },
                { "Set Playback Mode Local", "设置播放模式：本地" },
                { "Set Playback Mode MPC", "设置播放模式：MPC" },
                { "Set Playback Mode SamsungVR", "设置播放模式：SamsungVR" },
                { "Set Playback Mode VLC", "设置播放模式：VLC" },
                { "Set Playback Mode Whirligig", "设置播放模式：Whirligig" },
                { "Set Playback Mode ZoomPlayer", "设置播放模式：ZoomPlayer" },
                { "Show Settings", "显示设置" },
                { "Skip To Next Event", "跳到下一个事件" },
                { "Start Buttplug", "启动 Buttplug" },
                { "Start Scanning Buttplug", "开始扫描 Buttplug" },
                { "Toggle Play / Pause", "切换播放／暂停" },
                { "Toggle Source Video/None", "切换命令源：视频／无" },
                { "Toggle Source Video/Pattern", "切换命令源：视频／模式" },
                { "Volume Down", "降低音量" },
                { "Volume Up", "提高音量" },
                { "About / Check for new version", "关于 / 检查新版本" },
                { "Add another shortcut for this command", "为此命令添加另一个快捷键" },
                { "Add Bookmark", "添加书签" },
                { "Add e-stim audio device (experimental)", "添加电刺激音频设备（实验性）" },
                { "Add file to playlist", "添加文件到播放列表" },
                { "Add Files (first)", "添加文件（最前）" },
                { "Add Files (last)", "添加文件（最后）" },
                { "Add Folder (first)", "添加文件夹（最前）" },
                { "Add Folder (last)", "添加文件夹（最后）" },
                { "Add folder to Playlist", "添加文件夹到播放列表" },
                { "Add funstim audio device", "添加 Funstim 音频设备" },
                { "Add MK312 Serial Device", "添加 MK312 串口设备" },
                { "Add MK312 Wifi Device", "添加 MK312 Wi-Fi 设备" },
                { "After connecting to Buttplug:", "连接 Buttplug 后：" },
                { "After ScriptPlayer has started:", "ScriptPlayer 启动后：" },
                { "All", "全部" },
                { "Attributions", "致谢与许可" },
                { "Audio Delay", "音频延迟" },
                { "Audio Device", "音频设备" },
                { "Audio Device Options", "音频设备选项" },
                { "Auto Reload Script", "自动重新加载脚本" },
                { "Automatic", "自动" },
                { "Automatically generate:", "自动生成：" },
                { "Auto-skip gaps", "自动跳过空段" },
                { "\"Soft Seek\" blends two videos slowly into each other to smooth transistions", "“平滑跳转”会让两个视频缓慢衔接，以减少切换时的突兀感。" },
                { "A \"gap\" is whenever two script actions are more than 10 seconds appart.", "当两条脚本动作之间相隔超过 10 秒时，即视为一个空段。" },
                { "Beat Conversion Mode", "节拍转换模式" },
                { "Behavior when scripts can't be loaded:", "脚本无法加载时：" },
                { "Blind Mode", "盲操模式" },
                { "Blur Video", "模糊视频" },
                { "Bookmarks", "书签" },
                { "Bottom", "底部" },
                { "Buttplug / Intiface", "Buttplug / Intiface" },
                { "Cancel", "取消" },
                { "Chapter Mode:", "章节模式：" },
                { "Check Again (Include Pre-Releases)", "再次检查（包括预发行版）" },
                { "Check for new version on startup", "启动时检查新版本" },
                { "Check Help / About for details", "详情请查看“帮助 / 关于”" },
                { "Choose", "选择" },
                { "Clear", "清除" },
                { "Clear Playlist", "清空播放列表" },
                { "Clear Script", "清除脚本" },
                { "Close", "关闭" },
                { "Close when done", "完成后关闭" },
                { "Columns", "列数" },
                { "Command", "命令" },
                { "Comment", "备注" },
                { "Compact Controls", "紧凑控制栏" },
                { "Connect", "连接" },
                { "Connect to Buttplug", "连接 Buttplug" },
                { "Connect to Handy directly", "直接连接 Handy" },
                { "Connection Key", "连接密钥" },
                { "Connection Settings", "连接设置" },
                { "Content and Gaps", "内容和空段" },
                { "Content Only", "仅内容" },
                { "Convert position to speed", "将位置转换为速度" },
                { "Convert position to speed (Inverted)", "将位置转换为速度（反向）" },
                { "Copy Command ID", "复制命令 ID" },
                { "Copy Link", "复制链接" },
                { "Count", "数量" },
                { "Creation time (Media, ascending)", "媒体创建时间（升序）" },
                { "Creation time (Media, descending)", "媒体创建时间（降序）" },
                { "Creation time (Script, ascending)", "脚本创建时间（升序）" },
                { "Creation time (Script, descending)", "脚本创建时间（降序）" },
                { "Creator", "作者" },
                { "Crop Video", "裁剪视频" },
                { "Custom Range", "自定义范围" },
                { "Debug", "调试" },
                { "Default", "默认" },
                { "Delete (Permanently)", "永久删除" },
                { "Delete (Recycle Bin)", "移至回收站" },
                { "Device", "设备" },
                { "Device Id", "设备 ID" },
                { "Device Manager", "设备管理器" },
                { "Devices", "设备" },
                { "Chapter Settings", "章节设置" },
                { "Displayed Time", "时间显示" },
                { "E-Stim Audio", "电刺激音频" },
                { "Favourite Folders", "常用文件夹" },
                { "Funstim Converter", "Funstim 转换器" },
                { "Input", "输入" },
                { "Input Mappings", "输入映射" },
                { "Notifications", "通知" },
                { "Paths", "附加路径" },
                { "Players", "播放器" },
                { "Remember", "记忆设置" },
                { "Scripts", "脚本" },
                { "SoftSeek", "平滑跳转" },
                { "Subtitles", "字幕" },
                { "Timings", "时间调整" },
                { "Disconnect", "断开连接" },
                { "Documentation (Online)", "在线文档" },
                { "Download", "下载" },
                { "Download Scripts", "下载脚本" },
                { "Duration", "时长" },
                { "Duration (ascending)", "时长（升序）" },
                { "Duration (descending)", "时长（降序）" },
                { "Edit Favourites", "编辑常用文件夹" },
                { "Edit Meta-Data", "编辑元数据" },
                { "Edit this shortcut", "编辑此快捷键" },
                { "Enable Auto Homing", "启用自动归位" },
                { "Enable Video Crop", "启用视频裁剪" },
                { "Enable Fuzzy Name Matching", "启用模糊名称匹配" },
                { "Enable Subtitles", "启用字幕" },
                { "Enable Url Decoding", "启用网址解码" },
                { "Pause video while downloading script", "下载脚本时暂停视频" },
                { "Reload Script", "重新加载脚本" },
                { "Enabled", "启用" },
                { "Execute Pattern", "执行模式" },
                { "Export as Handy CSV", "导出为 Handy CSV" },
                { "Fade in/out", "淡入 / 淡出" },
                { "Fade out during pauses", "暂停时淡出" },
                { "Fastest Chapter", "最快章节" },
                { "Fastest Chapter (limited)", "最快章节（受限）" },
                { "Fastest Section", "最快片段" },
                { "Favorites", "收藏" },
                { "Favourite Folder", "常用文件夹" },
                { "File", "文件" },
                { "File loading", "文件加载" },
                { "Filename URL-Decoding", "文件名 URL 解码" },
                { "Fill gap after last action", "填充最后动作后的空段" },
                { "Fill gap before first action", "填充首个动作前的空段" },
                { "Fill gaps with movement", "用动作填充空段" },
                { "Filter double-clicks", "过滤双击" },
                { "Filter Range", "过滤范围" },
                { "Format Preference", "格式优先级" },
                { "Framerate", "帧率" },
                { "Frequencies (Hz)", "频率（Hz）" },
                { "From", "从" },
                { "Full Range", "完整范围" },
                { "Funstim Device Options", "Funstim 设备选项" },
                { "Fuzzy Name Matching", "模糊名称匹配" },
                { "Gaps / Events", "空段 / 事件" },
                { "General", "常规" },
                { "Generate", "生成" },
                { "Generate all", "全部生成" },
                { "Generate all missing files", "生成所有缺失文件" },
                { "Generate GIF", "生成 GIF" },
                { "Generate Heatmaps", "生成热力图" },
                { "Generate Previews", "生成预览" },
                { "Generate Thumbnail Banners", "生成缩略图横幅" },
                { "Generate Thumbnails", "生成缩略图" },
                { "Generator Progress", "生成进度" },
                { "Generator Settings", "生成器设置" },
                { "Global", "全局" },
                { "Handy Device Id Settings", "Handy 设备 ID 设置" },
                { "Heatmap", "热力图" },
                { "Height", "高度" },
                { "Help", "帮助" },
                { "HTTP Port", "HTTP 端口" },
                { "If the file already exists", "文件已存在时" },
                { "In the same directory as the video", "与视频相同的目录" },
                { "In this directory", "在此目录" },
                { "Include filled gaps", "包括已填充空段" },
                { "Installed Version:", "已安装版本：" },
                { "Invert Positions", "反转位置" },
                { "IP or host : Port", "IP 或主机 : 端口" },
                { "Keep Previously Loaded", "保留之前加载的内容" },
                { "Latest Version:", "最新版本：" },
                { "Limit to active selection", "限制为当前选择" },
                { "Limit to selection", "限制为选择范围" },
                { "List", "列表" },
                { "Load Playlist", "加载播放列表" },
                { "Local", "本地" },
                { "Local IP", "本地 IP" },
                { "Log Markers", "日志标记" },
                { "Loop", "循环" },
                { "Loop for full duration", "循环至完整时长" },
                { "Manage Bookmarks", "管理书签" },
                { "Media Format Preference:", "媒体格式优先级：" },
                { "Media Progress", "媒体进度" },
                { "Middle", "中间" },
                { "Min Command Delay", "最小命令延迟" },
                { "Mode", "模式" },
                { "Mode:", "模式：" },
                { "Move", "移动" },
                { "Move and remove from playlist", "移动并从播放列表移除" },
                { "Move Down", "下移" },
                { "Move First", "移到最前" },
                { "Move Last", "移到最后" },
                { "Move Up", "上移" },
                { "Movement Range", "动作范围" },
                { "Multiple Sections", "多个片段" },
                { "Name", "名称" },
                { "Name (ascending)", "名称（升序）" },
                { "Name (descending)", "名称（降序）" },
                { "Next", "下一个" },
                { "No Devices Connected", "未连接设备" },
                { "None", "无" },
                { "None (Script Only)", "无（仅脚本）" },
                { "Ok", "确定" },
                { "Only use left half", "仅使用左半部分" },
                { "Open in Explorer", "在资源管理器中打开" },
                { "Open script", "打开脚本" },
                { "Open video", "打开视频" },
                { "Original Name", "原始名称" },
                { "Overlay Script Positions", "叠加显示脚本位置" },
                { "Password", "密码" },
                { "Path", "路径" },
                { "Path (ascending)", "路径（升序）" },
                { "Path (descending)", "路径（降序）" },
                { "Path to exe", "程序路径" },
                { "Pattern", "模式" },
                { "Pattern Speed", "模式速度" },
                { "Performers", "演员" },
                { "Pick from file system", "从文件系统选择" },
                { "Play / Pause", "播放 / 暂停" },
                { "Playback Mode", "播放模式" },
                { "Playback Rate", "播放速度" },
                { "Playlist", "播放列表" },
                { "Port", "端口" },
                { "Position", "位置" },
                { "Preview", "预览" },
                { "Preview Generator Settings", "预览生成设置" },
                { "Preview GIF", "预览 GIF" },
                { "Progress", "进度" },
                { "Ramp Intensity", "渐变强度" },
                { "Random", "随机" },
                { "Random Chapter", "随机章节" },
                { "Random Chapter (limited)", "随机章节（受限）" },
                { "Random Chapters", "随机章节" },
                { "Random Section", "随机片段" },
                { "Range", "范围" },
                { "Range Extender", "范围扩展" },
                { "Range Filter", "范围过滤" },
                { "Recheck All Entries", "重新检查所有条目" },
                { "Related Files", "相关文件" },
                { "Remove", "移除" },
                { "Remove Completed", "移除已完成项" },
                { "Remove from playlist", "从播放列表移除" },
                { "Remove Gaps", "移除空段" },
                { "Remove Incomplete Entries", "移除不完整条目" },
                { "Remove Missing Entries", "移除文件缺失条目" },
                { "Remove this shortcut", "移除此快捷键" },
                { "Rename", "重命名" },
                { "Rename all related Files", "重命名所有相关文件" },
                { "Rename the existing file", "重命名已有文件" },
                { "Rename the new file", "重命名新文件" },
                { "Repeat", "重复" },
                { "Repeat Single File", "单文件循环" },
                { "Replace the existing file", "替换已有文件" },
                { "Replacement", "替换" },
                { "Reset", "重置" },
                { "Restore default shortcuts", "恢复默认快捷键" },
                { "Rows", "行数" },
                { "Save as", "另存为" },
                { "Save generated files", "保存生成的文件" },
                { "Save Playlist", "保存播放列表" },
                { "Scan for devices", "扫描设备" },
                { "Scene Selector", "场景选择器" },
                { "Script", "脚本" },
                { "Script Delay", "脚本延迟" },
                { "Script Format Preference:", "脚本格式优先级：" },
                { "Scroll to current entry", "滚动到当前条目" },
                { "Search for Devices", "搜索设备" },
                { "Select directory", "选择目录" },
                { "Selection", "选择范围" },
                { "Selection Section", "选择片段" },
                { "Serial Port", "串口" },
                { "Serial Port Options", "串口选项" },
                { "Set A", "设置 A 点" },
                { "Set B", "设置 B 点" },
                { "Set Shortcut", "设置快捷键" },
                { "Settings", "设置" },
                { "Shift", "偏移" },
                { "Shift Script", "偏移脚本" },
                { "Shortcut", "快捷键" },
                { "Show Device Manager", "显示设备管理器" },
                { "Show devices", "显示设备" },
                { "Show filled gaps in heat map", "在热力图中显示已填充空段" },
                { "Show Heat Map", "显示热力图" },
                { "Show loaded files", "显示已加载文件" },
                { "Show playlist", "显示播放列表" },
                { "Show progress", "显示进度" },
                { "Show Script Positions", "显示脚本位置" },
                { "Show Skip Button", "显示跳过按钮" },
                { "Show time left instead of elapsed", "显示剩余时间而非已用时间" },
                { "Shuffle", "随机排序" },
                { "Single Section", "单个片段" },
                { "Skip", "跳过" },
                { "Skip close to next event", "跳到下一个临近事件" },
                { "Skip existing", "跳过已有文件" },
                { "Soft Seek between videos", "视频间平滑跳转" },
                { "Soft Seek Gaps", "空段平滑跳转" },
                { "Soft Seek Loops", "循环平滑跳转" },
                { "Sort", "排序" },
                { "Source", "来源" },
                { "Speed", "速度" },
                { "Speed Multiplier", "速度倍率" },
                { "Start", "开始" },
                { "Start Buttplug (if it's not already running)", "启动 Buttplug（若尚未运行）" },
                { "Status", "状态" },
                { "Stay on top", "窗口置顶" },
                { "Style", "样式" },
                { "Subtitle Format Preference:", "字幕格式优先级：" },
                { "Tags", "标签" },
                { "Target Duration", "目标时长" },
                { "Test Patterns", "测试模式" },
                { "Thumbnail banner", "缩略图横幅" },
                { "Thumbnail Banner Preview", "缩略图横幅预览" },
                { "Thumbnail Banner Settings", "缩略图横幅设置" },
                { "Thumbnail Generator Settings", "缩略图生成设置" },
                { "Thumbnails", "缩略图" },
                { "Tiles", "磁贴" },
                { "Time elapsed", "已用时间" },
                { "Time remaining", "剩余时间" },
                { "Timestamp", "时间戳" },
                { "To", "到" },
                { "Toggle fullscreen", "切换全屏" },
                { "Toggle fullscreen when double-clicking video", "双击视频时切换全屏" },
                { "Toggle playback when clicking video", "单击视频时切换播放" },
                { "Top", "顶部" },
                { "Top / Bottom", "顶部 / 底部" },
                { "Total Width", "总宽度" },
                { "Trim", "裁剪" },
                { "Type", "类型" },
                { "UDP Port", "UDP 端口" },
                { "Use", "使用" },
                { "Use Default", "使用默认值" },
                { "Use external OSD if available", "可用时使用外部 OSD" },
                { "Use Fallback Script:", "使用备用脚本：" },
                { "User", "用户" },
                { "Version", "版本" },
                { "Vibration Conversion Mode:", "振动转换模式：" },
                { "Video", "视频" },
                { "Video Player", "视频播放器" },
                { "Video Url", "视频网址" },
                { "View", "视图" },
                { "Volume", "音量" },
                { "Web Socket URL", "WebSocket 地址" },
                { "Width", "宽度" },
                { "Window Position", "窗口位置" },
                { "FFmpeg can be used to generate Thumbnails and Preview GIFs from within ScriptPlayer. To use FFmpeg, download it from", "FFmpeg 可用于在 ScriptPlayer 中生成缩略图和预览 GIF。请先从" },
                { "the official homepage", "官方网站下载" },
                { ", install it on your computer and set the path to the", "，安装到电脑后，将路径设置为" },
                { "contained in the build.", "文件。" },
                { "This project would not have been possible (or at least a whole lot harder) without the help, support and contributions of a lot of people.", "没有众多参与者的帮助、支持与贡献，这个项目不可能完成，至少会困难得多。" },
                { "Connected", "已连接" },
                { "Determining Version...", "正在确定版本……" },
                { "Checking ...", "正在检查……" },
                { "Unknown", "未知" },
                { "You are up-to-date!", "当前已是最新版本！" },
                { "There is a new version available!", "发现新版本！" },
                { "Unable to determine version", "无法确定版本" },
                { "Automatically resets the device position to 0 when playback is paused.", "播放暂停时自动把设备位置重置为 0。" },
                { "Connection key used to connect to The Handy. Sometimes also referred to as device id oder device key", "用于连接 The Handy 的连接密钥，有时也称为设备 ID 或设备密钥。" },
                { "Convert script position to phase modulated audio output. See \"Funstim Converter\" in settings menu for more options.", "把脚本位置转换为相位调制音频输出。更多选项请查看设置菜单中的“Funstim 转换器”。" },
                { "Defines the command source", "定义命令来源" },
                { "Determines how beat files are converted to movements", "决定如何把节拍文件转换为动作" },
                { "Duration [ms] ", "时长 [ms] " },
                { "Enter the amount of seconds that the script will be shifted. Positive values mean the script will execute later.", "输入脚本偏移秒数。正值表示脚本稍后执行。" },
                { "Filler Intervall [ms] ", "填充间隔 [ms] " },
                { "GoPro VR Player Connection Settings", "GoPro VR Player 连接设置" },
                { "Here you can manage all folders you regularly use, e.g. to move files to or load scripts from. Double-click entries to edit them.", "在这里管理常用文件夹，例如移动文件或加载脚本。双击条目可编辑。" },
                { "Here you can set the prefered order in which scripts and media files are selected if there are multiple files with the same name but different extensions. Separate multiple extensions with a colon or semi-colon. Extension that are not explicitly listed will be prioritized alphabetically", "在这里设置同名文件存在多个扩展名时，脚本和媒体的选择优先级。多个扩展名使用冒号或分号分隔；未列出的扩展名按字母顺序排列。" },
                { "If you don't want to keep all your files (e.g. scripts and videos) in the same directory, you can specify additional paths for ScriptPlayer to search matching files in.\nActivate the * setting to include all subdirectories too.", "如果不想把脚本和视频放在同一目录，可指定 ScriptPlayer 搜索匹配文件的附加路径。\n启用 * 可同时包括全部子目录。" },
                { "If your videos are streamed over a webservice, video file names might need to be decoded so that ScriptPlayer can find related files correctly.", "通过网络服务串流视频时，可能需要解码视频文件名，ScriptPlayer 才能正确找到相关文件。" },
                { "Intervall", "间隔" },
                { "Inverts the input position before calculating the output position.", "计算输出位置前反转输入位置。" },
                { "Kodi Connection Settings", "Kodi 连接设置" },
                { "Minimal delay between two script commands", "两条脚本命令之间的最小延迟" },
                { "Minimum and maximum position of the Launch. Values received from the script will be scaled accordingly.", "Launch 的最小和最大位置。脚本中的值会按此范围缩放。" },
                { "Minimum and maximum speed of the Launch.", "Launch 的最小和最大速度。" },
                { "MPC-HC Connection Settings", "MPC-HC 连接设置" },
                { "only when loading fails", "仅在加载失败时" },
                { "Paid", "付费" },
                { "Playlist: ", "播放列表：" },
                { "Press key(s) to set new shortcut or close dialog to cancel.", "按下按键设置新快捷键，或关闭窗口取消。" },
                { "Preview GIF Creator", "预览 GIF 生成器" },
                { "Samsung VR Connection Settings", "Samsung VR 连接设置" },
                { "Saved minimum and maximum position of the device.", "已保存的设备最小和最大位置。" },
                { "second delay", "秒延迟" },
                { "Select a script file to always use the same fallback script, specify a directory to select a random script or specify a directory and add a * to select a random script from that directory or any sub-directory in it.", "选择脚本文件可始终使用同一个备用脚本；选择目录可随机选择脚本；在目录后添加 * 可从该目录及其子目录随机选择。" },
                { "Selection: ", "选择范围：" },
                { "Shadow", "阴影" },
                { "Sine Wave", "正弦波" },
                { "Some external video players (e.g. Zoom Player) can be used to display ScriptPlayer's notifications on their internal OSD (On Screen Displays)", "部分外部播放器（例如 Zoom Player）可在其 OSD 上显示 ScriptPlayer 通知。" },
                { "Some players change the name of the opened video file (e.g. add the year this video was made) which can make finding a matching script impossible. Fuzzy name matching can be used to transform the name of the video back but requires a certain level of knowledge of regular expressions to use.", "部分播放器会修改已打开视频的名称（例如添加年份），导致无法找到匹配脚本。模糊名称匹配可还原名称，但需要了解正则表达式。" },
                { "Spacing before/after gap [ms] ", "空段前后间隔 [ms] " },
                { "Static multiplier that is applied after the automatic speed detection.", "自动检测速度后应用的固定倍率。" },
                { "TCP Port", "TCP 端口" },
                { "The approximate duration of randomly selected sections.", "随机选择片段的大致时长。" },
                { "This feature is experimental and even remotely complete.", "此功能仍处于实验阶段，尚未完善。" },
                { "Thumbnails for opened videos", "为已打开视频生成缩略图" },
                { "Thumbnails, Heatmap & Preview for videos in playlist", "为播放列表视频生成缩略图、热力图和预览" },
                { "Timespan by which every script command will be delayed. Negative values will cause the commands to be executed ahead of time.", "每条脚本命令的延迟时间。负值会让命令提前执行。" },
                { "Transparent BAckground", "透明背景" },
                { "Url", "网址" },
                { "Use only left half", "仅使用左半部分" },
                { "Use speed", "使用速度" },
                { "Use speed * length", "使用速度 × 长度" },
                { "Use speed * lengthbut stop after half the duration", "使用速度 × 长度，但在一半时长后停止" },
                { "Use speed but stop after half the duration", "使用速度，但在一半时长后停止" },
                { "Video Crop can be used to hide the beatbar in CH / FH videos, hide other distracting elements or only show one side of a side-by-side video.", "视频裁剪可隐藏 CH / FH 视频中的节拍条或其他干扰元素，也可只显示并排视频的一侧。" },
                { "VLC Connection Settings", "VLC 连接设置" },
                { "When generating \"all\"", "生成“全部”时" },
                { "Whirligig Connection Settings", "Whirligig 连接设置" },
                { "Will automatically skip command gaps of more than 10s.", "自动跳过超过 10 秒的命令空段。" },
                { "Will loop short scripts for the entire duration of the video", "循环短脚本以覆盖完整视频时长。" },
                { "Will remove any gaps > 10s from the script", "移除脚本中所有超过 10 秒的空段。" },
                { "You can press a single key on your keyboard, a shortcut containing shift, alt and/or control and a key, or send a shortcut via automation software that uses WM_SENDKEY.", "可按单个按键，或使用包含 Shift、Alt、Ctrl 的组合键；也可通过使用 WM_SENDKEY 的自动化软件发送快捷键。" }
            };

        private static bool initialized;
        private static string currentLanguage = "zh-CN";
        private static readonly Dictionary<string, string> English = BuildEnglishStrings();
        private static readonly Dictionary<string, string> NativeChinese = BuildNativeChineseStrings();
        private static readonly ConditionalWeakTable<DependencyObject, LiteralState> Literals =
            new ConditionalWeakTable<DependencyObject, LiteralState>();
        private static readonly List<WeakReference> TrackedNodes = new List<WeakReference>();

        private sealed class LiteralState
        {
            internal readonly Dictionary<DependencyProperty, LiteralText> Properties =
                new Dictionary<DependencyProperty, LiteralText>();
        }

        private sealed class LiteralText
        {
            internal string Original;
            internal string LastOutput;
        }

        public static string CurrentLanguage { get { return currentLanguage; } }
        public static event EventHandler LanguageChanged;

        public static string NormalizeLanguage(string code)
        {
            if (code != null) code = code.Trim();
            return code != null && (code.Equals("en", StringComparison.OrdinalIgnoreCase)
                || code.StartsWith("en-", StringComparison.OrdinalIgnoreCase)) ? "en" : "zh-CN";
        }

        /// <summary>Changes UI projections only. Persist the canonical code in the calling settings layer.</summary>
        public static void SetLanguage(string code)
        {
            var application = Application.Current;
            if (application != null && !application.Dispatcher.CheckAccess())
            {
                application.Dispatcher.Invoke(new Action(() => SetLanguage(code)));
                return;
            }

            string language = NormalizeLanguage(code);
            if (language == currentLanguage) return;
            currentLanguage = language;
            for (int i = TrackedNodes.Count - 1; i >= 0; i--)
            {
                var node = TrackedNodes[i].Target as DependencyObject;
                if (node == null) TrackedNodes.RemoveAt(i);
                else TranslateNode(node);
            }
            if (application != null)
                foreach (Window window in application.Windows) TranslateTree(window);
            var handler = LanguageChanged;
            if (handler != null) handler(null, EventArgs.Empty);
        }

        public static void Initialize()
        {
            if (initialized) return;
            initialized = true;
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnTreeLoaded), true);
            EventManager.RegisterClassHandler(typeof(FrameworkElement), FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnElementLoaded), true);
            EventManager.RegisterClassHandler(typeof(FrameworkContentElement), FrameworkContentElement.LoadedEvent,
                new RoutedEventHandler(OnElementLoaded), true);
            EventManager.RegisterClassHandler(typeof(ContextMenu), ContextMenu.OpenedEvent,
                new RoutedEventHandler(OnTreeLoaded), true);
            EventManager.RegisterClassHandler(typeof(MenuItem), MenuItem.SubmenuOpenedEvent,
                new RoutedEventHandler(OnTreeLoaded), true);
        }

        public static string Translate(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;
            string translated;
            string key = value.Trim();
            if (currentLanguage == "en")
            {
                if (English.TryGetValue(key, out translated)) return PreservePadding(value, translated);
            }
            else if (Strings.TryGetValue(key, out translated) || NativeChinese.TryGetValue(key, out translated))
                return PreservePadding(value, translated);
            return value;
        }

        /// <summary>Arguments are data; only the exact UI format literal is translated.</summary>
        public static string TranslateFormat(string format, params object[] arguments)
        {
            return string.Format(CultureInfo.CurrentCulture, Translate(format), arguments);
        }

        private static Dictionary<string, string> BuildEnglishStrings()
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in Strings)
                if (!result.ContainsKey(pair.Value.Trim())) result.Add(pair.Value.Trim(), pair.Key);
            foreach (var pair in EnglishUiStrings.Native) result[pair.Key] = pair.Value;
            return result;
        }

        private static Dictionary<string, string> BuildNativeChineseStrings()
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in EnglishUiStrings.Native)
                if (pair.Value != "ScriptPlayer" && !result.ContainsKey(pair.Value)) result.Add(pair.Value, pair.Key);
            return result;
        }

        private static string PreservePadding(string original, string translated)
        {
            int start = 0;
            while (start < original.Length && char.IsWhiteSpace(original[start])) start++;
            int end = original.Length - 1;
            while (end >= start && char.IsWhiteSpace(original[end])) end--;
            return original.Substring(0, start) + translated + original.Substring(end + 1);
        }

        public static void TranslateTree(DependencyObject root)
        {
            if (root == null) return;
            TranslateTree(root, new HashSet<DependencyObject>());
        }

        private static void TranslateTree(DependencyObject current, ISet<DependencyObject> visited)
        {
            if (current == null || !visited.Add(current)) return;
            // Changing Run/Text invalidates WPF logical enumerators; take both snapshots first.
            var children = new List<DependencyObject>();
            try
            {
                foreach (object child in LogicalTreeHelper.GetChildren(current))
                {
                    var dependencyChild = child as DependencyObject;
                    if (dependencyChild != null) children.Add(dependencyChild);
                }
            }
            catch (InvalidOperationException) { /* Rebuilding document nodes are covered by Loaded. */ }
            if (current is Visual || current is System.Windows.Media.Media3D.Visual3D)
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(current); i++)
                    children.Add(VisualTreeHelper.GetChild(current, i));

            TranslateNode(current);
            var element = current as FrameworkElement;
            if (element != null)
            {
                if (element.ContextMenu != null) children.Add(element.ContextMenu);
                var tooltip = element.ToolTip as DependencyObject;
                if (tooltip != null) children.Add(tooltip);
            }
            foreach (var child in children) TranslateTree(child, visited);
        }

        private static void OnTreeLoaded(object sender, RoutedEventArgs e)
        {
            TranslateTree(sender as DependencyObject);
        }

        private static void OnElementLoaded(object sender, RoutedEventArgs e)
        {
            TranslateNode(sender as DependencyObject);
        }

        private static void TranslateNode(DependencyObject node)
        {
            var element = node as FrameworkElement;
            if (element != null)
            {
                var window = element as Window;
                if (window != null) TranslateProperty(window, Window.TitleProperty);
                var headeredItems = element as HeaderedItemsControl;
                if (headeredItems != null) TranslateProperty(headeredItems, HeaderedItemsControl.HeaderProperty);
                var headeredContent = element as HeaderedContentControl;
                if (headeredContent != null) TranslateProperty(headeredContent, HeaderedContentControl.HeaderProperty);
                var content = element as ContentControl;
                if (content != null && !(element is TextBoxBase)) TranslateProperty(content, ContentControl.ContentProperty);
                var text = element as TextBlock;
                if (text != null && text.Inlines.Count <= 1 && !HasBoundInlines(text.Inlines))
                    TranslateProperty(text, TextBlock.TextProperty);
                TranslateProperty(element, FrameworkElement.ToolTipProperty);
                TranslateProperty(element, AutomationProperties.NameProperty);
                var grid = element as DataGrid;
                if (grid != null)
                    foreach (DataGridColumn column in grid.Columns) TranslateProperty(column, DataGridColumn.HeaderProperty);
            }
            var run = node as Run;
            if (run != null)
            {
                // A plain TextBlock owns its one Run; tracking both would lose the exact source on a switch.
                var parentText = run.Parent as TextBlock;
                if (parentText == null || parentText.Inlines.Count > 1)
                    TranslateProperty(run, Run.TextProperty);
            }
            var gridColumn = node as DataGridColumn;
            if (gridColumn != null) TranslateProperty(gridColumn, DataGridColumn.HeaderProperty);
        }

        private static bool HasBoundInlines(InlineCollection inlines)
        {
            foreach (Inline inline in inlines)
            {
                var run = inline as Run;
                if (run != null && BindingOperations.IsDataBound(run, Run.TextProperty)) return true;
                var span = inline as Span;
                if (span != null && HasBoundInlines(span.Inlines)) return true;
            }
            return false;
        }

        private static void TranslateProperty(DependencyObject node, DependencyProperty property)
        {
            // SetCurrentValue alone preserves a binding but still corrupts its visible data.
            bool dataText = property != FrameworkElement.ToolTipProperty && property != AutomationProperties.NameProperty;
            if (BindingOperations.IsDataBound(node, property) || (dataText && IsDataPresentation(node))) return;
            var current = node.GetValue(property) as string;
            if (current == null) return;
            LiteralState state;
            if (!Literals.TryGetValue(node, out state))
            {
                state = new LiteralState();
                Literals.Add(node, state);
                TrackedNodes.Add(new WeakReference(node));
            }
            LiteralText literal;
            if (!state.Properties.TryGetValue(property, out literal))
            {
                literal = new LiteralText { Original = current, LastOutput = current };
                state.Properties.Add(property, literal);
            }
            else if (!string.Equals(current, literal.LastOutput, StringComparison.Ordinal))
                literal.Original = current;
            string translated = Translate(literal.Original);
            literal.LastOutput = translated;
            if (!string.Equals(current, translated, StringComparison.Ordinal)) node.SetCurrentValue(property, translated);
        }

        private static bool IsDataPresentation(DependencyObject node)
        {
            var visited = new HashSet<DependencyObject>();
            for (var current = node; current != null && visited.Add(current); current = GetParent(current))
            {
                if (current is TextBoxBase || current is PasswordBox) return true;
                var text = current as TextBlock;
                if (text != null && BindingOperations.IsDataBound(text, TextBlock.TextProperty)) return true;
                var content = current as ContentControl;
                if (content != null && BindingOperations.IsDataBound(content, ContentControl.ContentProperty)) return true;
                var presenter = current as ContentPresenter;
                if (presenter != null && presenter.Content is string
                    && BindingOperations.IsDataBound(presenter, ContentPresenter.ContentProperty))
                {
                    var owner = presenter.TemplatedParent as ContentControl;
                    if (owner == null || BindingOperations.IsDataBound(owner, ContentControl.ContentProperty)) return true;
                }
                var header = current as HeaderedItemsControl;
                if (header != null && BindingOperations.IsDataBound(header, HeaderedItemsControl.HeaderProperty)) return true;
                var headerContent = current as HeaderedContentControl;
                if (headerContent != null && BindingOperations.IsDataBound(headerContent, HeaderedContentControl.HeaderProperty)) return true;
                var itemOwner = ItemsControl.ItemsControlFromItemContainer(current);
                if (itemOwner != null && itemOwner.ItemsSource != null) return true;
            }
            return false;
        }

        private static DependencyObject GetParent(DependencyObject node)
        {
            var content = node as FrameworkContentElement;
            if (content != null) return content.Parent ?? ContentOperations.GetParent(content);
            var element = node as FrameworkElement;
            if (element != null)
            {
                var logical = element.Parent;
                if (logical != null) return logical;
                if (element.TemplatedParent != null) return element.TemplatedParent;
            }
            if (node is Visual || node is System.Windows.Media.Media3D.Visual3D)
                return VisualTreeHelper.GetParent(node);
            return null;
        }
    }
}
