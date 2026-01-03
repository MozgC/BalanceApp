using ScottPlot;
using ScottPlot.WinForms;

namespace StrategyTester
{
	class ScottPlotHelper
	{
		public static void ShowHeatMap(Dictionary<(int x, int y), List<decimal>> heatMap, bool labelValues)
		{
			/*
			heatMap = new Dictionary<(int x, int y), List<decimal>>();
			heatMap[(0, 4)] = new List<decimal>() { 1 };
			heatMap[(0, 5)] = new List<decimal>() { 2 };
			heatMap[(0, 6)] = new List<decimal>() { 3 };
			heatMap[(1, 4)] = new List<decimal>() { 4 };
			heatMap[(1, 5)] = new List<decimal>() { 5 };
			heatMap[(1, 6)] = new List<decimal>() { 6 };
			heatMap[(2, 4)] = new List<decimal>() { 7 };
			heatMap[(2, 5)] = new List<decimal>() { 8 };
			heatMap[(2, 6)] = new List<decimal>() { 9 };
			*/
			
			var maSet =  heatMap.Keys.Select(k => k.x).Distinct().OrderBy(x => x);
			var emaSet = heatMap.Keys.Select(k => k.y).Distinct().OrderBy(x => x);

			int[] xValues = maSet.ToArray();       // например: [30, 35, 40, ..., 150]
			int[] yValues = emaSet.ToArray(); // например: [6, 7, 8, ..., 22]

			int cols = xValues.Length;       // высота = количество разных maDays
			int rows = yValues.Length;  // ширина = количество разных процентов

			double[,] data = new double[rows, cols];
			
			for (int row = 0; row < rows; row++)
			{
				int ema = yValues[row];

				for (int col = 0; col < cols; col++)
				{
					int ma = xValues[col];

					if (heatMap.TryGetValue((ma, ema), out var list) && list.Count > 0)
					{
						data[yValues.Length - 1 - row, col] = (double)list.Average();
					}
					else
					{
						data[yValues.Length - 1 - row, col] = double.NaN; // пустые ячейки
					}
				}
			}

			var plot = new ScottPlot.Plot();

			var hm = plot.Add.Heatmap(data);
			hm.Colormap = new ScottPlot.Colormaps.Turbo();
			hm.Smooth = true;

			if (labelValues)
			{
				for (int y = 0; y < data.GetLength(0); y++)
				for (int x = 0; x < data.GetLength(1); x++)
				{
					Coordinates coordinates = new(x, y);
					string cellLabel = data[yValues.Length - 1 - y, x].ToString("0.0");
					var text = plot.Add.Text(cellLabel, coordinates);
					text.Alignment = Alignment.MiddleCenter;
					text.LabelFontSize = 30;
					text.LabelFontColor = Colors.White;
				}
			}

			// axis titles
			plot.Title("HeatMap");
			plot.XLabel("MA Days");
			plot.YLabel("EMA Days");

			// map indices -> your X/Y values using manual ticks
			double[] xPositions = Enumerable.Range(0, xValues.Length)
				.Select(i => (double)i)
				.ToArray();

			double[] yPositions = Enumerable.Range(0, yValues.Length)
				.Select(i => (double)i)
				.ToArray();

			plot.Axes.Bottom.SetTicks(
				xPositions,
				xValues.Select(v => v.ToString()).ToArray()
			);

			plot.Axes.Left.SetTicks(
				yPositions,
				yValues.Select(v => v.ToString()).ToArray()
			);

			// optional but keeps the heatmap tightly framed with half-cell padding
			plot.Axes.SetLimits(left: -0.5, right: cols - 0.5, bottom: -0.5, top: rows - 0.5);

			var cb = plot.Add.ColorBar(hm);
			cb.Label = "Profit ratio";
			///cb.LabelStyle.FontSize = 24;

			FormsPlotViewer.Launch(plot);
		}
	}
}
