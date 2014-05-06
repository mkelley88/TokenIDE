﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Merthsoft.CalcData;
using Merthsoft.Extensions;
using Merthsoft.TokenIDE.Properties;

namespace Merthsoft.TokenIDE {
	public partial class HexSprite : Form {
		private readonly int transparentColor = Color.Transparent.ToArgb();
		private const string XLIBTILES_HEADER = "xLIBPIC";
		private const string XLIBBGPIC_HEADER = "xLIBBG ";

		public event PasteTextEventHandler PasteTextEvent;

		private enum Tool { Pencil, Flood, Line, Rectangle, RectangleFill, Ellipse, EllipseFill, Circle, CircleFill, EyeDropper, }

		private enum SaveType { Png, XLibTiles, XLibBGPicture, MonochromePic, ColorPic, ColorImage }

		public enum Palette { BlackAndWhite, BasicColors, xLIBC, Full565 };

		private Tool currentTool = Tool.Pencil;
		private ToolStripButton currentButton = null;

		private Bitmap drawCanvas;

		private int mouseX, mouseY;
		private int mouseXOld, mouseYOld;
		private MouseButtons button;
		private bool drawing;
		private int shapeX, shapeY;

		private int penWidth {
			get { return (int)penWidthBox.Value; }
			set { penWidthBox.Value = value; }
		}

		public Palette SelectedPalette {
			get { return (Palette)paletteChoice.SelectedIndex; }
			set { paletteChoice.SelectedIndex = (int)value; }
		}

		private Palette? previousPalette = null;

		private List<Tuple<Sprite, Palette>> history;
		private bool shouldPushHistory = false;
		private int historyPosition;

		private Sprite sprite;
		private Sprite previewSprite = null;

		public int SpriteWidth {
			get { return (int)spriteWidthBox.Value; }
			set {
				spriteWidthBox.Value = value;
			}
		}

		public int SpriteHeight {
			get { return (int)spriteHeightBox.Value; }
			set {
				spriteHeightBox.Value = value;
			}
		}

		public string Hex {
			get {
				return getHex();
			}
			set {
				createSpriteFromHex(value);
			}
		}

		private int pixelSize = 2;
		private bool performResizeFlag = true;
		public string OutString = "";

		private bool useGCharacter {
			get {
				return useGBox.Checked;
			}
			set {
				useGBox.Checked = true;
			}
		}

		private bool drawGrid {
			get { return drawGridBox.Checked; }
		}

		private int leftPixel = 1;
		private int rightPixel = 0;

		private List<Color> CelticPalette = Pic8xC.Palette;

		private List<Color> XLibPalette = new List<Color>();

		private List<SolidBrush> CelticBrushes = new List<SolidBrush>();
		private List<SolidBrush> XLibBrushes = new List<SolidBrush>();

		private List<Sprite> Sprites = new List<Sprite>();
		private List<Bitmap> SpriteImages = new List<Bitmap>();

		private string fileName = null;
		private int picNumber = -1;
		private SaveType saveType;

		private bool mapMode = false;

		public HexSprite(bool isMapMode = false) {
			InitializeComponent();

			mapMode = isMapMode;

			Icon = Icon.FromHandle(Properties.Resources.icon_hexsprite.GetHicon());
			outputLabel.Text = "";
			spriteIndexLabel.Visible = true;
			spriteIndexLabel.Text = "";

			sprite = new Sprite(8, 8);

			history = new List<Tuple<Sprite, Palette>>();
			historyPosition = 0;

			IntPtr iconPtr = TokenIDE.Properties.Resources.icon_hexsprite.GetHicon();
			using (Icon icon = Icon.FromHandle(iconPtr)) {
				this.Icon = icon;
			}

			generatePalettes();
			if (!isMapMode) {
				splitContainer1.Panel1Collapsed = true;
			} else {
				SelectedPalette = Palette.xLIBC;
				paletteChoice.Hide();
				useGBox.Hide();
				saveAsToolStripMenuItem.Visible = false;
				saveToolStripMenuItem.Visible = false;
				loadTemplateToolStripMenuItem.Visible = false;
				this.Text = "xLIBC Map Editor";
				openToolStripMenuItem.Text = "&Add Tiles";
				paletteBox.Hide();

				setLeftMouseButton(0);
				setRightMouseButton(0);
			}

			loadTools();

			clearHistory();
			spriteBox.Invalidate();
		}

		private void loadTools() {
			foreach (Tool t in (Tool[])Enum.GetValues(typeof(Tool))) {
				ToolStripButton toolButton = new ToolStripButton();
				string toolName = t.ToString();
				toolButton.Text = toolName;
				toolButton.Image = (Image)Resources.ResourceManager.GetObject("icon_" + toolName.ToLower());
				toolButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
				if (toolButton.Image == null) {
					toolButton.Image = (Image)Resources.ResourceManager.GetObject("icon_blank");
					toolButton.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
				}
				toolButton.CheckOnClick = true;
				toolButton.Click += new EventHandler(toolButton_Click);
				toolButton.ImageScaling = ToolStripItemImageScaling.None;
				toolButton.Tag = t;

				mainToolStrip.Items.Add(toolButton);

				if (t == Tool.Pencil) {
					toolButton.Checked = true;
					currentButton = toolButton;
				}
			}
		}

		private void generatePalettes() {
			if (!mapMode) {
				CelticPalette.ForEach(c => {
					SolidBrush brush = new SolidBrush(c);
					CelticBrushes.Add(brush);
				});
			}

			for (int i = 0; i < 256; i++) {
				Color color = MerthsoftExtensions.ColorFrom8BitHLRGB(i);
				XLibPalette.Add(color);
				XLibBrushes.Add(new SolidBrush(color));
			}

			foreach (string palette in Enum.GetNames(typeof(Palette))) {
				if (palette == "Map") { continue; }
				paletteChoice.Items.Add(palette);
			}
		}

		private void toolButton_Click(object sender, EventArgs e) {
			ToolStripButton button = (ToolStripButton)sender;
			currentButton.Checked = false;
			button.Checked = true;
			currentButton = button;
			currentTool = (Tool)button.Tag;
		}

		private void createSpriteFromHex(string hex) {
			//performResizeFlag = false;
			string widthString;
			int width;
			do {
				widthString = InputBox.Show("Sprite width:", SpriteWidth.ToString());
				if (widthString == null) {
					return;
				}
			} while (!int.TryParse(widthString, out width));
			SpriteWidth = width;
			int height;
			Sprite newSprite;
			switch (SelectedPalette) {
				case Palette.BlackAndWhite:
					newSprite = new Sprite(hex, SpriteWidth, out height, 1);
					break;

				case Palette.BasicColors:
					newSprite = new Sprite(hex, SpriteWidth, out height, CelticPalette.Count / 4);
					break;

				default:
					throw new Exception("Can only create sprite from hex in Black and White or Celtic palette modes.");
			}
			SpriteHeight = height;
			//performResizeFlag = true;
			sprite = newSprite;
			spriteBox.Invalidate();

			if (hex.Contains("G")) {
				useGCharacter = true;
			}
		}

