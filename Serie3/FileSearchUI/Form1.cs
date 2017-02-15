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

namespace FileSearchUI {

    public partial class Form1 : Form {

        private string folder = "";
        private string ext = "";
        private string query = "";

        public Form1() {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e) {
        }


        private void button1_Click(object sender, EventArgs e) {
            var result = folderBrowserDialog1.ShowDialog();

            if (result == DialogResult.OK) {
                folder = folderBrowserDialog1.SelectedPath;


                textBox1.Text = folder;
            }
        }

        private void button2_Click(object sender, EventArgs e) {
            if (thereIsValidInformation() == false)
                return;
        }

        private bool thereIsValidInformation() {
            folder = textBox1.Text;
            ext = textBox2.Text;
            query = textBox3.Text;

            if (folder.Length == 0) {
                MessageBox.Show("A folder is necessary.");
                return false;
            }

            if (!Directory.Exists(folder)) {
                MessageBox.Show("That folder does not exist!");
                return false;
            }

            if (ext.Length == 0) {
                MessageBox.Show("File extension should be provided.");
                return false;
            }

            if (query.Length == 0) {
                MessageBox.Show("A word to search is necessary.");
                return false;
            }

            // just in case..
            ext = ext.Replace(".", "");

            return true;
        }

    }

}