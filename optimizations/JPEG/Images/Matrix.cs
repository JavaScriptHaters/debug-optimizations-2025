using System.Drawing;
using System.Drawing.Imaging;

namespace JPEG.Images;

unsafe class Matrix
{
	public readonly Pixel[,] Pixels;
	public readonly int Height;
	public readonly int Width;

	public Matrix(int height, int width)
	{
		Height = height;
		Width = width;

		Pixels = new Pixel[height, width];
		for (var i = 0; i < height; ++i)
		for (var j = 0; j < width; ++j)
			Pixels[i, j] = new Pixel(0, 0, 0, PixelFormat.RGB);
	}

	public static explicit operator Matrix(Bitmap bmp)
	{
		var height = bmp.Height - bmp.Height % 8;
		var width = bmp.Width - bmp.Width % 8;
		var matrix = new Matrix(height, width);

		var bd = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, bmp.PixelFormat);

		try
		{
			fixed (Pixel* pPixels = &matrix.Pixels[0,0])
			{
				for (var h = 0; h < height; h++)
				{
					var pBmpPixel = (byte*)bd.Scan0 + h * bd.Stride;
					for (var w = 0; w < width; w++)
					{
						var blue = *pBmpPixel++;
						var green = *pBmpPixel++;
						var red = *pBmpPixel++;
						
						*(pPixels + h * width + w) = new Pixel(red, green, blue, PixelFormat.RGB);
					}
				}
				return matrix;
			}
		}
		finally { bmp.UnlockBits(bd); }
	}

	public static explicit operator Bitmap(Matrix matrix)
	{
		var width = matrix.Width;
		var height = matrix.Height;
		
		var bmp = new Bitmap(matrix.Width, matrix.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
		var bd = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, bmp.PixelFormat);

		try
		{
			fixed (Pixel* pPixels = &matrix.Pixels[0,0])
			{
				for (var h = 0; h < height; h++)
				{
					var pBmpPixel = (byte*)bd.Scan0 + h * bd.Stride;
					for (var w = 0; w < width; w++)
					{
						var pixel = *(pPixels + h * width + w);
						*pBmpPixel = ToByte(pixel.B); pBmpPixel++;
						*pBmpPixel = ToByte(pixel.G); pBmpPixel++;
						*pBmpPixel = ToByte(pixel.R); pBmpPixel++;
					}
				}
				return bmp;
			}
		}
		finally { bmp.UnlockBits(bd); }
	}

	private static byte ToByte(double d) => d switch
	{ 
		> byte.MaxValue => byte.MaxValue, 
		< byte.MinValue => byte.MinValue, 
		_ => (byte)d
	};
}