		private void Width_ValueChanged(object sender, EventArgs e) {
			if (!performResizeFlag)
				return;
			if (MaintainDim.Checked) {
				int delta = SpriteWidth - (int)spriteWidthBox.Value;
				performResizeFlag = false;
				spriteHeightBox.Value = SpriteHeight + delta;
				performResizeFlag = true;
			}
			resizeSprite((int)spriteWidthBox.Value, (int)spriteHeightBox.Value);
		}

		private void Height_ValueChanged(object sender, EventArgs e) {
			if (!performResizeFlag)
				return;
			if (MaintainDim.Checked) {
				int delta = SpriteHeight - (int)spriteHeightBox.Value;
				performResizeFlag = false;
				spriteWidthBox.Value = SpriteWidth + delta;
				performResizeFlag = true;
			}

			resizeSprite((int)spriteWidthBox.Value, (int)spriteHeightBox.Value);
		}

		private void resizeSprite(int newW, int newH) {
			if (sprite == null || !performResizeFlag)
				return;
			if (sprite != null) {
				pushHistory();
			}

			var shouldPushBackup = shouldPushHistory;
			shouldPushHistory = false;
			SpriteWidth = newW;
			SpriteHeight = newH;
			shouldPushHistory = shouldPushBackup;

			sprite.Resize(newW, newH);
			spriteBox.Invalidate();
		}

		private void spriteBox_Paint(object sender, PaintEventArgs e) {
			int realPixelSize = pixelSize;
			if (mapMode) {
				realPixelSize *= 8;
			}

			spriteBox.Width = SpriteWidth * realPixelSize;
			spriteBox.Height = SpriteHeight * realPixelSize;

			//try {
			if (drawCanvas == null || drawCanvas.Width != SpriteWidth || drawCanvas.Height != SpriteHeight) {
				if (!mapMode) {
					drawCanvas = new Bitmap(SpriteWidth, SpriteHeight);
				} else {
					drawCanvas = new Bitmap(SpriteWidth*8, SpriteHeight*8);
				}
				sprite.Invalidate();
			}

			lock (drawCanvas) {
				drawSprite(drawCanvas, sprite);

				if (previewSprite != null) {
					drawSprite(drawCanvas, previewSprite, false);
				}
			}

			e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
			e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
			e.Graphics.DrawImage(drawCanvas, 0, 0, spriteBox.Width, spriteBox.Height);

			if (drawGrid && realPixelSize > 1) {
				using (Pen smallGridPen = new Pen(Color.DarkGray)) {
					smallGridPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;
					for (int j = 0; j < SpriteHeight; j++) {
						for (int i = 0; i < SpriteWidth; i++) {
							Rectangle grid = new Rectangle(i * realPixelSize, j * realPixelSize, realPixelSize, realPixelSize);
							e.Graphics.DrawRectangle(smallGridPen, grid);
						}
					}
				}

				if (!mapMode) {
					using (Pen largeGridPen = new Pen(Color.Black)) {
						largeGridPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;
						for (int j = 0; j < SpriteHeight; j += 8) {
							for (int i = 0; i < SpriteWidth; i += 8) {
								Rectangle grid = new Rectangle(i * realPixelSize, j * realPixelSize, realPixelSize * 8, realPixelSize * 8);
								e.Graphics.DrawRectangle(largeGridPen, grid);
							}
						}
					}
				}
			}
			//} catch {
			//	throw;
			//}
		}

