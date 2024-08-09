using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SeeSharpTools.JY.ArrayUtility;
using SeeSharpTools.JY.DSP.Fundamental;
using SeeSharpTools.JY.DSP.Utility;
using SeeSharpTools.JY.Mathematics;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using MathNet.Numerics.Data.Matlab;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics.LinearAlgebra;
using System.IO.Compression;
using System.IO;
using JY5320;
using System.Threading;


/// <summary>
/// 数据采集系统
/// sunxun
/// 日期：2024.7.31
/// 采集板卡：JY5515、JY6312（热电偶温度传感器）、JY5324(电涡流位移传感器)、JY9515（振动加速度传感器）、JY6311（铂电阻温度传感器）
/// 功能：数据采集、数据处理、频谱分析、传感器调试、数据存储
/// </summary>

namespace Rotor_Dynamic_test
{
    public partial class MainForm : Form
    {
        #region 控件大小随窗体大小等比例缩放

        private float x;//定义当前窗体的宽度
        private float y;//定义当前窗体的高度
        private void setTag(Control cons)
        {
            foreach (Control con in cons.Controls)
            {
                con.Tag = con.Width + ";" + con.Height + ";" + con.Left + ";" + con.Top + ";" + con.Font.Size;
                if (con.Controls.Count > 0)
                {
                    setTag(con);
                }
            }
        }
        private void setControls(float newx, float newy, Control cons)
        {
            //遍历窗体中的控件，重新设置控件的值
            foreach (Control con in cons.Controls)
            {
                //获取控件的Tag属性值，并分割后存储字符串数组
                if (con.Tag != null)
                {
                    string[] mytag = con.Tag.ToString().Split(new char[] { ';' });
                    //根据窗体缩放的比例确定控件的值
                    con.Width = Convert.ToInt32(System.Convert.ToSingle(mytag[0]) * newx);//宽度
                    con.Height = Convert.ToInt32(System.Convert.ToSingle(mytag[1]) * newy);//高度
                    con.Left = Convert.ToInt32(System.Convert.ToSingle(mytag[2]) * newx);//左边距
                    con.Top = Convert.ToInt32(System.Convert.ToSingle(mytag[3]) * newy);//顶边距
                    //Single currentSize = System.Convert.ToSingle(mytag[4]) * newy;//字体大小
                    //con.Font = new Font(con.Font.Name, currentSize, con.Font.Style, con.Font.Unit);
                    if (con.Controls.Count > 0)
                    {
                        setControls(newx, newy, con);
                    }
                }
            }
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            float newx = (this.Width) / x;
            float newy = (this.Height) / y;
            setControls(newx, newy, this);
        }
        #endregion

        #region Private Fields

        #region 系统变量

        /// <summary>
        /// 文件名
        /// </summary>
        private string fileName;

        /// <summary>
        /// 文件存储路径
        /// </summary>
        private string filePath;

        /// <summary>
        /// 初始化任务标志
        /// </summary>
        private bool isInitTaskFlag;

        /// <summary>
        /// 开始任务标志
        /// </summary>
        private bool isStartTaskFlag;

        /// <summary>
        /// 开始存储标志
        /// </summary>
        private bool isStartSaveFlag;
        #endregion

        #region 采集板卡配置文件名

        /// <summary>
        /// 5320采集板卡配置文件
        /// </summary>
        private string jsonConfig5320File;

        /// <summary>
        /// 5320json配置对象，承载配置文件中的配置信息
        /// </summary>
        private dynamic jsonConfig_5320Obj;

        /// <summary>
        /// 6312采集板卡配置文件
        /// </summary>
        private string jsonConfig6312File;

        /// <summary>
        /// 6312json配置对象，承载配置文件中的配置信息
        /// </summary>
        private dynamic jsonConfig_6312Obj;

        #endregion

        #region 采集板卡配置参数

        #region 5324采集板卡配置参数

