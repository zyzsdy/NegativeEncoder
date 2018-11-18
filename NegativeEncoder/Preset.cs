using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace NegativeEncoder
{
    [Serializable]
    public class PresetCollection
    {
        public ObservableCollection<Preset> Presets;
        public int ActiveIndex;

        public PresetCollection()
        {
            Presets = new ObservableCollection<Preset>();
            ActiveIndex = 0;
        }

        public static void Save(PresetCollection item, string baseDir)
        {
            var cfgFilepath = Path.Combine(baseDir, "presets.cfg");
            FileStream fs = new FileStream(cfgFilepath, FileMode.Create);
            BinaryFormatter b = new BinaryFormatter();
            b.Serialize(fs, item);
            fs.Close();
        }

        public static PresetCollection Load(string baseDir)
        {
            var cfgFilepath = Path.Combine(baseDir, "presets.cfg");
            var c = new PresetCollection();

            try
            {
                FileStream fs = new FileStream(cfgFilepath, FileMode.Open, FileAccess.Read, FileShare.Read);
                BinaryFormatter b = new BinaryFormatter();
                c = b.Deserialize(fs) as PresetCollection;
            }
            catch
            {
                
            }

            return c;
        }
    }

    [Serializable]
    public class Preset
    {
        public string Name { get; set; }
        public Encoder Encoder { get; set; }
        public EncoderMode EncoderMode { get; set; }
        public string EncoderParamValue { get; set; }
        public bool IsInterlaceSource { get; set; }
        public InterlacedMode InterlacedMode { get; set; }
        public DeintOption DeintOption { get; set; }
        public bool IsSetDar { get; set; }
        public string DarValue { get; set; }
        public bool IsSetCustomParams { get; set; }
        public string CustomParams { get; set; }

        public static Preset GetPresentPreset(MainWindow mw, string name)
        {
            var res = new Preset
            {
                Name = name,
                Encoder = (Encoder)Enum.ToObject(typeof(Encoder), mw.encoderSelecter.SelectedIndex),
                IsInterlaceSource = mw.isinterlaceCheckBox.IsChecked ?? false,
                InterlacedMode = (InterlacedMode)Enum.ToObject(typeof(InterlacedMode), mw.tffOrBffComboBox.SelectedIndex),
                DeintOption = (DeintOption)Enum.ToObject(typeof(DeintOption), mw.deintOptionComboBox.SelectedIndex),
                IsSetDar = mw.isSetDarCheckBox.IsChecked ?? false,
                DarValue = mw.darValueBox.Text,
                IsSetCustomParams = mw.customParameterSwitcher.IsChecked ?? false,
                CustomParams = mw.customParameterInputBox.Text
            };

            if (mw.cqpRadioButton.IsChecked == true)
            {
                res.EncoderMode = EncoderMode.CQP;
                res.EncoderParamValue = mw.cqpValueBox.Text;
            }
            if (mw.vqpRadioButton.IsChecked == true)
            {
                res.EncoderMode = EncoderMode.VQP;
                res.EncoderParamValue = mw.vqpValueBox.Text;
            }
            if (mw.laRadioButton.IsChecked == true)
            {
                res.EncoderMode = EncoderMode.LA;
                res.EncoderParamValue = mw.laValueBox.Text;
            }
            if (mw.cbrRadioButton.IsChecked == true)
            {
                res.EncoderMode = EncoderMode.CBR;
                res.EncoderParamValue = mw.cbrValueBox.Text;
            }
            if (mw.vbrRadioButton.IsChecked == true)
            {
                res.EncoderMode = EncoderMode.VBR;
                res.EncoderParamValue = mw.vbrValueBox.Text;
            }
            if (mw.icqRadioButton.IsChecked == true)
            {
                res.EncoderMode = EncoderMode.ICQ;
                res.EncoderParamValue = mw.icqValueBox.Text;
            }
            if (mw.laicqRadioButton.IsChecked == true)
            {
                res.EncoderMode = EncoderMode.LAICQ;
                res.EncoderParamValue = mw.laicqValueBox.Text;
            }

            return res;
        }
    }
}
