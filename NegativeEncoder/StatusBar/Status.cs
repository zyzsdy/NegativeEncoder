using PropertyChanged;

namespace NegativeEncoder.StatusBar;

[AddINotifyPropertyChangedInterface]
public class Status
{
    public string MainStatus { get; set; }
    public int Progress { get; set; }
    public string EncoderStatus { get; set; } = "空闲";
}