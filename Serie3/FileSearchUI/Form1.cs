using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FileSearchUI {

    public partial class Form1 : Form {

        private string folder = "";
        private string ext = "";
        private string query = "";
        private IProgress<CustomProgress> progress;
        private CancellationTokenSource cts = new CancellationTokenSource();
        private bool maximumSet = false;

        public Form1() {
            InitializeComponent();

            progress = new Progress<CustomProgress>(state => {
                // will run with the UI thread context

                if (state.file != null)
                    listBox1.Items.Add(state.file);

                // only set the maximum one time, 
                // does it affect performance to set it every time?
                if (!maximumSet) {
                    progressBar1.Maximum = state.total;
                    maximumSet = true;
                }

                progressBar1.PerformStep();

            });
        }

        private void Form1_Load(object sender, EventArgs e) {
        }

        private async void search_Click(object sender, EventArgs e) {
            if (thereIsValidInformation() == false)
                return;

            listBox1.Items.Clear();
            cancelButton.Enabled = true;
            results.Visible = false;

            try {
                var res = await FileSearch.Find(folder, ext, query, cts.Token, progress);
                // This code will run within the UI context
                var resText = String.Format("Total files: {0}, .{1} files: {2}. Files matched the search: {3}",
                    res.totalFiles, ext, res.totalFilesWithExtension, res.files.Count);
                results.Text = resText;
                results.Visible = true;
            } catch (OperationCanceledException ex) {
                // benign exception
            } catch (Exception ex) {
                MessageBox.Show("Error: " + ex.Message); // Ooopss..
            }
            
            progressBar1.Value = 0;
            cancelButton.Enabled = false;
            maximumSet = false;
            cts = new CancellationTokenSource(); // reset the token
            
            
        }

        private void folder_Click(object sender, EventArgs e) {
            var result = folderBrowserDialog1.ShowDialog();

            if (result == DialogResult.OK) {
                folder = folderBrowserDialog1.SelectedPath;

                textBox1.Text = folder;
            }
        }


        /*
        |--------------------------------------------------------------------------
        | Validation
        |--------------------------------------------------------------------------
        */

        private bool thereIsValidInformation() {
            folder = textBox1.Text;
            ext = textBox2.Text;
            query = textBox3.Text;

            if (folder.Length == 0) {
                MessageBox.Show("A folder is necessary.");
                return false;
            }

            if (!Directory.Exists(folder)) { // ignore sync io, should be very very very fast
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

        private void button3_Click(object sender, EventArgs e)
        {
            cts.Cancel();
        }

        private void results_Click(object sender, EventArgs e)
        {

        }
    }

}