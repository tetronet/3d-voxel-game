using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MetaTSUK
{
    public partial class AutoClosingWindow : Form
    {
        public AutoClosingWindow(string title, string text, int timeout)
        {
            InitializeComponent();
            Text = title;
            label1.Text = text;
            close.Interval = timeout;
        }

        private void AutoClosingWindow_Load(object sender, EventArgs e)
        {

        }

        private void close_Tick(object sender, EventArgs e)
        {
            Close();
        }
    }
}
