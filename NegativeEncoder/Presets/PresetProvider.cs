using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NegativeEncoder.Presets
{
    public static class PresetProvider
    {
        public static void InitPresetAutoSave()
        {
            AppContext.PresetContext.CurrentPreset.PropertyChanged += async (sender, e) =>
            {
                //检查预设是否合法
                var preset = AppContext.PresetContext.CurrentPreset;

                //非QSV
                if (preset.Encoder != Encoder.QSV)
                {
                    //编码器非QSV的时候禁止选择LA/LA-ICQ和QVBR模式
                    if (preset.EncodeMode == EncodeMode.LA || preset.EncodeMode == EncodeMode.LAICQ || preset.EncodeMode == EncodeMode.QVBR)
                    {
                        AppContext.PresetContext.CurrentPreset.EncodeMode = EncodeMode.VBR;
                    }

                    //编码器非QSV的时候禁止选择D3D模式
                    if (preset.D3DMode != D3DMode.Auto)
                    {
                        AppContext.PresetContext.CurrentPreset.D3DMode = D3DMode.Auto;
                    }
                }

                //非NVENC
                if (preset.Encoder != Encoder.NVENC)
                {
                    //编码器非NVENC时，只能使用8 bit模式
                    if (preset.ColorDepth != ColorDepth.C8Bit)
                    {
                        AppContext.PresetContext.CurrentPreset.ColorDepth = ColorDepth.C8Bit;
                    }
                }

                //存储预设到文件
                await SystemOptions.SystemOption.SaveOption(AppContext.PresetContext.CurrentPreset);
            };
        }

        public static async Task LoadPresetAutoSave()
        {
            AppContext.PresetContext.CurrentPreset = await SystemOptions.SystemOption.ReadOption<Preset>();
        }
    }
}
