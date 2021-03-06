﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using Merthsoft.Extensions;

namespace Merthsoft.TokenIDE {
	public class Sprite {
		private List<List<int>> sprite;

		public int Height { get; private set; }
		public int Width { get; private set; }

		public int this[int i, int j] {
			get { return sprite[i][j]; }
			set { setPoint(i, j, value); }
		}

		public Rectangle DirtyRectangle { get; set; }

		public Sprite(string hexData, int width, out int height, int bitsPerPixel) {
			Width = width;
			sprite = HexHelper.HexTo2DList(hexData, Width, out height, bitsPerPixel);
			Height = height;
			DirtyRectangle = new Rectangle(0, 0, Width, Height);
		}

		public Sprite(int[,] sprite) {
			Width = sprite.GetLength(0);
			Height = sprite.GetLength(1);
			DirtyRectangle = new Rectangle(0, 0, Width, Height);
			this.sprite = new List<List<int>>();
			for (int i = 0; i < Width; i++) {
				List<int> row = new List<int>(Width);
				for (int j = 0; j < Height; j++) {
					row.Add(sprite[i, j]);
				}
				this.sprite.Add(row);
				row = new List<int>(Width);
			}
		}

		public Sprite(int width, int height, int defaultTile = 0) {
			Height = height;
			Width = width;
			DirtyRectangle = new Rectangle(0, 0, Width, Height);
			this.sprite = new List<List<int>>();
			for (int i = 0; i < Width; i++) {
				List<int> row = new List<int>(Width);
				for (int j = 0; j < Height; j++) {
					row.Add(defaultTile);
				}
				this.sprite.Add(row);
				row = new List<int>(Width);
			}
		}

		public Sprite(Sprite oldSprite) {
			Height = oldSprite.Height;
			Width = oldSprite.Width;
			DirtyRectangle = new Rectangle(0, 0, Width, Height);
			this.sprite = new List<List<int>>();
			for (int i = 0; i < Width; i++) {
				List<int> row = new List<int>(Width);
				for (int j = 0; j < Height; j++) {
					row.Add(oldSprite[i, j]);
				}
				this.sprite.Add(row);
				row = new List<int>(Width);
			}
		}

		public Sprite(Bitmap image, List<Color> colors, int transparent = -1) 
			: this(image.PalettizeImage(colors, transparent)) { }

		public Sprite(Bitmap image) {
			Width = image.Width;
			Height = image.Height;
			this.sprite = new List<List<int>>();
			for (int i = 0; i < Width; i++) {
				List<int> row = new List<int>(Width);
				for (int j = 0; j < Height; j++) {
					row.Add(image.GetPixel(i, j).ToArgb());
				}
				this.sprite.Add(row);
				row = new List<int>(Width);
			}
			DirtyRectangle = new Rectangle(0, 0, Width, Height);
		}

		public Sprite Copy() {
			return new Sprite(this);
		}

		public void Resize(int width, int height) {
			int bigHeight = (int)Math.Max(height, Height);
			int littleHeight = (int)Math.Min(height, Height);
			int bigWidth = (int)Math.Max(width, Width);
			int littleWidth = (int)Math.Min(width, Width);
			
			for (int i = littleWidth; i < bigWidth; i++) {
				if (width < Width) {
					sprite.RemoveAt(width);
				} else if (width > Width) {
					int[] newRow = new int[height];
					sprite.Add(newRow.ToList());
				}
			}
			Width = width;

			for (int i = 0; i < Width; i++) {
				for (int j = littleHeight; j < bigHeight; j++) {
					if (height< Height) {
						sprite[i].RemoveAt(height);
					} else if (height > Height) {
						sprite[i].Add(0);
					}
				}
			}
			Height = height; 
		}

		public void DrawSprite(int x, int y, Sprite otherSprite) {
			int maxWidth = x + otherSprite.Width;
			int maxHeight = y + otherSprite.Height;

			if (maxWidth > Width) { maxWidth = Width; }
			if (maxHeight > Height) { maxHeight = Height; }

			for (int i = x; i < maxWidth; i++) {
				for (int j = y; j < maxWidth; j++) {
					int color = otherSprite[i, j];
					if (color != -1) {
						Plot(i, j, color);
					}
				}
			}
		}

		public Sprite SubSprite(int x, int y, int width, int height) {
			Sprite newSprite = new Sprite(width, height);
			for (int i = 0; i < width; i++) {
				for (int j = 0; j < height; j++) {
					newSprite[i, j] = this[x + i, y + j];
				}
			}

			return newSprite;
		}

		public override bool Equals(object obj) {
			Sprite comp = obj as Sprite;
			if (comp == null) {
				return base.Equals(obj);
			}

			if (comp.Width != Width || comp.Height != Height) {
				return false;
			}

			for (int i = 0; i < Width; i++) {
				for (int j = 0; j < Width; j++) {
					if (comp[i, j] != this[i, j]) { return false; }
				}
			}

			return true;
		}

		public override int GetHashCode() {
			return sprite.GetHashCode();
		}

