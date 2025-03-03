using System;
using JPEG.Utilities;

namespace JPEG;

public class DCT
{
	private readonly int dctSize;
	private readonly double[] transformMatrix;
	private readonly double[] transformMatrixT;
	
	public DCT(int dctSize)
	{
		this.dctSize = dctSize;
		transformMatrix = new double[dctSize * dctSize];
		transformMatrixT = new double[dctSize * dctSize];
		
		for (int i = 0; i < dctSize; i++)
		{
			for (int j = 0; j < dctSize; j++)
			{
				double c_i = i == 0 ? 1 / Math.Sqrt(dctSize) : 1 / Math.Sqrt(dctSize / 2d);

				transformMatrix[i * dctSize + j] = c_i * Math.Cos(Math.PI / dctSize * (j + 0.5) * i);
				transformMatrixT[j * dctSize + i] = transformMatrix[i * dctSize + j];
			}
		}
	}

	private Span<double> MultiplyMatrix(Span<double> leftMatrix, Span<double> rightMatrix)
	{
		var semiMatrix = new double[dctSize * dctSize];
		
		for (int i = 0; i < dctSize; i++)
		{
			for (int j = 0; j < dctSize; j++)
			{
				for (int k = 0; k < dctSize; k++)
				{
					semiMatrix[i * dctSize + j] += leftMatrix[i * dctSize + k] * rightMatrix[k * dctSize + j];
				}
			}
		}

		return semiMatrix.AsSpan();
	}
	
	public Span<double> DCT2D(Span<double> input)
	{
		var intermediateRes = MultiplyMatrix(transformMatrix, input);

		return MultiplyMatrix(intermediateRes, transformMatrixT);
	}

	public Span<double> IDCT2D(Span<double> input)
	{
		var intermediateRes = MultiplyMatrix(transformMatrixT, input);

		return MultiplyMatrix(intermediateRes, transformMatrix);
	}
}