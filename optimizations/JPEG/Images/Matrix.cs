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

		BitmapData bitmapData = null;

		try
		{
			bitmapData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, bmp.PixelFormat);

			byte* ptr = (byte*)bitmapData.Scan0.ToPointer();

			for (int j = 0; j < height; j++)
			{
				byte* rowPtr = ptr + j * bitmapData.Stride;

				for (int i = 0; i < width; i++)
				{
					byte b = rowPtr[0];
					byte g = rowPtr[1];
					byte r = rowPtr[2];

					matrix.Pixels[j, i] = new Pixel(r, g, b, PixelFormat.RGB);

					rowPtr += 3;
				}
			}
		}
		finally
		{
			if (bitmapData != null)
			{
				bmp.UnlockBits(bitmapData);
			}
		}

		return matrix;
	}

	public static explicit operator Bitmap(Matrix matrix)
	{
		var bmp = new Bitmap(matrix.Width, matrix.Height);

		BitmapData bitmapData = null;

		try
		{
			bitmapData = bmp.LockBits(new Rectangle(0, 0, matrix.Width, matrix.Height), ImageLockMode.WriteOnly, bmp.PixelFormat);

			byte* ptr = (byte*)bitmapData.Scan0.ToPointer();

			for (int j = 0; j < matrix.Height; j++)
			{
				byte* rowPtr = ptr + j * bitmapData.Stride;

				for (int i = 0; i < matrix.Width; i++)
				{
					var pixel = matrix.Pixels[j, i];

					rowPtr[0] = ToByte(pixel.B);
					rowPtr[1] = ToByte(pixel.G);
					rowPtr[2] = ToByte(pixel.R);

					rowPtr += 3;
				}
			}
		}
		finally
		{
			if (bitmapData != null)
			{
				bmp.UnlockBits(bitmapData);
			}
		}

		return bmp;
	}

	public static byte ToByte(double d)
	{
		var val = (int)d;
		if (val > byte.MaxValue)
			return byte.MaxValue;
		if (val < byte.MinValue)
			return byte.MinValue;
		return (byte)val;
	}
}