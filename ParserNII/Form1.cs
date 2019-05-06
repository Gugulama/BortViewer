using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ParserNII.DataStructures;
using ZedGraph;

namespace ParserNII
{
    public partial class Form1 : Form
    {
        private List<TextBox> textBoxes;
        private Dictionary<string, CheckBox> checkBoxes;
        private Dictionary<string, Panel> panels;
        private Dictionary<string, TextBox> uidNames;
        private LineObj verticalLine;
        private Drawer drawer;
        private Dictionary<string, int> LineIndexs;
        private readonly Dictionary<int, ConfigElement> binFileParams = Config.Instance.binFileParams.ToDictionary(d => d.number);
        private List<DataFile> result;
        private bool[] settings;
        private PointF mouseLocation;
        private bool isFirstOpen;

        public Form1()
        {
            InitializeComponent();
            drawer = new Drawer(zedGraphControl1);            
            FormClosing += Form1_FormClosing;
            DisplayPanelElements();
            zedGraphControl1.Enabled = false;
            isFirstOpen = true;
        }

        private void открытьToolStripMenuItem_Click(object sender, EventArgs e)
        {            
            OpenFileDialog ofd = new OpenFileDialog();  
            ofd.Filter = "dat files (*.dat)|*.dat|" +
                "gzdat files (*.gzdat)|*.gzdat|" +
                "bin files (*.bin)|*.bin|" +
                "gzbin files (*.gzbin)|*.gzbin|" +
                "All files (*.*)|*.*";
            ofd.FilterIndex = 5;
            ofd.RestoreDirectory = true;

            if (!isFirstOpen) settings = checkBoxes.Select(c => c.Value.Checked).ToArray();

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                isFirstOpen = false;
                RefreshPanelElements();                
                drawer.Clear();
                zedGraphControl1.Enabled = true;
                verticalLine = drawer.CrateVerticalLine();
                string filename = ofd.FileName;
                if (filename.EndsWith("gzdat", true, null) || filename.EndsWith("gzbin", true, null))
                    filename = GZip.Decompress(ofd.FileName);
                Stream stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                byte[] fileBytes = new byte[stream.Length];
                stream.Read(fileBytes, 0, (int)stream.Length);
                Размер.Text = (stream.Length / 1024).ToString() + " Кб";
                var parser = (Path.GetExtension(ofd.FileName) == ".dat" || Path.GetExtension(ofd.FileName) == ".gzdat") ? (Parser)new DatFileParser() : new BinFileParser();
                result = parser.Parse(fileBytes);

                List<XDate> xValues;
                if (Path.GetExtension(ofd.FileName) == ".gzdat" || Path.GetExtension(ofd.FileName) == ".dat")
                {
                    xValues = result.Select(r => new XDate(DateTimeOffset.FromUnixTimeSeconds((uint)r.Data["Время в “UNIX” формате"].OriginalValue).AddHours(3).DateTime)).ToList();

                    Тип.Text = TrainNames.NamesDictionary[(byte)result[0].Data["Тип локомотива"].OriginalValue];
                    Номер.Text = result[0].Data["№ тепловоза"].DisplayValue;
                    Секция.Text = result[0].Data["Секция локомотива"].DisplayValue;
                }
                else
                {
                    xValues = result.Select(r => new XDate(DateTimeOffset.FromUnixTimeMilliseconds((long)r.Data["Время в “UNIX” формате"].OriginalValue).AddHours(3).DateTime)).ToList();
                    string[] nameParams = Path.GetFileName(ofd.FileName).Split('_', '-');
                    Тип.Text = nameParams[2];
                    Номер.Text = nameParams[3];
                    Секция.Text = nameParams[4];
                }

                Время.Text = result.First().Data["Время в “UNIX” формате"].DisplayValue + " - " + result.Last().Data["Время в “UNIX” формате"].DisplayValue;

                var arrayResult = parser.ToArray(result);


                LineIndexs = new Dictionary<string, int>();
                int i = 0;
                foreach (var binFileParam in binFileParams)
                {
                    var checkBox = checkBoxes[binFileParam.Value.name];
                    var textBox = textBoxes[i];
                    if (arrayResult.Data.ContainsKey(binFileParam.Value.name))
                    {
                        drawer.DrawGraph(xValues,
                            arrayResult.Data[binFileParam.Value.name].Select(d => d.ChartValue).ToList(),
                            binFileParam.Value.name,
                            Drawer.GetColor(i));
                        zedGraphControl1.GraphPane.CurveList.Last().IsVisible = false;

                        LineIndexs.Add(binFileParam.Value.name, zedGraphControl1.GraphPane.CurveList.Count - 1);

                        checkBox.CheckedChanged += (object otherSender, EventArgs eventArgs) =>
                            {
                                try
                                {
                                    if (zedGraphControl1.GraphPane.CurveList[LineIndexs[binFileParam.Value.name]].IsVisible != checkBox.Checked)
                                    {
                                        zedGraphControl1.GraphPane.CurveList[LineIndexs[binFileParam.Value.name]].IsVisible = checkBox.Checked;

                                        if (checkBox.Checked)
                                        {
                                            zedGraphControl1.AxisChange();
                                            zedGraphControl1.Refresh();
                                        }
                                        else
                                        {
                                            zedGraphControl1.Refresh();
                                        }
                                    }
                                }
                                catch (KeyNotFoundException ex)
                                { }
                                                               
                            };
                    }
                    else
                    {
                        checkBox.Enabled = false;
                        textBox.Enabled = false;
                    }                  

                    i++;
                }

                drawer.Refresh();
                RefreshChecks();
                stream.Close();
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            string dataValues = JsonConvert.SerializeObject(checkBoxes.Select(c => c.Value.Checked).ToArray());
            File.WriteAllText("./settings.json", dataValues);
        }

