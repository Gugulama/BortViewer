using System.Windows.Forms;

namespace ParserNII
{
    public partial class Form2 : Form
    {
        public Form2(string param)
        {
            InitializeComponent();
            label2.Text = param;
        }

        private void Button1_Click(object sender, System.EventArgs e)
        {
            Properties.Settings.Default.paramEdit = paramEdit.Text;
            Properties.Settings.Default.Save();
        }

        private void button3_Click(object sender, System.EventArgs e)
        {
            Form1 mainForm = Owner as Form1;
            if (mainForm != null)
            {
                mainForm.zedGraphControl1.GraphPane.CurveList.Remove(mainForm.zedGraphControl1.GraphPane.CurveList["limit"]);
                mainForm.zedGraphControl1.Refresh();
                Close();
            }
        }
    }
}
