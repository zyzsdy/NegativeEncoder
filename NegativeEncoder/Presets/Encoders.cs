using System;
using System.Collections.Generic;
using System.Text;

namespace NegativeEncoder.Presets
{
    public enum Encoder
    {
        NVENC,
        QSV,
        VCE
    }

    public enum Codec
    {
        AVC,
        HEVC
    }

    public enum EncodeMode
    {
        CQP,
        CBR,
        VBR,
        LA,
        LAICQ,
        QVBR
    }

    public enum QualityPreset
    {
        Performance = 1,
        Balanced = 4,
        Quality = 7
    }

    public enum ColorDepth
    {
        C10Bit = 10,
        C8Bit = 8
    }

    public enum Decoder
    {
        AVSW,
        AVHW
    }

    public enum D3DMode
    {
        Disable = -1,
        Auto,
        D3D9 = 9,
        D3D11 = 11
    }
}
