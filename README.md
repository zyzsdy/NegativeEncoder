# NegativeEncoder

### **“消极压制(NegativeEncoder)”是什么？**

“消极压制”是指最大程度的节约系统资源，加快编码速度的一种压制方式。有时候视频质量并不是首要考虑因素，可以不用“积极的”进行一个精致的压制，何不“消极一把”呢？
本软件调用硬件加速编码（例如NVENC），这是十分有效的一种方式。虽然很多人说生成的质量不如x264的好，但是很多时候也足够用了，不是吗？

消极压制 v6.0.0 使用 .NET 10 + Avalonia UI 完全重制，这让本应用实现了跨平台。

我们提供 Windows x64、 macOS ARM64 (Apple Silicon) 和 Linux x64、 Linux ARM64 四种格式的安装包，以及多语言支持。

在 Apple Silicon 平台，使用 [VTEnc](https://github.com/zyzsdy/VTEnc) 作为硬件编码器。

## Download

请去[Releases](https://github.com/zyzsdy/NegativeEncoder/releases)下载最新的安装包。

首次使用时，请从“帮助 → 工具库管理”下载所需的编码工具。升级或卸载桌面应用都会保留已下载的工具库。

升级说明：使用安装包升级时，会默认覆盖原目录。

卸载说明：如果卸载后不想保留工具库，可以在“帮助 → 工具库管理”中点击“清理工具库”，然后再卸载。若卸载前忘记操作，你也可以简单的删除Lib目录。

## LICENSE

MIT (不包含Lib)

Lib内的各软件请遵守它们各自的LICENSE
