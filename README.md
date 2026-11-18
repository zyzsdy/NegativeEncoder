# NegativeEncoder

## next：6.0.0-alpha.1

当前版本使用 **.NET 10 + Avalonia UI** 的 `NegativeEncoder.Desktop`，支持
Windows / macOS / Linux，已接入工具库管理与主要编码任务执行。旧 WPF 代码、测试和安装器已移除。

```sh
# 所有桌面平台：需要稳定版 .NET 10 SDK
dotnet run --project NegativeEncoder.Desktop/NegativeEncoder.Desktop.csproj

# 构建、测试新前端（不依赖 Windows、Libs 或编码工具）
dotnet build NegativeEncoder.Desktop.slnx -c Release
dotnet test NegativeEncoder.Desktop.slnx -c Release
```

macOS / Linux 也可运行 `./scripts/run-desktop.sh`，它会查找 PATH 或
`~/.dotnet/dotnet`。macOS 本地独立应用：

```sh
./scripts/package-macos.sh osx-arm64
open artifacts/osx-arm64/NegativeEncoder.app
```

该脚本生成包含 .NET 运行时和 macOS 应用图标的本地应用包；正式签名、公证与发行安装包尚未实现。
Intel Mac 使用 `osx-x64`。其他平台可用 `dotnet publish` 的 `win-x64`、
`win-arm64`、`linux-x64` 或 `linux-arm64` RID；这些目标需要各自的交互验证。
Linux 需要 Avalonia 的桌面原生依赖和中文字体，参见
[Avalonia 支持平台](https://v11.docs.avaloniaui.net/docs/get-started/supported-platforms)。

已迁移：三栏主窗口、宽窗口下的编码器两栏表单、六个功能页（编码器 / HDR / VS / 音频 / 封装 / 常用）、
文件选择及拖放、预设菜单切换和管理、任务状态与执行日志、状态栏和关于窗口。
预设保存工作台的编码、HDR、音频、封装、批量和 VS 设置，包括手动编辑的脚本和填写的路径。
编辑时，窗口标题和预设菜单会在名称前显示 `*`。切换预设或重启应用都会恢复各自的草稿；“保存预设”会更新该预设并清除星号。
正式预设和草稿分别写入用户应用数据目录 `NegativeEncoder/Desktop/` 下的 `presets.json` 和 `preset-draft.json`。待处理视频列表和任务队列仅保留于本次会话。

VS 编辑器使用 AvaloniaEdit，保留 Python 语法高亮、行号和缩进；
代码生成器会随输入视频、字幕、滤镜选项和尺寸变化自动重建脚本（覆盖手动编辑），也可从菜单手动生成。生成器根据当前 Libs 的实际取帧检测启用功能；缺失或无效选项会阻止生成并保留原脚本。编码操作会加入串行队列并立即执行；任务支持取消、查看实时日志。失败时停止调度，修正后可从“文件 → 开始任务调度”继续后续任务。

启动时根据系统与显卡选择默认编码器：macOS 使用 VTEnc；Windows/Linux 按 NVIDIA → Intel → AMD
选择 NVENC / QSV / VCE。检测失败会显示错误；未检测到支持的显卡时提示手动选择，不会声称硬件可用。
启动检测不会覆盖已经编辑的预设。编码模式、编解码选项按编码器区分：

- NVENC/VCE：VBR、CBR、CQP；QSV 另有 LA、LA-ICQ、QVBR，沿用原 Windows 实现。
- VTEnc：ABR、Quality（0–1）、CBR；macOS 26+ 显示 VBR。只支持 H.264 8-bit、HEVC 8/10-bit。
  CBR 需要 CFR/Force CFR 和明确帧率；原生属性最终由硬件会话检查，失败直接报告。
- macOS 音频由 VTEnc 的 AudioToolbox AAC 编码；Windows/Linux 音频由当前工具库的 FFmpeg 编码，不依赖 qaac。
  当前 VTEnc 没有纯音频入口：独立音频任务需要含视频的输入，会先生成小尺寸临时视频，再用 FFmpeg 无损提取 AAC。
- 普通编码、HDR 编码/HEVC 元数据标记、字幕压制、VS/FFmpeg 管道、音频编码/提取与封装已接入。
  MKV 封装也使用 FFmpeg。VTEnc 的 FFmpeg / VapourSynth 管道在执行时用工具库中的 ffprobe 读取源视频的色彩标记。缺失项按 BT.709、有限范围和左置色度采样补齐，采用的默认值记录在任务日志中。自定义参数模式由用户指定管道色彩。
  HDR 编码与 HDR 字幕使用 HDR 页的设置，转换时可选 SDR、HDR10 或 HLG 作为源格式。

所有可执行文件从当前 Libs 解析，不搜索系统 PATH。执行期间保护正在使用的工具库版本。
输出不覆盖已有文件；临时目录与目标同目录，完整成功后才交付结果。失败/取消保留工作目录与日志供诊断；
成功清理临时媒体并保留输出旁的执行日志。自定义参数按引号分词，不经过 shell，输入/输出路径由任务管理。
预设导入导出、更新器、系统命令行集成尚未迁移。

“帮助 → 工具库管理”可切换 Libs 位置、检测显卡、安装／升级独立工具及 VS 推荐插件。
Windows/Linux 默认使用可执行文件目录的 `../Libs`；macOS 默认使用
`~/Library/Application Support/NegativeEncoder/Libs`。只切换目录，不搬迁原文件。
FFmpeg 追踪最新稳定版；NVEnc／QSVEnc／VCEEnc／VTEnc 追踪最新发行版（含 pre-release）；Python／VapourSynth／插件使用固定推荐组合。
没有当前平台发行包时禁用安装；未检测到相应显卡仍允许安装。
macOS VS 推荐组合要求 15+，Linux 要求 glibc 2.28+。手工插件放在
`Libs/vapoursynth/user-plugins`，Python 脚本放在 `Libs/vapoursynth/user-scripts`。
下载使用 SHA256 校验，新环境通过运行检查后才启用，旧版本保留。
发行包来源、支持矩阵和维护验证方式见 [工具库说明](TOOL-LIBRARY.md)。

仓库仅保留跨平台解决方案 `NegativeEncoder.Desktop.slnx`。旧 Windows 实现可通过 Git 历史查看。
迁移结构与验证记录见 [MIGRATION.md](MIGRATION.md)。


### **“消极压制(NegativeEncoder)”是什么？**

“消极压制”是指最大程度的节约系统资源，加快编码速度的一种压制方式。有时候视频质量并不是首要考虑因素，可以不用“积极的”进行一个精致的压制，何不“消极一把”呢？
本软件调用硬件加速编码（例如NVENC），这是十分有效的一种方式。虽然很多人说生成的质量不如x264的好，但是很多时候也足够用了，不是吗？

消极压制 v5.0 使用.NET Core完全重制。原始版本请查看oldver分支。

## Download

请去[Releases](https://github.com/zyzsdy/NegativeEncoder/releases)找安装包。原有百度网盘的下载不再提供了。

## LICENSE

MIT (不包含Lib)

Lib内的各软件请遵守它们各自的LICENSE