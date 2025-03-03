using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using JPEG.Images;
using PixelFormat = JPEG.Images.PixelFormat;

namespace JPEG.Processor;

public class JpegProcessor : IJpegProcessor
{
	public static readonly JpegProcessor Init = new();
	public const int CompressionQuality = 70;
	private const int DCTSize = 8;
	private readonly DCT dct = new DCT(DCTSize);

	public void Compress(string imagePath, string compressedImagePath)
	{
		using var fileStream = File.OpenRead(imagePath);
		using var bmp = (Bitmap)Image.FromStream(fileStream, false, false);
		var imageMatrix = (Matrix)bmp;
		//Console.WriteLine($"{bmp.Width}x{bmp.Height} - {fileStream.Length / (1024.0 * 1024):F2} MB");
		var compressionResult = Compress(imageMatrix, CompressionQuality);
		compressionResult.Save(compressedImagePath);
	}

	public void Uncompress(string compressedImagePath, string uncompressedImagePath)
	{
		var compressedImage = CompressedImage.Load(compressedImagePath);
		var uncompressedImage = Uncompress(compressedImage);
		var resultBmp = (Bitmap)uncompressedImage;
		resultBmp.Save(uncompressedImagePath, ImageFormat.Bmp);
	}

	private CompressedImage Compress(Matrix matrix, int quality = 50)
	{
		var allQuantizedBytes = new List<byte>();

		for (var y = 0; y < matrix.Height; y += DCTSize)
		{
			for (var x = 0; x < matrix.Width; x += DCTSize)
			{
				foreach (var selector in new Func<Pixel, double>[] { p => p.Y, p => p.Cb, p => p.Cr })
				{
					var subMatrix = GetSubMatrix(matrix, y, DCTSize, x, DCTSize, selector);
					ShiftMatrixValues(subMatrix.AsSpan(), -128);
					var channelFreqs = dct.DCT2D(subMatrix);
					var quantizedFreqs = Quantize(channelFreqs, quality);
					var quantizedBytes = ZigZagScan(quantizedFreqs);
					allQuantizedBytes.AddRange(quantizedBytes);
				}
			}
		}

		long bitsCount;
		Dictionary<BitsWithLength, byte> decodeTable;
		var compressedBytes = HuffmanCodec.Encode(allQuantizedBytes, out decodeTable, out bitsCount);

		return new CompressedImage
		{
			Quality = quality, CompressedBytes = compressedBytes, BitsCount = bitsCount, DecodeTable = decodeTable,
			Height = matrix.Height, Width = matrix.Width
		};
	}

	private Matrix Uncompress(CompressedImage image)
	{
		var result = new Matrix(image.Height, image.Width);
		using (var allQuantizedBytes =
		       new MemoryStream(HuffmanCodec.Decode(image.CompressedBytes, image.DecodeTable, image.BitsCount)))
		{
			for (var y = 0; y < image.Height; y += DCTSize)
			{
				for (var x = 0; x < image.Width; x += DCTSize)
				{
					var chn = new double[3][];  // [_y, cb, cr]

					Span<double> _y;
					Span<double> cb;
					Span<double> cr;
					
					var quantizedBytes = new byte[DCTSize * DCTSize];
					allQuantizedBytes.ReadAsync(quantizedBytes, 0, quantizedBytes.Length).Wait();
					var quantizedFreqs = ZigZagUnScan(quantizedBytes);
					var channelFreqs = DeQuantize(quantizedFreqs, image.Quality);
					_y = dct.IDCT2D(channelFreqs.AsSpan());
					ShiftMatrixValues(_y, 128);
					
					quantizedBytes = new byte[DCTSize * DCTSize];
					allQuantizedBytes.ReadAsync(quantizedBytes, 0, quantizedBytes.Length).Wait();
					quantizedFreqs = ZigZagUnScan(quantizedBytes);
					channelFreqs = DeQuantize(quantizedFreqs, image.Quality);
					cb = dct.IDCT2D(channelFreqs.AsSpan());
					ShiftMatrixValues(cb, 128);
					
					quantizedBytes = new byte[DCTSize * DCTSize];
					allQuantizedBytes.ReadAsync(quantizedBytes, 0, quantizedBytes.Length).Wait();
					quantizedFreqs = ZigZagUnScan(quantizedBytes);
					channelFreqs = DeQuantize(quantizedFreqs, image.Quality);
					cr = dct.IDCT2D(channelFreqs.AsSpan());
					ShiftMatrixValues(cr, 128);
					
					/*
					for (var i = 0; i < chn.Length; i++)
					{
						var quantizedBytes = new byte[DCTSize * DCTSize];
						allQuantizedBytes.ReadAsync(quantizedBytes, 0, quantizedBytes.Length).Wait();
						var quantizedFreqs = ZigZagUnScan(quantizedBytes);
						var channelFreqs = DeQuantize(quantizedFreqs, image.Quality);
						chn[i] = dct.IDCT2D(channelFreqs.AsSpan());
						ShiftMatrixValues(chn[i], 128);
					}
					*/

					SetPixels(result, _y, cb, cr, PixelFormat.YCbCr, y, x);
				}
			}
		}

		return result;
	}

