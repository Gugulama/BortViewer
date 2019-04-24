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
        private Dictionary<string, Panel> panels;
        private Dictionary<string, TextBox> uidNames;
        private List<ZedGraphControl.PointValueHandler> pointEventHandlers = new List<ZedGraphControl.PointValueHandler>();
        private LineObj verticalLine;
        private Drawer drawer;
        private Dictionary<string, int> LineIndexs;
        private readonly Dictionary<int, ConfigElement> binFileParams = Config.Instance.binFileParams.ToDictionary(d => d.number);
        private List<DataFile> result;

        public Form1()
        {
            InitializeComponent();
            button1.Visible = false;
            zedGraphControl1.Visible = false;
            FormClosing += Form1_FormClosing;
        }

        private void открытьToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "dat files (*.dat)|*.dat|bin files (*.bin)|*.bin";
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
                result = parser.Parse(fileBytes);

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

                drawer = new Drawer(zedGraphControl1);
                LineIndexs = new Dictionary<string, int>();
                int i = 0;
                foreach (var binFileParam in binFileParams)
                {
                    if (arrayResult.Data.ContainsKey(binFileParam.Value.name))
                    {
                        drawer.DrawGraph(xValues,
                            arrayResult.Data[binFileParam.Value.name].Select(d => d.ChartValue).ToList(),
                            binFileParam.Value.name,
                            Drawer.GetColor(i));

                        LineIndexs.Add(binFileParam.Value.name, zedGraphControl1.GraphPane.CurveList.Count - 1);
                    }

                    i++;
                }

                DisplayPanelElements(result[0]);

                verticalLine = drawer.CrateVerticalLine();

                zedGraphControl1.IsShowPointValues = true;

                pointEventHandlers.Add((pointSender, graphPane, curve, pt) =>
                {

                    //    foreach (var binFileParam in binFileParams)
                    //    {
                    //        if (result[pt].Data.ContainsKey(binFileParam.Value.name))
                    //        {
                    //            uidNames[binFileParam.Value.name].Text = result[pt].Data[binFileParam.Value.name].DisplayValue;
                    //        }
                    //    }

                    //    verticalLine.Location.X = xValues[pt];
                    //    verticalLine.Location.X1 = xValues[pt];
                    //    zedGraphControl1.Refresh();
                    return "";
                });

                zedGraphControl1.PointValueEvent += pointEventHandlers.Last();


                drawer.Refresh();

                stream.Close();
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            DialogResult result = MessageBox.Show("Сохранить изменения?", "Внимание", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (result == System.Windows.Forms.DialogResult.Yes)
            {
                string dataValues = Newtonsoft.Json.JsonConvert.SerializeObject(checkBoxes.Select(c => c.Checked).ToArray());
                File.WriteAllText("./settings.json", dataValues);
            }
            else if (result == System.Windows.Forms.DialogResult.No)
            {
                FormClosing -= Form1_FormClosing;
                Close();
            }
            else e.Cancel = true;
        }

        private void DisplayPanelElements(DataFile data)
        {
            panel3.Controls.Clear();
            textBoxes = new List<TextBox>();
            checkBoxes = new List<CheckBox>();
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
                checkBoxes.Add(checkBox);
                checkBox.Checked = true;
                int index = i;

                if (LineIndexs.ContainsKey(binFileParam.Value.name))
                {


                    checkBox.CheckedChanged += (object sender, EventArgs e) =>
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
                    };
                }
                else
                {
                    checkBox.Enabled = false;
                }




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

            CurveItem curve = pane.CurveList[0];

            // Look for the min and max value
            double min = curve.Points[0].X;
            double max = curve.Points[curve.NPts - 1].X;

            // Prevent error if mouse is out of bounds.
            if (x <= min) return;
            if (x >= max) return;

            double delta = (max - min) / (curve.NPts - 1);
            int index = (int)Math.Round((x - min) / delta, 0);

            foreach (var binFileParam in binFileParams)
            {
                if (result[index].Data.ContainsKey(binFileParam.Value.name))
                {
                    uidNames[binFileParam.Value.name].Text = result[index].Data[binFileParam.Value.name].DisplayValue;
                }
            }

            zedGraphControl1.Invalidate();
            //zedGraphControl1.Refresh();
            //drawer.Refresh();
            //this.Refresh();
        }
    }
}
