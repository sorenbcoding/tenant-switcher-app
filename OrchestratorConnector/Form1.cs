using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OrchestratorConnector
{
    public partial class Form1 : Form
    {
        private const string PresetsFilePath = "presets.json";

        public Form1()
        {
            InitializeComponent();
            LoadPresets();
        }

        private void buttonConnect_Click(object sender, EventArgs e)
        {
            var processName = "UiPath.Assistant";
            var processes = System.Diagnostics.Process.GetProcessesByName(processName);
            foreach (var process in processes)
            {
                process.Kill();
            }

        }

        private void txtPresetName_TextChanged(object sender, EventArgs e)
        {

        }

        private void listPresets_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void buttonAddSave_Click(object sender, EventArgs e)
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
                // Update status strip
                toolStripStatusLabel1.Text = "Entry saved!";
                statusStrip1.Refresh();
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
                // Update status strip
                toolStripStatusLabel1.Text = "Entry successfully added!";
                statusStrip1.Refresh();
            }

            listBox1.DisplayMember = "Text"; // Ensure the DisplayMember is set
            listBox1.Refresh(); // Refresh the listbox to reflect the new item

            // Clear the textboxes
            txtPresetName.Clear();
            txtOrchURL.Clear();
            txtClientID.Clear();
            txtClientSecret.Clear();

            SavePresets();
        }

        private void buttonDelete_Click(object sender, EventArgs e)
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

            SavePresets();
        }

        private void buttonEdit_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedItem != null)
            {
                var selectedItem = listBox1.SelectedItem as ListViewItem;
                if (selectedItem != null)
                {
                    var presetOptions = selectedItem.Tag as PresetOptions;
                    if (presetOptions != null)
                    {
                        txtPresetName.Text = selectedItem.Text;
                        txtOrchURL.Text = presetOptions.Option1;
                        txtClientID.Text = presetOptions.Option2;
                        txtClientSecret.Text = presetOptions.Option3;
                    }
                }
            }
        }

        private void SavePresets()
        {
            var presets = listBox1.Items.Cast<ListViewItem>().Select(item =>
            {
                var options = item.Tag as PresetOptions;
                return new PresetOptions
                {
                    Option1 = options != null ? options.Option1 : string.Empty,
                    Option2 = options != null ? options.Option2 : string.Empty,
                    Option3 = options != null ? options.Option3 : string.Empty,
                    PresetName = item.Text
                };
            }).ToList();

            var json = JsonSerializer.Serialize(presets);
            File.WriteAllText(PresetsFilePath, json);
        }

        private void LoadPresets()
        {
            if (File.Exists(PresetsFilePath))
            {
                var json = File.ReadAllText(PresetsFilePath);
                var presets = JsonSerializer.Deserialize<List<PresetOptions>>(json);

                if (presets != null)
                {
                    foreach (var preset in presets)
                    {
                        var listItem = new ListViewItem(preset.PresetName)
                        {
                            Tag = preset,
                            Text = preset.PresetName // Ensure the Text property is set correctly
                        };
                        listBox1.Items.Add(listItem);
                    }
                }
            }

            listBox1.DisplayMember = "Text"; // Ensure the DisplayMember is set
        }

        private void toolStripStatusLabel1_Click(object sender, EventArgs e)
        {

        }
    }

    public class PresetOptions
    {
        public string Option1 { get; set; } = string.Empty;
        public string Option2 { get; set; } = string.Empty;
        public string Option3 { get; set; } = string.Empty;
        public string PresetName { get; set; } = string.Empty;
    }
}
