using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OrchestratorConnector
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button4_Click(object sender, EventArgs e)
        {

        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void button5_Click(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            var presetName = txtPresetName.Text;
            var option_orchurl = txtOrchURL.Text;
            var option_clientid = txtClientID.Text;
            var option_clientsecret = txtClientSecret.Text;

            // Check if an item with the same name already exists
            var existingItem = listBox1.Items.Cast<ListViewItem>().FirstOrDefault(item => item.Text == presetName);

            if (existingItem != null)
            {
                // Overwrite the existing item
                existingItem.Tag = new PresetOptions
                {
                    Option1 = option_orchurl,
                    Option2 = option_clientid,
                    Option3 = option_clientsecret
                };
            }
            else
            {
                // Add a new item
                var listItem = new ListViewItem(presetName);
                listItem.Tag = new PresetOptions
                {
                    Option1 = option_orchurl,
                    Option2 = option_clientid,
                    Option3 = option_clientsecret
                };

                listBox1.Items.Add(listItem);
            }

            listBox1.DisplayMember = "Text"; // Ensure the DisplayMember is set
            listBox1.Refresh(); // Refresh the listbox to reflect the new item

            // Clear the textboxes
            txtPresetName.Clear();
            txtOrchURL.Clear();
            txtClientID.Clear();
            txtClientSecret.Clear();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedItem != null)
            {
                listBox1.Items.Remove(listBox1.SelectedItem);
            }

            // Clear the textboxes
            txtPresetName.Clear();
            txtOrchURL.Clear();
            txtClientID.Clear();
            txtClientSecret.Clear();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedItem != null)
            {
                var selectedItem = (ListViewItem)listBox1.SelectedItem;
                var presetOptions = (PresetOptions)selectedItem.Tag;

                txtPresetName.Text = selectedItem.Text;
                txtOrchURL.Text = presetOptions.Option1;
                txtClientID.Text = presetOptions.Option2;
                txtClientSecret.Text = presetOptions.Option3;
            }
        }
    }

    public class PresetOptions
    {
        public string Option1 { get; set; }
        public string Option2 { get; set; }
        public string Option3 { get; set; }
    }
}
