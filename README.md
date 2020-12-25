# NegativeEncoder

### **“消极压制(NegativeEncoder)”是什么？**

“消极压制”是指最大程度的节约系统资源，加快编码速度的一种压制方式。有时候视频质量并不是首要考虑因素，可以不用“积极的”进行一个精致的压制，何不“消极一把”呢？
本软件调用硬件加速编码（例如NVENC），这是十分有效的一种方式。虽然很多人说生成的质量不如x264的好，但是很多时候也足够用了，不是吗？

消极压制 v5.0 使用.NET Core完全重制。原始版本请查看oldver分支。

## Download

请去[Releases](https://github.com/zyzsdy/NegativeEncoder/releases)找安装包。原有百度网盘的下载不再提供了。

## VapourSynth相关

**我如何安装新的插件**

推荐使用vsrepo进行安装。进入Libs目录并打开cmd，键入`python.exe vsrepo.py update`更新vsrepo版本库，然后可以使用`python.exe vsrepo.py install [包名]`安装新的插件。

**如何手工安装**

请将VS插件直接放置在自动加载目录中，即`Libs\vapoursynth64\plugins\`下。

vpy脚本中引用到的Python库或者脚本放在安装目录`\Libs\site-packages\`下。

请注意通过vsrepo安装的Python库或脚本，可能存在`C:\Users\[用户名]\AppData\Roaming\Python\Python38\site-packages`下。而该路径在重装系统后会不可用，如果希望重装系统后保留或随NegativeEncoder主程序迁移，请在运行vsrepo后，将这些文件移动至安装目录`\Libs\site-packages\`下。

## LICENSE

MIT (不包含Lib)

Lib内的各软件请遵守它们各自的LICENSE