using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using NegativeEncoder.SystemOptions;
using NegativeEncoder.Utils;

namespace NegativeEncoder.Presets;

public static class PresetProvider
{
    private static bool _isLoadingPreset = false;

    public static void InitPresetAutoSave(MenuItem presetMenuItems)
    {
        //AppContext.PresetContext.CurrentPreset.PropertyChanged += CurrentPreset_PropertyChanged;

        ReBuildPresetMenu(presetMenuItems);
    }

    public static void CurrentPreset_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        //检查预设是否合法
        var preset = AppContext.PresetContext.CurrentPreset;

        //非QSV
        if (preset.Encoder != Encoder.QSV)
        {
            //编码器非QSV的时候禁止选择LA/LA-ICQ和QVBR模式
            if (preset.EncodeMode == EncodeMode.LA || preset.EncodeMode == EncodeMode.LAICQ ||
                preset.EncodeMode == EncodeMode.QVBR)
                AppContext.PresetContext.CurrentPreset.EncodeMode = EncodeMode.VBR;

            //编码器非QSV的时候禁止选择D3D模式
            if (preset.D3DMode != D3DMode.Auto) AppContext.PresetContext.CurrentPreset.D3DMode = D3DMode.Auto;
        }

        //非NVENC
        if (preset.Encoder == Encoder.VCE)
            //编码器非NVENC时，只能使用8 bit模式
            if (preset.ColorDepth != ColorDepth.C8Bit)
                AppContext.PresetContext.CurrentPreset.ColorDepth = ColorDepth.C8Bit;

        //目标HDR格式不为SDR时，SDR转换只能是None
        if (preset.NewHdrType != HdrType.SDR)
            if (preset.Hdr2SdrMethod != Hdr2Sdr.None)
                AppContext.PresetContext.CurrentPreset.Hdr2SdrMethod = Hdr2Sdr.None;

        //如果正在加载预设，不触发自动保存
        if (_isLoadingPreset) return;

        //标记当前预设已修改
        AppContext.PresetContext.IsPresetEdit = true;

