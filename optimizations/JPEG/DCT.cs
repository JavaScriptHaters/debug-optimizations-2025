using System;
using JPEG.Utilities;

namespace JPEG;

public class DCT
{
	private readonly int dctSize;
	private readonly double[,] transformMatrix;
	private readonly double[,] transformMatrixT;
	
	public DCT(int dctSize)
	{
		this.dctSize = dctSize;
		transformMatrix = new double[dctSize, dctSize];
		transformMatrixT = new double[dctSize, dctSize];
		
		for (int i = 0; i < dctSize; i++)
		{
			for (int j = 0; j < dctSize; j++)
			{
				double c_i = i == 0 ? 1 / Math.Sqrt(dctSize) : 1 / Math.Sqrt(dctSize / 2d);

				transformMatrix[i, j] = c_i * Math.Cos(Math.PI / dctSize * (j + 0.5) * i);
				transformMatrixT[j, i] = transformMatrix[i, j];
			}
		}
	}

	private double[,] MultiplyMatrix(double[,] leftMatrix, double[,] rightMatrix)
	{
		var semiMatrix = new double[leftMatrix.GetLength(0), rightMatrix.GetLength(0)];
		
		for (int i = 0; i < leftMatrix.GetLength(0); i++)
		{
			for (int j = 0; j < rightMatrix.GetLength(1); j++)
			{
				for (int k = 0; k < leftMatrix.GetLength(0); k++)
				{
					semiMatrix[i, j] += leftMatrix[i, k] * rightMatrix[k, j];
				}
			}
		}

		return semiMatrix;
	}
	
	public double[,] DCT2D(double[,] input)
	{
		//var height = input.GetLength(0);
		//var width = input.GetLength(1);

		var intermediateRes = MultiplyMatrix(transformMatrix, input);

		return MultiplyMatrix(intermediateRes, transformMatrixT);
	}

	public double[,] IDCT2D(double[,] input)
	{
		//var height = input.GetLength(0);
		//var width = input.GetLength(1);

		var intermediateRes = MultiplyMatrix(transformMatrixT, input);

		return MultiplyMatrix(intermediateRes, transformMatrix);
	}
}