﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Merthsoft.TokenIDE {
	public partial class InputBox:Form {
		public string outString;

		public InputBox(string title, string defaultText = "", string extraText = "") {
			InitializeComponent();
			this.Text = title;
			this.textBox1.Text = defaultText;
			this.extraTextLabel.Text = extraText;
		}

		public static string Show(string title, string defaultText = "", string extraText = "") {
			InputBox i = new InputBox(title, defaultText, extraText);
			i.ShowDialog();
			return i.outString;
		}

		private void button1_Click(object sender, EventArgs e) {
			outString = textBox1.Text;
			Close();
		}

		private void button2_Click(object sender, EventArgs e) {
			textBox1.Text = null;
			outString = null;
		}
	}
}
