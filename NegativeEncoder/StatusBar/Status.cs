using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace NegativeEncoder.StatusBar
{
    public class Status : INotifyPropertyChanged
    {
        private string _mainStatus = string.Empty;
        public string MainStatus { get => _mainStatus; set { _mainStatus = value; N(nameof(MainStatus)); } }

        private int _progress = 0;
        public int Progress { get => _progress; set { _progress = value; N(nameof(Progress)); } }

        private string _encoderStatus = "空闲";
        public string EncoderStatus { get => _encoderStatus; set { _encoderStatus = value; N(nameof(EncoderStatus)); } }

        public event PropertyChangedEventHandler PropertyChanged;
        protected internal virtual void N(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