        /// <summary>
        ///  AITask
        /// </summary>
        private JY5320AITask aiTask5324;

        /// <summary>
        /// 采集信号数据
        /// the Buffer of data acquisition by the AITask
        /// 由 AITask 进行的数据采集缓冲区
        /// </summary>
        double[,] readValue5324;
        private double[] readData_1ch;
        private double[] readData_2ch;
        private double[] spectrumData;

        /// <summary>
        /// 显示信号数据
        /// 读取值转置后的数据缓冲区，其容量与 readValue 相同
        /// </summary>
        double[,] displayValue5324;

        /// <summary>
        /// 存储信号数组
        /// </summary>
        private double[,] saveData5324;

        /// <summary>
        /// 通道
        /// </summary>
        private List<ChannelID5324> chnsList5324;

        /// <summary>
        /// 文件名
        /// </summary>
        private string fileName5324;

        /// <summary>
        /// 文件存储路径
        /// </summary>
        private string filePath5324;

        /// <summary>
        /// 数据写入器
        /// </summary>
        private StreamWriter swData5324;

        /// <summary>
        /// 文件写入器
        /// </summary>
        private FileStream fsData5324;

        /// <summary>
        /// 生产者线程，产生数据
        /// </summary>
        private Thread _producerThread5324;

        /// <summary>
        /// 消费者线程，存储数据
        /// </summary>
        private Thread _consumerThread5324;

        /// <summary>
        /// 队列缓存
        /// </summary>
        private Queue<double[,]> _bufferQ5324;

        /// <summary>
        /// 当前保存类型配置是否完成标志
        /// </summary>
        private bool isCurrentConfigFinishFlag5324;

        /// <summary>
        /// 保存状态，1表示开始当前保存状态；2表示当前保存结束，等待下次保存状态;3表示等待当前保存状态
        /// </summary>
        private int statusSave5324;

        /// <summary>
        /// 自定义时刻采集第一次标志，true：第1次采集，false：第2、3、....次采集
        /// </summary>
        private bool isFirstDefineSave5324;

        /// <summary>
        /// input low limit
        /// </summary>
        private double lowRange5320;

        /// <summary>
        /// input high limit
        /// </summary>
        /// <returns></returns>
        private double highRange5320;

        private int channelCount5320;

        private int channelChecked5320;

        private double[] JY5321_5322Range = new double[] { 10, 5, 2, 1, 0.5, 0.25 };
        private double[] JY5323_5324Range = new double[] { 10, 5, 1, 0.5 };

        private bool is6Range5320 = false;
        #endregion

        #region 6312采集板卡配置参数

        #endregion

        #region 6312采集板卡配置参数

        #endregion

        #region 6312采集板卡配置参数

        #endregion

        #endregion

        #endregion

        #region Contruct
        public MainForm()
        {
            InitializeComponent();

            //窗口大小调节
            x = this.Width;
            y = this.Height;
            setTag(this);

            #region 数据采集系统使用说明

            richTextBox_SystemUsageInstructions.Text = @"
    首次使用步骤：
      参数设置界面
        1、根据采集板卡在机箱中对应的插槽编号选择插槽号。
        2、根据试验需求设置各采集板卡的采样率及采集样本数。
        3、设置输入信号范围。
        4、点击保存配置文件。（在第二次打开程序时配置文件会自动加载）
        5、选择实验数据保存路径。
        6、选择是否需要保存试验数据。
      系统界面
        点击“开始”，程序开始采集实验数据，此时将启动全部的测量模块。如需要单独测量一个物理量请在“测量”界面选择相应的模块启动。    
            ";
            #endregion

            //调用方法来创建JSON文件
            EnsureJsonFileExists();
        }
        #endregion

        #region Event Handler

        /// <summary>
        /// 在主窗体加载时触发
        /// </summary>
        #region Form Initialization Fuction 窗体初始化

