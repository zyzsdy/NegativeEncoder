using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace NegativeEncoder
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool windowIsLoaded;
        private Config config;
        public string baseDir;
        private EncodingQueue encodingQueue;
        public PresetCollection presetcollection;

        public MainWindow()
        {
            // 启动
            string[] pargs = Environment.GetCommandLineArgs();
            baseDir = System.IO.Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            if (pargs.Length > 1)
            {
                if (pargs[1] != "--baseDir")
                {
                    MessageBox.Show("不支持的启动参数！", "初始化警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    baseDir = pargs[2];
                }
            }
            // 准备数据对象
            encodingQueue = new EncodingQueue();
            presetcollection = PresetCollection.Load(baseDir);
            // 初始化界面
            InitializeComponent();
            config = new Config();

            presetSelecter.ItemsSource = presetcollection.Presets;
        }

        private void changeDisableForEncodeMode(int encoder)
        {
            cqpRadioButton.IsEnabled = false;
            vqpRadioButton.IsEnabled = false;
            laRadioButton.IsEnabled = false;
            cbrRadioButton.IsEnabled = false;
            vbrRadioButton.IsEnabled = false;
            icqRadioButton.IsEnabled = false;
            laicqRadioButton.IsEnabled = false;
            isinterlaceCheckBox.IsEnabled = false;
            isSetDarCheckBox.IsEnabled = false;
            if (!customParameterSwitcher.IsChecked ?? false)
            {
                //encoder == 0 as QSV ; 1 as NVENC
                if (encoder == 0)
                {
                    vqpRadioButton.IsEnabled = true;
                    laRadioButton.IsEnabled = true;
                    icqRadioButton.IsEnabled = true;
                    laicqRadioButton.IsEnabled = true;
                }
                cqpRadioButton.IsEnabled = true;
                cbrRadioButton.IsEnabled = true;
                vbrRadioButton.IsEnabled = true;
                isinterlaceCheckBox.IsEnabled = true;
                isSetDarCheckBox.IsEnabled = true;
            }
            DeintOption sel = (DeintOption)Enum.ToObject(typeof(DeintOption), deintOptionComboBox.SelectedIndex);
            if (encoder == 1 && sel == DeintOption.IVTC)
            {
                MessageBox.Show("Nvenc not support option ivtc");
                deintOptionComboBox.SelectedIndex = (int)DeintOption.NORMAL;
                return;
            }
        }

        private void checkEncoderModeSelectAndSetDisable()
        {
            //if ((isAudioFix.IsChecked ?? false) && deintOptionComboBox.SelectedIndex == (int)DeintOption.DOUBLE) deintOptionComboBox.SelectedIndex = (int)DeintOption.NORMAL;
            cqpValueBox.IsEnabled = false;
            vqpValueBox.IsEnabled = false;
            laValueBox.IsEnabled = false;
            cbrValueBox.IsEnabled = false;
            vbrValueBox.IsEnabled = false;
            icqValueBox.IsEnabled = false;
            laicqValueBox.IsEnabled = false;
            tffOrBffComboBox.IsEnabled = false;
            //deintOptionComboBox.IsEnabled = false;
            darValueBox.IsEnabled = false;
            customParameterInputBox.IsEnabled = false;
            if (customParameterSwitcher.IsChecked ?? false)
            {
                customParameterInputBox.IsEnabled = true;
            }
            else
            {
                if (cqpRadioButton.IsChecked ?? false) cqpValueBox.IsEnabled = true;
                if (vqpRadioButton.IsChecked ?? false) vqpValueBox.IsEnabled = true;
                if (laRadioButton.IsChecked ?? false) laValueBox.IsEnabled = true;
                if (cbrRadioButton.IsChecked ?? false) cbrValueBox.IsEnabled = true;
                if (vbrRadioButton.IsChecked ?? false) vbrValueBox.IsEnabled = true;
                if (icqRadioButton.IsChecked ?? false) icqValueBox.IsEnabled = true;
                if (laicqRadioButton.IsChecked ?? false) laicqValueBox.IsEnabled = true;
                if (isinterlaceCheckBox.IsChecked ?? false)
                {
                    tffOrBffComboBox.IsEnabled = true;
                    //if(!isAudioFix.IsChecked ?? false) deintOptionComboBox.IsEnabled = true;
                }
                if (isSetDarCheckBox.IsChecked ?? false) darValueBox.IsEnabled = true;
            }
            
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //载入config
            if (config != null)
            {
                encoderSelecter.SelectedIndex = (int)(config.ActiveEncoder ?? Encoder.QSV);

                cqpRadioButton.IsChecked = (config.ActiveEncoderMode ?? EncoderMode.CQP) == EncoderMode.CQP;
                vqpRadioButton.IsChecked = (config.ActiveEncoderMode ?? EncoderMode.CQP) == EncoderMode.VQP;
                laRadioButton.IsChecked = (config.ActiveEncoderMode ?? EncoderMode.CQP) == EncoderMode.LA;
                cbrRadioButton.IsChecked = (config.ActiveEncoderMode ?? EncoderMode.CQP) == EncoderMode.CBR;
                vbrRadioButton.IsChecked = (config.ActiveEncoderMode ?? EncoderMode.CQP) == EncoderMode.VBR;
                icqRadioButton.IsChecked = (config.ActiveEncoderMode ?? EncoderMode.CQP) == EncoderMode.ICQ;
                laicqRadioButton.IsChecked = (config.ActiveEncoderMode ?? EncoderMode.CQP) == EncoderMode.LAICQ;

                if (config.CqpValue != null) cqpValueBox.Text = config.CqpValue;
                if (config.VqpValue != null) vqpValueBox.Text = config.VqpValue;
                if (config.LaValue != null) laValueBox.Text = config.LaValue;
                if (config.CbrValue != null) cbrValueBox.Text = config.CbrValue;
                if (config.VbrValue != null) vbrValueBox.Text = config.VbrValue;
                if (config.IcqValue != null) icqValueBox.Text = config.IcqValue;
                if (config.LaicqValue != null) laicqValueBox.Text = config.LaicqValue;

                isinterlaceCheckBox.IsChecked = config.IsInterlaceSource;
                tffOrBffComboBox.SelectedIndex = (int)(config.ActiveInterlacedMode ?? InterlacedMode.TFF);

                isSetDarCheckBox.IsChecked = config.IsSetDar;
                if (config.DarValue != null) darValueBox.Text = config.DarValue;

                customParameterSwitcher.IsChecked = config.IsUseCustomParameter;
                if (config.CustomParameter != null) customParameterInputBox.Text = config.CustomParameter;

                isAudioEncodeCheckBox.IsChecked = config.IsAudioEncoding;
                isAudioFix.IsChecked = config.IsAudioFix;

                if (config.BitrateValue != null) audioBitrateTextBox.Text = config.BitrateValue;

                boxFormatComboBox.SelectedIndex = (int)(config.ActiveBoxFormat ?? BoxFormat.MKV);
                deintOptionComboBox.SelectedIndex = (int)(config.ActiveDeintOption ?? DeintOption.NORMAL);

                simpleResizeCheckBox.IsChecked = config.IsSetResize;
                if (config.ResizeXValue != null) simpleResizeX.Text = config.ResizeXValue;
                if (config.ResizeYValue != null) simpleResizeY.Text = config.ResizeYValue;
            }

            // 初始化
            aboutText.Text = Version.AboutText;
            changeDisableForEncodeMode(encoderSelecter.SelectedIndex);
            checkEncoderModeSelectAndSetDisable();
            encodingTaskListBox.ItemsSource = encodingQueue;
            presetSelecter.SelectedIndex = presetcollection.ActiveIndex;
            windowIsLoaded = true;
        }

        private void encoderSelecter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(config != null) config.ActiveEncoder = (Encoder)Enum.ToObject(typeof(Encoder), encoderSelecter.SelectedIndex);
            if (windowIsLoaded)
            {
                if(cqpRadioButton.IsChecked == false && cbrRadioButton.IsChecked == false && vbrRadioButton.IsChecked == false)
                {
                    cqpRadioButton.IsChecked = true;
                }
                changeDisableForEncodeMode(encoderSelecter.SelectedIndex);
            }
        }

        private void cqpRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (config != null) config.ActiveEncoderMode = EncoderMode.CQP;
            if (windowIsLoaded) checkEncoderModeSelectAndSetDisable();
        }

        private void vqpRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (config != null) config.ActiveEncoderMode = EncoderMode.VQP;
            if (windowIsLoaded) checkEncoderModeSelectAndSetDisable();
        }

        private void laRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (config != null) config.ActiveEncoderMode = EncoderMode.LA;
            if (windowIsLoaded) checkEncoderModeSelectAndSetDisable();
        }

        private void cbrRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (config != null) config.ActiveEncoderMode = EncoderMode.CBR;
            if (windowIsLoaded) checkEncoderModeSelectAndSetDisable();
        }

        private void vbrRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (config != null) config.ActiveEncoderMode = EncoderMode.VBR;
            if (windowIsLoaded) checkEncoderModeSelectAndSetDisable();
        }

        private void icqRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (config != null) config.ActiveEncoderMode = EncoderMode.ICQ;
            if (windowIsLoaded) checkEncoderModeSelectAndSetDisable();
        }

        private void laicqRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (config != null) config.ActiveEncoderMode = EncoderMode.LAICQ;
            if (windowIsLoaded) checkEncoderModeSelectAndSetDisable();
        }

        private void isinterlaceCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (config != null) config.IsInterlaceSource = isinterlaceCheckBox.IsChecked ?? false;
            if (windowIsLoaded) checkEncoderModeSelectAndSetDisable();
        }

        private void isSetDarCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (config != null) config.IsSetDar = isSetDarCheckBox.IsChecked ?? false;
            if (windowIsLoaded) checkEncoderModeSelectAndSetDisable();
        }

        private void customParameterSwitcher_Checked(object sender, RoutedEventArgs e)
        {
            if (config != null) config.IsUseCustomParameter = customParameterSwitcher.IsChecked ?? false;
            if (windowIsLoaded)
            {
                changeDisableForEncodeMode(encoderSelecter.SelectedIndex);
                checkEncoderModeSelectAndSetDisable();
            }
        }

        private void cqpValueBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (config != null) config.CqpValue = cqpValueBox.Text;
        }

        private void vqpValueBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (config != null) config.VqpValue = vqpValueBox.Text;
        }

        private void laValueBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (config != null) config.LaValue = laValueBox.Text;
        }

        private void cbrValueBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (config != null) config.CbrValue = cbrValueBox.Text;
        }

        private void vbrValueBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (config != null) config.VbrValue = vbrValueBox.Text;
        }

        private void icqValueBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (config != null) config.IcqValue = icqValueBox.Text;
        }

        private void laicqValueBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (config != null) config.LaicqValue = laicqValueBox.Text;
        }

        private void tffOrBffComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (config != null) config.ActiveInterlacedMode = (InterlacedMode)Enum.ToObject(typeof(InterlacedMode), tffOrBffComboBox.SelectedIndex);
        }

        private void darValueBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (config != null) config.DarValue = darValueBox.Text;
        }

        private void customParameterInputBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (config != null) config.CustomParameter = customParameterInputBox.Text;
        }

        private void isAudioEncodeCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (config != null) config.IsAudioEncoding = isAudioEncodeCheckBox.IsChecked ?? false;
        }

        // “视频”选项卡：处理输入的拖动
        private void videoInputTextBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void videoInputTextBox_PreviewDrop(object sender, DragEventArgs e)
        {
            foreach (string f in (string[])e.Data.GetData(DataFormats.FileDrop)) {
                videoInputTextBox.Text = f;
            }
            AutoSetSaveVideoPath(videoInputTextBox, "_neenc.mp4", videoSaveTextBox);
        }

        private void AutoSetSaveVideoPath(TextBox source, string suffix, TextBox dest)
        {
            try
            {
                var directoryPath = System.IO.Path.GetDirectoryName(source.Text);
                var newOutputName = System.IO.Path.GetFileNameWithoutExtension(source.Text) + suffix;
                var finalPath = System.IO.Path.Combine(directoryPath, newOutputName);
                dest.Text = finalPath;
            }
            catch
            {
                MessageBox.Show("填入的路径不正确。", "输入校验失败", MessageBoxButton.OK, MessageBoxImage.Asterisk);
            }
            
        }

        private void inputBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if(ofd.ShowDialog() == true)
            {
                videoInputTextBox.Text = ofd.FileName;
            }
            AutoSetSaveVideoPath(videoInputTextBox, "_neenc.mp4", videoSaveTextBox);
        }

        private void saveBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog { DefaultExt = "mp4", Filter = "MP4 Video(*.mp4)|*.mp4|所有文件(*.*)|*.*" };
            if(sfd.ShowDialog() == true)
            {
                videoSaveTextBox.Text = sfd.FileName;
            }
        }

        private void startEncodingButton_Click(object sender, RoutedEventArgs e)
        {
            if(videoInputTextBox.Text == "" || videoSaveTextBox.Text == "")
            {
                MessageBox.Show("输入和输出都不能为空");
                return;
            }
            EncodingTask t;
            if (isAudioEncodeCheckBox.IsChecked == true)
            {
                t = encodingQueue.AddSimpleWithAudioEncodingTask(baseDir, videoInputTextBox.Text, videoSaveTextBox.Text, config);
            }
            else
            {
                t = encodingQueue.AddSimpleEncodingTask(baseDir, videoInputTextBox.Text, videoSaveTextBox.Text, config);
            }
            startEncodingButton.IsEnabled = false;
            startEncodingButton.Content = "请等待...";
            Task.Run(() =>
            {
                Thread.Sleep(3000);
                Dispatcher.Invoke(() =>
                {
                    startEncodingButton.IsEnabled = true;
                    startEncodingButton.Content = "开始压制";
                    OpenTaskDetailWindow(t);
                });
            });
        }

        // avs选项卡：avs视频拖动处理
        private void avsVideoInputTextBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void avsVideoInputTextBox_PreviewDrop(object sender, DragEventArgs e)
        {
            foreach (string f in (string[])e.Data.GetData(DataFormats.FileDrop))
            {
                avsVideoInputTextBox.Text = f;
            }
            AutoSetSaveVideoPath(avsVideoInputTextBox, "_nevs.mp4", avsVideoSaveTextBox);
            avsTextBox.Text = AvsBuilder.BuildAvs(this);
        }

        private void avsInputBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == true)
            {
                avsVideoInputTextBox.Text = ofd.FileName;
            }
            AutoSetSaveVideoPath(avsVideoInputTextBox, "_nevs.mp4", avsVideoSaveTextBox);
            avsTextBox.Text = AvsBuilder.BuildAvs(this);
        }

        // avs选项卡：avs字幕拖动处理
        private void avsSubtitleTextBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void avsSubtitleTextBox_PreviewDrop(object sender, DragEventArgs e)
        {
            foreach (string f in (string[])e.Data.GetData(DataFormats.FileDrop))
            {
                avsSubtitleTextBox.Text = f;
            }
            avsTextBox.Text = AvsBuilder.BuildAvs(this);
        }

        private void avsSubtitleBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog { Filter = "ASS 字幕文件(*.ass)|*.ass" };
            if (ofd.ShowDialog() == true)
            {
                avsSubtitleTextBox.Text = ofd.FileName;
            }
            avsTextBox.Text = AvsBuilder.BuildAvs(this);
        }

        private void avsResizeCheckBox_Click(object sender, RoutedEventArgs e)
        {
            avsTextBox.Text = AvsBuilder.BuildAvs(this);
        }

        private void avsSaveBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog { DefaultExt = "mp4", Filter = "MP4 Video(*.mp4)|*.mp4|所有文件(*.*)|*.*" };
            if (sfd.ShowDialog() == true)
            {
                avsVideoSaveTextBox.Text = sfd.FileName;
            }
        }

        private void audioBitrateTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (config != null) config.BitrateValue = audioBitrateTextBox.Text;
        }

        // 音频选项卡：输入视频拖动处理
        private void audioInputTextBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void audioInputTextBox_PreviewDrop(object sender, DragEventArgs e)
        {
            foreach (string f in (string[])e.Data.GetData(DataFormats.FileDrop))
            {
                audioInputTextBox.Text = f;
            }
            AutoSetSaveVideoPath(audioInputTextBox, "_neAAC.mp4", audioSaveTextBox);
        }

        private void audioInputBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == true)
            {
                audioInputTextBox.Text = ofd.FileName;
            }
            AutoSetSaveVideoPath(audioInputTextBox, "_neAAC.mp4", audioSaveTextBox);
        }

        private void audioSaveBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog { DefaultExt = "mp4", Filter = "MP4 Video(*.mp4)|*.mp4|所有文件(*.*)|*.*" };
            if (sfd.ShowDialog() == true)
            {
                audioSaveTextBox.Text = sfd.FileName;
            }
        }

        private void boxFormatComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(windowIsLoaded && boxFormatComboBox.SelectedIndex == (int)BoxFormat.MP4)
            {
                var result = MessageBox.Show("8102年了，B站都支持MKV直传了，居然还有人要用MP4格式。", "我看看日历", MessageBoxButton.OK, MessageBoxImage.Asterisk);
            }
            if (config != null)
            {
                config.ActiveBoxFormat = (BoxFormat)Enum.ToObject(typeof(BoxFormat), boxFormatComboBox.SelectedIndex);
                if (boxVideoInputTextBox.Text != "")
                {
                    if (config.ActiveBoxFormat == BoxFormat.MKV)
                    {
                        AutoSetSaveVideoPath(boxVideoInputTextBox, "_mux.mkv", boxSaveTextBox);
                    }
                    else
                    {
                        AutoSetSaveVideoPath(boxVideoInputTextBox, "_mux.mp4", boxSaveTextBox);
                    }
                }
            }
        }

        // 封装选项卡：输入视频拖动处理
        private void boxVideoInputTextBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void boxVideoInputTextBox_PreviewDrop(object sender, DragEventArgs e)
        {
            foreach (string f in (string[])e.Data.GetData(DataFormats.FileDrop))
            {
                boxVideoInputTextBox.Text = f;
            }
            if(config.ActiveBoxFormat == BoxFormat.MKV)
            {
                AutoSetSaveVideoPath(boxVideoInputTextBox, "_mux.mkv", boxSaveTextBox);
            }
            else
            {
                AutoSetSaveVideoPath(boxVideoInputTextBox, "_mux.mp4", boxSaveTextBox);
            }
            
        }

        private void boxInputBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == true)
            {
                boxVideoInputTextBox.Text = ofd.FileName;
            }
            if (config.ActiveBoxFormat == BoxFormat.MKV)
            {
                AutoSetSaveVideoPath(boxVideoInputTextBox, "_mux.mkv", boxSaveTextBox);
            }
            else
            {
                AutoSetSaveVideoPath(boxVideoInputTextBox, "_mux.mp4", boxSaveTextBox);
            }
        }

        // 封装选项卡：输入音频拖动处理
        private void boxAudioInputTextBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void boxAudioInputTextBox_PreviewDrop(object sender, DragEventArgs e)
        {
            foreach (string f in (string[])e.Data.GetData(DataFormats.FileDrop))
            {
                boxAudioInputTextBox.Text = f;
            }
        }

        private void boxAudioBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == true)
            {
                boxAudioInputTextBox.Text = ofd.FileName;
            }
        }

        private void boxSaveBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog { DefaultExt = "mp4", Filter = "MP4 Video(*.mp4)|*.mp4|所有文件(*.*)|*.*" };
            if (sfd.ShowDialog() == true)
            {
                boxSaveTextBox.Text = sfd.FileName;
            }
        }

        private void ListBoxItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            EncodingTask source = (EncodingTask)((ListBoxItem)sender).Content;

            OpenTaskDetailWindow(source);
        }

        private void OpenTaskDetailWindow(EncodingTask t)
        {
            TaskDetail taskDetail = new TaskDetail
            {
                DataContext = t
            };

            taskDetail.Show();
        }

        private void avsStartEncodingButton_Click(object sender, RoutedEventArgs e)
        {
            if (avsVideoSaveTextBox.Text == "")
            {
                MessageBox.Show("输出不能为空");
                return;
            }

            var t = encodingQueue.AddAvsEncodingTask(baseDir, avsTextBox.Text, avsVideoSaveTextBox.Text, config);
            avsStartEncodingButton.IsEnabled = false;
            avsStartEncodingButton.Content = "请等待...";
            Task.Run(() =>
            {
                Thread.Sleep(3000);
                Dispatcher.Invoke(() =>
                {
                    avsStartEncodingButton.IsEnabled = true;
                    avsStartEncodingButton.Content = "开始压制";
                    OpenTaskDetailWindow(t);
                });
            });
        }

        private void audioStartEncodingButton_Click(object sender, RoutedEventArgs e)
        {
            if (audioSaveTextBox.Text == "")
            {
                MessageBox.Show("输出不能为空");
                return;
            }
            try
            {
                var bittempint = int.Parse(audioBitrateTextBox.Text);
            }
            catch
            {
                MessageBox.Show("无法将比特率值解析为整数，请检查！");
                return;
            }

            var t = encodingQueue.AddAudioEncodingTask(baseDir, audioInputTextBox.Text, audioSaveTextBox.Text, config);
            audioStartEncodingButton.IsEnabled = false;
            audioStartEncodingButton.Content = "请等待...";
            Task.Run(() =>
            {
                Thread.Sleep(3000);
                Dispatcher.Invoke(() =>
                {
                    audioStartEncodingButton.IsEnabled = true;
                    audioStartEncodingButton.Content = "开始压制";
                    OpenTaskDetailWindow(t);
                });
            });
        }

        private void boxStartButton_Click(object sender, RoutedEventArgs e)
        {
            if(boxVideoInputTextBox.Text == "" || boxAudioInputTextBox.Text == "" || boxSaveTextBox.Text == "")
            {
                MessageBox.Show("输入和输出都不能为空");
                return;
            }

            EncodingTask t;
            if(boxFormatComboBox.SelectedIndex == (int)BoxFormat.MKV)
            {
                t = encodingQueue.AddMKVBoxEncodingTask(baseDir, boxVideoInputTextBox.Text, boxAudioInputTextBox.Text, boxSaveTextBox.Text, config);
            }
            else
            {
                t = encodingQueue.AddMP4BoxEncodingTask(baseDir, boxVideoInputTextBox.Text, boxAudioInputTextBox.Text, boxSaveTextBox.Text, config);
            }

            boxStartButton.IsEnabled = false;
            boxStartButton.Content = "请等待...";
            Task.Run(() =>
            {
                Thread.Sleep(3000);
                Dispatcher.Invoke(() =>
                {
                    boxStartButton.IsEnabled = true;
                    boxStartButton.Content = "开始封装";
                    OpenTaskDetailWindow(t);
                });
            });
        }

        private void avsRepeatCheckBox_Click(object sender, RoutedEventArgs e)
        {
            avsTextBox.Text = AvsBuilder.BuildAvs(this);
        }

        private void avsResizeX_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (windowIsLoaded) avsTextBox.Text = AvsBuilder.BuildAvs(this);
        }

        private void avsResizeY_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (windowIsLoaded) avsTextBox.Text = AvsBuilder.BuildAvs(this);
        }

        private void avsQTGMCCheckBox_Click(object sender, RoutedEventArgs e)
        {
            avsTextBox.Text = AvsBuilder.BuildAvs(this);
        }

        private void isAudioFix_Click(object sender, RoutedEventArgs e)
        {
            if (config != null) config.IsAudioFix = isAudioFix.IsChecked ?? false;
            if (windowIsLoaded) checkEncoderModeSelectAndSetDisable();
        }

        private void deintOptionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (config != null) config.ActiveDeintOption = (DeintOption)Enum.ToObject(typeof(DeintOption), deintOptionComboBox.SelectedIndex);
            if (windowIsLoaded)
            {
                changeDisableForEncodeMode(encoderSelecter.SelectedIndex);
                checkEncoderModeSelectAndSetDisable();
            }
        }

        private void simpleResizeCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (config != null) config.IsSetResize = simpleResizeCheckBox.IsChecked ?? false;
        }

        private void simpleResizeX_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (config != null) config.ResizeXValue = simpleResizeX.Text;
        }

        private void simpleResizeY_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (config != null) config.ResizeYValue = simpleResizeY.Text;
        }

        private void startFFPEncodingButton_Click(object sender, RoutedEventArgs e)
        {
            if (videoInputTextBox.Text == "" || videoSaveTextBox.Text == "")
            {
                MessageBox.Show("输入和输出都不能为空");
                return;
            }
            EncodingTask t;
            t = encodingQueue.AddFFPEncodingTask(baseDir, videoInputTextBox.Text, videoSaveTextBox.Text, config);
            startFFPEncodingButton.IsEnabled = false;
            startFFPEncodingButton.Content = "请等待...";
            Task.Run(() =>
            {
                Thread.Sleep(3000);
                Dispatcher.Invoke(() =>
                {
                    startFFPEncodingButton.IsEnabled = true;
                    startFFPEncodingButton.Content = "无音频";
                    OpenTaskDetailWindow(t);
                });
            });
        }

        private void avsVsfilterModCheckBox_Click(object sender, RoutedEventArgs e)
        {
            avsTextBox.Text = AvsBuilder.BuildAvs(this);
        }

        private void addPreset_Click(object sender, RoutedEventArgs e)
        {
            var inputDialog = new InputDialog();
            if(inputDialog.ShowDialog() == true)
            {
                var presetname = inputDialog.PresetName;
                if(presetname != "")
                {
                    if(presetcollection.Presets.Any(s => s.Name == presetname))
                    {
                        MessageBox.Show("无法建立具有相同的名称的预设: " + presetname);
                        return;
                    }
                    presetcollection.Presets.Add(Preset.GetPresentPreset(this, presetname));
                    presetcollection.ActiveIndex = presetcollection.Presets.Count - 1;
                    presetSelecter.SelectedIndex = presetcollection.ActiveIndex;
                    PresetCollection.Save(presetcollection, baseDir);
                }
            }
        }

        private void presetSelecter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (windowIsLoaded)
            {
                presetcollection.ActiveIndex = presetSelecter.SelectedIndex;
                PresetCollection.Save(presetcollection, baseDir);
            }
            if(presetcollection.ActiveIndex >= 0 && presetcollection.ActiveIndex < presetcollection.Presets.Count)
                ApplyPresetItem(presetcollection.Presets[presetcollection.ActiveIndex]);
        }

        private void ApplyPresetItem(Preset preset)
        {
            encoderSelecter.SelectedIndex = (int)preset.Encoder;
            if (config != null) config.ActiveEncoder = preset.Encoder;

            isinterlaceCheckBox.IsChecked = preset.IsInterlaceSource;
            if (config != null) config.IsInterlaceSource = preset.IsInterlaceSource;

            tffOrBffComboBox.SelectedIndex = (int)preset.InterlacedMode;
            if (config != null) config.ActiveInterlacedMode = preset.InterlacedMode;

            deintOptionComboBox.SelectedIndex = (int)preset.DeintOption;
            if (config != null) config.ActiveDeintOption = preset.DeintOption;

            isSetDarCheckBox.IsChecked = preset.IsSetDar;
            if (config != null) config.IsSetDar = preset.IsSetDar;

            darValueBox.Text = preset.DarValue;
            if (config != null) config.DarValue = preset.DarValue;

            customParameterSwitcher.IsChecked = preset.IsSetCustomParams;
            if (config != null) config.IsUseCustomParameter = preset.IsSetCustomParams;

            customParameterInputBox.Text = preset.CustomParams;
            if (config != null) config.CustomParameter = preset.CustomParams;

            if (config != null) config.ActiveEncoderMode = preset.EncoderMode;
            switch (preset.EncoderMode)
            {
                case EncoderMode.CQP:
                    cqpRadioButton.IsChecked = true;
                    cqpValueBox.Text = preset.EncoderParamValue;
                    if (config != null) config.CqpValue = preset.EncoderParamValue;
                    break;
                case EncoderMode.VQP:
                    vqpRadioButton.IsChecked = true;
                    vqpValueBox.Text = preset.EncoderParamValue;
                    if (config != null) config.VqpValue = preset.EncoderParamValue;
                    break;
                case EncoderMode.LA:
                    laRadioButton.IsChecked = true;
                    laValueBox.Text = preset.EncoderParamValue;
                    if (config != null) config.LaValue = preset.EncoderParamValue;
                    break;
                case EncoderMode.CBR:
                    cbrRadioButton.IsChecked = true;
                    cbrValueBox.Text = preset.EncoderParamValue;
                    if (config != null) config.CbrValue = preset.EncoderParamValue;
                    break;
                case EncoderMode.VBR:
                    vbrRadioButton.IsChecked = true;
                    vbrValueBox.Text = preset.EncoderParamValue;
                    if (config != null) config.VbrValue = preset.EncoderParamValue;
                    break;
                case EncoderMode.ICQ:
                    icqRadioButton.IsChecked = true;
                    icqValueBox.Text = preset.EncoderParamValue;
                    if (config != null) config.IcqValue = preset.EncoderParamValue;
                    break;
                case EncoderMode.LAICQ:
                    laicqRadioButton.IsChecked = true;
                    laicqValueBox.Text = preset.EncoderParamValue;
                    if (config != null) config.LaicqValue = preset.EncoderParamValue;
                    break;
            }

            if (windowIsLoaded)
            {
                changeDisableForEncodeMode(encoderSelecter.SelectedIndex);
                checkEncoderModeSelectAndSetDisable();
            }
        }

        private void deletePreset_Click(object sender, RoutedEventArgs e)
        {
            if(MessageBox.Show("确认删除 预设: " + presetcollection.Presets[presetcollection.ActiveIndex].Name + " 吗？\n删除后不可恢复",
                "确认删除", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
            {
                presetcollection.Presets.RemoveAt(presetcollection.ActiveIndex);
                presetcollection.ActiveIndex = 0;
                presetSelecter.SelectedIndex = 0;
            }
        }

        private void startFFPAudioEncodingButton_Click(object sender, RoutedEventArgs e)
        {
            if (videoInputTextBox.Text == "" || videoSaveTextBox.Text == "")
            {
                MessageBox.Show("输入和输出都不能为空");
                return;
            }
            EncodingTask t;
            t = encodingQueue.AddFFPWithAudioEncodingTask(baseDir, videoInputTextBox.Text, videoSaveTextBox.Text, config);
            
            startFFPAudioEncodingButton.IsEnabled = false;
            startFFPAudioEncodingButton.Content = "请等待...";
            Task.Run(() =>
            {
                Thread.Sleep(3000);
                Dispatcher.Invoke(() =>
                {
                    startFFPAudioEncodingButton.IsEnabled = true;
                    startFFPAudioEncodingButton.Content = "复制音频";
                    OpenTaskDetailWindow(t);
                });
            });
        }

        private void startFFPAVEncodingButton_Click(object sender, RoutedEventArgs e)
        {
            if (videoInputTextBox.Text == "" || videoSaveTextBox.Text == "")
            {
                MessageBox.Show("输入和输出都不能为空");
                return;
            }
            EncodingTask t;
            t = encodingQueue.AddFFPWithAudioVideoEncodingTask(baseDir, videoInputTextBox.Text, videoSaveTextBox.Text, config);

            startFFPAVEncodingButton.IsEnabled = false;
            startFFPAVEncodingButton.Content = "请等待...";
            Task.Run(() =>
            {
                Thread.Sleep(3000);
                Dispatcher.Invoke(() =>
                {
                    startFFPAVEncodingButton.IsEnabled = true;
                    startFFPAVEncodingButton.Content = "处理音频与视频";
                    OpenTaskDetailWindow(t);
                });
            });
        }
    }
}
