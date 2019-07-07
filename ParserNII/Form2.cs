using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
    }
}
