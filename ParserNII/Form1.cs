using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using ParserNII.DataStructures;
using ZedGraph;
using CefSharp;
using CefSharp.WinForms;

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
        private Dictionary<string, int> LineIndexes;
        private readonly Dictionary<int, ConfigElement> binFileParams = Config.Instance.binFileParams.ToDictionary(b => b.number);
        private readonly Dictionary<string, ConfigElement> datFileParams = Config.Instance.datFileParams.ToDictionary(d => d.name);
        private List<DataFile> result;
        private DataArrays arrayResult;
        private bool[] settings;
        private bool isFirstOpen;
        private bool isDatFile;
        private bool isAllowMouseMove;
        private readonly string mapUrl = String.Format("file:///{0}/index.html?", Directory.GetCurrentDirectory());
        private string latitude = "";
        private string longitude = "";
        private ChromiumWebBrowser browser;

        public Form1()
        {
            InitializeComponent();
            BinDisplayPanel();
            button1.Visible = true;
            zedGraphControl1.Visible = true;
            drawer = new Drawer(zedGraphControl1);
            FormClosing += Form1_FormClosing;
            zedGraphControl1.Enabled = false;
            isFirstOpen = true;
            var keys = datFileParams.Keys.ToArray();
            for (int i = 0; i < datFileParams.Count; i++)
            {
                if (i < 6)
                {
                    datFileParams.Remove(keys[i]);
                }
            }
            CefSettings cefSettings = new CefSettings();
            Cef.Initialize(cefSettings);
            browser = new ChromiumWebBrowser(mapUrl);
            groupBox3.Controls.Add(browser);
            browser.Dock = DockStyle.Fill;
            isAllowMouseMove = true;
            checkBox1.Checked = isAllowMouseMove;
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
            int i = 0;

            if (!isFirstOpen)
            {
                MakeSettings();
            }

            if (ofd.ShowDialog() == DialogResult.OK)
            {                
                if (Path.GetExtension(ofd.FileName) == ".gzdat" || Path.GetExtension(ofd.FileName) == ".dat")
                {
                    isDatFile = true;
                    //displaypanelElements
                    DatDisplayPanel();
                    RefreshDat();
                }
                else if (Path.GetExtension(ofd.FileName) == ".gzbin" || Path.GetExtension(ofd.FileName) == ".bin")
                {
                    isDatFile = false;
                    //displaypanelElements
                    BinDisplayPanel();
                    RefreshBin();
                }
                else
                {
                    MessageBox.Show("Выбран некорректный тип файла", "Внимание", MessageBoxButtons.OK);
                    открытьToolStripMenuItem.PerformClick();
                    return;
                }

                Enabled = false;
                isFirstOpen = false;
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

                    type.Text = TrainNames.NamesDictionary[(byte)result[0].Data["Тип локомотива"].OriginalValue];
                    number.Text = result[0].Data["№ тепловоза"].DisplayValue;
                    section.Text = result[0].Data["Секция локомотива"].DisplayValue;
                }
                else
                {
                    xValues = result.Select(r => new XDate(DateTimeOffset.FromUnixTimeMilliseconds((long)r.Data["Время в “UNIX” формате"].OriginalValue).AddHours(3).DateTime)).ToList();
                    string[] nameParams = Path.GetFileName(ofd.FileName).Split('_', '-');
                    type.Text = nameParams[2];
                    number.Text = nameParams[3];
                    section.Text = nameParams[4];
                }

                ВремяLabel.Text = result.First().Data["Время в “UNIX” формате"].DisplayValue + " - " + result.Last().Data["Время в “UNIX” формате"].DisplayValue;

                arrayResult = parser.ToArray(result);

                LineIndexes = new Dictionary<string, int>();
                i = 0;

                if (Path.GetExtension(ofd.FileName) == ".gzdat" || Path.GetExtension(ofd.FileName) == ".dat")
                    foreach (var datFileParam in datFileParams)
                    {
                        var checkBox = checkBoxes[datFileParam.Value.name];
                        var textBox = textBoxes[i];
                        if (arrayResult.Data.ContainsKey(datFileParam.Value.name))
                        {
                            drawer.DrawGraph(xValues,
                                arrayResult.Data[datFileParam.Value.name].Select(d => d.ChartValue).ToList(),
                                datFileParam.Value.name,
                                Drawer.GetColor(i));
                            zedGraphControl1.GraphPane.CurveList.Last().IsVisible = false;

                            LineIndexes.Add(datFileParam.Value.name, zedGraphControl1.GraphPane.CurveList.Count - 1);

                            checkBox.CheckedChanged += (object otherSender, EventArgs eventArgs) =>
                            {
                                try
                                {
                                    if (zedGraphControl1.GraphPane.CurveList[LineIndexes[datFileParam.Value.name]].IsVisible != checkBox.Checked)
                                    {
                                        zedGraphControl1.GraphPane.CurveList[LineIndexes[datFileParam.Value.name]].IsVisible = checkBox.Checked;

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
                else
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

                            LineIndexes.Add(binFileParam.Value.name, zedGraphControl1.GraphPane.CurveList.Count - 1);

                            checkBox.CheckedChanged += (object otherSender, EventArgs eventArgs) =>
                            {
                                try
                                {
                                    if (zedGraphControl1.GraphPane.CurveList[LineIndexes[binFileParam.Value.name]].IsVisible != checkBox.Checked)
                                    {
                                        zedGraphControl1.GraphPane.CurveList[LineIndexes[binFileParam.Value.name]].IsVisible = checkBox.Checked;

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
                Enabled = true;

                if (Path.GetExtension(ofd.FileName) == ".gzdat" || Path.GetExtension(ofd.FileName) == ".dat")
                {
                    i = 1;
                    foreach (var datFileParam in datFileParams)
                    {
                        var checkBox = checkBoxes[datFileParam.Value.name];
                        checkBox.Checked = false;
                        if (!settings[0]) break;
                        if (checkBox.Enabled && settings[i])
                        {
                            checkBox.Checked = true;
                        }
                        i++;
                    }
                }
                else
                {
                    i = 1;
                    foreach (var binFileParam in binFileParams)
                    {
                        var checkBox = checkBoxes[binFileParam.Value.name];
                        checkBox.Checked = false;
                        if (settings[0]) break;
                        if (checkBox.Enabled && settings[i])
                        {
                            checkBox.Checked = true;
                        }
                        i++;
                    }
                }
                stream.Close();                
            }
        }

        private void DatDisplayPanel()
        {
            {
                panel3.Controls.Clear();
                textBoxes = new List<TextBox>();
                checkBoxes = new Dictionary<string, CheckBox>();
                panels = new Dictionary<string, Panel>();
                uidNames = new Dictionary<string, TextBox>();

                int i = 0;

                foreach (var datFileParam in datFileParams)
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
                    string measure = datFileParam.Value.measure;
                    if (string.IsNullOrEmpty(measure))
                    {
                        checkBox.Text = datFileParam.Value.name;
                    }
                    else
                    {
                        checkBox.Text = datFileParam.Value.name + ", " + measure;
                    }
                    checkBox.UseVisualStyleBackColor = true;
                    checkBoxes.Add(datFileParam.Value.name, checkBox);
                    checkBox.Checked = false;
                    int index = i;

                    var panel = new Panel();
                    panel3.Controls.Add(panel);
                    panel.Location = new Point(70, 6 + i * 26);
                    panel.Name = $"panel{i}";
                    panel.Size = new Size(17, 17);
                    panel.TabIndex = 0;
                    panels.Add(datFileParam.Value.name, panel);
                    panel.BackColor = Drawer.GetColor(i);
                    uidNames.Add(datFileParam.Value.name, textBoxes[i]);
                    i++;
                }
                button1.Visible = true;
                zedGraphControl1.Visible = true;
            }
        }

        private void RefreshDat()
        {
            int i = 0;
            foreach (var datFileParam in datFileParams)
            {
                var checkBox = checkBoxes[datFileParam.Value.name];
                var textBox = textBoxes[i];
                checkBox.Enabled = true;
                textBox.Enabled = true;
                textBox.Clear();
                i++;
            }
        }

        private void BinDisplayPanel()
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

        private void RefreshBin()
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

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                MakeSettings();
                string dataValues = JsonConvert.SerializeObject(settings);
                File.WriteAllText("./settings.json", dataValues);
            }
            catch (Exception exp)
            {
                MessageBox.Show("Произошла ошибка\nТекст ошибки:\n" + exp.ToString(), "Внимание", MessageBoxButtons.OK);
            }
            Cef.Shutdown();

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
            bool isXBigger = (bool)((diff) < 0.9);

            if (isXLess || isXBigger) sender.ZoomOut(sender.GraphPane);
        }

        private void zedGraphControl1_MouseMove(object sender, MouseEventArgs e)
        {
            if (isAllowMouseMove)
            {
                GraphPane pane = zedGraphControl1.GraphPane;
                zedGraphControl1.GraphPane.ReverseTransform(e.Location, out var x, out var y);
                verticalLine.Location.X = x;
                bool Nan = false;
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
                            if (Double.IsNaN(curve.Points[i].Y)) Nan = true;
                            break;
                        }
                    }

                    try
                    {
                        var newLatitude = result[index].Data["Широта"].DisplayValue.Replace(',', '.');
                        var newLongitude = result[index].Data["Долгота"].DisplayValue.Replace(',', '.');
                        if (latitude != newLatitude || longitude != newLongitude)
                        {
                            latitude = newLatitude;
                            longitude = newLongitude;
                            browser.ExecuteScriptAsync("map.panTo([" + latitude + "," + longitude + "]); "
                                                             + "marker.setLatLng([" + latitude + "," + longitude + "]); ");
                        }
                    }
                    catch (Exception exp)
                    { }


                    if (isDatFile && !Nan)
                    {
                        foreach (var datFileParam in datFileParams)
                        {
                            if (result[index].Data.ContainsKey(datFileParam.Value.name))
                            {
                                uidNames[datFileParam.Value.name].Text = result[index].Data[datFileParam.Value.name].DisplayValue;
                            }
                        }
                    }
                    else if (!isDatFile && !Nan)
                    {
                        foreach (var binFileParam in binFileParams)
                        {
                            if (result[index].Data.ContainsKey(binFileParam.Value.name))
                            {
                                uidNames[binFileParam.Value.name].Text = result[index].Data[binFileParam.Value.name].DisplayValue;
                            }
                        }
                    }
                }
                else if (!(x > min && x < max) || Nan)
                {
                    if (isDatFile)
                    {
                        foreach (var datFileParam in datFileParams)
                        {
                            uidNames[datFileParam.Value.name].Text = "";
                        }
                    }
                    else
                    {
                        foreach (var binFileParam in binFileParams)
                        {
                            uidNames[binFileParam.Value.name].Text = "";
                        }
                    }

                }
                zedGraphControl1.Invalidate();
            }
        }

        private void выходToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            string settingsPath = "./settings.json";
            try
            {
                settings = JsonConvert.DeserializeObject<JArray>(File.ReadAllText(settingsPath)).ToObject<bool[]>();
            }
            catch (Exception ex)
            {
                settings = new bool[100];
            }
        }

        private void ЭкпортироватьToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!isFirstOpen)
            {
                this.Enabled = false;
                try
                {
                    string fileName;
                    if (isDatFile)
                    {
                        fileName = "DatExport-" + DateTime.Now.ToString("yyyy-MM-dd hh-mm-ss") + ".xlsx";
                    }
                    else
                    {
                        fileName = "BinExport-" + DateTime.Now.ToString("yyyy-MM-dd hh-mm-ss") + ".xlsx";
                    }

                    var file = new FileInfo("./" + fileName);

                    using (var package = new ExcelPackage(file))
                    {
                        ExcelWorksheet worksheet = package.Workbook.Worksheets.Add("Datalist 1");
                        if (isDatFile)
                        {
                            //headers
                            var keys = arrayResult.Data.Keys.ToArray();
                            worksheet.Cells[1, 1].Value = "Время";
                            for (int i = 8; i < keys.Length; i++)
                            {
                                worksheet.Cells[1, i - 6].Value = keys[i];
                            }
                            //data
                            var temp = arrayResult.Data["Время в “UNIX” формате"];
                            for (int i = 0; i < temp.Length; i++)
                            {
                                worksheet.Cells[i + 2, 1].Value = temp[i].DisplayValue;
                            }
                            for (int i = 8; i < keys.Length; i++)
                            {
                                temp = arrayResult.Data[keys[i]];
                                for (int j = 0; j < temp.Length; j++)
                                {
                                    worksheet.Cells[j + 2, i - 6].Value = temp[j].DisplayValue;
                                }
                            }
                        }
                        else
                        {
                            //headers
                            var keys = arrayResult.Data.Keys.ToArray();
                            worksheet.Cells[1, 1].Value = "Время";
                            for (int i = 1; i < keys.Length; i++)
                            {
                                worksheet.Cells[1, i + 1].Value = keys[i];
                            }
                            //data
                            var temp = arrayResult.Data["Время в “UNIX” формате"];
                            for (int i = 0; i < temp.Length; i++)
                            {
                                worksheet.Cells[i + 2, 1].Value = temp[i].DisplayValue;
                            }
                            for (int i = 1; i < keys.Length; i++)
                            {
                                temp = arrayResult.Data[keys[i]];
                                for (int j = 0; j < temp.Length; j++)
                                {
                                    worksheet.Cells[j + 2, i + 1].Value = temp[j].DisplayValue;
                                }
                            }
                        }
                        for (int i = 1; i < arrayResult.Data.Count + 2; i++)
                        {
                            worksheet.Column(i).AutoFit();
                            worksheet.Column(i).Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        }
                        package.Save();
                    }
                    this.Enabled = true;
                    MessageBox.Show("Файл успешно экспортирован", "Внимание", MessageBoxButtons.OK);
                }
                catch (Exception exc)
                {
                    MessageBox.Show("Произошла ошибка\nТекст ошибки:\n" + exc.ToString(), "Внимание", MessageBoxButtons.OK);
                    this.Enabled = true;
                }
            }
            else
            {
                MessageBox.Show("Откройте файл, необходимый для экспорта", "Внимание", MessageBoxButtons.OK);
            }
        }

        private void MakeSettings()
        {
            settings = checkBoxes.Select(c => c.Value.Checked).ToArray();
            int ParamCount = settings.Length + 1;
            bool[] temp = new bool[ParamCount];
            temp[0] = isDatFile;
            for (int c = 0; c < settings.Length; c++)
            {
                temp[c + 1] = settings[c];
            }
            settings = temp;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            GraphPane pane = zedGraphControl1.GraphPane;
            pane.XAxis.Scale.Min = drawer.xScaleMin;
            pane.XAxis.Scale.Max = drawer.xScaleMax;
            drawer.Refresh();
        }

        private void zedGraphControl1_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (isAllowMouseMove) isAllowMouseMove = false;
                else isAllowMouseMove = true;
                checkBox1.Checked = isAllowMouseMove;
            }            
        }
    }
}

