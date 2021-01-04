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

    public enum AVSync
    {
        Cfr,
        ForceCfr,
        Vfr
    }

    public enum FieldOrder
    {
        TFF,
        BFF
    }

    public enum DeInterlaceMethodPreset
    {
        HwNormal, //硬件反交错 普通模式（NVENC/QSV)
        HwBob, //硬件反交错 Double（NVENC/QSV)
        HwIt, //硬件反交错 IVTC (QSV)
        AfsDefault, //AFS Default (NVENC/VCE)
        AfsTriple, //AFS Triple (NVENC/VCE)
        AfsDouble, //AFS Double (NVENC/VCE)
        AfsAnime, //AFS Anime (NVENC/VCE)
        AfsAnime24fps, //AFS Anime 24fps (NVENC/VCE)
        Afs24fps, //AFS 24fps IVTC (NVENC/VCE)
        Afs30fps, //AFS 30fps (NVENC/VCE)
        Nnedi64NoPre, //nnedi 64(32x6) no prescreen slow (NVENC/VCE)
        Nnedi64Fast, //nnedi 64(32x6) fast (NVENC/VCE)
        Nnedi32Fast, //nnedi 32(32x4) fast (NVENC/VCE)
        YadifTff, //Yadif TFF (NVENC)
        YadifBff, //Yadif BFF (NVENC)
        YadifBob, //Yadif Double (NVENC)
    }

    public enum AudioEncode
    {
        None,
        Copy,
        Encode
    }

    public enum OutputFormat
    {
        MP4,
        MPEGTS,
        FLV
    }
}
