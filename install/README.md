# Windows x64 安装包

当前应用版本为 **5.2.1.0**。普通 CI 构建并测试主程序和收集脚本。GitHub Actions 的 **Windows Installer** 可手动运行，也会在推送数字版本 tag（例如 `v5.2.1` 或 `v5.2.1.0`）时自动运行。

成功后下载 artifact **NegativeEncoder-Windows-x64**，其中包含安装程序、SHA256SUMS、依赖锁文件及第三方声明。tag 构建还会将这些文件上传到对应的 GitHub Release 并自动发布；已有 Release 会更新同名附件，保留标题和说明。新 Release 会先创建为草稿，附件上传成功后再发布。手动运行普通分支采用项目 Version，仅上传 artifact；tag 构建采用 tag 的数字版本并发布 Release。

## 5.2.1.0 更新

- 修复 #76：VapourSynth 和 FFmpeg 管道在视频编码、音频处理或混流失败时保留已生成的中间媒体文件，并在任务日志中显示保留路径。保留的文件不代表完整成品；任务仍标记为失败，成功完成后仍正常清理中间文件和临时脚本。
- 修复 #77：任务面板不再显示独立任务栏入口；重新激活面板时同步恢复最小化的主窗口，并保留原来的最大化状态，避免关闭面板后主窗口仍处于最小化状态。
- 新增 11 个回归测试场景，覆盖失败后的媒体保留、成功后的清理及普通／最大化窗口的恢复流程。

## 本地构建

需要 Windows x64、.NET 10 SDK、Python 3.11+、7-Zip 和 NSIS 3.11。默认查找 Program Files 中的 7-Zip 与 NSIS；可用 `-SevenZip`、`-MakeNsis` 指定路径。

```powershell
dotnet test NegativeEncoder.sln --configuration Release
python -m unittest discover -s install/tests -v
./install/build-installer.ps1
```

输出为 `artifacts/installer/NegativeEncoder-<version>-win-x64-setup.exe`。下载缓存位于 `artifacts/dependency-cache`；默认暂存目录 `artifacts/package` 必须为空。脚本不会自动删除目录。可用 `-PackageDirectory`、`-OutputDirectory` 指定其他位置。

已经组装好的依赖可用 `-ReuseDependencies` 复用；它会检查锁文件一致性并重新运行验证。更改锁文件后请改用空暂存目录。如果已经以相同 win-x64 / SelfContained 参数完成 NuGet 还原，可加 `-NoRestore`。

## 固定依赖

`dependencies.lock.json` 保存版本化 URL、整个发行资产的 SHA256、所选文件映射及文件 SHA256。缓存命中同样需要校验。收集过程没有 latest 查询或在线 pip 版本解析。

应用内 FFmpeg 更新检查与下载共用 GyanD/codexffmpeg 的正式发布元数据，仅接受与发布版本匹配的 `ffmpeg-<version>-full_build.7z` 静态构建附件，与安装包使用同一发行源。安装包继续按锁文件固定版本，运行时更新使用该发行源的最新正式版本。

VapourSynth 插件和脚本采用固定 VSRepo commit 中的记录，直接下载指定发行资产并验证文件。修改依赖时需重新核对版本、URL、哈希及文件映射；不能只修改版本号。VSRepo 2.0.0 的 pip 转交和缺少版本锁定语法，不影响这个固定收集方式。

NVEnc、QSVEnc、VCEEnc、qaac、MKVToolNix 各自保留运行目录，避免 DLL 冲突。应用兼容旧版平铺 Libs。Python 3.13.16 与 VapourSynth R79 位于 `Libs/python`，应用直接调用 wheel 内的原生 VSPipe；插件在该包的 plugins 目录，脚本在 `Libs/site-packages`。

havsfunc r33 的 `_lambda` / `_global` 参数兼容修正由锁文件记录替换次数和修正后哈希。QTGMC 默认流程会初始化 EEDI3，因此同时包含 EEDI3m。源码生成的字幕脚本兼容新的 xyvsf 和旧版 vsf。

## 验证与运行前提

收集脚本检查工具版本、VapourSynth 插件加载、QTGMC 取帧、原生 VSPipe、视频解码、IVTC 和两种字幕渲染。工作流还会静默安装到临时目录、运行主程序 SaveCmdTools，再搬迁暂存目录重复检查。

2026-10-14 本地验证：28 项 .NET 测试与 5 项收集脚本测试通过；完整下载/组装、软件处理取帧、中文及空格路径迁移、self-contained 主程序运行、NSIS 3.11 编译和安装包完整性/SHA256 校验通过。Apple 安装包及 MSI 的 SHA256/Authenticode 验证、已有运行库跳过逻辑和一秒 CBR AAC 编码检查通过；未执行系统 MSI 安装或改变现有 Apple 运行库。GitHub 工作流尚未远程运行，静默安装步骤由远程工作流执行；本地使用解包完整性检查，未改动现有安装注册表。

安装器的组件页提供默认不勾选的 **Apple AAC 运行库（可选，联网安装）**。勾选后先运行 qaac 自检；已可用就跳过，否则根据 `apple-runtime.lock.json` 从 Apple 官方下载约 264 MB 的历史 iTunes 12.7.2 安装包，提取 x64 Apple Application Support 6.2.1 MSI（约 51 MB）。完整安装包和 MSI 均验证固定 SHA256 及 Windows Authenticode 签名，然后打开 Apple 的交互式许可/安装向导，按需请求管理员权限。安装失败或取消不会阻止主程序完成安装。此选项使用的是已明确标注的 2017 年历史版本，不会替换已经可用的运行库。

Apple 安装包/MSI 不进入 Git，也不内嵌到本产品安装包。下载缓存位于安装目录的 `Setup/AppleRuntime-cache`；卸载 NegativeEncoder 不会卸载共享 Apple Application Support。

应用执行需要 qaac 的任务前重新运行 `qaac64.exe --check`，同时检查成功退出和输出中的 CoreAudioToolbox 版本。缺失、损坏或超时会阻止该任务启动并提示重新运行安装程序选择运行库组件；不会静默切换编码器。不需要 qaac 的普通编码、音频复制等流程不受影响。“关于”窗口也会显示 qaac 状态和已加载运行库版本。

可用 `NegativeEncoder.exe --runFunc CheckQaac` 检查（退出码 0 可用，1 不可用），`--runFunc InstallAppleRuntime` 重新准备运行库，`--runFunc PrepareAppleRuntime` 仅验证下载、签名和提取，不执行系统安装。运行这些命令时同样支持 `--baseDir`。

GPU 编码、NNEDI3CL/OpenCL 需要对应硬件和驱动；普通 hosted runner 不验证实际硬件编码。原生工具还要求兼容的 Microsoft Visual C++ 2015-2026 x64 运行库。运行库可加载并不代表所有编码模式的音质问题已解决，CBR 仍需按选定版本进行编码验证。

原始发行包中的许可、README 和版权通知保留在 `ThirdParty`，wheel 内的许可随 Python 包保留。锁文件和声明记录上游来源；对外发布时还应准备各组件许可要求的对应源码。FFmpeg 采用 Gyan full static GPL 构建，不能把整个安装包的第三方部分统称为主程序的 MIT 许可。
