using System;
using System.Globalization;
using System.Windows.Forms;

namespace MetaTSUK
{
    public partial class Settings : Form
    {
        private bool enable_earth = true;
        private long fd = 0;
        private Action end;
        public Settings(long fd, decimal currentX, decimal currentY, decimal currentZ, int currentRenderQualityMultipliedByOneHundred, Action end, double currentRenderDistance)
        {
            InitializeComponent();
            this.fd = fd;
            textBox1.Text = currentX.ToString();
            textBox2.Text = currentY.ToString();
            textBox3.Text = currentZ.ToString();
            trackBar1.Value = currentRenderQualityMultipliedByOneHundred;
            label10.Text = "Client Fd: " + fd;
            numericUpDown1.Value = (decimal)currentRenderDistance;
            this.end = end;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            numericUpDown1.Value = 32m;
        }

        private void button5_Click(object sender, EventArgs e)
        {
            Form1.renderDistance = (double)numericUpDown1.Value;
            Form1.quality = (double)trackBar1.Value / 100;
            //Form1.ee = enable_earth;
            try
            {
                // Float разрешает и экспоненту (1E+15). Дальше края мира (±9E+18) позиция зажимается.
                Program.game_world.ClientPositions[fd] = new PlayerPosition(
                    decimal.Parse(textBox1.Text, NumberStyles.Float),
                    decimal.Parse(textBox2.Text, NumberStyles.Float),
                    decimal.Parse(textBox3.Text, NumberStyles.Float));
            }
            catch
            {
                MessageBox.Show("Coordinates must be valid numbers (could be an exponent like 1E+15), the world ends at 9E+18 and -9E+18.", "Coordinate parsing error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
            //domainUpDown1.SelectedIndex;
            end();
            Close();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            numericUpDown1.Value = 64m;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            numericUpDown1.Value = 128m;
        }

        private void button6_Click(object sender, EventArgs e)
        {
            numericUpDown1.Value = 1024m;
        }

        private void button4_Click(object sender, EventArgs e)
        {
            numericUpDown1.Value = 256m;
        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {

        }

        private void refresher_Tick(object sender, EventArgs e)
        {
            label2.Text = "Selected presicion is " + trackBar1.Value.ToString();
        }

        private void button7_Click(object sender, EventArgs e)
        {
            trackBar1.Value = 1;
        }

        private void button8_Click(object sender, EventArgs e)
        {
            trackBar1.Value = 5;
        }

        private void button9_Click(object sender, EventArgs e)
        {
            trackBar1.Value = 20;
        }

        private void Settings_Load(object sender, EventArgs e)
        {
            refresher.Start();
        }

        private void button10_Click(object sender, EventArgs e)
        {
            if (enable_earth) {
                enable_earth = false;
                button10.Text = "Enable";
            }
            if (!enable_earth) {
                enable_earth = true;
                button10.Text = "Disable";
            }
        }

        private void label3_Click(object sender, EventArgs e)
        {

        }

        private void Settings_FormClosing(object sender, FormClosingEventArgs e)
        {
            end();
        }
    }
}
