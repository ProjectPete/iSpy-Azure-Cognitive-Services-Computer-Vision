using System;
using System.Windows.Forms;

namespace Plugins
{
    public partial class Configure : Form
    {
        public string Configuration;
        private readonly Main _owner;

        public static bool ShowingConfig = false;
        public static PictureBox RawImage;
        public static PictureBox ChangesThumb;
        public static PictureBox PixelsThumb;
        public static PictureBox ActionImage;
        public static PictureBox AlarmPercentage;
        public static Label PercentLabel;

        public Configure(Main owner)
        {
            InitializeComponent();
            _owner = owner;
            PixelsThumb = pbPixels;
            RawImage = pbRaw;
            ChangesThumb = pbChanges;
            ActionImage = pbAction;
            AlarmPercentage = pbAlertLevel;
            PercentLabel = lbPercent;

            FormClosed += Configure_FormClosed;

            Load += Configure_Load1;
            Shown += Configure_Shown;

        }

        private void Configure_Shown(object sender, EventArgs e)
        {
//            MessageBox.Show("Shown");
        }

        private void Configure_Load1(object sender, EventArgs e)
        {
//            MessageBox.Show("Load");
        }

        private void Configure_FormClosed(object sender, FormClosedEventArgs e)
        {
            ShowingConfig = false;
        }

        private void Configure_Load(object sender, EventArgs e)
        {
//            MessageBox.Show("ConfLoad");
            numSensitivity.Value = _owner.Sensitivity;
            numMinPercent.Value = _owner.MinAlartPercent;
            numMaxPercent.Value = _owner.MaxAlartPercent;
            numMaxPixelsDetail.Value = _owner.MaxPixelsDetail;
            chkShow.Checked = _owner.ShowPixels;
            ShowingConfig = true;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            _owner.UpdateConfig((int)numSensitivity.Value, (int)numMinPercent.Value, (int)numMaxPercent.Value, (int)numMaxPixelsDetail.Value, chkShow.Checked);
            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnTest_Click(object sender, EventArgs e)
        {
            _owner.UpdateConfig((int)numSensitivity.Value, (int)numMinPercent.Value, (int)numMaxPercent.Value, (int)numMaxPixelsDetail.Value, chkShow.Checked);
        }
    }
}
