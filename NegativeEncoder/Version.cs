using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NegativeEncoder
{
    class Version
    {
        public const string VER = "0.4.2";
        public static string AboutText = "Negative Encoder (消极压制) v" + VER + @"
By Zyzsdy

Github页面: https://github.com/zyzsdy/NegativeEncoder

“消极压制”的含义是什么？
“消极压制”是指最大程度的节约系统资源，加快编码速度的一种压制方式。有时候视频质量并不是首要考虑因素，可以不用“积极的”进行一个精致的压制，何不“消极一把”呢？
本软件调用QuickSync或是NVENC进行硬件加速编码，这是十分有效的一种方式。虽然很多人说生成的质量不如x264的好，但是很多时候也足够用了，不是吗？

使用前需要确保你使用了支持QuickSync的Intel核芯显卡，或是支持NVENC的NVIDIA显卡，才可用本软件编码。

---------------------------------------
本软件集成了各种视频处理工具包，在此向他们致谢：
特别感谢rigaya的QSVEnc和NVEnc，它们是本软件的核心：
* QSVEnc（https://github.com/rigaya/QSVEnc）
* NVEnc (https://github.com/rigaya/NVEnc)
其他工具：
* VapourSynth
* L-SMASH Works
* VSFilterMod
* FFMpeg
* neroAacEnc
* MKVToolNix

感谢：
本软件的集成方式与界面皆参考了小丸工具箱。
---------------------------------------

高级参数指南：
QuickSync调用QSVEncC.exe，NVENC调用NVEncC.exe
请分别参考原作者的选项指南：

QSVEncC：https://github.com/rigaya/QSVEnc/blob/master/QSVEncC_Options.en.md
NVEncC：https://github.com/rigaya/NVEnc/blob/master/NVEncC_Options.zh-cn.md

---------------------------------------

更新记录
v0.4.2 pre-release

v0.4.1 (2019-1-1)
* 2019相关标记修改
* 移除MP4Box及相关依赖，使用ffmpeg进行MP4混流
* 修复bug

v0.4.0 (2019-1-1)
* 使用VapourSynth替代AVS，移除了AVS的相关组件

v0.3.0 (2018-11-18)
* 增加FFmpeg Pipe模式的多个选项
* 增加选项保存功能
* 修复bug

v0.2.1 (2018-8-6)
* 增加FFmpeg Pipe压制模式

v0.2.0 (2018-6-12)
* 删除VQP模式
* 简单压制选项中增加分辨率调整
* 增加强制音频同步功能

v0.1.4 (2018-6-11)
* 增加反交错选项(normal, bob, ivtc)，原有it选项合并到这里
* 简单压制的音频码率受到“音频”标签页内的选项控制
* 修复bug

v0.1.3 (2018-5-6)
* 增加Inverse Telecine

v0.1.2 (2018-4-15)
* 改善AVS编辑框不便输入的问题
* 修复中止按钮无法结束所有进程的问题
* AvsBuilder增加高精度转换选项（感谢 灵动 提供解决方案）

v0.1.1 (2018-4-11)
* AvsBuilder增加repeat选项
* 修改文本框内容时实时更新AVS
* UI细节调整

v0.1.0 (2018-4-10)
* 修复bug
* 更新依赖工具
* 准备开源

v0.0.3 (2018-4-8)
* 修复bug

v0.0.2 (2018-4-8)
* 修复音频压制初始化失败的问题
* 优化UI

v0.0.1 (2018-4-8)
* 初个版本";
    }
}
