using System;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Management;
using System.Windows;
using System.Threading.Tasks;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        // set up some global variables to be used later
        Boolean cursor = true;
        string selectedType;
        int frequency;
        int amplitude;
        SerialPort port;
        String getValue;
        int counter = 0;
        Thread masterthread;
        Boolean WaveGenOn = false;
        Boolean multimeterOn = true;
        String mode;

        float coordinate1 = 0;
        float coordinate2 = 0;
        float time = 0;
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // create thread to take inputs
            masterthread = new Thread(runit);

            // make labels invisible until needed 
            line_1_label.Visible = false;
            line_2_label.Visible = false;
            cursor_1.Visible = false;
            cursor_2.Visible = false;
            // chart2.ChartAreas[0].AxisX.LabelStyle.Enabled = false;
            chart2.ChartAreas[0].AxisX.LabelStyle.Format = "0";

            //gather all ports and use correct port
            String defaultport = "COM5";
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Caption like '%(COM%'"))
            {
                var portnames = SerialPort.GetPortNames();
                var ports = searcher.Get().Cast<ManagementBaseObject>().ToList().Select(p => p["Caption"].ToString());

                var portList = portnames.Select(n => n + " - " + ports.FirstOrDefault(s => s.Contains(n))).ToList();

                foreach (string s in portList)
                {
                    if (s.Contains("USB Serial Port"))
                    {
                        defaultport = s.Substring(0,4);
                    }
                    comboBox8.Items.Add(s);
                }
            }
            port = new SerialPort(defaultport, 691200);
            port.Open();

            // start thread
            masterthread.Start();

            // add oscilloscope line 1
            chart1.Series.Add("Line1");
            chart1.Series["Line1"].ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Spline;
            chart1.Series["Line1"].Color = Color.Yellow;
            //chart1.Series["Line1"].Points.Add(0);

            // add oscilloscope line 2
            chart1.Series.Add("Line2");
            chart1.Series["Line2"].ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Spline;
            chart1.Series["Line2"].Color = Color.Red;

            // add default values for wave gen
            comboBox1.Text = "None";
            comboBox2.Text = "0";
            comboBox3.Text = "0";
            comboBox4.Text = "50";
            comboBox5.Text = "0";
            comboBox6.Text = "0";
            label3.Visible = false;
            comboBox4.Visible = false;
            plotSelectedOptions();

        }

        // computes linear interpolation of two nearest points
        DataPoint getClosestXPoint(Series s, double xval)
        {
            try
            {
                // get next previous value
                DataPoint pPrev = s.Points.Last(x => x.XValue <= xval);
                // get next value
                DataPoint pNext = s.Points.First(x => x.XValue >= xval);

                // interpolate
                double m = (pPrev.YValues[0] - pNext.YValues[0]) / (pPrev.XValue - pNext.XValue);
                double yval = xval*m + (pPrev.YValues[0]);

                return new DataPoint(xval, yval);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("failed to get mid point");
                return s.Points.First(x => x.XValue >= xval);
            }


        }


        private void chart1_MouseClick(object sender, MouseEventArgs e)
        {
            // convert to mouse event to check if left or right click
            MouseEventArgs me = (MouseEventArgs)e;
            if (OscilliscopeStartButton.Text == "Start")
            {
                if (true)
                {

                    try
                    {
                        // figure out which label and cursor to move
                        Label curr_label = line_1_label;
                        Label curr_cursor = cursor_1;

                        if (me.Button == System.Windows.Forms.MouseButtons.Right)
                        {
                            cursor_2.Visible = true;
                            line_2_label.Visible = true;
                            curr_label = line_2_label;
                            curr_cursor = cursor_2;
                        }
                        else
                        {
                            cursor_1.Visible = true;
                            line_1_label.Visible = true;
                        }

                        // Get x value from chart using pixel location
                        double line_1_x_value = chart1.ChartAreas[0].AxisX2.PixelPositionToValue(me.X);

                        // Add minimum value of chart
                        //line_1_x_value += chart1.ChartAreas[0].AxisX.Minimum;

                        // Find closest previousX
                        DataPoint x1_Point = getClosestXPoint(chart1.Series["Line1"], line_1_x_value);
                        double x1_val = Math.Round(x1_Point.XValue, 3);
                        double y1_val = Math.Round(x1_Point.YValues[0],3);

                        // Find associated line 2 value for x value
                        DataPoint x2_Point = getClosestXPoint(chart1.Series["Line2"], line_1_x_value);
                        double x2_val = Math.Round(x2_Point.XValue, 3);
                        double y2_val = Math.Round(x2_Point.YValues[0],3);

                        // Add label to mouse click
                        curr_label.Location = new System.Drawing.Point((int)me.X, (int)me.Y);
                        curr_cursor.Location = new System.Drawing.Point((int)me.X, 110);

                        // Add text to label 
                        line_1_x_value = Math.Round(line_1_x_value, 2);
                        curr_label.Text = curr_label.Text = String.Concat("x1: ", x1_val, "\ny1: " , y1_val, "\nx2: ", x2_val, "\ny2: ", y2_val);
                    }   
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.ToString());
                    }
                }
            }
            else
            {
                line_1_label.Visible = false;
                line_1_label.Visible = false;
            }
        }
        private void OscilliscopeStartButton_Click(object sender, EventArgs e)
        {
            if (OscilliscopeStartButton.Text == "Start")
            {
                chart1.Series["Line1"].Points.Clear();
                chart1.Series["Line2"].Points.Clear();
                OscilliscopeStartButton.Text = "Stop";
                port.WriteLine("O");

            }
            else
            {
                OscilliscopeStartButton.Text = "Start";
                port.WriteLine("P");
            }
        }
        private async void runit()
        {
            while (true)    
            {
                // read input continuously
                getValue = port.ReadLine();
                if ((getValue != null) && (getValue.Contains(",")) && (OscilliscopeStartButton.Text.Equals("Stop")))
                {
                    String[] args = getValue.Split(',');
                    coordinate1 = float.Parse(args[0]);
                    coordinate2 = float.Parse(args[1]);
                    time += float.Parse(args[2])*50;

                    // if it is a number plot it
                    chart1.Invoke((MethodInvoker)(() => chart1.Series["Line1"].Points.AddXY(time, coordinate1)));
                    chart1.Invoke((MethodInvoker)(() => chart1.Series["Line2"].Points.AddXY(time, coordinate2)));
                    if (chart1.Series["Line1"].Points.Count > 50)
                    {
                        chart1.Invoke((MethodInvoker)(() => chart1.Series["Line1"].Points.RemoveAt(0)));
                        chart1.Invoke((MethodInvoker)(() => chart1.Series["Line2"].Points.RemoveAt(0)));
                        //chart1.Invoke((MethodInvoker)(() => chart1.ChartAreas[0].AxisX.Minimum = Convert.ToInt32(chart1.Series["Line1"].Points[0].XValue)));
                        //Debug.WriteLine(Convert.ToInt32(chart1.Series["Line1"].Points[0].XValue));
                    }
                    counter++;
                    if (counter % 20 == 0)
                    {
                        port.WriteLine("O");
                    }

                }
                
                // otherwise check for any other expected behavior
                else if (!string.IsNullOrEmpty(getValue) && getValue.Substring(0,1).Equals("C") && multimeterOn && mode.Equals("Voltage (Volts)"))
                {
                    float resistance = float.Parse(getValue.Substring(1));
                    if (resistance < 0)
                    {
                        label10.Invoke((MethodInvoker)(() => label10.Text = "open"));
                    }
                    else
                    {
                        label10.Invoke((MethodInvoker)(() => label10.Text = getValue.Substring(1, 5) + " Volts"));
                    }
                    await Task.Delay(500);
                    port.WriteLine("Q");
                }
                else if (!string.IsNullOrEmpty(getValue) && getValue.Substring(0, 1).Equals("D") && multimeterOn && mode.Equals("Current (Ampere)"))
                {
                    float resistance = float.Parse(getValue.Substring(1))-5;
                   
                    label10.Invoke((MethodInvoker)(() => label10.Text = resistance + " mA"));
                    await Task.Delay(500);
                    port.WriteLine("R");
                }
                else if (!string.IsNullOrEmpty(getValue) && getValue.Substring(0, 1).Equals("E") && multimeterOn && mode.Equals("Resistance (Ohms)"))
                {
                    float resistance = float.Parse(getValue.Substring(1));
                    if (resistance < 0)
                    {
                        label10.Invoke((MethodInvoker)(() => label10.Text = "open"));
                    }
                    else
                    {
                        label10.Invoke((MethodInvoker)(() => label10.Text = getValue.Substring(1) + " \u2126"));
                    }
                    await Task.Delay(500);
                    port.WriteLine("S");
                }
                else if (!string.IsNullOrEmpty(getValue) && getValue.Substring(0, 1).Equals("F") && multimeterOn && mode.Equals("Inductance (Henry)"))
                {
                    label10.Invoke((MethodInvoker)(() => label10.Text = getValue.Substring(1) + " Henry"));
                }
                else if (!string.IsNullOrEmpty(getValue) && getValue.Substring(0, 1).Equals("G") && multimeterOn && mode.Equals("Capacitance (Farads)"))
                {
                    label10.Invoke((MethodInvoker)(() => label10.Text = getValue.Substring(1) + " Farad"));
                }
                else if (!string.IsNullOrEmpty(getValue) && getValue.Contains("PIC32MZ 403 Demo"))
                {
                    label10.Invoke((MethodInvoker)(() => label10.Text = "Recieving PIC 32"));
                }
            }
        }
        private void chart1_MouseWheel(object sender, MouseEventArgs e)
        {
            // scroll functionality
            if (e.Delta > 0)
            {

                Axis ax = chart1.ChartAreas[0].AxisX;
                ax.ScaleView.Size = double.IsNaN(ax.ScaleView.Size) ?
                                    (ax.Maximum - ax.Minimum) / 2 : ax.ScaleView.Size /= 2;
                Axis ay = chart1.ChartAreas[0].AxisY;
                ay.ScaleView.Size = double.IsNaN(ay.ScaleView.Size) ?
                                    (ay.Maximum - ay.Minimum) / 2 : ay.ScaleView.Size /= 2;
            }
            else
            {

                Axis ax = chart1.ChartAreas[0].AxisX;
                ax.ScaleView.Size = double.IsNaN(ax.ScaleView.Size) ?
                                    ax.Maximum : ax.ScaleView.Size *= 2;
                if (ax.ScaleView.Size > ax.Maximum - ax.Minimum)
                {
                    ax.ScaleView.Size = ax.Maximum;
                    ax.ScaleView.Position = 0;
                }
                Axis ay = chart1.ChartAreas[0].AxisY;
                ay.ScaleView.Size = double.IsNaN(ay.ScaleView.Size) ?
                                    ay.Maximum : ay.ScaleView.Size *= 2;
                if (ay.ScaleView.Size > ay.Maximum - ay.Minimum)
                {
                    ay.ScaleView.Size = ay.Maximum;
                    ay.ScaleView.Position = 0;
                }
            }
        }
        // plot graph on any option changed
        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

            if (comboBox1.Text.Contains("Sine"))
            {
                label3.Visible = false;
                comboBox4.Visible = false;
            }
            else if (comboBox1.Text.Contains("None"))
            {
                label3.Visible = false;
                comboBox4.Visible = false;
            }
            else
            {
                label3.Visible = true;
                comboBox4.Visible = true;
            }
            plotSelectedOptions();
        }
        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            plotSelectedOptions();
        }
        private void comboBox3_SelectedIndexChanged(object sender, EventArgs e)
        {
            plotSelectedOptions();
        }
        private void comboBox4_SelectedIndexChanged(object sender, EventArgs e)
        {
            plotSelectedOptions();
        }
        // fucntion to plot graph
        private void plotSelectedOptions()
        {
            if(string.IsNullOrEmpty(comboBox1.Text) || string.IsNullOrEmpty(comboBox2.Text) || string.IsNullOrEmpty(comboBox3.Text) || string.IsNullOrEmpty(comboBox4.Text))
            {
                return;
            }
            else
            {
                // get variables
                selectedType = (string)comboBox1.Text;
                frequency = Convert.ToInt32(comboBox2.Text);
                amplitude = Convert.ToInt32(comboBox3.Text);

                // plot for each type
                if (string.Equals(selectedType, "Sine"))
                {
                    label6.Text = "Frequency";
                    chart2.Series[0].Points.Clear();
                    for (double i = 0; i < 6.27; i += 0.01)
                    {
                        chart2.Series[0].Points.AddXY(i, (amplitude*Math.Sin(i)));
                    }
                }
                else if (string.Equals(selectedType, "DC"))
                {
                    chart2.Series[0].Points.Clear();
                    for (double i = 0; i < 6.27; i += 0.01)
                    {
                        chart2.Series[0].Points.AddXY(i, amplitude);
                    }
                }
                else if (string.Equals(selectedType, "Square"))
                {
                    chart2.Series[0].Points.Clear();
                    for (double i = 0; i < 2.09; i += 0.01)
                    {
                        chart2.Series[0].Points.AddXY(i, amplitude);
                    }
                    for (double i = 2.09; i < 4.18; i += 0.01)
                    {
                        chart2.Series[0].Points.AddXY(i, 0.001);
                    }
                    for (double i = 4.18; i < 6.27; i += 0.01)
                    {
                        chart2.Series[0].Points.AddXY(i, amplitude);
                    }
                }
                else if (string.Equals(selectedType, "Triangle"))
                {
                    chart2.Series[0].Points.Clear();
                    for (double i = 0; i < 2.09; i += 0.01)
                    {
                        chart2.Series[0].Points.AddXY(i, (amplitude*i));
                    }
                    double j = 2.09;
                    for (double i = 2.09; i < 4.18; i += 0.01)
                    {
                        chart2.Series[0].Points.AddXY(i, (amplitude*j));
                        j -= .01;
                    }
                    for (double i = 4.18; i < 6.27; i += 0.01)
                    {
                        chart2.Series[0].Points.AddXY(i, (amplitude*(i - (2.09 * 2))));
                    }
                }
                
            }
        }

        private void comboBox7_SelectedIndexChanged(object sender, EventArgs e)
        {
            dealWithMultimeterStuff();
        }

        private void label4_Click(object sender, EventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e)
        {
            port.WriteLine("SPI");
        }

        private void button1_Click(object sender, EventArgs e)
        {
            WaveGenOn = true;
            startWavegen();

        }

        private void button2_Click_1(object sender, EventArgs e)
        {
            WaveGenOn = false;
            port.WriteLine("A");
            port.WriteLine("C0");
            port.WriteLine("D0");
        }

        private void comboBox5_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void button3_Click(object sender, EventArgs e)
        {
            port.WriteLine("F");
            port.WriteLine("L0x32");
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            port.WriteLine("A");
            port.WriteLine("C0");
            port.WriteLine("D0");
        }
        private void startWavegen()
        {
            if (WaveGenOn)
            {
                int amplitude = 0;
                int duty = (int)((float.Parse(comboBox4.Text) / 100.0) * 127);
                if (comboBox1.SelectedItem.Equals("Sine"))
                {
                    port.WriteLine("F");
                    Debug.WriteLine("F");
                    amplitude = (int)((float.Parse(comboBox3.Text) / 3.255) * 127);
                }
                else if (comboBox1.SelectedItem.Equals("Square"))
                {
                    port.WriteLine("G");
                    amplitude = (int)((float.Parse(comboBox3.Text) / 15.3) * 127);
                }
                else if (comboBox1.SelectedItem.Equals("Triangle"))
                {
                    port.WriteLine("H");
                    amplitude = (int)((float.Parse(comboBox3.Text) / 4.84) * 127);
                }
                else
                {
                    port.WriteLine("A");
                }

                // Frequency
                int freq = int.Parse(comboBox2.Text);
                if (freq > 200)
                {
                    port.WriteLine("J3");
                    freq = (int)((freq / 2012.49) * 127);
                }
                else
                {
                    port.WriteLine("J4");
                    freq = (int)((freq / 250.32) * 127);
                }
                port.WriteLine("K" + Convert.ToString(freq, 16));

                // Amplitude
                port.WriteLine("L0x" + Convert.ToString(amplitude, 16));

                //Duty Cycle
                if (!comboBox1.SelectedItem.Equals("Sine"))
                {
                    port.WriteLine("M0x" + Convert.ToString(duty, 16));
                    Debug.WriteLine("M0x" + Convert.ToString(duty, 16));
                }
                else
                {
                    port.WriteLine("M0x3F");
                }

                port.WriteLine("C" + comboBox5.Text);
                port.WriteLine("D" + comboBox6.Text);
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            dealWithMultimeterStuff();
        }
        public void dealWithMultimeterStuff()
        {
            // send multimeter information to microcontroller
            if (comboBox7.SelectedItem.Equals("Voltage (Volts)"))
            {
                mode = "Voltage (Volts)";
                port.WriteLine("Q");
                startWavegen();
                button4.Visible = false;
            }
            if (comboBox7.SelectedItem.Equals("Current (Ampere)"))
            {
                mode = "Current (Ampere)";
                port.WriteLine("R");
                startWavegen();
                button4.Visible = false;
            }
            if (comboBox7.SelectedItem.Equals("Resistance (Ohms)"))
            {
                mode = "Resistance (Ohms)";
                port.WriteLine("S");
                startWavegen();
                button4.Visible = false;
            }
            if (comboBox7.SelectedItem.Equals("Inductance (Henry)"))
            {
                mode = "Inductance (Henry)";
                port.WriteLine("T");
                button4.Visible = true;

            }
            if (comboBox7.SelectedItem.Equals("Capacitance (Farads)"))
            {
                mode = "Capacitance (Farads)";
                port.WriteLine("U");
                button4.Visible = true;
            }
        }
        private void button5_Click(object sender, EventArgs e)
        {
            if (multimeterOn)
            {
                button5.Text = "Resume";
            }
            else
            {
                button5.Text = "Pause";
                dealWithMultimeterStuff();
            }
            multimeterOn = !multimeterOn;
        }
    }
}