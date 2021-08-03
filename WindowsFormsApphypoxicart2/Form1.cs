using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Management;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;
using System.Threading;
using System.Diagnostics;
using Microsoft.SqlServer.Server;
using System.IO;
using CsvHelper;
using System.Globalization;
using System.Security.Policy;
using System.Text.RegularExpressions;

namespace WindowsFormsApphypoxicart2
{
    public partial class Form1 : Form
    {
//Declaration of Variables
        SerialPort sp = new SerialPort("COM6", 9600, Parity.None, 8, StopBits.One);
        List<double> termslist = new List<double>();
        List<double> prlist = new List<double>();
        List<double> inlist = new List<double>();
        List<string> timelist = new List<string>();
        List<double> newlist = new List<double>();
        List<double> newprlist = new List<double>();
        List<double> newinlist = new List<double>();
        List<string> modelist = new List<string>();
        Stopwatch stopwatch = Stopwatch.StartNew();
        public string modeflag = "0";
        public int editflag = 0;
        public int recflag = 0;
        public int intrecflag = 0;
        public TimeSpan saveTime = TimeSpan.Zero;
        StringBuilder sb = new StringBuilder();

        public Form1()
        {
            InitializeComponent();
            GetAvailablePorts();

//Specify Chart Characteristics
            SpPlot.ChartAreas[0].AxisY.Maximum = 100;
            SpPlot.ChartAreas[0].AxisY.Minimum = 60;
            SpPlot.ChartAreas[0].AxisX.Maximum = 1000;
            SpPlot.ChartAreas[0].AxisX.LabelStyle.Enabled = false;

            prPlot.ChartAreas[0].AxisY.Maximum = 150;
            prPlot.ChartAreas[0].AxisY.Minimum = 50;
            prPlot.ChartAreas[0].AxisX.Maximum = 1000;
            prPlot.ChartAreas[0].AxisX.LabelStyle.Enabled = false;

            inPlot.ChartAreas[0].AxisY.Maximum = 25;
            inPlot.ChartAreas[0].AxisY.Minimum = 0;
            inPlot.ChartAreas[0].AxisX.Maximum = 1000;
            inPlot.ChartAreas[0].AxisX.LabelStyle.Enabled = false;

            protocolBox.SelectedIndex = 0;
            stopwatch.Stop();
            hypbut.Enabled = false;
            this.KeyPreview = true;
        }
//COM Ports
        void GetAvailablePorts()
        {
            comboBox_portnames.Items.Clear();
            string[] ports = SerialPort.GetPortNames();
            comboBox_portnames.Items.AddRange(ports);
            try
            {
                comboBox_portnames.SelectedIndex = 0;
            }
            catch (ArgumentOutOfRangeException){ }
        }
//Serial Ports
        public void SerialPortProgram()
        {
            sp.PortName = comboBox_portnames.Text;
            sp.DataReceived += new SerialDataReceivedEventHandler(port_OnReceiveData);
            sp.Open();
            sp.Write("o"); //o is rOom air, don't want \n and n and r confusion for normoxia 
            stopwatch.Start();
        }
        private static readonly Regex boxNumberRegex = new Regex(@"[s]\d{4}[p]\d{4}[i]\d{4}[e]");
        public static bool VerifyBoxNumber(string boxNumber)
        {
            return boxNumberRegex.IsMatch(boxNumber);
        }
        private void port_OnReceiveData(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                SerialPort sp = (SerialPort)sender;

                string data = sp.ReadExisting();
                foreach (char c in data)
                {
                    if (c==(char)10 | c==(char)13)
                    {
                        sb.Append(c);
                        string tempval = sb.ToString();
                        sb.Clear();
                        if (VerifyBoxNumber(tempval))
                        {
                            UpdateTextBox(tempval);
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
            }
            catch (IOException)
            {
                datasaving();
                MessageBox.Show("Serial Port disconnected\nData has been saved");
                Environment.Exit(0);
            }
        }
//Updating based on inputs
        public void UpdateTextBox(string value)
        {
            if (InvokeRequired)
            {
                this.Invoke(new Action<string>(UpdateTextBox), new object[] { value });
                return;
            }
            int sIndex = value.IndexOf('s');
            if (sIndex == 0)
            {
                int pIndex = value.IndexOf('p', sIndex);
                int iIndex = value.IndexOf('i', pIndex);
                int eIndex = value.IndexOf('e', iIndex);
                string sValue = value.Substring(sIndex + 1, pIndex - sIndex - 1);
                string pValue = value.Substring(pIndex + 1, iIndex - pIndex - 1);
                string iValue = value.Substring(iIndex + 1, eIndex - iIndex - 1);
                try
                {
                    double sval = Math.Round(Double.Parse(sValue) * (0.106 + (Double.Parse(SpGain.Value.ToString()) / 1000)),0);
                    double pval = Math.Round(Double.Parse(pValue) * (0.322 + (Double.Parse(pulseGain.Value.ToString()) / 1000)), 0);
                    double ival = Math.Round(Double.Parse(iValue) * (0.104 + (Double.Parse(iO2Gain.Value.ToString()) / 1000)), 1);
                    string tim = stopwatch.Elapsed.ToString("mm':'ss'.'fff");
                    string mdeTime = (stopwatch.Elapsed-saveTime).ToString("mm':'ss'.'fff");

                    termslist.Add(sval);
                    prlist.Add(pval);
                    inlist.Add(ival);
                    timelist.Add(tim);
                    modelist.Add(modeflag);
                    etime.Text = tim;
                    mtime.Text = mdeTime;
                    if (termslist.Count > 999)
                    {
                        newlist = termslist.GetRange(termslist.Count - 999, 999);
                        newprlist = prlist.GetRange(prlist.Count - 999, 999);
                        newinlist = inlist.GetRange(inlist.Count - 999, 999);
                    }
                    else
                    {
                        newlist = termslist;
                        newprlist = prlist;
                        newinlist = inlist;
                    }
                    testtext.Text = sval.ToString();
                    prText.Text = pval.ToString();
                    inText.Text = ival.ToString();
                }
                catch (FormatException){ }
            }

            if (SpPlot.IsHandleCreated)
            {
                this.Invoke((MethodInvoker)delegate { UpdateSpPlot(); });
            }
        }
        private void UpdateSpPlot()
        {
            if(termslist.Count>999)
            {
                SpPlot.Series["Series1"].Points.Clear();
                SpPlot.Series["Series1"].Points.DataBindY(newlist);
                inPlot.Series["Series1"].Points.Clear();
                inPlot.Series["Series1"].Points.DataBindY(newinlist);
                prPlot.Series["Series1"].Points.Clear();
                prPlot.Series["Series1"].Points.DataBindY(newprlist);
            }
            else
            {
                SpPlot.Series["Series1"].Points.DataBindY(newlist);
                inPlot.Series["Series1"].Points.DataBindY(newinlist);
                prPlot.Series["Series1"].Points.DataBindY(newprlist);
            }
        }
//Hyp and Norm Functions
        public int GetRecString(int data, char cmode)
        {
            int intrec = 0;
            if (data == 103)
            {
                intrec = 1;
                return intrec;
            }
            else
            {
                if (cmode.Equals('h'))
                {
                    sp.Write("h");
                }
                if (cmode.Equals('o'))
                {
                    sp.Write("o");
                }
                return intrec;
            }
        }
        public void hypbutFunc()
        {
            sp.Write("h");
            int datarec = sp.ReadChar();
            intrecflag = GetRecString(datarec, 'h');
            while (intrecflag == 0)
            {
                try
                {
                    datarec = sp.ReadChar();
                    hypbut.Enabled = false;
                    normbut.Enabled = false;
                    intrecflag = GetRecString(datarec, 'h');
                }
                catch (ArithmeticException) { }
            } 
            hypbut.Enabled = false;
            normbut.Enabled = true;
            labelmode.ForeColor = Color.Red;
            labelmode.Text = "Mode: Hypoxia";
            modeflag = "1";
            recflag = 0;
            intrecflag = 0;
        }
        public void normbutFunc()
            {
                sp.Write("o");
                int datarec = sp.ReadChar();
                intrecflag = GetRecString(datarec, 'o');
                while (intrecflag == 0)
                {
                    try
                    {
                        datarec = sp.ReadChar();
                        hypbut.Enabled = false;
                        normbut.Enabled = false;
                        intrecflag = GetRecString(datarec,'o');

                    }
                    catch (ArithmeticException) { }
                }

                hypbut.Enabled = true;
                normbut.Enabled = false;
                labelmode.ForeColor = Color.Blue;
                labelmode.Text = "Mode: Normoxia";
                modeflag = "0";
                recflag = 0;
                intrecflag = 0;
            }
//Loading and closing the form
        private void Form1_Load(object sender, EventArgs e)
        {
            this.FormClosing += new FormClosingEventHandler(Form1_FormClosing);
        }
        private void Form1_FormClosing(object sender, EventArgs e)
        {
            try
            {
                sp.Write("o");
            }
             catch(InvalidOperationException)
            { }
            datasaving();
            Environment.Exit(0);
        }
//Click Events 
        public void begin_Click(object sender, EventArgs e)
        {
            try
            {
                begin.Enabled = false;
                SerialPortProgram();
                modeSwitch.Enabled = true;  
            }
            catch (IOException)
            {
                MessageBox.Show("Nothing Connected");
                begin.Enabled = true;
            }
            catch(ArgumentException)
            {
                MessageBox.Show("Please select a COM Port");
                begin.Enabled = true;
            }
        }
        private void comRefresh_Click(object sender, EventArgs e)
        {
            GetAvailablePorts();
        }
        private void gainbut_Click(object sender, EventArgs e)
        {
            if (SpGain.Visible == true)
            {
                SpGain.Visible = false;
                pulseGain.Visible = false;
                iO2Gain.Visible = false;
            }
            else
            {
                SpGain.Visible = true;
                pulseGain.Visible = true;
                iO2Gain.Visible = true;
            }
        }
        private void modeSwitch_Click(object sender, EventArgs e)
        {
                if (modeflag == "0")
                {
                    hypbutFunc();
                }
                else if (modeflag == "1")
                {
                    normbutFunc();
                }
            saveTime = stopwatch.Elapsed;
        }
        private void button1_Click(object sender, EventArgs e)
        {
            this.Close();
        }
//The next few lines deal with switching modes and adding/changing subject ID
        private void SubID_Enter(object sender, EventArgs e)
        {
            editflag = 1; 
        }
        private void SubID_Leave(object sender, EventArgs e)
        {
            editflag = 0;
        }
        private void Form1_Click(object sender, EventArgs e)
        {
            modeSwitch.Focus();   
        }
        private void SubID_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Space)
            {
                if (modeSwitch.Enabled == true)
                {
                    modeSwitch.PerformClick();
                }
                e.Handled = e.KeyChar == ' ';
            }
        }
        private void Form1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (editflag == 0)
            {
                if (e.KeyChar == (char)Keys.Space)
                {

                    if (hypbut.Enabled == true)
                    {
                        hypbut.PerformClick();
                    }
                    else if (normbut.Enabled == true)
                    {
                        normbut.PerformClick();
                    }
                    if (modeSwitch.Enabled == true)
                    {
                        modeSwitch.PerformClick();
                    }

                }
                e.Handled = true;
            }
        }
//Saving
        public void datasaving()
        {
            var lstNew2 = termslist.ConvertAll<string>(delegate (double j) { return j.ToString(); });
            var lstNewPr = prlist.ConvertAll<string>(delegate (double j) { return j.ToString(); });
            var lstNewIn = inlist.ConvertAll<string>(delegate (double j) { return j.ToString(); });

            var savlist = timelist.ZipFive(lstNew2, lstNewPr, lstNewIn, modelist, (s, k, p, i, m) => new { time = s, FiO2 = k, Pulse = p, InO2 = i, Mode = m }).ToList();

            DateTime dt = DateTime.Now;
            string datfil = SubID.Text + "_" + dt.ToString("yyyyMMddHHmmss") + ".csv";
            var fileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), datfil);
            using (var writer = new StreamWriter(fileName))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(savlist);
            }
        }
    }
//This zips the file when saving
    public static class MyFunkyExtensions
    {
        public static IEnumerable<TResult> ZipFive<T1, T2, T3, T4, T5, TResult>(
        this IEnumerable<T1> source,
        IEnumerable<T2> second,
        IEnumerable<T3> third,
        IEnumerable<T4> fourth,
        IEnumerable<T5> fifth,
        Func<T1, T2, T3, T4, T5, TResult> func)
        {
            using (var e1 = source.GetEnumerator())
            using (var e2 = second.GetEnumerator())
            using (var e3 = third.GetEnumerator())
            using (var e4 = fourth.GetEnumerator())
            using (var e5 = fifth.GetEnumerator())
            {
                while (e1.MoveNext() && e2.MoveNext() && e3.MoveNext() && e4.MoveNext() && e5.MoveNext())
                    yield return func(e1.Current, e2.Current, e3.Current, e4.Current, e5.Current);
            }
        }
    }
}