        /// <summary>
        /// 窗体初始化函数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainForm_Load(object sender, EventArgs e)
        {
            #region 主窗体设置

            /// <summary>
            /// 禁用按钮
            /// </summary>
            //禁用全部采集进程停止按钮
            button_Stop.Enabled = false;
            //禁用位移数据采集停止按钮
            button_DispStop.Enabled = false;

            //参数赋初值
            isInitTaskFlag = false;
            isStartSaveFlag = false;
            isInitTaskFlag = false;

            // 初始化toolStripStatusLabel控件的文本属性，即不显示任何内容
            toolStripStatusLabel1.Text = "";

            #endregion

            #region 5324Initialization

            //初始化5320板卡号
            comboBox_5320cardID.SelectedIndex = 0;
            //初始化5320槽号
            comboBox_5320SlotNumber.SelectedIndex = 0;
            //启用groupBox_5320set控件，使其可以与用户交互。
            groupBox_5320set.Enabled = true;
            //设置采样率
            numericUpDown_5320sampleRate.Value = (decimal)10000.0;
            numericUpDown_5320samples.Value = (decimal)2000.0;

            //线程置为空
            _producerThread5324 = null;
            _consumerThread5324 = null;

            // 加载5324采集板卡参数
            jsonConfig5320File = "jsonConfig5320.json";
            StreamReader streamReader = new StreamReader(jsonConfig5320File);      // 读取文件到流
            string jsonconfig5320Str = streamReader.ReadToEnd();    // 一次性读入所有数据
            // 解析配置信息
            jsonConfig_5320Obj = JsonConvert.DeserializeObject<dynamic>(jsonconfig5320Str);
            // 将配置信息写入窗体物理通道名控件
            comboBox_5320cardID.Text = (string)jsonConfig_5320Obj["cardID5320"];
            comboBox_5320SlotNumber.Text = (string)jsonConfig_5320Obj["SlotNumber"];
            numericUpDown_5320sampleRate.Text = (string)jsonConfig_5320Obj["SampleRate"];
            numericUpDown_5320samples.Text = (string)jsonConfig_5320Obj["SamplesToAcquire"];
            comboBox_5320inputRange.Text = (string)jsonConfig_5320Obj["InputRange"];
            textBox_DataFilePath.Text = (string)jsonConfig_5320Obj["DataFilePath5320"];

            // 关闭文件流
            streamReader.Close();

            // 采集状态0，未开始采集状态
            statusSave5324 = 0;
            #endregion

            #region 6312Initialization

            #endregion

            #region 5515Initialization

            #endregion

            #region 9515Initialization

            #endregion

            #region 6311Initialization

            #endregion
        }
        #endregion

        /// <summary>
        /// 设置各采集板卡的默认参数
        /// </summary>
        #region Parameter Configuration for Each Acquisition Board Card 板卡参数配置

        #region Parameter Configuration for the 5324 

        /// <summary>
        /// select all channel
        /// 点击“选择全部通道”按钮后，选择5324的全部通道
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkBox_5320selectchannel_CheckedChanged(object sender, EventArgs e)
        {
            // 检查CheckBox是否被选中
            if (checkBox_5320selectchannel.Checked)
            {
                // 如果CheckBox被选中，则遍历checkedListBox_5320portChoose中的所有项目    
                for (int i = 0; i < checkedListBox_5320portChoose.Items.Count; i++)
                {
                    // 将checkedListBox_5320portChoose中的每个项目的状态设为选中（Checked）    
                    checkedListBox_5320portChoose.SetItemCheckState(i, CheckState.Checked);
                }
            }
            else
            {
                // 如果CheckBox未被选中，则同样遍历checkedListBox_5320portChoose中的所有项目    
                for (int i = 0; i < checkedListBox_5320portChoose.Items.Count; i++)
                {
                    // 将checkedListBox_5320portChoose中的每个项目的状态设为未选中（Unchecked）  
                    checkedListBox_5320portChoose.SetItemCheckState(i, CheckState.Unchecked);
                }
            }
        }