        private void RefreshChecks()
        {
            int i = 0;
            foreach (var binFileParam in binFileParams)
            {
                var checkBox = checkBoxes[binFileParam.Value.name];
                checkBox.Checked = false;
                if (checkBox.Enabled && settings[i]) checkBox.Checked = true;                
                i++;
            }
        }

        private void RefreshPanelElements()
        {
            int i = 0;
            foreach (var binFileParam in binFileParams)
            {
                var checkBox = checkBoxes[binFileParam.Value.name];
                var textBox = textBoxes[i];
                checkBox.Enabled = true;
                textBox.Enabled = true;
                textBox.Clear();
                i++;
            }
        }

        private void DisplayPanelElements()
        {
            panel3.Controls.Clear();
            textBoxes = new List<TextBox>();
            checkBoxes = new Dictionary<string, CheckBox>();
            panels = new Dictionary<string, Panel>();
            uidNames = new Dictionary<string, TextBox>();

            int i = 0;
            foreach (var binFileParam in binFileParams)
            {
                var textBox = new TextBox();
                textBoxes.Add(textBox);
                panel3.Controls.Add(textBox);
                textBox.Location = new Point(2, 4 + i * 26);
                textBox.Name = $"textBox{i}";
                textBox.ReadOnly = true;
                textBox.Size = new Size(62, 20);
                textBox.TabIndex = 30 + i;

                var checkBox = new CheckBox();
                panel3.Controls.Add(checkBox);
                checkBox.AutoSize = true;
                checkBox.Location = new Point(90, 6 + i * 26);
                checkBox.Name = $"checkBox{i}";
                checkBox.Size = new Size(80, 17);
                checkBox.TabIndex = 0;
                string measure = binFileParam.Value.measure;
                if (string.IsNullOrEmpty(measure))
                {
                    checkBox.Text = binFileParam.Value.name;
                }
                else
                {
                    checkBox.Text = binFileParam.Value.name + ", " + measure;
                }
                checkBox.UseVisualStyleBackColor = true;
                checkBoxes.Add(binFileParam.Value.name, checkBox);
                checkBox.Checked = false;
                int index = i;

                var panel = new Panel();
                panel3.Controls.Add(panel);
                panel.Location = new Point(70, 6 + i * 26);
                panel.Name = $"panel{i}";
                panel.Size = new Size(17, 17);
                panel.TabIndex = 0;
                panels.Add(binFileParam.Value.name, panel);
                panel.BackColor = Drawer.GetColor(i);
                uidNames.Add(binFileParam.Value.name, textBoxes[i]);
                i++;
            }


            button1.Visible = true;
            zedGraphControl1.Visible = true;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            zedGraphControl1.GraphPane.CurveList.ForEach(c => c.IsVisible = false);

            zedGraphControl1.Refresh();

            foreach (var cb in checkBoxes)
            {
                cb.Value.Checked = false;
            }
        }

        private void zedGraphControl1_ZoomEvent(ZedGraphControl sender, ZoomState oldState, ZoomState newState)
        {
            GraphPane pane = sender.GraphPane;
            double origialXScale = drawer.xScaleMax - drawer.xScaleMin;
            double newXScale = pane.XAxis.Scale.Max - pane.XAxis.Scale.Min;

            double diff = origialXScale / newXScale;
            bool isXLess = (bool)((diff) > 1500);
            bool isXBigger = (bool)((diff) < 1);

            if (isXLess || isXBigger) sender.ZoomOut(sender.GraphPane);
        }

        private void zedGraphControl1_MouseMove(object sender, MouseEventArgs e)
        {
            //mouseLocation = e.Location;
            //timer1.Interval = 1;
            //timer1.Enabled = true;

            GraphPane pane = zedGraphControl1.GraphPane;
            zedGraphControl1.GraphPane.ReverseTransform(e.Location, out var x, out var y);            
            verticalLine.Location.X = x; 
            

            CurveItem curve = pane.CurveList[0];


            // Look for the min and max value
            double min = curve.Points[0].X;
            double max = curve.Points[curve.NPts - 1].X;

            // Prevent error if mouse is out of bounds.
            if (x > min && x < max)
            {
                int index = 0;
                for (int i = 0; i < curve.NPts; i++)
                {

                    if (x < curve.Points[i].X)
                    {
                        index = i - 1;
                        break;
                    }
                }
                foreach (var binFileParam in binFileParams)
                {
                    if (result[index].Data.ContainsKey(binFileParam.Value.name))
                    {
                        uidNames[binFileParam.Value.name].Text = result[index].Data[binFileParam.Value.name].DisplayValue;
                    }
                }
            }
            else
            {
                foreach (var binFileParam in binFileParams)
                {
                    uidNames[binFileParam.Value.name].Text = "";
                }
            }
            zedGraphControl1.Invalidate();
        }

        private void Timer1_Tick(object sender, EventArgs e)
        {

        }

        private void выходToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
          
                string settingsPath = "./settings.json";
                settings = JsonConvert.DeserializeObject<JArray>(File.ReadAllText(settingsPath)).ToObject<bool[]>();

        }
    }
}