		public override string ToString() {
			return string.Format("Sprite {{{0}, {1}}}", Width, Height);
		}

		public void ClearDirtyRectangle() {
			DirtyRectangle = Rectangle.Empty;
		}

		public void Invalidate() {
			DirtyRectangle = new Rectangle(0, 0, Width, Height);
		}

		/// <summary>
		/// Plots a point to the sprite.
		/// </summary>
		/// <param name="x">The X coordinate to plot.</param>
		/// <param name="y">The Y coordinate to plot.</param>
		/// <param name="color">The color to draw.</param>
		/// <param name="plotWidth">The pen width.</param>
		public void Plot(int x, int y, int color, int plotWidth = 1) {
			if (plotWidth == 1) {
				if (x >= 0 && y >= 0 && x < Width && y < Height) {
					setPoint(x, y, color);
				}
			} else {
				int r = (int)Math.Floor((plotWidth) / 2.0);
				int evenOffSet = plotWidth % 2 == 0 ? 1 : 0;
				DrawRectangle(x - r + evenOffSet, y - r + evenOffSet, x + r, y + r, color, 1, true);
			}
		}

		private void setPoint(int x, int y, int color) {
			if (x < 0 || x >= Width || y < 0 || y >= Height)
				return;
						
			if (DirtyRectangle.IsEmpty) {
				DirtyRectangle = new Rectangle(x, y, 1, 1);
			} else if (!DirtyRectangle.Contains(x, y)) {
				DirtyRectangle = Rectangle.Union(DirtyRectangle, new Rectangle(x, y, 1, 1));
			}
			
			sprite[x][y] = color;
		}

		/// <summary>
		/// Draws a line to the sprite.
		/// </summary>
		/// <param name="x1">The X coordinate of one end point.</param>
		/// <param name="y1">The Y coordinate of one end point.</param>
		/// <param name="x2">The X coordinate of the second end point.</param>
		/// <param name="y2">The Y coordinate of the second end point.</param>
		/// <param name="color">The color to draw.</param>
		/// <param name="plotWidth">The pen width.</param>
		public void DrawLine(int x1, int y1, int x2, int y2, int color, int plotWidth = 1) {
			int deltaX = (int)Math.Abs(x1 - x2);
			int deltaY = (int)Math.Abs(y1 - y2);
			int stepX = x2 < x1 ? 1 : -1;
			int stepY = y2 < y1 ? 1 : -1;

			int err = deltaX - deltaY;

			while (true) {
				Plot(x2, y2, color, plotWidth);
				if (x2 == x1 && y2 == y1) { break; }

				int e2 = 2 * err;
				if (e2 > -deltaY) {
					err = err - deltaY;
					x2 = x2 + stepX;
				}

				if (x2 == x1 && y2 == y1) {
					Plot(x2, y2, color, plotWidth);
					break;
				}

				if (e2 < deltaX) {
					err = err + deltaX;
					y2 = y2 + stepY;
				}
			}
		}

		/// <summary>
		/// Draws a rectangle to the sprite.
		/// </summary>
		/// <param name="x1"></param>
		/// <param name="y1"></param>
		/// <param name="x2"></param>
		/// <param name="y2"></param>
		/// <param name="color">The color to draw.</param>
		/// <param name="plotWidth">The pen width.</param>
		/// <param name="fill">True to fill the rectangle.</param>
		public void DrawRectangle(int x1, int y1, int x2, int y2, int color, int plotWidth = 1, bool fill = false) {
			if (!fill) {
				DrawLine(x1, y1, x1, y2, color, plotWidth);
				DrawLine(x1, y2, x2, y2, color, plotWidth);
				DrawLine(x2, y2, x2, y1, color, plotWidth);
				DrawLine(x1, y1, x2, y1, color, plotWidth);
			} else {
				if (x1 > x2) {
					MerthsoftExtensions.Swap(ref x1, ref x2);
				}
				for (int x = x1; x <= x2; x++) {
					DrawLine(x, y1, x, y2, color, plotWidth);
				}
			}
		}

		/// <summary>
		/// Draws an ellipse to the sprite.
		/// </summary>
		/// <param name="x1"></param>
		/// <param name="y1"></param>
		/// <param name="x2"></param>
		/// <param name="y2"></param>
		/// <param name="color">The color to draw.</param>
		/// <param name="plotWidth">The pen width.</param>
		/// <param name="fill">True to fill the ellipse.</param>
		public void DrawEllipse(int x1, int y1, int x2, int y2, int color, int plotWidth = 1, bool fill = false) {
			if (x2 < x1) { MerthsoftExtensions.Swap(ref x1, ref x2); }
			if (y2 < y1) { MerthsoftExtensions.Swap(ref y1, ref y2); }

			int hr = (x2 - x1) / 2;
			int kr = (y2 - y1) / 2;
			int h = x1 + hr;
			int k = y1 + kr;

			DrawEllipseUsingRadius(h, k, hr, kr, color, plotWidth, fill);
		}