	private static void ShiftMatrixValues(Span<double> subMatrix, int shiftValue)
	{
		for (var y = 0; y < DCTSize; y++)
		for (var x = 0; x < DCTSize; x++)
			subMatrix[y * DCTSize + x] = subMatrix[y * DCTSize + x] + shiftValue;
	}

	private static void SetPixels(Matrix matrix, Span<double> a, Span<double> b, Span<double> c, PixelFormat format,
		int yOffset, int xOffset)
	{
		for (var y = 0; y < DCTSize; y++)
		for (var x = 0; x < DCTSize; x++)
			matrix.Pixels[yOffset + y, xOffset + x] = new Pixel(a[y * DCTSize + x], b[y * DCTSize + x], c[y * DCTSize + x], format);
	}

	private static double[] GetSubMatrix(Matrix matrix, int yOffset, int yLength, int xOffset, int xLength,
		Func<Pixel, double> componentSelector)
	{
		var result = new double[yLength * xLength];
		for (var j = 0; j < yLength; j++)
		for (var i = 0; i < xLength; i++)
			result[j * yLength + i] = componentSelector(matrix.Pixels[yOffset + j, xOffset + i]);
		return result;
	}

	private static IEnumerable<byte> ZigZagScan(byte[] channelFreqs)
	{
		return new[]
		{
			channelFreqs[0 * DCTSize + 0], channelFreqs[0 * DCTSize + 1], channelFreqs[1 * DCTSize + 0], channelFreqs[2 * DCTSize + 0], channelFreqs[1 * DCTSize + 1],
			channelFreqs[0 * DCTSize + 2], channelFreqs[0 * DCTSize + 3], channelFreqs[1 * DCTSize + 2],
			channelFreqs[2 * DCTSize + 1], channelFreqs[3 * DCTSize + 0], channelFreqs[4 * DCTSize + 0], channelFreqs[3 * DCTSize + 1], channelFreqs[2 * DCTSize + 2],
			channelFreqs[1 * DCTSize + 3], channelFreqs[0 * DCTSize + 4], channelFreqs[0 * DCTSize + 5],
			channelFreqs[1 * DCTSize + 4], channelFreqs[2 * DCTSize + 3], channelFreqs[3 * DCTSize + 2], channelFreqs[4 * DCTSize + 1], channelFreqs[5 * DCTSize + 0],
			channelFreqs[6 * DCTSize + 0], channelFreqs[5 * DCTSize + 1], channelFreqs[4 * DCTSize + 2],
			channelFreqs[3 * DCTSize + 3], channelFreqs[2 * DCTSize + 4], channelFreqs[1 * DCTSize + 5], channelFreqs[0 * DCTSize + 6], channelFreqs[0 * DCTSize + 7],
			channelFreqs[1 * DCTSize + 6], channelFreqs[2 * DCTSize + 5], channelFreqs[3 * DCTSize + 4],
			channelFreqs[4 * DCTSize + 3], channelFreqs[5 * DCTSize + 2], channelFreqs[6 * DCTSize + 1], channelFreqs[7 * DCTSize + 0], channelFreqs[7 * DCTSize + 1],
			channelFreqs[6 * DCTSize + 2], channelFreqs[5 * DCTSize + 3], channelFreqs[4 * DCTSize + 4],
			channelFreqs[3 * DCTSize + 5], channelFreqs[2 * DCTSize + 6], channelFreqs[1 * DCTSize + 7], channelFreqs[2 * DCTSize + 7], channelFreqs[3 * DCTSize + 6],
			channelFreqs[4 * DCTSize + 5], channelFreqs[5 * DCTSize + 4], channelFreqs[6 * DCTSize + 3],
			channelFreqs[7 * DCTSize + 2], channelFreqs[7 * DCTSize + 3], channelFreqs[6 * DCTSize + 4], channelFreqs[5 * DCTSize + 5], channelFreqs[4 * DCTSize + 6],
			channelFreqs[3 * DCTSize + 7], channelFreqs[4 * DCTSize + 7], channelFreqs[5 * DCTSize + 6],
			channelFreqs[6 * DCTSize + 5], channelFreqs[7 * DCTSize + 4], channelFreqs[7 * DCTSize + 5], channelFreqs[6 * DCTSize + 6], channelFreqs[5 * DCTSize + 7],
			channelFreqs[6 * DCTSize + 7], channelFreqs[7 * DCTSize + 6], channelFreqs[7 * DCTSize + 7]
		};
	}