        /// <summary>
        /// Set CardID Basic Param
        /// 设定5324板卡基础参数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void comboBox_5320cardID_SelectedIndexChanged(object sender, EventArgs e)
        {
            comboBox_5320inputRange.Items.Clear();
            channelCount5320 = 0;
            //设置5320通道号
            checkedListBox_5320portChoose.Items.Clear();   //清除所有选项           
            switch (comboBox_5320cardID.Text)
            {
                case "5324":
                    is6Range5320 = false;
                    channelCount5320 = 5;
                    break;
                default:
                    is6Range5320 = true;
                    channelCount5320 = 32;
                    break;
            }
            if (is6Range5320)
            {
                for (int j = 0; j < JY5321_5322Range.Length; j++)
                {
                    comboBox_5320inputRange.Items.Add(string.Format("±{0}V", JY5321_5322Range[j]));
                }
            }
            else
            {
                for (int j = 0; j < JY5323_5324Range.Length; j++)
                {
                    comboBox_5320inputRange.Items.Add(string.Format("±{0}V", JY5323_5324Range[j]));
                }
            }
            // 项目名称为"5324Channel [数字]"，其中[数字]是从0到4的循环计数器i。
            for (int i = 0; i < channelCount5320; ++i)
            {
                // 使用string.Format方法格式化字符串，将i的值插入到"5324Channel {0}"模板中
                //将格式化后的字符串作为新的项目添加到checkedListBox_5320portChoose中。
                checkedListBox_5320portChoose.Items.Add(string.Format("5324Channel {0}", i), false);
            }
            /// <summary>
            ///此行代码用于设置 checkedListBox_5320portChoose 控件中索引为 0 的项的检查状态。          
            ///它允许用户从多个选项中选择一个或多个项。
            ///SetItemCheckState 方法接受两个参数：
            ///第一个参数是一个整数，表示要更改检查状态的项在 Items 集合中的索引位置。
            ///注意：索引是从零开始的，因此这里的 '0' 表示第一项。
            ///第二个参数是一个枚举值，来自 System.Windows.Forms.CheckState 枚举，
            /// 它定义了三个可能的状态：Unchecked、Indeterminate 和 Checked。
            /// 这里的 'CheckState.Checked' 将对应项的检查状态设置为已选中。
            /// 综上所述，这行代码将 checkedListBox_5320portChoose 控件的第一项设置为选中状态。
            /// </summary>
            checkedListBox_5320portChoose.SetItemCheckState(0, CheckState.Checked);
            comboBox_5320inputRange.SelectedIndex = 0;
        }

        /// <summary>
        /// select input limit
        /// 选择5324板卡的输入电压范围
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void comboBox_5320inputRange_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (comboBox_5320inputRange.SelectedIndex)
            {
                case 0:
                    lowRange5320 = -10;
                    highRange5320 = 10;
                    break;
                case 1:
                    lowRange5320 = -5;
                    highRange5320 = 5;
                    break;
                case 2:
                    lowRange5320 = -1;
                    highRange5320 = 1;
                    break;
                case 3:
                    lowRange5320 = -0.5;
                    highRange5320 = 0.5;
                    break;
                default:
                    lowRange5320 = -10;
                    highRange5320 = 10;
                    break;
            }
        }

        /// <summary>
        /// 创建5324通道号
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkedListBox_5320portChoose_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (chnsList5324 == null)    //如果通道未初始化，则实例化
            {
                chnsList5324 = new List<ChannelID5324>();
            }
            else    //否则清空项目
            {
                chnsList5324.Clear();
            }