		private void incrementX(ref int x, ref int dxt, ref int d2xt, ref int t) { x++; dxt += d2xt; t += dxt; }
		private void incrementY(ref int y, ref int dyt, ref int d2yt, ref int t) { y--; dyt += d2yt; t += dyt; }

		/// <summary>
		/// Draws a filled ellipse to the sprite.
		/// </summary>
		/// <remarks>Taken from http://enchantia.com/graphapp/doc/tech/ellipses.html. </remarks>
		/// <param name="x">The center point X coordinate.</param>
		/// <param name="y">The center point Y coordinate.</param>
		/// <param name="xRadius">The x radius.</param>
		/// <param name="yRadius">The y radius.</param>
		/// <param name="color">The color to draw.</param>
		/// <param name="plotWidth">The pen width.</param>
		/// <param name="fill">True to fill the ellipse.</param>
		public void DrawEllipseUsingRadius(int x, int y, int xRadius, int yRadius, int color, int plotWidth = 1, bool fill = false) {
			int plotX = 0;
			int plotY = yRadius;

			int xRadiusSquared = xRadius * xRadius;
			int yRadiusSquared = yRadius * yRadius;
			int crit1 = -(xRadiusSquared / 4 + xRadius % 2 + yRadiusSquared);
			int crit2 = -(yRadiusSquared / 4 + yRadius % 2 + xRadiusSquared);
			int crit3 = -(yRadiusSquared / 4 + yRadius % 2);

			int t = -xRadiusSquared * plotY;
			int dxt = 2 * yRadiusSquared * plotX, dyt = -2 * xRadiusSquared * plotY;
			int d2xt = 2 * yRadiusSquared, d2yt = 2 * xRadiusSquared;

			while (plotY >= 0 && plotX <= xRadius) {
				Plot(x + plotX, y + plotY, color, plotWidth);
				if (plotX != 0 || plotY != 0) {
					Plot(x - plotX, y - plotY, color, plotWidth);
				}

				if (plotX != 0 && plotY != 0) {
					Plot(x + plotX, y - plotY, color, plotWidth);
					Plot(x - plotX, y + plotY, color, plotWidth);
				}

				if (t + yRadiusSquared * plotX <= crit1 || t + xRadiusSquared * plotY <= crit3) {
					incrementX(ref plotX, ref dxt, ref d2xt, ref t);
				} else if (t - xRadiusSquared * plotY > crit2) {
					incrementY(ref plotY, ref dyt, ref d2yt, ref t);
				} else {
					incrementX(ref plotX, ref dxt, ref d2xt, ref t);
					incrementY(ref plotY, ref dyt, ref d2yt, ref t);
				}
			}

			if (fill) {
				FloodFill(x, y, color);
			}
		}

		/// <summary>
		/// Draws a circle to the sprite.
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="r"></param>
		/// <param name="color">The color to draw.</param>
		/// <param name="plotWidth">The pen width.</param>
		/// <param name="fill">True to fill the circle.</param>
		public void DrawCircle(int x, int y, int r, int color, int plotWidth = 1, bool fill = false) {
			DrawEllipseUsingRadius(x, y, r, r, color, plotWidth, fill);
		}

		/// <summary>
		/// Performs a flood fill to the sprite.
		/// </summary>
		/// <param name="x">The starting X coordinate.</param>
		/// <param name="y">The starting Y coordinate.</param>
		/// <param name="color">The color to draw.</param>
		public void FloodFill(int x, int y, int color) {
			if (x < 0 || y < 0 || x >= Width || y >= Height) { return; }
			if (sprite[x][y] == color) { return; }

			int baseColor = sprite[x][y];
			Stack<Point> s = new Stack<Point>();
			s.Push(new Point(x, y));
			while (s.Count > 0) {
				Point p = s.Pop();
				if (p.X < 0 || p.Y < 0 || p.X >= Width || p.Y >= Height) {
					continue;
				}
				if (sprite[p.X][p.Y] == baseColor) {
					Plot(p.X, p.Y, color, 1);
					s.Push(new Point(p.X + 1, p.Y));
					s.Push(new Point(p.X - 1, p.Y));
					s.Push(new Point(p.X, p.Y + 1));
					s.Push(new Point(p.X, p.Y - 1));
				}
			}
		}

		/// <summary>
		/// Replaces all pixels with color at x,y with color
		/// </summary>
		/// <param name="x">The starting X coordinate.</param>
		/// <param name="y">The starting Y coordinate.</param>
		/// <param name="color">The color to draw.</param>
		public void ReplaceAll(int x, int y, int color) {
			if (x < 0 || y < 0 || x >= Width || y >= Height) { return; }
			if (sprite[x][y] == color) { return; }

			int baseColor = sprite[x][y];
			for (int i = 0; i < Width; i++) {
				for (int j = 0; j < Height; j++) {
					if (sprite[i][j] == baseColor) {
						sprite[i][j] = color;
					}
				}
			}

			DirtyRectangle = new Rectangle(0, 0, Width, Height);
		}
	}
}
