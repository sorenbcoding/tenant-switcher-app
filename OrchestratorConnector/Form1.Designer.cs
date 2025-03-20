namespace OrchestratorConnector
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            listBox1 = new ListBox();
            button1 = new Button();
            button2 = new Button();
            button3 = new Button();
            button4 = new Button();
            label1 = new Label();
            txtPresetName = new TextBox();
            label3 = new Label();
            label2 = new Label();
            txtOrchURL = new TextBox();
            label4 = new Label();
            txtClientID = new TextBox();
            label5 = new Label();
            txtClientSecret = new TextBox();
            SuspendLayout();
            // 
            // listBox1
            // 
            listBox1.Font = new Font("Segoe UI", 15.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
            listBox1.FormattingEnabled = true;
            listBox1.ItemHeight = 30;
            listBox1.Location = new Point(434, 12);
            listBox1.Name = "listBox1";
            listBox1.Size = new Size(354, 424);
            listBox1.TabIndex = 0;
            listBox1.SelectedIndexChanged += listBox1_SelectedIndexChanged;
            // 
            // button1
            // 
            button1.Location = new Point(8, 52);
            button1.Name = "button1";
            button1.Size = new Size(136, 23);
            button1.TabIndex = 1;
            button1.Text = "Add/Save";
            button1.UseVisualStyleBackColor = true;
            button1.Click += buttonAddSave_Click;
            // 
            // button2
            // 
            button2.Location = new Point(150, 52);
            button2.Name = "button2";
            button2.Size = new Size(136, 23);
            button2.TabIndex = 2;
            button2.Text = "Edit";
            button2.UseVisualStyleBackColor = true;
            button2.Click += buttonEdit_Click;
            // 
            // button3
            // 
            button3.Location = new Point(292, 52);
            button3.Name = "button3";
            button3.Size = new Size(136, 23);
            button3.TabIndex = 3;
            button3.Text = "Delete";
            button3.UseVisualStyleBackColor = true;
            button3.Click += buttonDelete_Click;
            // 
            // button4
            // 
            button4.Font = new Font("Segoe UI", 15.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
            button4.Location = new Point(138, 379);
            button4.Name = "button4";
            button4.Size = new Size(168, 57);
            button4.TabIndex = 4;
            button4.Text = "Connect";
            button4.UseVisualStyleBackColor = true;
            button4.Click += buttonConnect_Click;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("Segoe UI", 20.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            label1.Location = new Point(4, 12);
            label1.Name = "label1";
            label1.Size = new Size(110, 37);
            label1.TabIndex = 6;
            label1.Text = "Presets";
            // 
            // txtPresetName
            // 
            txtPresetName.Location = new Point(8, 116);
            txtPresetName.Name = "txtPresetName";
            txtPresetName.Size = new Size(420, 23);
            txtPresetName.TabIndex = 7;
            txtPresetName.TextChanged += textBox1_TextChanged;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
            label3.Location = new Point(8, 92);
            label3.Name = "label3";
            label3.Size = new Size(107, 21);
            label3.TabIndex = 9;
            label3.Text = "Preset Name";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
            label2.Location = new Point(8, 142);
            label2.Name = "label2";
            label2.Size = new Size(139, 21);
            label2.TabIndex = 10;
            label2.Text = "Orchestrator URL";
            // 
            // txtOrchURL
            // 
            txtOrchURL.Location = new Point(8, 166);
            txtOrchURL.Name = "txtOrchURL";
            txtOrchURL.Size = new Size(420, 23);
            txtOrchURL.TabIndex = 11;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
            label4.Location = new Point(8, 192);
            label4.Name = "label4";
            label4.Size = new Size(149, 21);
            label4.TabIndex = 12;
            label4.Text = "Client/Machine ID";
            // 
            // txtClientID
            // 
            txtClientID.Location = new Point(8, 216);
            txtClientID.Name = "txtClientID";
            txtClientID.Size = new Size(420, 23);
            txtClientID.TabIndex = 13;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
            label5.Location = new Point(8, 242);
            label5.Name = "label5";
            label5.Size = new Size(106, 21);
            label5.TabIndex = 14;
            label5.Text = "Client Secret";
            // 
            // txtClientSecret
            // 
            txtClientSecret.Location = new Point(8, 266);
            txtClientSecret.Name = "txtClientSecret";
            txtClientSecret.Size = new Size(420, 23);
            txtClientSecret.TabIndex = 15;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(txtClientSecret);
            Controls.Add(label5);
            Controls.Add(txtClientID);
            Controls.Add(label4);
            Controls.Add(txtOrchURL);
            Controls.Add(label2);
            Controls.Add(label3);
            Controls.Add(txtPresetName);
            Controls.Add(label1);
            Controls.Add(button4);
            Controls.Add(button3);
            Controls.Add(button2);
            Controls.Add(button1);
            Controls.Add(listBox1);
            Name = "Form1";
            Text = "Form1";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private ListBox listBox1;
        private Button button1;
        private Button button2;
        private Button button3;
        private Button button4;
        private Label label1;
        private TextBox txtPresetName;
        private Label label3;
        private Label label2;
        private TextBox txtOrchURL;
        private Label label4;
        private TextBox txtClientID;
        private Label label5;
        private TextBox txtClientSecret;
    }
}