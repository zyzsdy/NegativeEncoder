using System.ComponentModel;
using PropertyChanged;

namespace NegativeEncoder.VsScriptBuilder;

[AddINotifyPropertyChangedInterface]
public class VsScript : INotifyPropertyChanged
{
    /// <summary>
    ///     字幕文件路径
    /// </summary>
    public string SubFile { get; set; } = string.Empty;

    /// <summary>
    ///     是否调整大小
    /// </summary>
    public bool IsResize { get; set; } = false;

    public string ResizeWidth { get; set; } = "1920";
    public string ResizeHeight { get; set; } = "1080";

    public bool UseVsfmod { get; set; } = false;
    public bool UseRepeat { get; set; } = false;
    public bool UseQTGMC { get; set; } = false;
    public bool UseIVTC { get; set; } = false;
    public bool UseYadifDouble { get; set; } = false;
    public bool UseYadifNormal { get; set; } = false;

    public event PropertyChangedEventHandler PropertyChanged;

    private void RaisePropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}