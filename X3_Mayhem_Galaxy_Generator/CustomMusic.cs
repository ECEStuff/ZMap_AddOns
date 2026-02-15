using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Security.Cryptography;

namespace X3_Mayhem_Galaxy_Generator
{
    public partial class CustomMusic : Form
    {
        public string SelectedFolder;
        private string m_x3root;

        public CustomMusic()
        {
            InitializeComponent();
        }

        public CustomMusic(string existingFolder, string x3root)
        {
            InitializeComponent();
            lblFolderName.Text = existingFolder;
            m_x3root = x3root;
        }


        private void btnSelectFolder_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                if (Directory.Exists(folderBrowserDialog1.SelectedPath))
                {
                    lblFolderName.Text = folderBrowserDialog1.SelectedPath;
                }
                else
                {
                    lblFolderName.Text = "None";
                }
            }
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            if (Directory.Exists(lblFolderName.Text))
            {
                if (Directory.GetFiles(lblFolderName.Text, "*.mp3").Length > 9)
                {
                    // Copy these mp3's into the Mayhem/soundtrack folder, skipping duplicates.
                    string dest = m_x3root + "\\soundtrack\\";
                    try
                    {
                        Directory.GetFiles(lblFolderName.Text, "*.mp3").ToList().ForEach(item => CopyFile(item, dest));
                    }
                    catch(Exception ex)
                    {
                        MessageBox.Show("Unable to copy music files.  Error: " + ex.Message);
                        return;
                    }


                    SelectedFolder = lblFolderName.Text;
                    DialogResult = DialogResult.OK;
                    Close();
                    return;
                }
                else
                {
                    MessageBox.Show("You need to pick a folder with at least 10 mp3 files in it.");
                }
            }
            else
            {
                MessageBox.Show("Folder does not exist");
            }

            DialogResult = DialogResult.Cancel;
        }

        /// <summary>
        /// Copy file from full source path to a destination folder dst, but only if it doesn't exist in the destination.
        /// Transmutes each filename in the source so that it appears like an "Integer" in the destination.
        /// X3 seems to like to use "00000.mp3" filenames for all it's music, and I'm sticking with the convention just so nothing breakes.
        /// </summary>
        /// <param name="src"></param>
        /// <param name="dst"></param>
        private void CopyFile(string src, string dst)
        {
            //string srcFile = Path.GetFileName(src);                 // root name, not including path.     "mymusicfile.mp3"
            string srcFile = Path.GetFileNameWithoutExtension(src);
            string intengerSrcFile = X3Utils.Transmute(srcFile) + ".mp3";            // converted integer-like name for this.
            string fulldst = dst + intengerSrcFile;
            if (!File.Exists(fulldst))
            {
                File.Copy(src, fulldst, false);
            }
        }



        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
