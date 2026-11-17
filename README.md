# NegativeEncoder

## next：6.0.0-alpha.1

`next` 分支新增 **.NET 10 + Avalonia UI** 的 `NegativeEncoder.Desktop`，目标为
Windows / macOS / Linux。原有 WPF 项目与底层实现保留，已接入工具库管理；编码任务执行仍待迁移。

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
文件选择及拖放、预设菜单切换和管理、任务草稿详情、状态栏和关于窗口。
参数、预设和队列仅保留于本次会话。VS 编辑器使用 AvaloniaEdit，保留 Python 语法高亮、行号和缩进；
代码生成器会随输入视频、字幕、滤镜选项和尺寸变化自动重建脚本（覆盖手动编辑），也可从菜单手动生成。生成器根据当前 Libs 的实际取帧检测启用功能；缺失或无效选项会阻止生成并保留原脚本。所有编码操作只创建 **未执行的任务草稿**；不会写输出文件。

未接入：编码与调度、VideoToolbox 编码参数、
预设磁盘持久化及导入导出、更新器、系统命令行集成。UI 中的编码器选项来自原版，
不代表当前机器具备这些硬件。VideoToolbox 的能力与参数将在后端阶段单独建模，
不把现有 NVENC/QSV 参数直接当作 VideoToolbox 参数。

“帮助 → 工具库管理”可切换 Libs 位置、检测显卡、安装／升级独立工具及 VS 推荐插件。
Windows/Linux 默认使用可执行文件目录的 `../Libs`；macOS 默认使用
`~/Library/Application Support/NegativeEncoder/Libs`。只切换目录，不搬迁原文件。
FFmpeg 追踪最新稳定版；NVEnc／QSVEnc／VCEEnc／VTEnc 追踪最新发行版（含 pre-release）；Python／VapourSynth／插件使用固定推荐组合。
没有当前平台发行包时禁用安装；未检测到相应显卡仍允许安装。
macOS VS 推荐组合要求 15+，Linux 要求 glibc 2.28+。手工插件放在
`Libs/vapoursynth/user-plugins`，Python 脚本放在 `Libs/vapoursynth/user-scripts`。
下载使用 SHA256 校验，新环境通过运行检查后才启用，旧版本保留。
发行包来源、支持矩阵和维护验证方式见 [工具库说明](TOOL-LIBRARY.md)。

原 Windows 版继续使用 `NegativeEncoder.sln`；仓库现在有两个解决方案，
构建和测试时请显式选择。迁移结构与验证记录见 [MIGRATION.md](MIGRATION.md)。


### **“消极压制(NegativeEncoder)”是什么？**

“消极压制”是指最大程度的节约系统资源，加快编码速度的一种压制方式。有时候视频质量并不是首要考虑因素，可以不用“积极的”进行一个精致的压制，何不“消极一把”呢？
本软件调用硬件加速编码（例如NVENC），这是十分有效的一种方式。虽然很多人说生成的质量不如x264的好，但是很多时候也足够用了，不是吗？

消极压制 v5.0 使用.NET Core完全重制。原始版本请查看oldver分支。

## Download

请去[Releases](https://github.com/zyzsdy/NegativeEncoder/releases)找安装包。原有百度网盘的下载不再提供了。

## 旧 Windows 版 VapourSynth 相关

**我如何安装新的插件**

推荐使用vsrepo进行安装。进入Libs目录并打开cmd，键入`python.exe vsrepo.py update`更新vsrepo版本库，然后可以使用`python.exe vsrepo.py install [包名]`安装新的插件。

**如何手工安装**

请将VS插件直接放置在自动加载目录中，即`Libs\vapoursynth64\plugins\`下。

vpy脚本中引用到的Python库或者脚本放在安装目录`\Libs\site-packages\`下。

请注意通过vsrepo安装的Python库或脚本，可能存在`C:\Users\[用户名]\AppData\Roaming\Python\Python38\site-packages`下。而该路径在重装系统后会不可用，如果希望重装系统后保留或随NegativeEncoder主程序迁移，请在运行vsrepo后，将这些文件移动至安装目录`\Libs\site-packages\`下。

## LICENSE

MIT (不包含Lib)

Lib内的各软件请遵守它们各自的LICENSE