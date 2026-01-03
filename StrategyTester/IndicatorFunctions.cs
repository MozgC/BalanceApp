using System.Collections.Concurrent;

namespace StrategyTester
{
	public static class IndicatorFunctions
	{
		private static ConcurrentDictionary<(string ticker, DateTime currentDate, int periods), decimal> _maDict  = new ConcurrentDictionary<(string ticker, DateTime currentDate, int periods), decimal>();
		private static ConcurrentDictionary<(string ticker, DateTime currentDate, int periods), decimal> _emaDict = new ConcurrentDictionary<(string ticker, DateTime currentDate, int periods), decimal>();

		public static decimal? MovingAverage(string ticker, IList<StockPrice> dataPoints, int periods, int currentDayIndex)
		{
			if (currentDayIndex < periods) return null;

			var currentDate = dataPoints[currentDayIndex].Date;
				
			if (_maDict.TryGetValue((ticker, currentDate, periods), out decimal res))
				return res;

			decimal sum = 0;

			for (int i = currentDayIndex; i > currentDayIndex - periods; i--)
			{
				sum += dataPoints[i].ClosingPrice;
			}

			return _maDict[(ticker, currentDate, periods)] = sum / periods;
		}

		public static decimal? ExponentialMovingAverage(string ticker, IList<StockPrice> dataPoints, int periods, int currentDayIndex)
		{
			if (currentDayIndex < periods - 1) return null;  // Need at least 'periods' points for SMA seed

			var currentDate = dataPoints[currentDayIndex].Date;
				
			if (_emaDict.TryGetValue((ticker, currentDate, periods), out decimal res))
				return res;

			var alpha = 2m / (periods + 1m);

			// Allocate array for all EMAs up to currentDayIndex (0-based indexing)
			var emas = new decimal[currentDayIndex + 1];

			// Step 1: Seed the first EMA as SMA of first 'periods' closing prices
			decimal sma = 0m;
			for (int i = 0; i < periods; i++)
			{
				sma += dataPoints[i].ClosingPrice;
			}
			emas[periods - 1] = sma / periods;  // EMA at index periods-1

			var sma2 = MovingAverage(ticker, dataPoints, periods, dataPoints.Count - 1);

			// Step 2: Recurse forward from seed to currentDayIndex
			for (int i = periods; i <= currentDayIndex; i++)
			{
				emas[i] = alpha * dataPoints[i].ClosingPrice + (1 - alpha) * emas[i - 1];
			}

			return _emaDict[(ticker, currentDate, periods)] = emas[currentDayIndex];
		}
	}
}
