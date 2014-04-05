﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ColorMatrixViewer
{
	public partial class Form1 : Form
	{

		private Bitmap input;
		private Bitmap displayed;

		private static readonly float[,] identity = new float[,] {
			{  1.0f,  0.0f,  0.0f,  0.0f,  0.0f },
			{  0.0f,  1.0f,  0.0f,  0.0f,  0.0f },
			{  0.0f,  0.0f, 1.0f,  0.0f,  0.0f },
			{  0.0f,  0.0f,  0.0f,  1.0f,  0.0f },
			{  0.0f,  0.0f,  0.0f,  0.0f,  1.0f }
		};

		private float[,] _Matrix = null;
		public float[,] Matrix
		{
			get { return _Matrix; }
			set { _Matrix = value; }
		}

		private TextBox[,] textboxes;

		public Form1()
		{
			ResetMatrix();
			InitializeComponent();
			InitializeMatrixTextboxes(this, new Point(20, 40));
			RefreshMatrixOrTextBoxes(RefreshDirection.FromMatrix);
		}

		private void InitializeMatrixTextboxes(Control control, Point location)
		{
			textboxes = new TextBox[5, 5];
			const int xSpacing = 47, ySpacing = 17;
			for (int i = 0; i < 5; i++)
			{
				for (int j = 0; j < 5; j++)
				{
					var newTextBox = new TextBox();
					newTextBox.Parent = control;
					newTextBox.Location = new Point(location.X + i * xSpacing, location.Y + j * ySpacing);
					newTextBox.Width = 50;
					newTextBox.Height = 20;
					newTextBox.TextAlign = HorizontalAlignment.Center;
					newTextBox.KeyPress += (o, e) => { if (e.KeyChar == ',') { e.Handled = true; newTextBox.SelectedText = "."; } };
					newTextBox.MouseWheel += (o, e) =>
					{
						decimal parsed = 0; //exact decimal rounding
						if (!decimal.TryParse(newTextBox.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsed))
							parsed = 0;
						decimal increment = 1;
						if (ModifierKeys == Keys.Control)
						{
							increment = .1m;
						}
						parsed += increment * (e.Delta / (Math.Abs(e.Delta)));
						newTextBox.Text = parsed.ToString(System.Globalization.CultureInfo.InvariantCulture);
					};
					textboxes[j, i] = newTextBox;
				}
			}
		}

		private void ResetMatrix()
		{
			Matrix = new float[5, 5];
			for (int i = 0; i < Matrix.GetLength(0); i++)
			{
				for (int j = 0; j < Matrix.GetLength(1); j++)
				{
					Matrix[i, j] = identity[i, j];
				}
			}
		}

		enum RefreshDirection
		{
			FromMatrix,
			FromTextboxes,
		}
		private void RefreshMatrixOrTextBoxes(RefreshDirection direction)
		{
			switch (direction)
			{
				case RefreshDirection.FromMatrix:
					for (int i = 0; i < 5; i++)
					{
						for (int j = 0; j < 5; j++)
						{
							textboxes[i, j].Text = Matrix[i, j].ToString();
						}
					}
					break;
				case RefreshDirection.FromTextboxes:
					try
					{
						for (int i = 0; i < 5; i++)
						{
							for (int j = 0; j < 5; j++)
							{
								Matrix[i, j] = float.Parse(textboxes[i, j].Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
							}
						}
					}
					catch (Exception)
					{
						ResetMatrix();
						MessageBox.Show("Invalid matrix!");
					}
					break;
				default:
					throw new Exception("Fuck you!");
			}

		}

		private void loadAnImageToolStripMenuItem_Click(object sender, EventArgs e)
		{
			using (var dialog = new OpenFileDialog())
			{
				if (dialog.ShowDialog() == DialogResult.OK)
				{
					this.BackgroundImage = input = displayed = (Bitmap)Bitmap.FromFile(dialog.FileName);
				}
			}
		}

		private void resetMatrixToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ResetMatrix();
			RefreshMatrixOrTextBoxes(RefreshDirection.FromMatrix);
		}

		private void applyMatrixToolStripMenuItem_Click(object sender, EventArgs e)
		{
			RefreshMatrixOrTextBoxes(RefreshDirection.FromTextboxes);

			input = ApplyColorMatrix(input, Matrix);
			this.BackgroundImage = input;
			return;
			Bitmap b = input;
			Rectangle rect = new Rectangle(0, 0, b.Width, b.Height);
			System.Drawing.Imaging.BitmapData bmpData = b.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite, b.PixelFormat);
			IntPtr ptr = bmpData.Scan0;
			int bytes = Math.Abs(bmpData.Stride) * b.Height;
			byte[] rgbValues = new byte[bytes];
			System.Runtime.InteropServices.Marshal.Copy(ptr, rgbValues, 0, bytes);

			float[,] pixel = new float[1, 5] { { 0, 0, 0, 0, 1 } };
			for (int i = 0; i < rgbValues.Length; i += 3)
			{
				pixel[0, 0] = rgbValues[i + 2] / 255f; //r
				pixel[0, 1] = rgbValues[i + 1] / 255f; //g
				pixel[0, 2] = rgbValues[i + 0] / 255f; //b
				var newPixel = Util.Multiply(pixel, Matrix);
				rgbValues[i + 2] = (byte)Math.Round(newPixel[0, 0] * 255f);
				rgbValues[i + 1] = (byte)Math.Round(newPixel[0, 1] * 255f);
				rgbValues[i + 0] = (byte)Math.Round(newPixel[0, 2] * 255f);
			}
			System.Runtime.InteropServices.Marshal.Copy(rgbValues, 0, ptr, bytes);
			b.UnlockBits(bmpData);
			this.Invalidate();
			//displayed = b;

			//this.BackgroundImage = displayed;
		}

		private static Bitmap ApplyColorMatrix(Image original, float[,] colorMatrix)
		{
			Bitmap bmp = new Bitmap(original.Width, original.Height);

			ColorMatrix matrix = new ColorMatrix(Util.ToJaggedArrays(colorMatrix));

			using (var attributes = new ImageAttributes())
			{
				//Attach matrix to image attributes
				attributes.SetColorMatrix(matrix);
				using (var g = Graphics.FromImage(bmp))
				{
					g.DrawImage(original,
						new Rectangle(0, 0, original.Width, original.Height),
						0, 0, original.Width, original.Height, GraphicsUnit.Pixel, attributes);
				}
			}
			return bmp;
		}


	}
}
