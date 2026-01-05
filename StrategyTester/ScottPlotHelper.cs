using ScottPlot;
using ScottPlot.WinForms;

namespace StrategyTester
{
	class ScottPlotHelper
	{
		public static void ShowHeatMap(IDictionary<(decimal x, decimal y), List<decimal>> heatMap, bool labelValues)
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

			decimal[] xValues = maSet.ToArray();       // например: [30, 35, 40, ..., 150]
			decimal[] yValues = emaSet.ToArray(); // например: [6, 7, 8, ..., 22]

			int cols = xValues.Length;       // высота = количество разных maDays
			int rows = yValues.Length;  // ширина = количество разных процентов

			double[,] data = new double[rows, cols];

			for (int row = 0; row < rows; row++)
			{
				decimal ema = yValues[row];

				for (int col = 0; col < cols; col++)
				{
					decimal ma = xValues[col];

					if (heatMap.TryGetValue((ma, ema), out var list) && list.Count > 0)
					{
						data[yValues.Length - 1 - row, col] = (double) list.Average();
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
				.Select(i => (double) i)
				.ToArray();

			double[] yPositions = Enumerable.Range(0, yValues.Length)
				.Select(i => (double) i)
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

		public static void DrawEquityCurve(string ticker, decimal percentOfPassedOosRuns, IList<(DateTime date, decimal equity)> equityCurve)
		{
			// Extract DateTime[] and double[] (ScottPlot works best with arrays for performance)
			DateTime[] xs = equityCurve.Select(item => item.date).ToArray();
			double[] ys   = equityCurve.Select(item => (double) item.equity).ToArray();  // decimal → double

			var plot = new ScottPlot.Plot();
			
			// Add a scatter plot (points connected by lines - ideal for time series)
			var scatter = plot.Add.Scatter(xs, ys);
			scatter.MarkerSize = 6;          // Optional: make points visible
			scatter.LineWidth = 2;           // Optional: thicker line

			// If you want only points (no connecting line), use:
			// var scatter = formsPlot1.Plot.Add.ScatterPoints(xs, ys);

			// Enable DateTime formatting on the bottom X-axis
			plot.Axes.DateTimeTicksBottom();

			// Optional: style the default grid (it's already visible)
			plot.Grid.MajorLineColor = ScottPlot.Colors.Gray.WithOpacity(0.4);
			plot.Grid.MajorLineWidth = 1;

			// Enable DateTime formatting on the bottom X-axis
			plot.Axes.DateTimeTicksBottom();

			plot.Title(ticker + $", {percentOfPassedOosRuns:P} runs passed OOS filter");
			plot.Axes.AutoScale();

			FormsPlotViewer.Launch(plot);
		}
	}
}