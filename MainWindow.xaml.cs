using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
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
using System.IO;
using Newtonsoft.Json.Linq;
using System.Runtime.ConstrainedExecution;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace vscode快速c艹器
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    ///

    public partial class MainWindow : Window
    {
        //默认模板
        string[] templates = new string[] {
        @"#include <stdio.h>
int main()
{
    printf(""hello world in c!"");
    return 0;
}
",
         @"#include <iostream>
using namespace std;
int main()
{
    cout<<""hello world in cpp!""<<endl;
    return 0;
}
"};
        //vs code json配置文件
        JObject tasks=JObject.Parse(@"{
        'statement':'',
	    'version': '2.0.0',
        'tasks': [
		    {
			    'type': 'cppbuild',
                'label': 'C/C++: cl.exe 生成活动文件',
                'command': 'cl.exe',
                //查看更多编译器命令：https://docs.microsoft.com/en-us/cpp/build/reference/compiler-options-listed-by-category?view=msvc-160
                'args': [
                    '/Zi',
                    '/EHsc',
                    '/nologo',
                    '/Fe:',
                    '${workspaceFolder}/bin/hi.exe',
                    '/MTd:', //debug mode
                    '${workspaceFolder}/src/hi.cpp',
                    '/Fo:${workspaceFolder}/obj/'
			    ],
			    'options': {
				    'cwd': '${fileDirname}'
                },
			    'problemMatcher': [
				    '$msCompile'
			    ],
			    'group': {
				    'kind': 'build',
				    'isDefault': true
			    },
			    'detail': '编译器: cl.exe'
		    },
            {
			    'label': 'clear',
                'type': 'shell',
                'command': 'rm',
                'args': [
                    'obj/*,',
                    'bin/*,',
                    '*.pdb',
                    '-Force'
                ],
			    'group': 'none',
			    'problemMatcher': []
            }
	    ]
    }
    ");
        JObject launch= JObject.Parse(@"{
            // 使用 IntelliSense 了解相关属性。 
            // 悬停以查看现有属性的描述。
            // 欲了解更多信息，请访问: https://go.microsoft.com/fwlink/?linkid=830387
            'version': '0.2.0',
            'configurations': [
                {
                    'name': '(Windows) 启动',
                    'type': 'cppvsdbg',
                    'request': 'launch',
                    'program': '${workspaceFolder}/bin/hi.exe',
                    'args': [],
                    'stopAtEntry': false,
                    'cwd': '${fileDirname}',
                    'environment': [],
                    'console': 'externalTerminal'
                }
            ]
        }
        ");
        JObject c_cpp_properties= JObject.Parse(@"{
            'configurations': [
                {
                    'name': 'Win32',
                    'includePath': [
                        '${workspaceFolder}/**'
                    ],
                    'defines': [
                        '_DEBUG',
                        'UNICODE',
                        '_UNICODE'
                    ],
                    'windowsSdkVersion': '10.0.19041.0',
                    'compilerPath': 'C:/Program Files/Microsoft Visual Studio/2022/Preview/VC/Tools/MSVC/14.30.30401/bin/Hostx64/x64/cl.exe',
                    'cStandard': 'c17',
                    'cppStandard': 'c++17',
                    'intelliSenseMode': 'windows-msvc-x64'
                }
            ],
            'version': 4
        }
        ");
        string workerPlace = "";//工作空间
        string fileName = "";//文件名
        string exeName = "";//生成程序名
        string stand = "";//c标准
        string[] lang = new string[]{ ".c",".cpp"};//语言
        string winSDK = "";//使用的SDK版本
        string vsWhere = "";//vs路径
        string clPath = "";//cl.exe路径
        string devPrompt = "";//vs 开发者prompt路径
        bool configMSVC = false;//是否配置完成msvc
        bool configwksp = false;//是否配置完成工作空间
        bool configVer = false;//是否配置好c标准
        bool configLoaded = false;//是否使用加载配置
        List<string> winSDKs = new List<string>();//SDK
        List<string> rpVers = new List<string>();//vc运行库版本
        string bitsRunning = "";//目标文件位数
        string configFolder = "";//存放配置的目录
        string lastWksp = "";//上一次使用的工作空间目录

        //使能msvc配置ui
        private void SetConfigAvaliable()
        {
            rp.IsEnabled = true;
            sdk.IsEnabled = true;
            type.IsEnabled = true;
            bits.IsEnabled = true;
            getClPath.IsEnabled = true;
            if (!configLoaded)//没有加载配置就默认
            {
                rp.SelectedIndex = 0;
                sdk.SelectedIndex = 0;
                type.SelectedIndex = 0;
                bits.SelectedIndex = 0;
            }
            SetCL();//拼接cl.exe路径
        }
        //加载配置
        private void LoadConfig()
        {
            //文件创建
            //分别存放工具配置和上一次使用的工作空间
            if(!Directory.Exists(configFolder)) 
                Directory.CreateDirectory(configFolder);
            if (!File.Exists(configFolder + @"/vsct.dat"))
                File.Create(configFolder + @"/vsct.dat").Dispose();
            if (!File.Exists(configFolder + @"/rctwksp.cfg"))
                File.Create(configFolder + @"/rctwksp.cfg").Dispose();
            //读配置
            Stream fs = new FileStream(configFolder + @"/vsct.dat", FileMode.Open, FileAccess.Read, FileShare.Read);
            if(fs.Length > 0)
            {
                BinaryReader bw = new BinaryReader(fs, Encoding.Unicode);
                vsWhere = bw.ReadString();
                //检查vs目录正确性
                Check_vsWhere();
                //依次读取
                type.SelectedIndex = bw.ReadInt32();
                standard.Text = bw.ReadString();
                bits.SelectedIndex = bw.ReadInt32();
                sdk.SelectedIndex = winSDKs.IndexOf(bw.ReadString());//考虑到环境可能发生变化，所以如此查找
                rp.SelectedIndex = rpVers.IndexOf(bw.ReadString());
                manual.IsChecked = bw.ReadBoolean();
                shut.IsChecked = bw.ReadBoolean();
                bw.Close();
                configLoaded = true;
                SetConfigAvaliable();//使能部分ui
                configMSVC = true;
                SetFileAvaliable();//使能部分ui
                SetSourceName();//检查文件名合法否 其实这里没必要
            }
            fs.Close();
            //读上一次历史
            fs = new FileStream(configFolder + @"/rctwksp.cfg", FileMode.Open, FileAccess.Read, FileShare.Read);
            StreamReader sr = new StreamReader(fs, Encoding.Unicode);
            lastWksp = sr.ReadLine();
            sr.Close();
            fs.Close();
        }

        private void WriteConfig()
        {
            //依次写配置（覆盖）
            Stream fs = new FileStream(configFolder + @"/vsct.dat", FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            fs.Seek(0, SeekOrigin.Begin);
            fs.SetLength(0);
            BinaryWriter bw = new BinaryWriter(fs, Encoding.Unicode);
            bw.Write(vsWhere);
            bw.Write(type.SelectedIndex);
            bw.Write(stand);
            bw.Write(bits.SelectedIndex);
            bw.Write(winSDKs[sdk.SelectedIndex]);
            bw.Write(rpVers[rp.SelectedIndex]);
            bw.Write(manual.IsChecked.Value);
            bw.Write(shut.IsChecked.Value);
            bw.Close();
            fs.Close();
            //如果都ok，就存储历史路径
            if(configwksp&&configVer&&configMSVC)
            {
                fs = new FileStream(configFolder + @"/rctwksp.cfg", FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                fs.Seek(0, SeekOrigin.Begin);
                fs.SetLength(0);
                StreamWriter sw = new StreamWriter(fs, Encoding.Unicode);
                sw.WriteLine(workerPlace);
                sw.Close();
                fs.Close();
            }
        }
        public MainWindow()
        {
            InitializeComponent();
            //初始化一些ui设置 
            //如果写在xaml里，初始化ui的时候会提前触发一些事件 造成麻烦 
            bits.ItemsSource = new string[] { "x64", "x86" };
            wksp.Text = "双击以打开目录";
            standard.Text = "17";
            rp.ItemsSource = rpVers;
            sdk.ItemsSource = winSDKs;
            //这里是配置文件目录 默认在我的文档里
            configFolder = Environment.GetFolderPath(Environment.SpecialFolder.Personal)+@"\vs code c tool";
            //尝试加载配置
            LoadConfig();
        }
        //json文件配置
        private void ConfigJson()
        {
            tasks["statement"] = "由vs code c艹生成器 于" + DateTime.Now.ToString() + "生成";
            tasks["tasks"][0]["args"][4] = @"${workspaceFolder}/bin/" + exeName + ".exe";
            tasks["tasks"][0]["args"][6] = @"${workspaceFolder}/src/" + fileName + lang[type.SelectedIndex];
            launch["configurations"][0]["program"] = @"${workspaceFolder}/bin/" + exeName + ".exe";
            c_cpp_properties["configurations"][0]["windowsSdkVersion"] = winSDK;
            c_cpp_properties["configurations"][0]["compilerPath"] = clPath;
            c_cpp_properties["configurations"][0]["cStandard"] = "c" + stand;
            c_cpp_properties["configurations"][0]["cppStandard"] = "c++" + stand;
            c_cpp_properties["configurations"][0]["intelliSenseMode"] = "windows-msvc-" + bitsRunning;
        }
        //copy来的代码 隐式运行
        private void RunExe(string exePath, string arguments, out string output, out string error)
        {
            using (Process process = new System.Diagnostics.Process())
            {
                process.StartInfo.FileName = exePath;
                process.StartInfo.Arguments = arguments;
                // 必须禁用操作系统外壳程序  
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;

                process.Start();

                output = process.StandardOutput.ReadToEnd();
                error = process.StandardError.ReadToEnd();

                process.WaitForExit();
                process.Close();
            }
        }

        //检查源文件名合法
        private void SetSourceName()
        {
            //检查后缀 如果已经填了后缀名
            if (Regex.IsMatch(sourceFileName.Text, @"(.*)(\.cpp)$"))
            {
                if (type.SelectedIndex == 0) fileName = sourceFileName.Text.Substring(0, sourceFileName.Text.Length - 4);//裁得名称
            }
            else if (Regex.IsMatch(sourceFileName.Text, @"(.*)(\.c)$"))
            {
                if (type.SelectedIndex == 1) fileName = sourceFileName.Text.Substring(0, sourceFileName.Text.Length - 2);//裁得名称
            }
            //如果没填
            else
            {
                fileName = sourceFileName.Text != "" ? sourceFileName.Text : "main";//裁得名称&&默认名称
            }
            sourceFileName.Text = fileName + lang[type.SelectedIndex];//恢复成带后缀的显示
        }
        //检查是否可以填写文件名&是否可以启动（最终判据）
        private void SetFileAvaliable()
        {
            sourceFileName.IsEnabled = configMSVC && configwksp && configVer;
            exeFileName.IsEnabled = configMSVC && configwksp && configVer;
            run.IsEnabled = configMSVC && configwksp && configVer;
            run.Content = configMSVC && configwksp && configVer ? "启动" : "似乎还没有准备好";
        }
        //拼接cl.exe路径
        private void SetCL()
        {
            getClPath.Text = vsWhere + @"\VC\Tools\MSVC\" + rp.SelectedItem.ToString() + @"\bin\" + (Environment.Is64BitOperatingSystem ? @"Hostx64\" : @"Hostx32\") + bitsRunning + @"\cl.exe";
        }
        //检查vs目录是否正确
        private void Check_vsWhere()
        {
            devPrompt = vsWhere + @"\Common7\Tools\VsDevCmd.bat";
            if (File.Exists(devPrompt))//通过判断devprompt查看
            {
                //初始化相关值
                rpVers.Clear();
                foreach (var p in Directory.GetDirectories(vsWhere + @"\VC\Tools\MSVC\"))
                    rpVers.Add(System.IO.Path.GetFileName(p));
                winSDKs.Clear();
                //SDK存放于注册表中：
                //HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Kits
                RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows Kits\Installed Roots");
                foreach (var _winSDK in key.GetSubKeyNames())
                    winSDKs.Add(_winSDK);
            }
            //目录不正确 恢复至初始状态
            else
            {
                MessageBox.Show("啊啊啊啊 你在干什么呀 ≧ ﹏ ≦  爬！！");
                //重置
                configMSVC = false;
                winSDKs.Clear();
                rpVers.Clear();
                sdk.IsEnabled = false;
                sdk.SelectedIndex = 0;
                sdk.Text = "";
                rp.IsEnabled = false;
                rp.SelectedIndex = 0;
                rp.Text = "";
                type.IsEnabled = false;
                type.SelectedIndex = 0;
                type.Text = "";
                bits.IsEnabled = false;
                bits.SelectedIndex = 0;
                bits.Text = "";
                getClPath.IsEnabled = false;
                getClPath.Text = "";
                SetFileAvaliable();//重置状态
                manual.IsChecked = false;
                configLoaded = false;
            }
        }
        //msvc配置
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            //手动方法
            if (manual.IsChecked == true)
            {
                System.Windows.Forms.FolderBrowserDialog vsFolder = new System.Windows.Forms.FolderBrowserDialog();
                vsFolder.Description = "选择msvc目录 （应当出现有common7、msbuild之属）";
                vsFolder.SelectedPath = @"C:\Users";
                if (vsFolder.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    vsWhere = vsFolder.SelectedPath;
            }
            //通过微软提供的vswhere查找
            else
            {
                vsWhere = Environment.GetEnvironmentVariable("programfiles(x86)") + @"\Microsoft Visual Studio\Installer\vswhere.exe";
                RunExe(vsWhere, "-prerelease -latest -property installationPath", out vsWhere, out _);
                vsWhere = vsWhere.Remove(vsWhere.Length - 2, 2);
            }
            configLoaded = false;//不加载先前的配置
            Check_vsWhere();//检查vs目录
            SetConfigAvaliable();//使能部分ui
            configMSVC = true;//环境配置ok
            SetFileAvaliable();//设置
            SetSourceName();//设置
        }

        private void wksp_TextChanged(object sender, RoutedEventArgs e)//空间路径
        {
            //检查工作空间路径
            workerPlace = wksp.Text;
            //错误 则禁止下一步文件名填写
            configwksp = Directory.Exists(workerPlace);
            //设置当前工作目录 非常重要 因为批处理命令中使用相对路径 不如此 则会导致启动失败
            if(configwksp) System.IO.Directory.SetCurrentDirectory(workerPlace);
            SetFileAvaliable();
        }
        //设置路径
        private void wksp_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog wkspFolder = new System.Windows.Forms.FolderBrowserDialog();
            wkspFolder.Description = "选择工作空间目录";
            wkspFolder.SelectedPath = @"C:\Users";
            if (wkspFolder.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                workerPlace=wkspFolder.SelectedPath;
            wksp.Text = workerPlace;
            if (File.Exists(workerPlace+ @"\.vscode\c_cpp_properties.json")
                && File.Exists(workerPlace + @"\.vscode\tasks.json")
                && File.Exists(workerPlace + @"\.vscode\launch.json")
                )//是否已经存在任务配置
            {
                System.Windows.MessageBoxResult res = MessageBox.Show("发现已有项目，直接进入吗？","wow",MessageBoxButton.YesNo);
                string startBat = workerPlace + @"\" + System.IO.Path.GetFileName(workerPlace) + @"_start.bat";
                if (res == MessageBoxResult.Yes)
                {
                    if (!File.Exists(startBat))//没有由该工具生成的批处理，则现场创建
                    {
                        StreamWriter sw = new StreamWriter(new FileStream(startBat, FileMode.Create));
                        sw.WriteLine("call "+ '"' +devPrompt + '"');
                        sw.WriteLine("code .");
                        sw.WriteLine("exit");
                        sw.Close();
                    }
                    //执行启动批处理
                    RunExe(startBat,"", out _, out _);
                    //自动关闭
                    if (shut.IsChecked == true)
                        Close();
                }
            }
        }

        private void bits_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //设置位数~~
            bitsRunning = bits.SelectedItem.ToString();
            if (configMSVC)
                SetCL();
        }

        private void sourceFileName_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            //小设计 双击源文件一栏 默认与工作空间同名
            fileName = "";
            if (configwksp&&configMSVC)
            {
                fileName = System.IO.Path.GetFileName(workerPlace);
                sourceFileName.Text = fileName + lang[type.SelectedIndex];
            }
        }

        private void exeFileName_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            //小设计 双击生成文件一栏 默认与源文件同名
            exeName = "";
            if (configwksp&&configMSVC)
            {
                exeName = fileName!="" ? fileName : "main";
                exeFileName.Text = exeName + ".exe";
            }
        }

        private void standard_TextChanged(object sender, TextChangedEventArgs e)
        {
            //硬编码（雾）
            if (standard.Text == "99" || standard.Text == "11" || standard.Text == "17" || standard.Text == "20")
            {
                stand = standard.Text;
                standardWarn.Content = "标准";
                configVer = true;
            }
            else
            {
                standardWarn.Content = "标准（输入不符）";
                configVer = false;
            }
            SetFileAvaliable();
        }

        //检查cl.exe路径
        private void getClPath_TextChanged(object sender, TextChangedEventArgs e)
        {
            clPath = getClPath.Text;
            configMSVC = File.Exists(getClPath.Text) && (System.IO.Path.GetFileName(getClPath.Text) == "cl.exe");
            SetFileAvaliable();
        }
        //检查源文件名
        private void sourceFileName_LostFocus(object sender, RoutedEventArgs e)
        {
            SetSourceName();
        }
        //检查生成目标文件名
        private void exeFileName_LostFocus(object sender, RoutedEventArgs e)
        {
            if (Regex.IsMatch(exeFileName.Text, @"(.*)(\.exe)$"))
                exeFileName.Text = exeFileName.Text.Substring(0, exeFileName.Text.Length - 4);
            exeName = exeFileName.Text;
            exeFileName.Text += ".exe";
        }
        //同理
        private void type_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (configMSVC) SetSourceName();
        }
        //同理
        private void sourceFileName_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!Regex.IsMatch(exeFileName.Text, @"(.*)(\.exe)$"))
            {
                exeFileName.Text = fileName + ".exe";
                exeName = fileName;
            }
        }
        //启动
        private void run_Click(object sender, RoutedEventArgs e)
        {
            //创建必要文件和目录 配置json文件
            winSDK = winSDKs[sdk.SelectedIndex];
            ConfigJson();
            Directory.CreateDirectory(workerPlace + @"\bin");
            Directory.CreateDirectory(workerPlace + @"\obj");
            Directory.CreateDirectory(workerPlace + @"\src");
            Directory.CreateDirectory(workerPlace + @"\.vscode");
            StreamWriter sw = new StreamWriter(new FileStream(workerPlace + @"\.vscode\本项目由vscode快速c艹器生成.txt",
                                FileMode.Create));
            sw.WriteLine(System.DateTime.Now.ToString());
            sw.WriteLine("由vscode快速c艹器辣鸡生成. copyright ");
            sw.Close();
            //生成批处理 日后使用
            string startBat = workerPlace + @"\" + System.IO.Path.GetFileName(workerPlace) + @"_start.bat";
            sw = new StreamWriter(new FileStream(startBat, FileMode.Create));
            sw.WriteLine("call " + '"' + devPrompt + '"');
            sw.WriteLine("code .");
            sw.WriteLine("exit");
            sw.Close();
            //各种写文件
            sw = new StreamWriter(workerPlace + @"\src\" + fileName + lang[type.SelectedIndex]);
            sw.Write(templates[type.SelectedIndex]);
            sw.Close();

            sw = new StreamWriter(new FileStream(workerPlace + @"\.vscode\tasks.json", FileMode.Create));
            sw.Write(tasks.ToString());
            sw.Close();
            sw = new StreamWriter(new FileStream(workerPlace + @"\.vscode\c_cpp_properties.json", FileMode.Create));
            sw.Write(c_cpp_properties.ToString());
            sw.Close();
            sw = new StreamWriter(new FileStream(workerPlace + @"\.vscode\launch.json", FileMode.Create));
            sw.Write(launch.ToString());
            sw.Close();
            //运行
            RunExe(startBat,"", out _, out _);
            //保存配置
            WriteConfig();
            if(shut.IsChecked == true)
                this.Close();
    }
        //保存配置
        private void Window_Closed(object sender, EventArgs e)
        {
            if(configMSVC&&configVer) WriteConfig();
        }

        //小设计 双击”工作空间“跳转至最近一次使用路径
        private void Label_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lastWksp != "")
                Process.Start("Explorer.exe", lastWksp);
        }
    }

    

}
