using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Text.RegularExpressions;
using System.Linq;

namespace HashcatUI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private Process? _hashcatProcess;
    private string _hashcatPath = "hashcat\\hashcat.exe";

    public MainWindow()
    {
        InitializeComponent();
        // 检查hashcat是否存在
        if (!File.Exists(_hashcatPath))
        {
            OutputResult.AppendText($"警告: 找不到hashcat.exe，请确保它在正确的路径下。\n");
        }
    }
    
    // 掩码预设类
    private class MaskPreset
    {
        public string Name { get; set; } = string.Empty;
        public string Mask { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsSelected { get; set; } = false;
    }
    
    // 显示掩码预设对话框
    private void ShowMaskPresetDialog(object sender, RoutedEventArgs e)
    {
        // 创建带预设选项的对话框
        Window presetWindow = new Window
        {
            Title = "选择掩码预设",
            Width = 600,
            Height = 500,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this
        };

        StackPanel mainPanel = new StackPanel { Margin = new Thickness(15) };
        
        // 说明文字
        TextBlock header = new TextBlock 
        { 
            Text = "选择预设掩码模式或手动输入自定义掩码", 
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 15)
        };
        mainPanel.Children.Add(header);

        // 预设掩码选项列表
        ListView presetListView = new ListView 
        { 
            Margin = new Thickness(0, 0, 0, 15),
            Height = 250,
            IsSynchronizedWithCurrentItem = true,
            SelectionMode = SelectionMode.Single,
            IsHitTestVisible = true,
            Focusable = true,
            Background = Brushes.White, // 设置背景色以便更好地显示选择效果
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(1)
        };
        
        // 添加项目容器样式以增强选择效果
        Style itemContainerStyle = new Style(typeof(ListViewItem));
        Setter setter = new Setter(ListViewItem.IsSelectedProperty, new Binding("IsSelected"));
        itemContainerStyle.Setters.Add(setter);
        presetListView.ItemContainerStyle = itemContainerStyle;
        
        // 定义列
        GridView gridView = new GridView();
        
        GridViewColumn nameColumn = new GridViewColumn
        { 
            Header = "预设名称", 
            Width = 200,
            DisplayMemberBinding = new Binding("Name")
        };
        
        GridViewColumn maskColumn = new GridViewColumn
        { 
            Header = "掩码", 
            Width = 250,
            DisplayMemberBinding = new Binding("Mask")
        };
        
        GridViewColumn descriptionColumn = new GridViewColumn
        { 
            Header = "说明", 
            Width = 100,
            DisplayMemberBinding = new Binding("Description")
        };
        
        gridView.Columns.Add(nameColumn);
        gridView.Columns.Add(maskColumn);
        gridView.Columns.Add(descriptionColumn);
        presetListView.View = gridView;
        
        // 创建预设掩码数据
        List<MaskPreset> presets = new List<MaskPreset>
        {
            new MaskPreset { Name = "8位数字", Mask = "?d?d?d?d?d?d?d?d", Description = "纯数字" },
            new MaskPreset { Name = "6-16位数字", Mask = "--increment --increment-min 6 --increment-max 16 ?d?d?d?d?d?d", Description = "数字范围" },
            new MaskPreset { Name = "8位小写字母", Mask = "?l?l?l?l?l?l?l?l", Description = "小写字母" },
            new MaskPreset { Name = "8位大写字母", Mask = "?u?u?u?u?u?u?u?u", Description = "大写字母" },
            new MaskPreset { Name = "4位大写+4位数字", Mask = "?u?u?u?u?d?d?d?d", Description = "组合模式" },
            new MaskPreset { Name = "8位字符+数字", Mask = "?h?h?h?h?h?h?h?h", Description = "小写+数字" },
            new MaskPreset { Name = "8位混合字符", Mask = "?a?a?a?a?a?a?a?a", Description = "所有字符" },
            new MaskPreset { Name = "6-16位混合字符", Mask = "--increment --increment-min 6 --increment-max 16 ?a?a?a?a?a?a", Description = "包含键盘上所有可见字符(大小写字母、数字、特殊符号)" },
            new MaskPreset { Name = "常见字符", Mask = "?l?u?d?s", Description = "大小写+数字+特殊字符" },
            new MaskPreset { Name = "常见小写+数字", Mask = "0123456789abcdef", Description = "常见字符集" },
            new MaskPreset { Name = "常见大写+数字", Mask = "0123456789ABCDEF", Description = "常见字符集" },
            new MaskPreset { Name = "特殊字符", Mask = "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~", Description = "特殊字符集" },
            new MaskPreset { Name = "包含空格", Mask = "0x00 - 0xff", Description = "包含所有ASCII字符" },
            // 中国人常用密码掩码
            new MaskPreset { Name = "生日(YYYYMMDD)", Mask = "?d?d?d?d?d?d?d?d", Description = "年-月-日格式" },
            new MaskPreset { Name = "生日(MMDDYYYY)", Mask = "?d?d?d?d?d?d?d?d", Description = "月-日-年格式" },
            new MaskPreset { Name = "生日(YYMMDD)", Mask = "?d?d?d?d?d?d", Description = "短生日格式" },
            new MaskPreset { Name = "手机号(11位)", Mask = "?d?d?d?d?d?d?d?d?d?d?d", Description = "中国手机号格式" },
            new MaskPreset { Name = "手机号后8位", Mask = "?d?d?d?d?d?d?d?d", Description = "手机号末尾8位" },
            new MaskPreset { Name = "身份证号(18位)", Mask = "?d?d?d?d?d?d?d?d?d?d?d?d?d?d?d?d?a", Description = "18位身份证号" },
            new MaskPreset { Name = "身份证号(15位)", Mask = "?d?d?d?d?d?d?d?d?d?d?d?d?d?d?d", Description = "15位身份证号" },
            new MaskPreset { Name = "姓名首字母+4位数字", Mask = "?u?u?d?d?d?d", Description = "姓氏+名字首字母+数字" },
            new MaskPreset { Name = "姓名全拼+数字", Mask = "?l?l?l?l?d?d?d", Description = "姓名拼音+数字后缀" },
            new MaskPreset { Name = "中文姓氏首字母", Mask = "?u?d?d?d?d", Description = "姓氏首字母+数字" },
            new MaskPreset { Name = "常见密码+数字", Mask = "?l?l?l?l?l?d?d?d", Description = "abcde123等常见格式" },
            new MaskPreset { Name = "数字+年份", Mask = "?d?d?d?d?d?d?d?d", Description = "数字组合+年份" },
            new MaskPreset { Name = "123+自定义", Mask = "123?l?l?l?l?l", Description = "123开头+字母组合" },
            new MaskPreset { Name = "abc+数字", Mask = "abc?d?d?d?d", Description = "abc开头+4位数字" },
            new MaskPreset { Name = "数字+abc", Mask = "?d?d?d?dabc", Description = "4位数字+abc结尾" },
            // 中国社交平台常用密码格式
            new MaskPreset { Name = "QQ号+简单密码", Mask = "?d?d?d?d?d?d?d?d?l?l?l", Description = "QQ号+简单字母组合" },
            new MaskPreset { Name = "QQ号+数字后缀", Mask = "?d?d?d?d?d?d?d?d?d?d?d", Description = "QQ账号+数字后缀" },
            new MaskPreset { Name = "微博常用密码", Mask = "?l?l?l?l?d?d?d?d", Description = "字母+数字组合(微博常见)" },
            new MaskPreset { Name = "微信号码+生日", Mask = "?d?d?d?d?d?d?d?d", Description = "微信相关号码+生日" },
            new MaskPreset { Name = "邮箱账号+数字", Mask = "?l?l?l?l?l?l?d?d", Description = "邮箱前缀+数字" },
            new MaskPreset { Name = "平台账号格式", Mask = "?l?l?l?d?d?d?d", Description = "字母开头+数字组合(平台账号)" },
            new MaskPreset { Name = "密码+下划线+年份", Mask = "?l?l?l?l?_?d?d?d?d", Description = "密码_2024格式" },
            new MaskPreset { Name = "字母+特殊符号+数字", Mask = "?l?l?l?l?s?d?d?d", Description = "字母+符号+数字组合" },
            new MaskPreset { Name = "游戏账号密码", Mask = "?u?l?l?l?l?d?d?d", Description = "首字母大写+字母+数字" },
            new MaskPreset { Name = "电商平台密码", Mask = "?l?l?l?l?l?l?d?d?d", Description = "多字母+数字组合(电商常见)" },
            new MaskPreset { Name = "手机号+平台标识", Mask = "1?d?d?d?d?d?d?d?d?d?a", Description = "手机号+平台识别字符" },
            new MaskPreset { Name = "昵称拼音+数字", Mask = "?l?l?l?l?l?d?d?d", Description = "用户昵称拼音+数字" },
            new MaskPreset { Name = "平台缩写+密码", Mask = "?u?u?l?l?l?l?l", Description = "平台缩写+密码组合" },
        };
        
        presetListView.ItemsSource = presets;
        mainPanel.Children.Add(presetListView);
        
        // 自定义掩码输入
        TextBlock customLabel = new TextBlock { Text = "或输入自定义掩码:" };
        TextBox customMaskInput = new TextBox { Margin = new Thickness(0, 5, 0, 15) };
        
        // 增量范围选择
        StackPanel incrementPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
        CheckBox useIncrement = new CheckBox { Content = "启用增量模式", VerticalAlignment = VerticalAlignment.Center };
        
        TextBlock minLabel = new TextBlock { Text = "最小长度:", Margin = new Thickness(20, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center };
        TextBox minLength = new TextBox { Width = 50, Text = "6" };
        
        TextBlock maxLabel = new TextBlock { Text = "最大长度:", Margin = new Thickness(10, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center };
        TextBox maxLength = new TextBox { Width = 50, Text = "16" };
        
        incrementPanel.Children.Add(useIncrement);
        incrementPanel.Children.Add(minLabel);
        incrementPanel.Children.Add(minLength);
        incrementPanel.Children.Add(maxLabel);
        incrementPanel.Children.Add(maxLength);
        
        mainPanel.Children.Add(customLabel);
        mainPanel.Children.Add(customMaskInput);
        mainPanel.Children.Add(incrementPanel);
        
        // 掩码语法帮助
        Expander helpExpander = new Expander { Header = "掩码语法说明" };
        StackPanel helpPanel = new StackPanel { Margin = new Thickness(10) };
        helpPanel.Children.Add(new TextBlock { Text = "?l = 小写字母 (a-z)", Margin = new Thickness(0, 2, 0, 2) });
        helpPanel.Children.Add(new TextBlock { Text = "?u = 大写字母 (A-Z)", Margin = new Thickness(0, 2, 0, 2) });
        helpPanel.Children.Add(new TextBlock { Text = "?d = 数字 (0-9)", Margin = new Thickness(0, 2, 0, 2) });
        helpPanel.Children.Add(new TextBlock { Text = "?s = 特殊字符", Margin = new Thickness(0, 2, 0, 2) });
        helpPanel.Children.Add(new TextBlock { Text = "?h = 小写字母 + 数字", Margin = new Thickness(0, 2, 0, 2) });
        helpPanel.Children.Add(new TextBlock { Text = "?H = 大写字母 + 数字", Margin = new Thickness(0, 2, 0, 2) });
        helpPanel.Children.Add(new TextBlock { Text = "?a = 所有可打印字符", Margin = new Thickness(0, 2, 0, 2) });
        helpExpander.Content = helpPanel;
        mainPanel.Children.Add(helpExpander);
        
        // 按钮区域
        StackPanel buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        Button okButton = new Button { Content = "确定", Width = 80, Margin = new Thickness(0, 0, 10, 0) };
        Button cancelButton = new Button { Content = "取消", Width = 80 };
        
        okButton.Click += (s, args) => 
        { 
            // 构建最终掩码
            string finalMask = string.Empty;
            
            // 如果选择了预设项
            if (presetListView.SelectedItem != null)
            {
                MaskPreset selectedPreset = (MaskPreset)presetListView.SelectedItem;
                finalMask = selectedPreset.Mask;
            }
            // 如果输入了自定义掩码
            else if (!string.IsNullOrEmpty(customMaskInput.Text))
            {
                finalMask = customMaskInput.Text;
                
                // 如果启用了增量模式
                if (useIncrement.IsChecked.GetValueOrDefault())
                {
                    int min, max;
                    if (int.TryParse(minLength.Text, out min) && int.TryParse(maxLength.Text, out max) && min <= max)
                    {
                        finalMask = $"--increment --increment-min {min} --increment-max {max} {finalMask}";
                    }
                }
            }
            
            if (!string.IsNullOrEmpty(finalMask))
            {
                DictionaryMaskInput.Text = finalMask;
            }
            
            presetWindow.DialogResult = true; 
        };
        
        cancelButton.Click += (s, args) => { presetWindow.DialogResult = false; };
        
        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);
        
        mainPanel.Children.Add(buttonPanel);
        
        presetWindow.Content = mainPanel;
        presetWindow.ResizeMode = ResizeMode.NoResize;
        presetWindow.ShowInTaskbar = false;
        
        presetWindow.ShowDialog();
    }

    // 从文件加载Hash
    private void LoadHashFromFile_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "文本文件 (*.txt;*.hash)|*.txt;*.hash|所有文件 (*.*)|*.*",
            Title = "选择包含Hash值的文件"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                HashInput.Text = File.ReadAllText(openFileDialog.FileName);
                OutputResult.AppendText($"已从文件加载Hash值: {openFileDialog.FileName}\n");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // 浏览文件
    private void BrowseFile_Click(object sender, RoutedEventArgs e)
    {
        string filter = "字典文件 (*.txt)|*.txt|所有文件 (*.*)|*.*";
        string title = "选择字典文件";

        // 如果是掩码攻击，显示掩码预设对话框
        if (AttackMode.SelectedIndex == 1) // 掩码攻击
        {
            ShowMaskPresetDialog(this, new RoutedEventArgs());
            return;
        }

        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = filter,
            Title = title
        };

        if (openFileDialog.ShowDialog() == true)
        {
            DictionaryMaskInput.Text = openFileDialog.FileName;
        }
    }

    // 开始攻击
    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        // 检查是否已有hashcat进程在运行
        if (Process.GetProcessesByName("hashcat").Length > 0)
        {
            MessageBox.Show("检测到已有hashcat进程在运行，请先关闭它再继续。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // 验证输入
        if (string.IsNullOrWhiteSpace(HashInput.Text))
        {
            MessageBox.Show("请输入或加载Hash值", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (string.IsNullOrWhiteSpace(DictionaryMaskInput.Text) && AttackMode.SelectedIndex != 1)
        {
            MessageBox.Show("请选择字典文件或设置掩码", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // 清理和验证哈希值
        string[] hashLines = HashInput.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var validHashes = new List<string>();
        var invalidHashes = new List<string>();
        
        // 简单的MD5哈希验证（32位十六进制字符）
        Regex md5Regex = new Regex("^[a-fA-F0-9]{32}$");
        
        foreach (var line in hashLines)
        {
            string trimmedLine = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmedLine)) continue;
            
            if (md5Regex.IsMatch(trimmedLine))
            {
                validHashes.Add(trimmedLine);
            }
            else
            {
                invalidHashes.Add(trimmedLine);
            }
        }
        
        if (validHashes.Count == 0)
        {
            MessageBox.Show("未找到有效的MD5哈希值，请检查输入", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        
        if (invalidHashes.Count > 0)
        {
            OutputResult.AppendText($"警告: 已过滤掉 {invalidHashes.Count} 个无效的哈希值\n");
        }
        
        // 保存有效哈希到临时文件
        string hashFilePath = Path.Combine(Environment.CurrentDirectory, "temp_hash.txt");
        File.WriteAllLines(hashFilePath, validHashes);

        // 构建命令行参数
        StringBuilder arguments = new StringBuilder();

        // 添加哈希类型参数
        int hashTypeIndex = HashType.SelectedIndex;
        string[] hashTypes = { "-m 0", "-m 100", "-m 1400", "-m 1000", "-m 300" };
        arguments.Append(hashTypes[hashTypeIndex] + " ");

        // 添加攻击模式参数
        int attackModeIndex = AttackMode.SelectedIndex;
        string[] attackModes = { "-a 0", "-a 3", "-a 1", "-a 6", "-a 7" };
        arguments.Append(attackModes[attackModeIndex] + " ");

        // 添加hash文件
        arguments.Append($"\"{hashFilePath}\" ");

        // 添加字典文件或掩码
        if (AttackMode.SelectedIndex != 1) // 不是掩码攻击
        {
            arguments.Append($"\"{DictionaryMaskInput.Text}\" ");
        }
        else
        {
            arguments.Append($"{DictionaryMaskInput.Text} ");
        }

        // 是否使用GPU
        if (!UseGPU.IsChecked.GetValueOrDefault())
        {
            arguments.Append("--force --opencl-disable ");
        }

        // 添加额外选项
        if (!string.IsNullOrWhiteSpace(AdditionalOptions.Text))
        {
            // 检查AdditionalOptions中是否已包含"-w"和"-O"选项
            bool hasWorkloadOption = AdditionalOptions.Text.Contains("-w");
            bool hasOptimizedOption = AdditionalOptions.Text.Contains("-O");
            arguments.Append(AdditionalOptions.Text + " ");
            
            // 如果使用GPU且AdditionalOptions中没有包含-w选项，则添加-w 3
            if (UseGPU.IsChecked.GetValueOrDefault() && !hasWorkloadOption)
            {
                arguments.Append("-w 3 "); // 使用工作负载配置文件3
            }
            
            // 如果没有包含-O优化选项，提示用户但不自动添加，因为可能会限制密码长度
            if (!hasOptimizedOption && AttackMode.SelectedIndex != 1)
            {
                OutputResult.AppendText("提示: 您可以尝试添加 -O 选项以提高性能（注意：这可能会限制密码长度）\n");
            }
        }
        else if (UseGPU.IsChecked.GetValueOrDefault())
        {    
            // 如果没有额外选项但使用GPU，则添加-w 3
            arguments.Append("-w 3 "); // 使用工作负载配置文件3
            
            // 提示用户可以添加优化选项
            if (AttackMode.SelectedIndex != 1)
            {
                OutputResult.AppendText("提示: 您可以尝试在额外选项中添加 -O 选项以提高性能（注意：这可能会限制密码长度）\n");
            }
        }

        // 添加恢复和输出选项
        arguments.Append("--potfile-path hashcat.potfile ");

        OutputResult.AppendText($"开始执行: {_hashcatPath} {arguments}\n");

        try
            {
                // 获取hashcat目录路径
                string hashcatDir = Path.GetDirectoryName(_hashcatPath) ?? string.Empty;
                if (string.IsNullOrEmpty(hashcatDir))
                {
                    hashcatDir = Directory.GetCurrentDirectory();
                }
                else if (!Path.IsPathRooted(hashcatDir))
                {
                    hashcatDir = Path.Combine(Directory.GetCurrentDirectory(), hashcatDir);
                }
                
                _hashcatProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _hashcatPath,
                        Arguments = arguments.ToString(),
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8,
                        WorkingDirectory = hashcatDir // 设置工作目录为hashcat目录
                    },
                    EnableRaisingEvents = true
                };

            _hashcatProcess.OutputDataReceived += (s, eventArgs) =>
            {
                if (!string.IsNullOrEmpty(eventArgs.Data))
                {
                    Dispatcher.Invoke(() =>
                    {
                        OutputResult.AppendText(eventArgs.Data + "\n");
                        OutputResult.ScrollToEnd();
                        
                        // 检测GPU内核超时警告
                        if (eventArgs.Data.Contains("WARNING! Kernel exec timeout is not disabled"))
                        {
                            OutputResult.AppendText("\n=======================================================\n");
                            OutputResult.AppendText("警告: GPU内核执行超时未禁用，可能导致CL_OUT_OF_RESOURCES错误!\n");
                            OutputResult.AppendText("请访问 https://hashcat.net/q/timeoutpatch 了解如何修复此问题\n");
                            
                            // 生成修复脚本
                            try
                            {
                                string regFilePath = Path.Combine(Environment.CurrentDirectory, "wddm_timeout_patch.reg");
                                string regContent = "Windows Registry Editor Version 5.00\n\n[HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Control\\GraphicsDrivers]\n\"TdrLevel\"=dword:00000000\n";
                                File.WriteAllText(regFilePath, regContent);
                                OutputResult.AppendText($"\n已自动生成修复脚本: {regFilePath}\n");
                                OutputResult.AppendText("请右键以管理员身份运行此注册表文件，然后重启计算机以应用更改\n");
                            }
                            catch (Exception ex)
                            {
                                OutputResult.AppendText($"生成修复脚本失败: {ex.Message}\n");
                            }
                            
                            OutputResult.AppendText("=======================================================\n\n");
                        }
                        
                        // 检测哈希:密码格式的结果
                        Regex hashPasswordRegex = new Regex(@"^[a-fA-F0-9]{32,128}:.*$");
                        if (hashPasswordRegex.IsMatch(eventArgs.Data))
                        {
                            OutputResult.AppendText("================================\n");
                            OutputResult.AppendText("找到匹配! " + eventArgs.Data + "\n");
                            OutputResult.AppendText("================================\n");
                            
                            // 自动保存解密成功的结果到txt文件
                            try
                            {
                                string resultFilePath = Path.Combine(Environment.CurrentDirectory, "cracked_results.txt");
                                using (StreamWriter writer = new StreamWriter(resultFilePath, true))
                                {
                                    writer.WriteLine(eventArgs.Data);
                                }
                                OutputResult.AppendText($"解密结果已自动保存到: {resultFilePath}\n");
                            }
                            catch (Exception ex)
                            {
                                OutputResult.AppendText($"自动保存结果失败: {ex.Message}\n");
                            }
                        }
                    });
                }
            };

            _hashcatProcess.ErrorDataReceived += (s, eventArgs) =>
            {
                if (!string.IsNullOrEmpty(eventArgs.Data))
                {
                    Dispatcher.Invoke(() =>
                    {
                        OutputResult.AppendText("错误: " + eventArgs.Data + "\n");
                        OutputResult.ScrollToEnd();
                    });
                }
            };

            _hashcatProcess.Exited += (s, eventArgs) =>
            {
                Dispatcher.Invoke(() =>
                {
                    int exitCode = _hashcatProcess?.ExitCode ?? 0;
                    OutputResult.AppendText($"\n执行完成，退出代码: {exitCode}\n");
                    
                    // 根据退出代码显示不同的提示信息
                    if (exitCode == 0)
                    {
                        OutputResult.AppendText("\n攻击成功完成!\n");
                    }
                    else if (exitCode == 1)
                    {
                        OutputResult.AppendText("\n攻击完成，但可能没有发现新的匹配结果（可能从potfile中恢复了部分结果）。\n");
                        OutputResult.AppendText("请尝试更换攻击模式或使用更强大的字典/掩码。\n");
                    }
                    else if (exitCode == 255)
                    {
                        OutputResult.AppendText("\n攻击被用户中断。\n");
                    }
                    else
                    {
                        OutputResult.AppendText($"\n攻击完成，但遇到了一些问题，退出代码: {exitCode}。\n");
                    }
                    
                    StartButton.IsEnabled = true;
                    StopButton.IsEnabled = false;
                });
            };

            _hashcatProcess.Start();
            _hashcatProcess.BeginOutputReadLine();
            _hashcatProcess.BeginErrorReadLine();

            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            OutputResult.AppendText($"启动进程失败: {ex.Message}\n");
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
        }
    }

    // 停止攻击
    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        bool processStopped = false;
        
        // 首先尝试终止当前启动的进程
        if (_hashcatProcess != null && !_hashcatProcess.HasExited)
        {
            try
            {
                _hashcatProcess.Kill();
                _hashcatProcess.WaitForExit(5000);
                OutputResult.AppendText("\n已停止当前执行的进程\n");
                processStopped = true;
            }
            catch (Exception ex)
            {
                OutputResult.AppendText($"停止当前进程失败: {ex.Message}\n");
            }
        }
        
        // 查找并终止所有正在运行的hashcat.exe进程
        try
        {
            int killedProcessCount = 0;
            foreach (var process in Process.GetProcessesByName("hashcat"))
            {
                try
                {
                    process.Kill();
                    process.WaitForExit(2000);
                    killedProcessCount++;
                }
                catch (Exception ex)
                {
                    OutputResult.AppendText($"终止hashcat进程(ID: {process.Id})失败: {ex.Message}\n");
                }
            }
            
            if (killedProcessCount > 0)
            {
                OutputResult.AppendText($"成功终止 {killedProcessCount} 个hashcat进程\n");
                processStopped = true;
            }
        }
        catch (Exception ex)
        {
            OutputResult.AppendText($"查找hashcat进程失败: {ex.Message}\n");
        }
        
        if (!processStopped)
        {
            OutputResult.AppendText("没有发现正在运行的hashcat进程\n");
        }
        
        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
    }

    // 清除内容
    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        HashInput.Clear();
        DictionaryMaskInput.Clear();
        OutputResult.Clear();
    }

    // 导出结果
    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        var saveFileDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
            Title = "导出结果",
            FileName = "hashcat_result_" + DateTime.Now.ToString("yyyyMMdd_HHmmss")
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            try
            {
                File.WriteAllText(saveFileDialog.FileName, OutputResult.Text);
                OutputResult.AppendText($"\n结果已导出到: {saveFileDialog.FileName}\n");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // 窗口关闭时确保进程停止
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (_hashcatProcess != null && !_hashcatProcess.HasExited)
        {
            try
            {
                _hashcatProcess.Kill();
            }
            catch { }
        }
        // 清理临时文件
        try
        {
            string tempHashFile = Path.Combine(Environment.CurrentDirectory, "temp_hash.txt");
            if (File.Exists(tempHashFile))
                File.Delete(tempHashFile);
        }
        catch { }
    }
}