            for (int i = 0; i < checkedListBox_5320portChoose.Items.Count; ++i)
            {
                //选中当前项则添加通道
                if (checkedListBox_5320portChoose.GetItemChecked(i))
                {
                    ChannelID5324 channelID5324;
                    channelID5324.channels = i;
                    chnsList5324.Add(channelID5324);
                }
            }
        }

        #endregion

        #region Parameter Configuration for the 6312 

        #endregion

        #region Parameter Configuration for the 5515 

        #endregion

        #region Parameter Configuration for the 9515 

        #endregion

        #region Parameter Configuration for the 6311 

        #endregion

        #endregion

        /// <summary>
        /// 存储各采集板卡的配置文件
        /// </summary>
        #region Storage Configuration File 存储配置文件

        private void button_SaveConfigData_Click(object sender, EventArgs e)
        {
            #region 5320ConfigSave

            //存储5324板卡配置参数
            jsonConfig_5320Obj["cardID5320"] = comboBox_5320cardID.Text;
            jsonConfig_5320Obj["SlotNumber"] = comboBox_5320SlotNumber.Text;
            jsonConfig_5320Obj["InputRange"] = comboBox_5320inputRange.Text;
            jsonConfig_5320Obj["lowRange5320"] = lowRange5320;
            jsonConfig_5320Obj["highRange5320"] = highRange5320;
            jsonConfig_5320Obj["SampleRate"] = numericUpDown_5320sampleRate.Value;
            jsonConfig_5320Obj["SamplesToAcquire"] = numericUpDown_5320samples.Value;
            //存储5324板卡采集到的数据文件路径
            jsonConfig_5320Obj["DataFilePath5320"] = textBox_DataFilePath.Text;

            //存储5324板卡通道列表
            //5324被选中的通道数量
            channelChecked5320 = 0;
            // 遍历 checkedListBox_5320portChoose 中的所有项
            for (int Count5324 = 0; Count5324 < checkedListBox_5320portChoose.Items.Count; Count5324++)
            {
                // 使用 GetItemCheckState 方法检查当前项是否被选中
                if (checkedListBox_5320portChoose.GetItemCheckState(Count5324) == CheckState.Checked)
                {
                    // 如果当前项被选中，增加 channelChecked5320 的值
                    channelChecked5320++;
                }
            }
            //将 channelChecked5320 的值存入json文件中
            jsonConfig_5320Obj["5320ChannelCount"] = channelChecked5320;

            // 使用JsonConvert类的SerializeObject方法，将jsonConfig_5320Obj对象转换成JSON格式的字符串。
            // Formatting.Indented参数使得输出的JSON字符串具有可读性，即包含缩进和换行。
            string config5320Output = JsonConvert.SerializeObject(jsonConfig_5320Obj, Formatting.Indented);

            // 使用File类的WriteAllText方法，将config5320Output字符串写入到jsonConfig5320File指定的文件中，
            // 从而保存了JSON配置信息。
            File.WriteAllText(jsonConfig5320File, config5320Output);
            #endregion

            #region 6312ConfigSave

            #endregion

            #region 5515ConfigSave

            #endregion

            #region 9515ConfigSave

            #endregion
        }

        /// <summary>
        /// 选择保存路径
        /// </summary>
        private void button_DataFilePath_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog_DataSave.ShowDialog() == DialogResult.OK)
            {
                //显示保存的路径
                textBox_DataFilePath.Text = folderBrowserDialog_DataSave.SelectedPath;
            }
        }
        #endregion

        /// <summary>
        /// 开始数据采集
        /// </summary>
        #region Start Acquisition 开始采集

        /// <summary>
        /// 仅启动位移数据采集，用于位移传感器调试或者特殊测试需求
        /// 5324板卡
        /// </summary>       
        private void button_DispStart_Click(object sender, EventArgs e)
        {
            try
            {
                // 检测参数配置是否合理
                CheckConfigParams5324();

                // 初始化采集参数

                // 开始采集任务

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }


            //按钮禁用，防止用户第二次点击
            button_DispStart.Enabled = false;
            //按钮启用，允许用户停止数据采集
            button_DispStop.Enabled = true;

            //禁用5324板卡参数配置选项，防止用户在采集过程中更改
            comboBox_5320cardID.Enabled = false;
            comboBox_5320SlotNumber.Enabled = false;
            numericUpDown_5320sampleRate.Enabled = false;
            numericUpDown_5320samples.Enabled = false;
            comboBox_5320inputRange.Enabled = false;
            checkBox_5320selectchannel.Enabled = false;
            checkedListBox_5320portChoose.Enabled = false;

            //均认为是第一次开始存储
            isFirstDefineSave5324 = true;
            isCurrentConfigFinishFlag5324 = false;

            //使能定时器
            timer_FetchData.Enabled = true;
        }

        #endregion

        /// <summary>
        /// 停止数据采集
        /// </summary>
        #region Stop Acquisition 停止采集

        /// <summary>
        /// 仅停止位移数据采集
        /// </summary>
        private void button_DispStop_Click(object sender, EventArgs e)
        {
            //按钮禁用，防止用户第二次点击
            button_DispStop.Enabled = false;
            //按钮启用，允许用户再次开始数据采集
            button_DispStart.Enabled = true;

            //启用5324板卡参数配置选项，允许用户更改
            comboBox_5320cardID.Enabled = true;
            comboBox_5320SlotNumber.Enabled = true;
            numericUpDown_5320sampleRate.Enabled = true;
            numericUpDown_5320samples.Enabled = true;
            comboBox_5320inputRange.Enabled = true;
            checkBox_5320selectchannel.Enabled = true;
            checkedListBox_5320portChoose.Enabled = true;

            // 设置toolStripStatusLabel控件的文本属性，停止了由AITask执行的数据采集操作
            toolStripStatusLabel1.Text = "已停止位移采集。";

            //停止振动分析采集任务（电涡流位移传感器）
            Stop5320TaskAcquisition();
        }

        #endregion

        /// <summary>
        /// 窗口关闭
        /// </summary>
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            //停止振动分析采集任务（电涡流位移传感器）
            Stop5320TaskAcquisition();
        }


        #endregion

        #region Method

        #region 5324板卡方法

        /// <summary>
        /// 检测参数配置是否合理
        /// </summary>
        private void CheckConfigParams5324()
        {
            //如果没有选择数据保存路径，则提示用户先选择保存路径
            if (textBox_DataFilePath.Text == string.Empty)
            {
                MessageBox.Show("请选择数据文件存储路径");
                return;
            }
            else
            {
                filePath5324 = textBox_DataFilePath.Text;
            }

            // 判断通道数
            if (chnsList5324 == null)         // 未添加通道
            {
                throw new Exception("请选择待采样通道！");
            }
            else if (chnsList5324.Count == 0)     // 如果通道数为0，则添加通道
            {
                throw new Exception("当前通道数为0，请选择采样通道！");
            }

        }

        /// <summary>
        /// 初始化采集任务
        /// </summary>
        private void InitTaskAcquistion5324()
        {
            try
            {
                //实例化5324板卡模拟输入（AI）采集任务
                aiTask5324 = new JY5320AITask(comboBox_5320SlotNumber.Text);

                //添加通道
                for(int i = 0; i < chnsList5324.Count; ++i)
                {
                    aiTask5324.AddChannel(chnsList5324[i].channels, lowRange5320, highRange5320);
                }

                //设置连续采集
                aiTask5324.Mode = AIMode.Continuous;
                // 设置采样率
                aiTask5324.SampleRate = (double)numericUpDown_5320sampleRate.Value;
                // 设置采样点数
                aiTask5324.SamplesToAcquire = (int)numericUpDown_5320samples.Value;
                // 配置触发参数
                aiTask5324.Trigger.Type = AITriggerType.Immediate;

                //初始化数组
            }
            catch (JYDriverException ex)
            {
                toolStripStatusLabel1.Text = string.Format("参数配置错误：{0}", ex.Message);
                throw new Exception(toolStripStatusLabel1.Text);
            }
        }

        #endregion

        #region 6312板卡方法

        #endregion

        /// <summary>
        /// 确保应用程序目录下存在指定的JSON文件。
        /// 如果文件不存在，则创建一个默认内容的JSON文件。
        /// 分别建立每张采集板卡的json文件
        /// </summary>
        private void EnsureJsonFileExists()
        {

            #region Ensure5320JsonFileExists
            string filePath5320 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "jsonConfig5320.json");
            if (!File.Exists(filePath5320))
            {
                var defaultData5320 = new
                {
                    default5320 = "0",
                };

                string json5320Content = JsonConvert.SerializeObject(defaultData5320, Formatting.Indented);
                File.WriteAllText(filePath5320, json5320Content);
                Console.WriteLine("JSON file created with default content.");
            }
            else
            {
                Console.WriteLine("JSON file already exists.");
            }
            #endregion

            #region Ensure6312JsonFileExists

            string filePath6312 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "jsonConfig6312.json");
            if (!File.Exists(filePath6312))
            {
                var defaultData6312 = new
                {
                    default6312 = "0"
                };

                string json6312Content = JsonConvert.SerializeObject(defaultData6312, Formatting.Indented);
                File.WriteAllText(filePath6312, json6312Content);
                Console.WriteLine("JSON file created with default content.");
            }
            else
            {
                Console.WriteLine("JSON file already exists.");
            }
            #endregion

            #region Ensure5515JsonFileExists

            string filePath5515 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "jsonConfig5515 .json");
            if (!File.Exists(filePath5515))
            {
                var defaultData5515 = new
                {
                    default5515 = "0"
                };

                string json5515Content = JsonConvert.SerializeObject(defaultData5515, Formatting.Indented);
                File.WriteAllText(filePath5515, json5515Content);
                Console.WriteLine("JSON file created with default content.");
            }
            else
            {
                Console.WriteLine("JSON file already exists.");
            }
            #endregion

            #region Ensure9515JsonFileExists

            string filePath9515 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "jsonConfig9515 .json");
            if (!File.Exists(filePath9515))
            {
                var defaultData9515 = new
                {
                    default9515 = "0"
                };

                string json9515Content = JsonConvert.SerializeObject(defaultData9515, Formatting.Indented);
                File.WriteAllText(filePath9515, json9515Content);
                Console.WriteLine("JSON file created with default content.");
            }
            else
            {
                Console.WriteLine("JSON file already exists.");
            }
            #endregion

            #region Ensure6311JsonFileExists

            string filePath6311 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "jsonConfig6311 .json");
            if (!File.Exists(filePath6311))
            {
                var defaultData6311 = new
                {
                    default6311 = "0"
                };

                string json6311Content = JsonConvert.SerializeObject(defaultData6311, Formatting.Indented);
                File.WriteAllText(filePath6311, json6311Content);
                Console.WriteLine("JSON file created with default content.");
            }
            else
            {
                Console.WriteLine("JSON file already exists.");
            }
            #endregion
        }

        /// <summary>
        /// 振动分析数据采集停止方法
        /// </summary>
        private void Stop5320TaskAcquisition()
        {
            try
            {
                //Determine if the task exists
                if (aiTask5324 != null)
                {
                    //stop
                    aiTask5324.Stop();
                    //Clear the channel that was added last time
                    aiTask5324.Channels.Clear();
                }
            }
            catch (JYDriverException ex)
            {
                //Drive error message display
                MessageBox.Show(ex.Message);
            }

            //Disable timer, enable parameter configuration button and start button, display status
            timer_FetchData.Enabled = false;
        }



        #endregion


    }

    /// <summary>
    /// 5324通道定义
    /// </summary>
    public struct ChannelID5324
    {
        public int channels;       //  通道号
    }

    /// <summary>
    /// 采集模式
    /// </summary>
}
