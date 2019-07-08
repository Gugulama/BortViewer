using System.Windows.Forms;

namespace ParserNII
{
    public partial class Form2 : Form
    {
        Form1 mainForm;
        public Form2(string param)
        {
            InitializeComponent();
            Form1 mainForm = Owner as Form1;
            label2.Text = param;
        }

        private void Button1_Click(object sender, System.EventArgs e)
        {
            Properties.Settings.Default.paramEdit = paramEdit.Text;

            Properties.Settings.Default.Save();
        }
    }
}