		private void drawSprite(Bitmap b, Sprite spriteToUse, bool clearDirty = true, Palette? palette = null, bool dontUseMapMode = false) {
			Rectangle drawBounds = spriteToUse.DirtyRectangle;

			if (drawBounds == Rectangle.Empty) { return; }

			if (mapMode && !dontUseMapMode && Sprites.Count == 0) { return; }

			Palette realPalette = palette ?? SelectedPalette;

			Rectangle lockRect = drawBounds;
			if (mapMode && !dontUseMapMode) {
				lockRect = new Rectangle(lockRect.X * 8, lockRect.Y * 8, lockRect.Width * 8, lockRect.Height * 8);
			}

			BitmapData data = b.LockBits(lockRect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
			int stride = data.Stride;
			int[] dataToCopy = new int[data.Height * data.Stride / 4];
			Marshal.Copy(data.Scan0, dataToCopy, 0, dataToCopy.Length);
			int skippedColor = realPalette == Palette.Full565 ? transparentColor : -1;
			Rectangle tileRect = new Rectangle(0, 0, 8, 8);
			for (int j = 0; j < drawBounds.Height; j++) {
				for (int i = 0; i < drawBounds.Width; i++) {
					int paletteIndex = spriteToUse[i + drawBounds.X, j + drawBounds.Y];
					if (paletteIndex == skippedColor) { continue; }

					if (!mapMode || dontUseMapMode) {
						Color drawColor = Color.White;
						switch (realPalette) {
							case Palette.BlackAndWhite:
								drawColor = paletteIndex == 0 ? Color.White : Color.Black;
								break;
							case Palette.BasicColors:
								drawColor = CelticPalette[paletteIndex];
								break;
							case Palette.xLIBC:
								drawColor = XLibPalette[paletteIndex];
								break;
							case Palette.Full565:
								drawColor = Color.FromArgb(paletteIndex);
								break;
							default:
								break;
						}

						dataToCopy[i + j * data.Stride / 4] = drawColor.ToArgb();
					} else {
						Bitmap tile = SpriteImages[paletteIndex];
						BitmapData tileData = tile.LockBits(tileRect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
						int[] tileDataToCopy = new int[tileData.Height * tileData.Stride / 4];
						Marshal.Copy(tileData.Scan0, tileDataToCopy, 0, tileDataToCopy.Length);
						int index = 0;
						for (int y = 0; y < 8; y++) {
							for (int x = 0; x < 8; x++) {
								dataToCopy[(i * 8 + x) + (j * 8 + y) * data.Stride / 4] = tileDataToCopy[index++];
							}
						}
						tile.UnlockBits(tileData);
					}
				}
			}
			Marshal.Copy(dataToCopy, 0, data.Scan0, dataToCopy.Length);
			b.UnlockBits(data);

			sprite.ClearDirtyRectangle();
		}

		private void handleMouse(MouseEventArgs e) {
			button = e.Button;
			mouseXOld = mouseX;
			mouseYOld = mouseY;
			mouseX = e.X / pixelSize;
			mouseY = e.Y / pixelSize;

			if (mapMode) {
				mouseX /= 8;
				mouseY /= 8;
			}

			if (previewSprite != null) {
				sprite.DirtyRectangle = previewSprite.DirtyRectangle;
			}

			int pixelColor = -1;

			switch (button) {
				case MouseButtons.Left:
					pixelColor = leftPixel;
					break;

				case MouseButtons.Right:
					pixelColor = rightPixel;
					break;
			}

			switch (currentTool) {
				case Tool.Pencil:
					if (button != System.Windows.Forms.MouseButtons.None) {
						sprite.DrawLine(mouseXOld, mouseYOld, mouseX, mouseY, pixelColor, penWidth);
					}
					break;

				case Tool.Flood:
					if (button != System.Windows.Forms.MouseButtons.None) {
						if (MerthsoftExtensions.IsShiftDown) {
							sprite.ReplaceAll(mouseX, mouseY, pixelColor);
						} else {
							sprite.FloodFill(mouseX, mouseY, pixelColor);
						}
					}
					break;

				case Tool.Line:
					if (!drawing) {
						shapeX = mouseX;
						shapeY = mouseY;
						drawing = true;
					}
					if (button == System.Windows.Forms.MouseButtons.None) {
						copyPreviewSprite();
						previewSprite = null;
						drawing = false;
					} else {
						createPreviewSprite();
						previewSprite.DrawLine(shapeX, shapeY, mouseX, mouseY, pixelColor, penWidth);
					}
					break;

				case Tool.Rectangle:
				case Tool.RectangleFill:
					if (!drawing) {
						shapeX = mouseX;
						shapeY = mouseY;
						drawing = true;
					}
					if (button == System.Windows.Forms.MouseButtons.None) {
						copyPreviewSprite();
						previewSprite = null;
						drawing = false;
					} else {
						createPreviewSprite();
						if (MerthsoftExtensions.IsShiftDown) {
							mouseY = shapeY + mouseX - shapeX;
						}
						previewSprite.DrawRectangle(shapeX, shapeY, mouseX, mouseY, pixelColor, penWidth, currentTool == Tool.RectangleFill);
					}
					break;

				case Tool.Ellipse:
				case Tool.EllipseFill:
					if (!drawing) {
						shapeX = mouseX;
						shapeY = mouseY;
						drawing = true;
					}
					if (button == System.Windows.Forms.MouseButtons.None) {
						copyPreviewSprite();
						previewSprite = null;
						drawing = false;
					} else {
						createPreviewSprite();
						if (MerthsoftExtensions.IsShiftDown) {
							mouseY = shapeY + mouseX - shapeX;
						}
						previewSprite.DrawEllipse(shapeX, shapeY, mouseX, mouseY, pixelColor, penWidth, currentTool == Tool.EllipseFill);
					}
					break;

				case Tool.Circle:
				case Tool.CircleFill:
					if (!drawing) {
						shapeX = mouseX;
						shapeY = mouseY;
						drawing = true;
					}
					if (button == System.Windows.Forms.MouseButtons.None) {
						copyPreviewSprite();
						previewSprite = null;
						drawing = false;
					} else {
						createPreviewSprite();
						int radius = (int)Math.Sqrt((shapeX - mouseX) * (shapeX - mouseX) + (shapeY - mouseY) * (shapeY - mouseY));
						previewSprite.DrawCircle(shapeX, shapeY, radius, pixelColor, penWidth, currentTool == Tool.CircleFill);
					}
					break;

				case Tool.EyeDropper:
					if (SelectedPalette != Palette.BlackAndWhite && mouseX >= 0 && mouseY >= 0 && mouseX < SpriteWidth && mouseY < SpriteHeight) {
						if (button == System.Windows.Forms.MouseButtons.Left) {
							setLeftMouseButton(sprite[mouseX, mouseY]);
						} else if (button == System.Windows.Forms.MouseButtons.Right) {
							setRightMouseButton(sprite[mouseX, mouseY]);
						}
					}
					break;

				default:
					break;
			}

			spriteBox.Invalidate();
		}

		private void createPreviewSprite() {
			Rectangle drawRect = new Rectangle(0, 0, SpriteWidth, SpriteHeight);
			Rectangle oldRectangle = Rectangle.Empty;

			if (previewSprite != null) {
				oldRectangle = previewSprite.DirtyRectangle;
				drawRect = oldRectangle;
			}

			previewSprite = new Sprite(SpriteWidth, SpriteHeight);
			int defaultColor = SelectedPalette == Palette.Full565 ? transparentColor : -1;
			for (int j = drawRect.Y; j < drawRect.Y + drawRect.Height; j++) {
				for (int i = drawRect.X; i < drawRect.X + drawRect.Width; i++) {
					previewSprite[i, j] = defaultColor;
				}
			}

			previewSprite.ClearDirtyRectangle();
			if (oldRectangle != Rectangle.Empty) {
				previewSprite.DirtyRectangle = oldRectangle;
			}
		}

		private void copyPreviewSprite() {
			Rectangle drawRect = previewSprite.DirtyRectangle;
			int skippedColor = SelectedPalette == Palette.Full565 ? transparentColor : -1;
			for (int j = drawRect.Y; j < drawRect.Y + drawRect.Height; j++) {
				for (int i = drawRect.X; i < drawRect.X + drawRect.Width; i++) {
					if (previewSprite[i, j] != skippedColor) {
						sprite[i, j] = previewSprite[i, j];
					}
				}
			}
		}

		private void spriteBox_MouseMove(object sender, MouseEventArgs e) {
			if (e.Button != MouseButtons.None) {
				handleMouse(e);
			}

			setSpriteIndexText(e.X, e.Y);
		}

		private void setSpriteIndexText(int x, int y) {
			spriteIndexLabel.Visible = true;
			spriteIndexLabel.Text = string.Format("({0}, {1}) - {2} (0x{2:X2})", x / pixelSize, y / pixelSize, 8 * (x / pixelSize / 8) + y / pixelSize / 8);
		}

		private void spriteBox_MouseLeave(object sender, EventArgs e) {
			spriteBox.Invalidate();
			spriteIndexLabel.Visible = false;
		}

		private void spriteBox_MouseDown(object sender, MouseEventArgs e) {
			pushHistory();

			mouseX = e.X / pixelSize;
			mouseY = e.Y / pixelSize; 
			if (mapMode) {
				mouseX /= 8;
				mouseY /= 8;
			}
			handleMouse(e);
		}

		private void clearHistory() {
			history.Clear();
			historyPosition = 0;
		}

		private void pushHistory(Palette? palette = null) {
			if (shouldPushHistory == false) { return; }
			if (sprite == null || previousPalette == null) { return; }
			if (historyPosition != history.Count) {
				history.RemoveRange(historyPosition, history.Count - historyPosition);
			}
			history.Add(Tuple.Create(sprite.Copy(), palette ?? SelectedPalette));
			historyPosition = history.Count;
			toggleRedo(false);
			toggleUndo(true);
		}

		private void spriteBox_MouseUp(object sender, MouseEventArgs e) {
			MouseEventArgs ne = new MouseEventArgs(MouseButtons.None, e.Clicks, e.X, e.Y, e.Delta);
			handleMouse(ne);
		}

		private void PixelSize_ValueChanged(object sender, EventArgs e) {
			pixelSize = (int)pixelSizeBox.Value;
			spriteBox.Invalidate();
		}

		private void DrawGrid_CheckedChanged(object sender, EventArgs e) {
			spriteBox.Invalidate();
		}

		private string getHex() {
			StringBuilder bin = new StringBuilder();
			for (int j = 0; j < SpriteHeight; j++) {
				string t = "";
				StringBuilder line = new StringBuilder();
				for (int i = 0; i < SpriteWidth; i++) {
					if (i % 8 == 0) {
						t += ",%";
					}
					t += sprite[i, j].ToString();

					switch (SelectedPalette) {
						case Palette.BlackAndWhite:
							line.Append(sprite[i, j].ToString());
							break;

						case Palette.BasicColors:
							line.Append(sprite[i, j].ToString("X1"));
							break;

						case Palette.xLIBC:
							line.Append(sprite[i, j].ToString("X2"));
							break;

						default:
							break;
					}
				}

				if (SelectedPalette == Palette.BlackAndWhite) {
					line = new StringBuilder(HexHelper.BinToHex(line.ToString()));
				}
				// Try to backtrack and add G
				if (useGCharacter) {
					int lineWidth = line.Length;
					if (line[lineWidth - 1] == '0') {
						int zeroCount = 1;
						for (int k = lineWidth - 2; k >= 0; k--) {
							if (line[k] == '0') {
								zeroCount++;
							} else {
								break;
							}
						}

						if (zeroCount > 1) {
							line.Remove(lineWidth - zeroCount, zeroCount);
							line.Append('G');
						}
					}
				}

				bin.Append(line);
			}

			return bin.ToString();
		}

		private void paletteBox_Paint(object sender, PaintEventArgs e) {
			Graphics g = e.Graphics;
			drawPalette(g);
		}

		private double getBrightness(Color c) {
			double red = c.R;
			double green = c.G;
			double blue = c.B;

			return red * 0.299 + green * 0.587 + blue * 0.114;
		}

		private void drawPalette(Graphics g) {
			int boxWidth;
			int boxHeight;
			int colorCount;
			int maxWidth;

			if (SelectedPalette == Palette.Full565) {
				g.DrawImage(Resources._565palette, 0, 0);
				return;
			}

			if (SelectedPalette == Palette.BasicColors) {
				boxWidth = 44;
				boxHeight = 44;
				colorCount = CelticPalette.Count;
				maxWidth = 352;
			} else {
				boxWidth = 11;
				boxHeight = 11;
				colorCount = 256;
				maxWidth = 352;
			}

			int paletteX = 0;
			int paletteY = 0;

			try {
				for (int colorIndex = 0; colorIndex < colorCount; colorIndex++) {
					if (SelectedPalette == Palette.BasicColors) {
						Color c = CelticPalette[colorIndex];
						g.FillRect(CelticBrushes[colorIndex], paletteX, paletteY, paletteX + boxWidth, paletteY + boxHeight);
						g.DrawRect(Pens.Black, paletteX, paletteY, paletteX + boxWidth, paletteY + boxHeight);
					} else {
						SolidBrush brush = XLibBrushes[colorIndex];
						g.FillRect(brush, paletteX, paletteY, paletteX + boxWidth, paletteY + boxHeight);
					}

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

		private void paletteChoice_SelectedIndexChanged(object sender, EventArgs e) {
			if (SelectedPalette == previousPalette) { return; }

			pushHistory(previousPalette);
			
			switch (SelectedPalette) {
				case Palette.BlackAndWhite:
					togglePalette(false);
					toggleHexOutput(true);

					setLeftMouseButton(1);
					setRightMouseButton(0);
					break;

				case Palette.BasicColors:
					togglePalette(true);
					toggleHexOutput(true);

					setLeftMouseButton(1);
					setRightMouseButton(0);
					break;

				case Palette.xLIBC:
					togglePalette(true);
					toggleHexOutput(false);

					setLeftMouseButton(0);
					setRightMouseButton(0xFF);
					break;

				case Palette.Full565:
					togglePalette(true);
					toggleHexOutput(false);

					setLeftMouseButton(-16777216);
					setRightMouseButton(-1);
					break;
			}

			// If you're changing palettes, just give up for now
			// [TODO] Make this smarter?
			if (sprite != null) {
				using (Bitmap b = new Bitmap(SpriteWidth, SpriteHeight)) {
					shouldPushHistory = false;
					sprite.DirtyRectangle = new Rectangle(0, 0, SpriteWidth, SpriteHeight);
					drawSprite(b, sprite, palette: previousPalette);
					loadImage(b);
					shouldPushHistory = true;
				}
			}

			previousPalette = SelectedPalette;
			sprite.Invalidate();
			spriteBox.Invalidate();
		}

		private void toggleHexOutput(bool enabled) {
			useGBox.Enabled = enabled;
			insertAndExitToolStripMenuItem.Enabled = enabled;
			copyToolStripMenuItem.Enabled = enabled;
		}

		private void togglePalette(bool enabled) {
			paletteBox.Visible = enabled;
			if (enabled) {
				paletteBox.Invalidate();
				leftMousePictureBox.Invalidate();
				rightMousePictureBox.Invalidate();
			}
		}

		private void paletteBox_MouseClick(object sender, MouseEventArgs e) {
			selectPalette(e);
		}

		private void paletteBox_MouseMove(object sender, MouseEventArgs e) {
			if (e.Button != System.Windows.Forms.MouseButtons.None) {
				selectPalette(e);
			}
		}

		private void selectPalette(MouseEventArgs e) {
			if (e.X < 0 || e.Y < 0 || e.X >= paletteBox.Width || e.Y >= paletteBox.Height) {
				return;
			}
			int boxWidth;
			int boxHeight;
			int maxWidth;

			if (SelectedPalette == Palette.BasicColors) {
				boxWidth = 44;
				boxHeight = 44;
				maxWidth = 352;
			} else {
				boxWidth = 11;
				boxHeight = 11;
				maxWidth = 352;
			}


			int paletteIndex;
			if (SelectedPalette == Palette.Full565) {
				paletteIndex = Resources._565palette.GetPixel(e.X, e.Y).ToArgb();
			} else {
				paletteIndex = (e.X / boxWidth) + (maxWidth / boxWidth) * (e.Y / boxHeight);
			}

			if (e.Button == System.Windows.Forms.MouseButtons.Left || SelectedPalette == Palette.BlackAndWhite) {
				setLeftMouseButton(paletteIndex);
			} else if (e.Button == System.Windows.Forms.MouseButtons.Right) {
				setRightMouseButton(paletteIndex);
			}
		}

		private void setRightMouseButton(int paletteIndex) {
			rightPixel = paletteIndex;
			rightMousePictureBox.Invalidate();
			if (SelectedPalette == Palette.Full565 || SelectedPalette == Palette.BlackAndWhite) {
				rightMouseLabel.Text = string.Format("Right");
			} else {
				rightMouseLabel.Text = string.Format("Right: ({0:X2})", paletteIndex);
			}
		}

		private void setLeftMouseButton(int paletteIndex) {
			leftPixel = paletteIndex;
			leftMousePictureBox.Invalidate();
			if (SelectedPalette == Palette.Full565 || SelectedPalette == Palette.BlackAndWhite) {
				leftMouseLabel.Text = string.Format("Left");
			} else {
				leftMouseLabel.Text = string.Format("Left: ({0:X2})", paletteIndex);
			}
		}

		public void Open(string fileName) {
			Cursor c = Cursor;
			Cursor = Cursors.WaitCursor;
			pushHistory();
			FileInfo f = new FileInfo(fileName);
			string extension = f.Extension.ToLower();
			switch (extension) {
				case ".8xv":
					openAppVar(fileName);
					break;
				case ".8xi":
					openMonochromePic(fileName);
					break;
				case ".8ci":
					openColorPic(fileName);
					break;
				case ".8ca":
					openColorImage(fileName);
					break;
				default:
					openBitmap(fileName);
					break;
			}
			spriteBox.Invalidate();
			leftMousePictureBox.Invalidate();
			rightMousePictureBox.Invalidate();
			Cursor = c;
		}

		private void openBitmap(string fileName) {
			using (Bitmap image = new Bitmap(fileName)) {
				loadImage(image);
			}
		}

		private void loadImage(Bitmap image) {
			spriteWidthBox.Value = image.Width;
			spriteHeightBox.Value = image.Height;
			if ((int)SelectedPalette == -1) {
				SelectedPalette = Palette.Full565;
			}

			switch (SelectedPalette) {
				case Palette.BlackAndWhite:
					image.PosterizeImage();
					sprite = new Sprite(image, new List<Color>() { Color.White, Color.Black });
					break;

				case Palette.BasicColors:
					sprite = new Sprite(image, CelticPalette, 0);
					break;

				case Palette.xLIBC:
					sprite = new Sprite(image, XLibPalette);
					break;

				case Palette.Full565:
					sprite = new Sprite(image);
					break;
			}

			saveType = SaveType.Png;
		}

		private void openAppVar(string fileName) {
			AppVar8x appVar = null;
			using (FileStream pstream = new FileStream(fileName, FileMode.Open))
			using (BinaryReader preader = new BinaryReader(pstream)) {
				appVar = new AppVar8x(preader);
			}

			string headerString = Encoding.ASCII.GetString(appVar.Data.Take(7).ToArray());
			if (headerString == XLIBTILES_HEADER) {
				saveType = SaveType.XLibTiles;
				openxLibTiles(appVar);
			} else if (headerString == XLIBBGPIC_HEADER) {
				saveType = SaveType.XLibBGPicture;
				openxLibBG(appVar);
			}
		}

		private void openMonochromePic(string fileName) {
			Pic8x pic = null;
			using (FileStream pstream = new FileStream(fileName, FileMode.Open))
			using (BinaryReader preader = new BinaryReader(pstream)) {
				pic = new Pic8x(preader);
			}
			picNumber = pic.PicNumber;

			SelectedPalette = Palette.BlackAndWhite;
			using (Bitmap b = pic.GetBitmap()) {
				loadImage(b);
			}
			saveType = SaveType.MonochromePic;
		}

		private void openColorPic(string fileName) {
			Pic8xC pic = null;
			using (FileStream pstream = new FileStream(fileName, FileMode.Open))
			using (BinaryReader preader = new BinaryReader(pstream)) {
				pic = new Pic8xC(preader);
			}
			picNumber = pic.PicNumber;

			SelectedPalette = Palette.BasicColors;
			using (Bitmap b = pic.GetBitmap()) {
				loadImage(b);
			}
			saveType = SaveType.ColorPic;
		}

		private void openColorImage(string fileName) {
			Image8xC pic = null;
			using (FileStream pstream = new FileStream(fileName, FileMode.Open))
			using (BinaryReader preader = new BinaryReader(pstream)) {
				pic = new Image8xC(preader);
			}
			picNumber = pic.PicNumber;

			SelectedPalette = Palette.Full565;
			using (Bitmap b = pic.GetBitmap()) {
				loadImage(b);
			}
			saveType = SaveType.ColorImage;
		}

		private void openxLibBG(AppVar8x appVar) {
			SpriteWidth = 80;
			SpriteHeight = 60;

			sprite = new Sprite(SpriteWidth, SpriteHeight);

			SelectedPalette = Palette.xLIBC;

			int x = 0;
			int y = 0;
			for (int i = 7; i < appVar.Data.Length; i++) {
				sprite[x, y] = appVar.Data[i];
				y++;
				if (y == 60) {
					y = 0;
					x++;
				}
			}
		}

		private void openxLibTiles(AppVar8x appVar) {
			int dataLength = appVar.Data.Length;

			if (!mapMode) {
				SelectedPalette = Palette.xLIBC;
				SpriteWidth = 128;
				SpriteHeight = 64;
				sprite = loadTiles(appVar, dataLength);
			} else {
				loadTiles(appVar, dataLength);
				sprite.DirtyRectangle = new Rectangle(0, 0, sprite.Width, sprite.Height);
			}
		}

		private Sprite loadTiles(AppVar8x appVar, int dataLength) {
			Sprite newSprite = new Sprite(128, 64);

			int x = 0;
			int y = 0;
			int spriteX = 0;
			int spriteY = 0;

			Sprite subSprite = new Sprite(8, 8);
			for (int i = 7; i < dataLength; i++) {
				newSprite[x + spriteX, y + spriteY] = appVar.Data[i];
				if (mapMode) {
					subSprite[spriteX, spriteY] = appVar.Data[i];
				}
				spriteY++;
				if (spriteY == 8) {
					spriteY = 0;
					spriteX++;
				}
				if (spriteX == 8) {
					if (mapMode) {
						Bitmap b = new Bitmap(8, 8);
						drawSprite(b, subSprite, palette: Palette.xLIBC, dontUseMapMode: true);
						SpriteImages.Add(b);
						b = ResizeBitmap(b, 32, 32);
						Sprites.Add(subSprite);

						//tilesList.LargeImageList.Images.Add(ResizeBitmap(b, 64, 64));
						//ListViewItem lvi = new ListViewItem("", SpriteImages.Count-1);
						//tilesList.Items.Add(lvi);

						PictureBox tile = new PictureBox() { Image = b, Size = new Size(32, 32), Tag = Sprites.Count - 1, };
						tile.MouseClick += tile_MouseClick;
						tilesFlow.Controls.Add(tile);
						subSprite = new Sprite(8, 8);
					}
					spriteX = 0;
					spriteY = 0;
					y += 8;
					if (y == 64) {
						y = 0;
						x += 8;
					}
				}
			}

			return newSprite;
		}

		private Bitmap ResizeBitmap(Bitmap b, int nWidth, int nHeight) {
			Bitmap result = new Bitmap(nWidth, nHeight);
			using (Graphics g = Graphics.FromImage((Image)result)) {
				g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
				g.DrawImage(b, 0, 0, nWidth, nHeight);
			}
			return result;
		}

		private void openToolStripButton_Click(object sender, EventArgs e) {
			OpenFileDialog f = new OpenFileDialog();
			f.AddFilter("xLibC AppVars", "*.8xv", "*.8cv");
			if (!mapMode) {
				f.AddFilter("Readable image files", "*.bmp", "*.png", "*.jpg", "*.jpeg", "*.gif", "*.8xv", "*.8cv", "*.8xi", "*.8ci", "*.8ca");
				f.AddFilter("Image files", "*.bmp", "*.png", "*.jpg", "*.jpeg", "*.gif");
				f.AddFilter("Monochrome Pic files", "*.8xi");
				f.AddFilter("Color Pic files", "*.8ci", "*.8ca");
			}
			if (f.ShowDialog() != System.Windows.Forms.DialogResult.OK) { return; }
			Open(f.FileName);
			fileName = f.FileName;
		}

		private void undoButton_Click(object sender, EventArgs e) {
			undo();
		}

		private void redoButton_Click(object sender, EventArgs e) {
			redo();
		}

		private void undo() {
			if (historyPosition == history.Count) {
				//history.Add(copySpriteArray());
				history.Add(Tuple.Create(sprite.Copy(), SelectedPalette));
			}

			copySpriteFromHistory(--historyPosition);

			if (historyPosition == 0) {
				toggleUndo(false);
			}
			toggleRedo(true);

			sprite.DirtyRectangle = new Rectangle(0, 0, sprite.Width, sprite.Height);
			spriteBox.Invalidate();
		}

		private void copySpriteFromHistory(int position) {
			shouldPushHistory = false;
			performResizeFlag = false;

			var historyItem = history[position];
			SelectedPalette = historyItem.Item2;
			sprite = historyItem.Item1;

			SpriteHeight = sprite.Height;
			SpriteWidth = sprite.Width;
			shouldPushHistory = true;
			performResizeFlag = true;
		}

		private void redo() {
			copySpriteFromHistory(++historyPosition);
			if (historyPosition + 1 == history.Count) {
				toggleRedo(false);
			}
			toggleUndo(true);

			sprite.DirtyRectangle = new Rectangle(0, 0, sprite.Width, sprite.Height);
			spriteBox.Invalidate();
		}

		private void toggleUndo(bool enabled) {
			undoButton.Enabled = enabled;
			undoToolStripMenuItem.Enabled = enabled;
		}

		private void toggleRedo(bool enabled) {
			redoButton.Enabled = enabled;
			redoToolStripMenuItem.Enabled = enabled;
		}

		private void leftMousePictureBox_Paint(object sender, PaintEventArgs e) {
			Brush brush = null;
			bool dispose = false;

			if (mapMode) {
				if (Sprites.Count > 0) {
					e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
					e.Graphics.DrawImage(SpriteImages[leftPixel], 0, 0, 32, 32);
				}
				return;
			}

			switch (SelectedPalette) {
				case Palette.BlackAndWhite:
					brush = Brushes.Black;
					break;
				case Palette.BasicColors:
					brush = CelticBrushes[leftPixel];
					break;
				case Palette.xLIBC:
					brush = XLibBrushes[leftPixel];
					break;
				case Palette.Full565:
					brush = new SolidBrush(Color.FromArgb(leftPixel | (0xFF << 24)));
					dispose = true;
					break;
			}

			if (brush != null) {
				e.Graphics.FillRectangle(brush, e.ClipRectangle);
				if (dispose) { brush.Dispose(); }
			}
		}

		private void rightMousePictureBox_Paint(object sender, PaintEventArgs e) {
			Brush brush = null;
			bool dispose = false;

			if (mapMode) {
				if (Sprites.Count > 0) {
					e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
					e.Graphics.DrawImage(SpriteImages[rightPixel], 0, 0, 32, 32);
				}
				return;
			}

			switch (SelectedPalette) {
				case Palette.BlackAndWhite:
					brush = Brushes.White;
					break;
				case Palette.BasicColors:
					brush = CelticBrushes[rightPixel];
					break;
				case Palette.xLIBC:
					brush = XLibBrushes[rightPixel];
					break;
				case Palette.Full565:
					brush = new SolidBrush(Color.FromArgb(rightPixel | (0xFF << 24)));
					dispose = true;
					break;
			}

			if (brush != null) {
				e.Graphics.FillRectangle(brush, e.ClipRectangle);
				if (dispose) { brush.Dispose(); }
			}
		}

		private bool saveDialog() {
			SaveFileDialog sfd = new SaveFileDialog();
			sfd.AddFilter("PNG", "*.png");
			sfd.AddFilter("xLIB Tiles", "*.8xv");
			sfd.AddFilter("xLIB Picture", "*.8xv");
			sfd.AddFilter("Monochrome Pic", "*.8xi");
			sfd.AddFilter("Color Pic", "*.8ci");
			sfd.AddFilter("Color Image", "*.8ca");
			if (fileName != null) {
				sfd.FileName = new FileInfo(fileName).GetFileName();
			}

			if (sfd.ShowDialog() != System.Windows.Forms.DialogResult.OK) { return false; }

			fileName = sfd.FileName;
			saveType = (SaveType)(sfd.FilterIndex - 1);
			return true;
		}

		private void saveToolStripMenuItem_Click(object sender, EventArgs e) {
			if (fileName == null && !saveDialog()) { return; }
			saveFile();
		}

		private void saveAsToolStripMenuItem_Click(object sender, EventArgs e) {
			if (!saveDialog()) { return; }
			saveFile(true);
		}

		private void saveFile(bool saveAs = false) {
			bool success = false;

			switch (saveType) {
				case SaveType.XLibTiles:
					success = saveXLibTiles();
					break;

				case SaveType.XLibBGPicture:
					success = saveXLibBGPic();
					break;

				case SaveType.MonochromePic:
					success = saveMonochromePic(saveAs);
					break;

				case SaveType.Png:
					success = savePng();
					break;

				case SaveType.ColorPic:
					success = saveColorPic(saveAs);
					break;

				case SaveType.ColorImage:
					success = saveColorImage(saveAs);
					break;
			}

			if (success) {
				outputLabel.Text = "File saved.";
			} else {
				outputLabel.Text = "Failed to save image.";
			}
		}

		private bool saveMonochromePic(bool saveAs = false) {
			if (SelectedPalette != Palette.BlackAndWhite) {
				var res = MessageBox.Show("You cannot save a pic file using a color palette.", "Wrong Palette", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return false;
			}
			if (sprite.Width != 96 || sprite.Height != 64) {
				var res = MessageBox.Show("Pic files should be 96 wide by 64 tall. Are you sure you want to continue?", "Wrong Dimensions", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
				if (res == System.Windows.Forms.DialogResult.No) { return false; }
			}

			if (!getPicNumber(saveAs)) {
				return false;
			}

			Pic8x picture = new Pic8x((byte)(picNumber));
			int dataSize = sprite.Width * sprite.Height / 8;
			byte[] data = new byte[dataSize];
			byte b = 0;
			int index = 0;
			int bit = 7;
			for (int j = 0; j < sprite.Height; j++) {
				for (int i = 0; i < sprite.Width; i++) {
					int val = sprite[i, j];
					b |= (byte)(val << bit--);
					if (bit == -1) {
						bit = 7;
						data[index++] = b;
						b = 0;
					}
				}
			}
			picture.SetData(new object[] { dataSize.ToString(), data });
			StreamWriter s = new StreamWriter(fileName);
			picture.Save(new BinaryWriter(s.BaseStream));
			s.Close();

			return true;
		}

		private bool saveColorPic(bool saveAs = false) {
			if (SelectedPalette != Palette.BasicColors) {
				var res = MessageBox.Show("You cannot save a color pic file using a palette other than CelticIICSE.", "Wrong Palette", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return false;
			}
			if (sprite.Width != 265 || sprite.Height != 165) {
				var res = MessageBox.Show("Pic files should be 265 wide by 165 tall. Are you sure you want to continue?", "Wrong Dimensions", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
				if (res == System.Windows.Forms.DialogResult.No) { return false; }
			}

			if (!getPicNumber(saveAs)) {
				return false;
			}

			Pic8xC picture = new Pic8xC((byte)(picNumber));
			int dataSize = sprite.Width * sprite.Height / 2;
			byte[] data = new byte[dataSize];

			int i = 0; int j = 0;
			for (int index = 0; index < dataSize; index++) {
				byte b = (byte)(sprite[i, j] << 4);
				if (i + 1 < sprite.Width) {
					b |= (byte)sprite[i + 1, j];
				}
				data[index] = b;
				i += 2;
				if (i > sprite.Width) {
					i = 0;
					j++;
				}
			}

			picture.SetData(new object[] { dataSize.ToString(), data });
			StreamWriter s = new StreamWriter(fileName);
			picture.Save(new BinaryWriter(s.BaseStream));
			s.Close();

			return true;
		}

		private bool saveColorImage(bool saveAs = false) {
			if (SelectedPalette != Palette.Full565) {
				var res = MessageBox.Show("You cannot save a color image file using a palette other than Full565.", "Wrong Palette", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return false;
			}
			if (sprite.Width != 133 || sprite.Height != 83) {
				var res = MessageBox.Show("Pic files should be 133 wide by 83 tall. Are you sure you want to continue?", "Wrong Dimensions", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
				if (res == System.Windows.Forms.DialogResult.No) { return false; }
			}

			if (!getPicNumber(saveAs, "image")) {
				return false;
			}

			Image8xC picture = new Image8xC((byte)(picNumber));
			int dataSize = (sprite.Width+1) * sprite.Height * 2 + 2;
			byte[] data = new byte[dataSize];
			data[0] = 0x81;
			data[1] = 0x80;

			int i = 0; int j = sprite.Height - 1;
			for (int index = 2; index < dataSize; index+=2) {
				int val = sprite[i, j];
				var color = MerthsoftExtensions.Color565FromRGB(val);
				data[index] = color.Item1;
				data[index + 1] = color.Item2;

				i += 1;
				if (i >= sprite.Width) {
					index += 2;
					i = 0;
					j--;
				}
			}

			picture.SetData(new object[] { dataSize.ToString(), data });
			StreamWriter s = new StreamWriter(fileName);
			picture.Save(new BinaryWriter(s.BaseStream));
			s.Close();

			return true;
		}

		private bool getPicNumber(bool saveAs, string prefix = "pic") {
			if (picNumber == -1 || saveAs) {
				int seed = 0;
				FileInfo fi = new FileInfo(fileName);
				string name = fi.GetFileName().ToLower();
				if (fileName != null && name.StartsWith(prefix.ToLower())) {
					if (!int.TryParse(name.Substring(prefix.Length), out seed)) {
						seed = picNumber;
					}
				} else if (picNumber != -1) {
					if (picNumber == 9) {
						seed = 0;
					} else {
						seed = picNumber;
						if (seed < 9) {
							seed++;
						}
					}
				}
				string outString = null;
				do {
					outString = InputBox.Show("Pic number (0-255)", seed.ToString(), "Note: Use display number, e.g. Pic1 = 1, Pic0 = 0");
					if (outString == null) {
						return false;
					}
				} while (!int.TryParse(outString, out picNumber));
				if (picNumber == 0) {
					picNumber = 9;
				} else if (picNumber < 9) {
					picNumber--;
				}
			}
			return true;
		}

		private bool savePng() {
			bool success;
			using (Bitmap b = new Bitmap(sprite.Width, sprite.Height)) {
				//using (Graphics g = Graphics.FromImage(b)) {
				sprite.Invalidate();
				drawSprite(b, sprite);
				try {
					b.Save(fileName);
					success = true;
				} catch {
					success = false;
				}
			}
			return success;
		}

		private bool saveXLibBGPic() {
			if (SelectedPalette != Palette.xLIBC) {
				var res = MessageBox.Show("You are trying to save an xLIBC file without using the xLIBC palette. Are you sure you want to continue?", "Wrong Palette", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
				if (res == System.Windows.Forms.DialogResult.No) { return false; }
			}
			if (sprite.Width != 80 || sprite.Height != 60) {
				var res = MessageBox.Show("xLIBC background pictures should be 80 wide by 60 tall. Are you sure you want to continue?", "Wrong Dimensions", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
				if (res == System.Windows.Forms.DialogResult.No) { return false; }
			}
			AppVar8x appVar = new AppVar8x(new FileInfo(fileName).GetFileName(), Var8x.CalcType.Calc8x) { Archived = true };
			byte[] buffer = new byte[sprite.Width * sprite.Height + 7];
			using (MemoryStream ms = new MemoryStream(buffer)) {
				ms.Write(Encoding.ASCII.GetBytes(XLIBBGPIC_HEADER), 0, Encoding.ASCII.GetByteCount(XLIBBGPIC_HEADER));

				int x = 0;
				int y = 0;

				for (int i = 0; i < sprite.Width * sprite.Height; i++) {
					byte data = (byte)sprite[x, y];
					ms.WriteByte(data);
					y++;
					if (y == sprite.Height) {
						y = 0;
						x++;
					}
				}
			}

			appVar.SetRawData(buffer);
			using (FileStream fs = new FileStream(fileName, FileMode.Create))
			using (BinaryWriter bw = new BinaryWriter(fs)) {
				appVar.Save(bw);
			}

			return true;
		}

		private bool saveXLibTiles() {
			if (SelectedPalette != Palette.xLIBC) {
				var res = MessageBox.Show("You are trying to save an xLIBC file without using the xLIBC palette, are you sure you want to continue?", "Wrong Palette", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
				if (res == System.Windows.Forms.DialogResult.No) { return false; }
			}
			if (sprite.Width != 128 || sprite.Height != 64) {
				var res = MessageBox.Show("xLIBC tile/sprite definitions should be 128 wide by 64 tall. Are you sure you want to continue?", "Wrong Dimensions", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
				if (res == System.Windows.Forms.DialogResult.No) { return false; }
			}
			AppVar8x appVar = new AppVar8x(new FileInfo(fileName).GetFileName(), Var8x.CalcType.Calc8x) { Archived = true };
			byte[] buffer = new byte[sprite.Width * sprite.Height + 7];
			using (MemoryStream ms = new MemoryStream(buffer)) {
				ms.Write(Encoding.ASCII.GetBytes(XLIBTILES_HEADER), 0, Encoding.ASCII.GetByteCount(XLIBTILES_HEADER));

				int x = 0;
				int y = 0;
				int spriteX = 0;
				int spriteY = 0;

				for (int i = 0; i < sprite.Width * sprite.Height; i++) {
					byte data = (byte)sprite[x + spriteX, y + spriteY];
					ms.WriteByte(data);

					spriteY++;
					if (spriteY == 8) {
						spriteY = 0;
						spriteX++;
					}
					if (spriteX == 8) {
						spriteX = 0;
						spriteY = 0;
						y += 8;
						if (y == sprite.Height) {
							y = 0;
							x += 8;
						}
					}
				}
			}
			appVar.SetRawData(buffer);
			using (FileStream fs = new FileStream(fileName, FileMode.Create))
			using (BinaryWriter bw = new BinaryWriter(fs)) {
				appVar.Save(bw);
			}

			return true;
		}

		private void insertAndExitToolStripMenuItem_Click(object sender, EventArgs e) {
			OutString = getHex();
			PasteTextEventHandler temp = PasteTextEvent;
			if (temp != null) {
				temp(this, new PasteTextEventArgs(OutString));
			}
			Close();
		}

		private void copyToolStripMenuItem_Click(object sender, EventArgs e) {
			Clipboard.SetText(getHex(), TextDataFormat.Text);
			outputLabel.Text = "Hex copied to clipboard.";
		}

		private void outputLabel_TextChanged(object sender, EventArgs e) {
			clearTextTimer.Start();
		}

		private void clearTextTimer_Tick(object sender, EventArgs e) {
			try {
				Invoke((Action)delegate {
					outputLabel.Text = "";
					clearTextTimer.Stop();
				});
			} catch { }
		}

		private void spriteBox_MouseEnter(object sender, EventArgs e) {
			spriteIndexLabel.Visible = true;
		}

		private void changeTemplate(SaveType saveType) {
			if (MessageBox.Show("This will crop your image to size, are you sure you want to continue?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Hand) == System.Windows.Forms.DialogResult.No) {
				return;
			}
			pushHistory(SelectedPalette);
			shouldPushHistory = false;
			this.saveType = saveType;
			switch (saveType) {
				case SaveType.XLibTiles:
					SelectedPalette = Palette.xLIBC;
					resizeSprite(128, 64);
					break;
				case SaveType.XLibBGPicture:
					SelectedPalette = Palette.xLIBC;
					resizeSprite(80, 60);
					break;
				case SaveType.MonochromePic:
					SelectedPalette = Palette.BlackAndWhite;
					resizeSprite(96, 64);
					break;
				case SaveType.ColorPic:
					SelectedPalette = Palette.BasicColors;
					resizeSprite(265, 165);
					break;
				case SaveType.ColorImage:
					SelectedPalette = Palette.Full565;
					resizeSprite(133, 83);
					break;
			}
		}

		private void monochromePicToolStripMenuItem_Click(object sender, EventArgs e) {
			changeTemplate(SaveType.MonochromePic);
		}

		private void colorPicToolStripMenuItem_Click(object sender, EventArgs e) {
			changeTemplate(SaveType.ColorPic);
		}

		private void colorImageToolStripMenuItem_Click(object sender, EventArgs e) {
			changeTemplate(SaveType.ColorImage);
		}

		private void xLIBCToolStripMenuItem_Click(object sender, EventArgs e) {
			changeTemplate(SaveType.XLibTiles);
		}

		private void xLIBCBackgroundToolStripMenuItem_Click(object sender, EventArgs e) {
			changeTemplate(SaveType.XLibBGPicture);
		}

		private void tilesFlow_MouseClick(object sender, MouseEventArgs e) {
			PictureBox p = tilesFlow.GetChildAtPoint(e.Location) as PictureBox;
			if (p == null) { return; }
			int tileIndex = (int)p.Tag;
			switch (e.Button) {
				case System.Windows.Forms.MouseButtons.Left:
					setLeftMouseButton(tileIndex);
					break;

				case System.Windows.Forms.MouseButtons.Right:
					setRightMouseButton(tileIndex);
					break;
			}
		}

		void tile_MouseClick(object sender, MouseEventArgs e) {
			PictureBox p = sender as PictureBox;
			int tileIndex = (int)p.Tag;
			switch (e.Button) {
				case System.Windows.Forms.MouseButtons.Left:
					setLeftMouseButton(tileIndex);
					break;

				case System.Windows.Forms.MouseButtons.Right:
					setRightMouseButton(tileIndex);
					break;
			}
		}
	}
}