﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CabViewerUI
{
    public partial class FormMain : Form
    {
        public FormMain()
        {
            InitializeComponent();
        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            FormStructureViewer formStructureViewer = new FormStructureViewer();
            formStructureViewer.Show(this.mainDockPanel);
            formStructureViewer.DockTo(this.mainDockPanel, DockStyle.Left);
        }
    }
}