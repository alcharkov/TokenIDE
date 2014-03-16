﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Merthsoft.TokenIDE.Forms {
	public partial class ColorPicker : Form {
		public event PasteTextEventHandler PasteTextEvent;

		List<Color> XLibPalette = new List<Color>();
		List<SolidBrush> XLibBrushes = new List<SolidBrush>();

		private int selectedColor;
		public int SelectedColor {
			get {
				return selectedColor;
			}
			set {
				if (value >= 0 && value < 256) {
					selectedColor = value;
					pixelBox.Invalidate();
					numberLabel.Text = string.Format("{0} (0x{0:X2})", value);
				}
			}
		}

		public ColorPicker() {
			InitializeComponent();

			for (int i = 0; i < 256; i++) {
				Color color = MerthsoftExtensions.ColorFrom8BitHLRGB(i);
				XLibPalette.Add(color);
				XLibBrushes.Add(new SolidBrush(color));
			}
		}

		private void drawPalette(Graphics g) {
			int boxWidth;
			int boxHeight;
			int colorCount;
			int maxWidth;

			//if (SelectedPalette == Palette.CelticIICSE) {
			//	boxWidth = 44;
			//	boxHeight = 44;
			//	colorCount = CelticPalette.Count;
			//	maxWidth = 352;
			//} else {
				boxWidth = 22;
				boxHeight = 22;
				colorCount = 256;
				maxWidth = 704;
			//}

			int paletteX = 0;
			int paletteY = 0;

			try {
				for (int colorIndex = 0; colorIndex < colorCount; colorIndex++) {
					//Color c;

					//if (SelectedPalette == Palette.CelticIICSE) {
					//	c = CelticPalette[colorIndex];
					//	g.FillRect(CelticBrushes[colorIndex], paletteX, paletteY, paletteX + boxWidth, paletteY + boxHeight);
					//	g.DrawRect(Pens.Black, paletteX, paletteY, paletteX + boxWidth, paletteY + boxHeight);
					//} else {
						SolidBrush brush = XLibBrushes[colorIndex];
						g.FillRect(brush, paletteX, paletteY, paletteX + boxWidth, paletteY + boxHeight);
					//}

					paletteX += boxWidth;
					if (paletteX >= maxWidth) {
						paletteX = 0;
						paletteY += boxHeight;
					}
				}
			} catch {
				throw;
			}
		}

		private void paletteBox_Paint(object sender, PaintEventArgs e) {
			Graphics g = e.Graphics;
			drawPalette(g);
		}

		private void paletteBox_MouseClick(object sender, MouseEventArgs e) {
			selectPalette(e);
		}

		private void paletteBox_MouseMove(object sender, MouseEventArgs e) {
			if (e.Button != System.Windows.Forms.MouseButtons.None) {
				if (!paletteBox.Bounds.Contains(e.Location)) { return; }
				selectPalette(e);
			}
		}

		private void selectPalette(MouseEventArgs e) {

			int boxWidth;
			int boxHeight;
			int maxWidth;

			//if (SelectedPalette == Palette.CelticIICSE) {
			//	boxWidth = 44;
			//	boxHeight = 44;
			//	maxWidth = 352;
			//} else {
				boxWidth = 22;
				boxHeight = 22;
				maxWidth = 704;
			//}

			//int paletteIndex = (e.X / boxWidth) + (maxWidth / boxWidth) * (e.Y / boxHeight);
			SelectedColor = (e.X / boxWidth) + (maxWidth / boxWidth) * (e.Y / boxHeight);

			//if (e.Button == System.Windows.Forms.MouseButtons.Left) {
			//	setLeftMouseButton(paletteIndex);
			//} else if (e.Button == System.Windows.Forms.MouseButtons.Right) {
			//	setRightMouseButton(paletteIndex);
			//}
		}

		private void pixelBox_Paint(object sender, PaintEventArgs e) {
			e.Graphics.FillRectangle(XLibBrushes[selectedColor], e.ClipRectangle);
		}

		private void button2_Click(object sender, EventArgs e) {
			Clipboard.SetText(SelectedColor.ToString());
		}

		private void button3_Click(object sender, EventArgs e) {
			Close();
		}

		private void insertButton_Click(object sender, EventArgs e) {
			PasteTextEventHandler temp = PasteTextEvent;
			if (temp != null) {
				temp(this, new PasteTextEventArgs(SelectedColor.ToString()));
			}
			Close();
		}
	}
}
