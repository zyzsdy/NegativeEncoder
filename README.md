# NegativeEncoder

### **“消极压制(NegativeEncoder)”是什么？**

“消极压制”是指最大程度的节约系统资源，加快编码速度的一种压制方式。有时候视频质量并不是首要考虑因素，可以不用“积极的”进行一个精致的压制，何不“消极一把”呢？
本软件调用QuickSync或是NVENC进行硬件加速编码，这是十分有效的一种方式。虽然很多人说生成的质量不如x264的好，但是很多时候也足够用了，不是吗？

## Download

唯一指定下载地址：https://pan.baidu.com/s/1UK2NsaY4h8j-rwC2KwbB4A 密码:7x7x

## 关于Lib目录

```python
Lib/
    avstools/
        plugins/  # 这里面放AVS插件
            VSFilterMod.dll # 字幕
            LSMASHSource.dll # 源
            # ... 其他插件看喜好
        avisynth.dll # AVS(32位)
        avs2pipemod.exe # https://github.com/chikuzen/avs2pipemod
        DevIL.dll # 是AVS本体依赖的dll
    nvenc/ # https://github.com/rigaya/NVEnc
    qsvenc/ # https://github.com/rigaya/QSVEnc
    ffmpeg.exe
    mkvmerge.exe
    neroAacEnc.exe
    MP4Box.exe
    js.dll # MP4Box的依赖dll
    libeay32.dll # MP4Box的依赖dll
    libgpac.dll # MP4Box的依赖dll
    ssleay.dll # MP4Box的依赖dll
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