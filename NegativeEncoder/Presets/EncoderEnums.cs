using System;
using System.Collections.Generic;
using System.Text;

namespace NegativeEncoder.Presets
{
    public enum EncodingAction
    {
        Simple,
        HDRTagUseFFMpeg,
        VSPipe,
        AudioEncoding,
        AudioExtract,
        Muxer,
        FFMpegPipe,
        SimpleWithAss
    }

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
        /// <summary>
        /// 硬件反交错 普通模式（NVENC/QSV)
        /// </summary>
        HwNormal,
        /// <summary>
        /// 硬件反交错 Double（NVENC/QSV)
        /// </summary>
        HwBob,
        /// <summary>
        /// 硬件反交错 IVTC (QSV)
        /// </summary>
        HwIt,
        /// <summary>
        /// AFS Default (NVENC/VCE)
        /// </summary>
        AfsDefault,
        /// <summary>
        /// AFS Triple (NVENC/VCE)
        /// </summary>
        AfsTriple,
        /// <summary>
        /// AFS Double (NVENC/VCE)
        /// </summary>
        AfsDouble,
        /// <summary>
        /// AFS Anime (NVENC/VCE)
        /// </summary>
        AfsAnime,
        /// <summary>
        /// AFS Anime 24fps (NVENC/VCE)
        /// </summary>
        AfsAnime24fps,
        /// <summary>
        /// AFS 24fps IVTC (NVENC/VCE)
        /// </summary>
        Afs24fps,
        /// <summary>
        /// AFS 30fps (NVENC/VCE)
        /// </summary>
        Afs30fps,
        /// <summary>
        /// nnedi 64(32x6) no prescreen slow (NVENC/VCE)
        /// </summary>
        Nnedi64NoPre,
        /// <summary>
        /// nnedi 64(32x6) fast (NVENC/VCE)
        /// </summary>
        Nnedi64Fast,
        /// <summary>
        /// nnedi 32(32x4) fast (NVENC/VCE)
        /// </summary>
        Nnedi32Fast,
        /// <summary>
        /// Yadif TFF (NVENC)
        /// </summary>
        YadifTff,
        /// <summary>
        /// Yadif BFF (NVENC)
        /// </summary>
        YadifBff,
        /// <summary>
        /// Yadif Double (NVENC)
        /// </summary>
        YadifBob,
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
        FLV,
        MKV
    }

    public enum HdrType
    {
        SDR,
        HDR10,
        HLG
    }

    public enum Hdr2Sdr
    {
        None,
        Hable,
        Mobius,
        Reinhard,
        Bt2390
    }
}