        //存储预设到文件
        _ = SystemOption.SaveOption(AppContext.PresetContext.CurrentPreset);
    }

    public static void ReBuildPresetMenu(MenuItem presetMenuItems)
    {
        var deletable = new List<MenuItem>();

        foreach (var presetSubmenu in presetMenuItems.Items)
            if (presetSubmenu is MenuItem submenu)
                if (submenu.IsCheckable)
                    deletable.Add(submenu);

        foreach (var deleteSubmenu in deletable) presetMenuItems.Items.Remove(deleteSubmenu);

        AppContext.PresetContext.IsPresetEdit = true;

        if (AppContext.PresetContext.PresetList.Count == 0)
        {
            var emptySubMenu = new MenuItem
            {
                Header = "(空)",
                IsCheckable = true,
                IsEnabled = false,
                IsChecked = false
            };
            presetMenuItems.Items.Add(emptySubMenu);

            return;
        }

        foreach (var preset in AppContext.PresetContext.PresetList)
        {
            var presetSubMenu = new MenuItem
            {
                Header = preset.PresetName,
                IsCheckable = true
            };
            presetSubMenu.Click += PresetSubMenu_Click;
            if (preset.PresetName == AppContext.PresetContext.CurrentPreset.PresetName)
            {
                presetSubMenu.IsChecked = true;

                if (DeepCompare.EqualsDeep1(preset, AppContext.PresetContext.CurrentPreset))
                    AppContext.PresetContext.IsPresetEdit = false;
            }

            presetMenuItems.Items.Add(presetSubMenu);
        }
    }

    private static async void PresetSubMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem m)
        {
            if (AppContext.PresetContext.IsPresetEdit)
                if (MessageBox.Show("当前预设未保存，是否放弃？", "预设", MessageBoxButton.YesNo, MessageBoxImage.Question) !=
                    MessageBoxResult.Yes)
                {
                    ReBuildPresetMenu(m.Parent as MenuItem);
                    return;
                }

            var checkName = m.Header as string;

            _isLoadingPreset = true;
            try
            {
                foreach (var p in AppContext.PresetContext.PresetList)
                    if (p.PresetName == checkName)
                    {
                        AppContext.PresetContext.CurrentPreset = DeepCompare.CloneDeep1(p);
                        break;
                    }
            }
            finally
            {
                _isLoadingPreset = false;
            }

            ReBuildPresetMenu(m.Parent as MenuItem);
            //存储预设到文件
            await SystemOption.SaveOption(AppContext.PresetContext.CurrentPreset);
        }
    }

    public static async Task LoadPresetAutoSave()
    {
        _isLoadingPreset = true;
        try
        {
            AppContext.PresetContext.CurrentPreset = await SystemOption.ReadOption<Preset>();
            AppContext.PresetContext.PresetList =
                (await SystemOption.ReadListOption<Preset>()).OrderBy(it => it.PresetName).ToList();
        }
        finally
        {
            _isLoadingPreset = false;
        }
    }

    public static void NewPreset(Window parentWindow = null)
    {
        if (AppContext.PresetContext.IsPresetEdit)
        {
            var res = MessageBox.Show(parentWindow, "当前预设未保存，是否确认覆盖", "预设", MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) return;
        }

        AppContext.PresetContext.CurrentPreset = new Preset();
        AppContext.PresetContext.IsPresetEdit = true;
    }

    public static async Task RenamePreset(MenuItem presetMenuItems, string newName)
    {
        var oldName = AppContext.PresetContext.CurrentPreset.PresetName;
        AppContext.PresetContext.CurrentPreset.PresetName = newName;

        if (AppContext.PresetContext.PresetList.Count(p => p.PresetName == oldName) > 0)
            for (var p = 0; p < AppContext.PresetContext.PresetList.Count; p++)
                if (AppContext.PresetContext.PresetList[p].PresetName == oldName)
                {
                    AppContext.PresetContext.PresetList[p].PresetName = newName;
                    await SystemOption.SaveListOption(AppContext.PresetContext.PresetList);
                    break;
                }

        ReBuildPresetMenu(presetMenuItems);
    }

    public static async Task ExportPreset(string fileName)
    {
        await SystemOption.SaveListOption(AppContext.PresetContext.PresetList, fileName);
    }

    public static async Task ImportPreset(MenuItem presetMenuItems, string fileName)
    {
        var configList = await SystemOption.ReadListOption<Preset>(fileName);

        foreach (var config in configList)
        {
            var cName = config.PresetName;
            if (AppContext.PresetContext.PresetList.Count(p => p.PresetName == cName) > 0)
            {
                if (MessageBox.Show($"正在导入的预设 {cName} 存在同名预设，要覆盖吗？", "导入预设冲突", MessageBoxButton.YesNo,
                        MessageBoxImage.Question) == MessageBoxResult.Yes)
                    for (var p = 0; p < AppContext.PresetContext.PresetList.Count; p++)
                        if (AppContext.PresetContext.PresetList[p].PresetName == cName)
                        {
                            AppContext.PresetContext.PresetList[p] = config;
                            break;
                        }
            }
            else
            {
                AppContext.PresetContext.PresetList.Add(config);
            }
        }

        await SystemOption.SaveListOption(AppContext.PresetContext.PresetList);
        ReBuildPresetMenu(presetMenuItems);
    }

    public static async Task DeletePreset(MenuItem presetMenuItems)
    {
        var presetName = AppContext.PresetContext.CurrentPreset.PresetName;
        Preset deletePreset = null;
        if (AppContext.PresetContext.PresetList.Count(p => p.PresetName == presetName) > 0)
            for (var p = 0; p < AppContext.PresetContext.PresetList.Count; p++)
                if (AppContext.PresetContext.PresetList[p].PresetName == presetName)
                {
                    deletePreset = AppContext.PresetContext.PresetList[p];
                    break;
                }

        if (deletePreset != null)
        {
            AppContext.PresetContext.PresetList.Remove(deletePreset);
            await SystemOption.SaveListOption(AppContext.PresetContext.PresetList);
        }

        _isLoadingPreset = true;
        try
        {
            if (AppContext.PresetContext.PresetList.Count > 0)
            {
                AppContext.PresetContext.CurrentPreset = DeepCompare.CloneDeep1(AppContext.PresetContext.PresetList[0]);
                AppContext.PresetContext.IsPresetEdit = false;
            }
            else
            {
                AppContext.PresetContext.CurrentPreset = new Preset();
                AppContext.PresetContext.IsPresetEdit = true;
            }
        }
        finally
        {
            _isLoadingPreset = false;
        }

        ReBuildPresetMenu(presetMenuItems);
    }

    public static async Task SavePreset(MenuItem presetMenuItems)
    {
        var presetName = AppContext.PresetContext.CurrentPreset.PresetName;
        if (AppContext.PresetContext.PresetList.Count(p => p.PresetName == presetName) > 0)
        {
            for (var p = 0; p < AppContext.PresetContext.PresetList.Count; p++)
                if (AppContext.PresetContext.PresetList[p].PresetName == presetName)
                {
                    AppContext.PresetContext.PresetList[p] = AppContext.PresetContext.CurrentPreset;
                    await SystemOption.SaveListOption(AppContext.PresetContext.PresetList);
                    break;
                }
        }
        else
        {
            await AddPresetList(presetMenuItems, AppContext.PresetContext.CurrentPreset);
        }

        ReBuildPresetMenu(presetMenuItems);
        AppContext.PresetContext.IsPresetEdit = false;
    }

    public static async Task SaveAsPreset(MenuItem presetMenuItems, string newName)
    {
        AppContext.PresetContext.CurrentPreset = DeepCompare.CloneDeep1(AppContext.PresetContext.CurrentPreset);
        AppContext.PresetContext.CurrentPreset.PresetName = newName;

        await SavePreset(presetMenuItems);
    }

    private static async Task AddPresetList(MenuItem presetMenuItems, Preset currentPreset)
    {
        AppContext.PresetContext.PresetList.Add(currentPreset);
        presetMenuItems.Items.Add(new MenuItem
        {
            Header = currentPreset.PresetName,
            IsCheckable = true,
            IsChecked = true
        });

        await SystemOption.SaveListOption(AppContext.PresetContext.PresetList);
    }
}