# 工具库管理

## 已确认的产品规则

工具库独立安装，不使用系统已有的编码工具、Python、Python 用户包或 VS 插件。
Windows/Linux 默认位置为程序目录的 `../Libs`；macOS 为
`~/Library/Application Support/NegativeEncoder/Libs`。自定义路径持久保存，只切换，
不搬迁、不删除旧目录。路径区域始终解释这一行为。

“使用网络代理”接受无需认证的 `http://主机:端口` 或 `socks5://主机:端口`，
点击“应用代理”后保存并对后续请求生效。留空并应用恢复系统默认网络设置。
代理用于版本信息、校验文件、安装包及其重定向请求；连接失败直接报告，不回退直连。
无效输入或保存失败保留原配置，操作期间禁止修改代理。

代理独立保存在应用配置目录的 `NegativeEncoder/network.json`，不随工具库目录切换。
`Networking/NetworkSettings` 是应用级配置与 HTTP handler 的统一入口；未来程序自身的
更新请求也必须使用该入口。程序自身更新功能目前仍未实现。

硬件检测是建议，不是安装门槛。缺少适配 OS、CPU 架构、最低系统版本或 ABI 的
二进制发行包时禁用安装。无法检测和未检测到硬件是不同状态。

FFmpeg 和硬件编码器追踪最新稳定发行，排除预发布、源码和 AviUtl 插件。
Python 是 VapourSynth 的依赖，分行展示、联动管理。推荐 VS 插件使用固定兼容组合，
不跟随上游最新版自动变化。手工插件不升级、不覆盖、不删除。

安装采用新目录准备、校验、实际运行检查、最后切换清单的流程。失败和取消保留原环境，
详细诊断对用户可见。系统驱动、系统框架、C/C++ 运行库是运行前提，不由管理器安装。
不源码编译。macOS 首套推荐 VS 组合以 15+ 为目标。

## 发行来源核对（2026-11-18）

| 组件 | 来源及资源规则 | 限制 |
|---|---|---|
| FFmpeg Windows x64 | GyanD/codexffmpeg，数字稳定 tag 的 full_build.zip | 与 FFmpeg 官方最新稳定版本核对，不选 nightly |
| FFmpeg macOS/Linux x64、ARM64 | Martin Riedl 的 release ZIP 和 SHA256 | 选择 release，不选 snapshot；保留独立 ffmpeg/ffprobe |
| NVEnc | rigaya/NVEnc，NVEncC_*_x64.7z；Linux amd64.deb | 当前稳定版 9.41；Linux 包可能还需要发行版原生库 |
| QSVEnc | rigaya/QSVEnc，QSVEncC_*_x64.7z；Linux amd64.deb | 当前稳定版 8.38 |
| VCEEnc | rigaya/VCEEnc，VCEEncC_*_x64.7z；Linux amd64.deb | 当前稳定版 9.23 |
| VTEnc | zyzsdy/VTEnc，vtenc-*-macos-arm64.zip | 核对时尚无稳定 Release（预发布不自动安装）；保留解析器，界面如实禁用 |
| Python | astral-sh/python-build-standalone，CPython 3.13 普通 GIL install_only_stripped | 按平台锁定 URL 和 SHA256，不使用宿主 Python |
| VapourSynth | PyPI 官方 VapourSynth wheel | 固定兼容版本，检查 Python ABI 和系统最低版本 |
| VS 插件 | 上游 PyPI wheel、VSRepo 记录的上游/社区构建 | 锁定版本及 SHA256，逐功能执行 smoke probe |

FFmpeg 官方只提供源码；Windows 构建源由官方下载页列出。macOS/Linux 使用有稳定
release 通道和校验值的静态构建，避免把发行分支的每日构建称为稳定版本。
deb 只在 Libs 中解包，不执行维护脚本、不调用 sudo/dpkg、不修改系统。
动态库或驱动缺失必须报告，不能借用 PATH 中其他编码器伪装成功。

## 推荐功能组

基础：L-SMASH-Works、VIVTC、Subtext、Bwdif。QTGMC：havsfunc r33、mvsfunc、adjust、
MVTools、Miscfilters、RGVS、fmtconv、ZNEDI3/权重、EEDI3m（r33 初始化时即访问）。
Windows 字幕兼容：xy-VSFilter、VSFilterMod。旧 Yadif：Yadifmod、NNEDI3CL，需 OpenCL。

生成器按当前环境实际通过探测的能力启用选项，明确区分字幕和反交错算法。
失效选项阻止重新生成并保留原脚本；没有静默算法替换。QTGMC 不直接升级到已移除
旧接口的 havsfunc 新版。更换实现需独立验证。

