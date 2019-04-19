using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Newtonsoft.Json;
using ParserNII.DataStructures;
using ZedGraph;

namespace ParserNII
{
    public partial class Form1 : Form
    {
        private List<TextBox> textBoxes;
        private List<CheckBox> checkBoxes;
        private Dictionary<string, TextBox> uidNames;
        private List<Panel> panels;
        private Dictionary<string, int> DisplayedParamNames;
        private List<ZedGraphControl.PointValueHandler> pointEventHandlers = new List<ZedGraphControl.PointValueHandler>();
        private LineObj verticalLine;
        private Drawer drawer;

        public Form1()
        {
            InitializeComponent();
            button1.Visible = false;
            zedGraphControl1.Visible = false;
        }

        private void открытьToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "dat files (*.dat)|*.dat|All files (*.*)|*.*";
            ofd.FilterIndex = 2;
            ofd.RestoreDirectory = true;
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                string filename = ofd.FileName;
                if (filename.EndsWith("gzdat", true, null) || filename.EndsWith("gzbin", true, null))
                    filename = GZip.Decompress(ofd.FileName);
                Stream stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                byte[] fileBytes = new byte[stream.Length];
                stream.Read(fileBytes, 0, (int)stream.Length);
                Размер.Text = (stream.Length / 1024).ToString() + " Кб";
                var parser = Path.GetExtension(ofd.FileName) == ".dat" ? (Parser)new DatFileParser() : new BinFileParser();
                List<DataFile> result = parser.Parse(fileBytes);

                foreach (var pointEventHandler in pointEventHandlers)
                {
                    zedGraphControl1.PointValueEvent -= pointEventHandler;
                }

                pointEventHandlers = new List<ZedGraphControl.PointValueHandler>();

                List<XDate> xValues;
                if (Path.GetExtension(ofd.FileName) == ".dat")
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

                DisplayPanelElements(result[0]);
                drawer = new Drawer(zedGraphControl1);

                var keys = result[0].Data.Keys.Where(k => result[0].Data[k].Display).ToArray();

                for (int i = 0; i < keys.Length; i++)
                {
                    drawer.DrawGraph(xValues,
                        arrayResult.Data[keys[i]].Select(d => d.ChartValue).ToList(),
                        keys[i],
                        Drawer.GetColor(i));

                    panels[DisplayedParamNames[keys[i]]].BackColor = Drawer.GetColor(i);
                }

                verticalLine = drawer.CrateVerticalLine();

                zedGraphControl1.IsShowPointValues = true;

                pointEventHandlers.Add((pointSender, graphPane, curve, pt) =>
                {
                    for (int i = 0; i < keys.Length; i++)
                    {
                        uidNames[keys[i]].Text = result[pt].Data[keys[i]].DisplayValue;
                    }

                    verticalLine.Location.X = xValues[pt];
                    verticalLine.Location.X1 = xValues[pt];
                    zedGraphControl1.Refresh();
                    return "";
                });

                zedGraphControl1.PointValueEvent += pointEventHandlers.Last();


                drawer.Refresh();

                stream.Close();
            }
        }

        private void DisplayPanelElements(DataFile data)
        {
            panel3.Controls.Clear();
            DisplayedParamNames = new Dictionary<string, int>();
            textBoxes = new List<TextBox>();
            checkBoxes = new List<CheckBox>();
            panels = new List<Panel>();
            uidNames = new Dictionary<string, TextBox>();
            var keys = data.Data.Keys.Where(k => data.Data[k].Display).ToArray();

            for (int i = 0; i < keys.Length; i++)
            {
                DisplayedParamNames.Add(keys[i], i);

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
                String s1 = data.Data[keys[i]].DataParams.measure;
                String s2 = "";
                if (s1.Equals(s2))
                    checkBox.Text = keys[i];
                else
                    checkBox.Text = keys[i] + ", " + s1;
                checkBox.UseVisualStyleBackColor = true;
                checkBoxes.Add(checkBox);
                checkBox.Checked = true;
                int index = i;

                checkBox.CheckedChanged += (object sender, EventArgs e) =>
                {
                    if (zedGraphControl1.GraphPane.CurveList[index].IsVisible != checkBox.Checked)
                    {
                        zedGraphControl1.GraphPane.CurveList[index].IsVisible = checkBox.Checked;

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
                };

                var panel = new Panel();
                panel3.Controls.Add(panel);
                panel.Location = new Point(70, 6 + i * 26);
                panel.Name = $"panel{i}";
                panel.Size = new Size(17, 17);
                panel.TabIndex = 0;
                panels.Add(panel);

                uidNames.Add(keys[i], textBoxes[i]);
            }

            button1.Visible = true;
            zedGraphControl1.Visible = true;
        }        

        private void button1_Click(object sender, EventArgs e)
        {
            zedGraphControl1.GraphPane.CurveList.ForEach(c => c.IsVisible = false);

            zedGraphControl1.Refresh();

            checkBoxes.ForEach(cb => cb.Checked = false);
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
            GraphPane pane = zedGraphControl1.GraphPane;
            zedGraphControl1.GraphPane.ReverseTransform(e.Location, out var x, out var y);
            verticalLine.Location.X = x;  
            zedGraphControl1.Refresh();
            drawer.Refresh();
        }
    }
}