	private static byte[,] ZigZagUnScan(IReadOnlyList<byte> quantizedBytes)
	{
		return new[,]
		{
			{
				quantizedBytes[0], quantizedBytes[1], quantizedBytes[5], quantizedBytes[6], quantizedBytes[14],
				quantizedBytes[15], quantizedBytes[27], quantizedBytes[28]
			},
			{
				quantizedBytes[2], quantizedBytes[4], quantizedBytes[7], quantizedBytes[13], quantizedBytes[16],
				quantizedBytes[26], quantizedBytes[29], quantizedBytes[42]
			},
			{
				quantizedBytes[3], quantizedBytes[8], quantizedBytes[12], quantizedBytes[17], quantizedBytes[25],
				quantizedBytes[30], quantizedBytes[41], quantizedBytes[43]
			},
			{
				quantizedBytes[9], quantizedBytes[11], quantizedBytes[18], quantizedBytes[24], quantizedBytes[31],
				quantizedBytes[40], quantizedBytes[44], quantizedBytes[53]
			},
			{
				quantizedBytes[10], quantizedBytes[19], quantizedBytes[23], quantizedBytes[32], quantizedBytes[39],
				quantizedBytes[45], quantizedBytes[52], quantizedBytes[54]
			},
			{
				quantizedBytes[20], quantizedBytes[22], quantizedBytes[33], quantizedBytes[38], quantizedBytes[46],
				quantizedBytes[51], quantizedBytes[55], quantizedBytes[60]
			},
			{
				quantizedBytes[21], quantizedBytes[34], quantizedBytes[37], quantizedBytes[47], quantizedBytes[50],
				quantizedBytes[56], quantizedBytes[59], quantizedBytes[61]
			},
			{
				quantizedBytes[35], quantizedBytes[36], quantizedBytes[48], quantizedBytes[49], quantizedBytes[57],
				quantizedBytes[58], quantizedBytes[62], quantizedBytes[63]
			}
		};
	}

	private static byte[] Quantize(Span<double> channelFreqs, int quality)
	{
		var result = new byte[DCTSize * DCTSize];

		var quantizationMatrix = GetQuantizationMatrix(quality);
		for (int y = 0; y < DCTSize; y++)
		{
			for (int x = 0; x < DCTSize; x++)
			{
				result[y * DCTSize + x] = (byte)(channelFreqs[y * DCTSize + x] / quantizationMatrix[y, x]);
			}
		}

		return result;
	}

	private static double[] DeQuantize(byte[,] quantizedBytes, int quality)
	{
		var result = new double[DCTSize * DCTSize];
		var quantizationMatrix = GetQuantizationMatrix(quality);

		for (int y = 0; y < DCTSize; y++)
		{
			for (int x = 0; x < DCTSize; x++)
			{
				result[y * DCTSize + x] =
					((sbyte)quantizedBytes[y, x]) *
					quantizationMatrix[y, x]; //NOTE cast to sbyte not to loose negative numbers
			}
		}

		return result;
	}

	private static int[,] GetQuantizationMatrix(int quality)
	{
		if (quality < 1 || quality > 99)
			throw new ArgumentException("quality must be in [1,99] interval");

		var multiplier = quality < 50 ? 5000 / quality : 200 - 2 * quality;

		var result = new[,]
		{
			{ 16, 11, 10, 16, 24, 40, 51, 61 },
			{ 12, 12, 14, 19, 26, 58, 60, 55 },
			{ 14, 13, 16, 24, 40, 57, 69, 56 },
			{ 14, 17, 22, 29, 51, 87, 80, 62 },
			{ 18, 22, 37, 56, 68, 109, 103, 77 },
			{ 24, 35, 55, 64, 81, 104, 113, 92 },
			{ 49, 64, 78, 87, 103, 121, 120, 101 },
			{ 72, 92, 95, 98, 112, 100, 103, 99 }
		};

		for (int y = 0; y < result.GetLength(0); y++)
		{
			for (int x = 0; x < result.GetLength(1); x++)
			{
				result[y, x] = (multiplier * result[y, x] + 50) / 100;
			}
		}

		return result;
	}
}