## 范围

本次只连接 Avalonia 工具库管理和脚本生成器。编码任务执行、卸载、任意插件市场、
历史版本 UI、系统驱动安装不在本次范围。旧 WPF 项目保持独立。

## 来源

- https://ffmpeg.org/download.html
- https://github.com/GyanD/codexffmpeg/releases
- https://ffmpeg.martin-riedl.de/
- https://github.com/rigaya/NVEnc/releases
- https://github.com/rigaya/QSVEnc/releases
- https://github.com/rigaya/VCEEnc/releases
- https://github.com/zyzsdy/VTEnc
- https://github.com/astral-sh/python-build-standalone
- https://pypi.org/project/VapourSynth/
- https://github.com/vapoursynth/vsrepo
- https://github.com/Stefan-Olt/vs-plugin-build

## 支持矩阵与维护

| 组合 | Windows x64 | macOS x64 / ARM64（15+） | Linux x64（glibc 2.28+） | Linux ARM64（glibc 2.28+） |
|---|---|---|---|---|
| Python / VS | 有包 | 有包；ARM64 已实测 | 有包 | 有包 |
| 基础处理 | 有包 | 已实测 ARM64 | 有包 | 有包 |
| QTGMC | 有包 | 已实测 ARM64 | 有包 | 缺少配套二进制，禁用 |
| xy-VSFilter / VSFilterMod | 有包 | 禁用 | 禁用 | 禁用 |
| Yadifmod / NNEDI3CL | 有包；需要 OpenCL | 禁用 | 禁用 | 禁用 |

“有包”仅表示已锁定匹配资产，并不等同于该平台已执行测试。每次安装均执行真实
取帧验证，失败不启用。固定组合标识 `2026-11-18.1`，具体 URL、版本、SHA256
及逐文件映射见 `NegativeEncoder.Desktop/ToolLibrary/runtime-catalog.json`。
fmtconv 使用 VSRepo 记录的独立构建；其 PyPI r31 仅有 Windows wheel。

`library.json` 记录活动版本，`.managed/<组件>/<generation>` 保存各代组件，
`.downloads/<SHA256>` 缓存经过校验的包。安装验证通过并原子切换清单后，自动清理
所有组件中未被清单引用的旧代（仅限管理器生成的 GUID 目录）；失败或取消只清理
本次准备目录和未完成的下载文件，保留原活动版本及经过校验的下载缓存。
清理保护清单中的目录、可执行文件及活动环境链接引用的文件；不会清理手工目录。
安装、清理共用排他锁；工具检测和使用方通过 `AcquireUsageLease()` 持有共享锁，
从解析工具路径直到进程退出，防止其他实例在使用期间切换或删除版本。
外部手工启动的工具不参与此锁协议，应在其退出后执行安装或清理。
清理失败会报告目录与错误；若新版本已经启用，不会回滚或删除它。
可在没有外部工具进程使用旧目录时，清理历史遗留副本并重新检测活动环境：

```sh
dotnet run --project scripts/ToolLibrary.Verify -- /absolute/path/to/Libs cleanup
```

自定义插件使用 `vapoursynth/user-plugins`，脚本使用 `vapoursynth/user-scripts`；
升级也保留旧代中不在受管理文件清单内的手工文件，与推荐包冲突时中止。
普通工具可识别 Libs 根目录、组件名目录、组件 ID 目录及其 bin 子目录内的二进制；
不扫描 PATH。VS 必须使用此管理器创建的独立环境，旧版环境应先备份再自行整理插件。

维护者更新固定版本后运行 `python3 scripts/refresh-vs-catalog.py`，审查清单差异，
再在目标机器明确执行以下验证（会下载并在指定目录安装）：

```sh
dotnet run --project scripts/ToolLibrary.Verify -- /absolute/test/Libs runtime basic qtgmc
dotnet run --project scripts/ToolLibrary.Verify -- /absolute/test/Libs ffmpeg
dotnet run --project scripts/ToolLibrary.Verify -- /absolute/test/Libs probe
dotnet run --project scripts/ToolLibrary.Verify -- /absolute/test/Libs generated
```

常规开发只运行相关测试，不隐式安装外部组件：

```sh
dotnet test NegativeEncoder.Desktop.Tests --filter 'FullyQualifiedName~ToolLibraryTests|FullyQualifiedName~VapourSynthTests'
```

本次 macOS ARM64 已验证独立环境首次安装、重复升级、手工脚本保留、原生 vspipe、
全部基础功能及 QTGMC 取帧，以及 FFmpeg 9.0.2 和 ffprobe。Windows/Linux 的真实
运行尚未在本机验证，不将构建/清单检查等同于运行验证。
