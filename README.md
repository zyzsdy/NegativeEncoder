# NegativeEncoder

### **“消极压制(NegativeEncoder)”是什么？**

“消极压制”是指最大程度的节约系统资源，加快编码速度的一种压制方式。有时候视频质量并不是首要考虑因素，可以不用“积极的”进行一个精致的压制，何不“消极一把”呢？
本软件调用QuickSync或是NVENC进行硬件加速编码，这是十分有效的一种方式。虽然很多人说生成的质量不如x264的好，但是很多时候也足够用了，不是吗？

## Download

请去[Releases](https://github.com/zyzsdy/NegativeEncoder/releases)找安装包。原有百度网盘的下载不再提供了。

## VapourSynth相关

Q: 没有自带的VS插件放在哪里？

A: 请放在安装目录\Libs\vapoursynth64\plugins\中

Q: vpy脚本中引用到的Python库或者脚本放在哪里？

A: 请放在安装目录\Libs\PyLibs\中

## 关于Lib目录

```python
Lib/
    python 3.7 embed版本
    vapoursynth portable版本
    nvencc.exe及依赖dll # https://github.com/rigaya/NVEnc
    qsvencc.exe及依赖dll # https://github.com/rigaya/QSVEnc
    ffmpeg.exe
    mkvmerge.exe
    neroAacEnc.exe
    MP4Box.exe及依赖dll
```

Q: 为什么源码不包含Lib目录

A: 因为大（200多MB呢）

Q: 为什么作者不在本代码里提供Build出Lib目录的简易方式？

A: 因为懒

Q: 这个工具的全部功能都可以用这些Lib实现，而且这个工具仅仅是调用了这些Lib而已，为什么还需要开发这个工具？

A: 因为懒，麻烦一次受益终生。

## LICENSE

MIT (不包含Lib)

Lib内的各软件请遵守它们各自的LICENSE