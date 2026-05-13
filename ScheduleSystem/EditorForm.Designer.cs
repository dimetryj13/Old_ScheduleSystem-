namespace ScheduleSystem
{
    partial class EditorForm
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
            this.comboGroupEdit = new System.Windows.Forms.ComboBox();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.Monday = new System.Windows.Forms.TabPage();
            this.Tuesday = new System.Windows.Forms.TabPage();
            this.Wednesday = new System.Windows.Forms.TabPage();
            this.Thursday = new System.Windows.Forms.TabPage();
            this.Friday = new System.Windows.Forms.TabPage();
            this.Saturday = new System.Windows.Forms.TabPage();
            this.tabControl1.SuspendLayout();
            this.SuspendLayout();
            // 
            // comboGroupEdit
            // 
            this.comboGroupEdit.Dock = System.Windows.Forms.DockStyle.Top;
            this.comboGroupEdit.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboGroupEdit.Font = new System.Drawing.Font("Times New Roman", 13.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.comboGroupEdit.FormattingEnabled = true;
            this.comboGroupEdit.Location = new System.Drawing.Point(0, 0);
            this.comboGroupEdit.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.comboGroupEdit.Name = "comboGroupEdit";
            this.comboGroupEdit.Size = new System.Drawing.Size(1067, 33);
            this.comboGroupEdit.TabIndex = 4;
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.Monday);
            this.tabControl1.Controls.Add(this.Tuesday);
            this.tabControl1.Controls.Add(this.Wednesday);
            this.tabControl1.Controls.Add(this.Thursday);
            this.tabControl1.Controls.Add(this.Friday);
            this.tabControl1.Controls.Add(this.Saturday);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Location = new System.Drawing.Point(0, 33);
            this.tabControl1.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(1067, 521);
            this.tabControl1.TabIndex = 5;
            // 
            // Monday
            // 
            this.Monday.Location = new System.Drawing.Point(4, 25);
            this.Monday.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.Monday.Name = "Monday";
            this.Monday.Size = new System.Drawing.Size(1059, 492);
            this.Monday.TabIndex = 0;
            this.Monday.Text = "ПН";
            this.Monday.UseVisualStyleBackColor = true;
            // 
            // Tuesday
            // 
            this.Tuesday.Location = new System.Drawing.Point(4, 25);
            this.Tuesday.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.Tuesday.Name = "Tuesday";
            this.Tuesday.Size = new System.Drawing.Size(1059, 490);
            this.Tuesday.TabIndex = 1;
            this.Tuesday.Text = "ВТ";
            this.Tuesday.UseVisualStyleBackColor = true;
            // 
            // Wednesday
            // 
            this.Wednesday.Location = new System.Drawing.Point(4, 25);
            this.Wednesday.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.Wednesday.Name = "Wednesday";
            this.Wednesday.Size = new System.Drawing.Size(1059, 490);
            this.Wednesday.TabIndex = 2;
            this.Wednesday.Text = "СР";
            this.Wednesday.UseVisualStyleBackColor = true;
            // 
            // Thursday
            // 
            this.Thursday.Location = new System.Drawing.Point(4, 25);
            this.Thursday.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.Thursday.Name = "Thursday";
            this.Thursday.Size = new System.Drawing.Size(1059, 490);
            this.Thursday.TabIndex = 3;
            this.Thursday.Text = "ЧТ";
            this.Thursday.UseVisualStyleBackColor = true;
            // 
            // Friday
            // 
            this.Friday.Location = new System.Drawing.Point(4, 25);
            this.Friday.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.Friday.Name = "Friday";
            this.Friday.Size = new System.Drawing.Size(1059, 490);
            this.Friday.TabIndex = 4;
            this.Friday.Text = "ПТ";
            this.Friday.UseVisualStyleBackColor = true;
            // 
            // Saturday
            // 
            this.Saturday.Location = new System.Drawing.Point(4, 25);
            this.Saturday.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.Saturday.Name = "Saturday";
            this.Saturday.Size = new System.Drawing.Size(1059, 490);
            this.Saturday.TabIndex = 5;
            this.Saturday.Text = "СБ";
            this.Saturday.UseVisualStyleBackColor = true;
            // 
            // EditorForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1067, 554);
            this.Controls.Add(this.tabControl1);
            this.Controls.Add(this.comboGroupEdit);
            this.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.Name = "EditorForm";
            this.Text = "EditorForm";
            this.Load += new System.EventHandler(this.EditorForm_Load_1);
            this.tabControl1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ComboBox comboGroupEdit;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage Monday;
        private System.Windows.Forms.TabPage Tuesday;
        private System.Windows.Forms.TabPage Wednesday;
        private System.Windows.Forms.TabPage Thursday;
        private System.Windows.Forms.TabPage Friday;
        private System.Windows.Forms.TabPage Saturday;
    }
}