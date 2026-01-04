namespace StrategyTester
{
	partial class Program
	{
		private const int                  _holdingPeriodDays = 30;
		private static IStockPriceProvider _client;

		[STAThread]
		static void Main(string[] args)
		{
			Console.OutputEncoding = System.Text.Encoding.UTF8;
			Console.InputEncoding = System.Text.Encoding.UTF8;

			string security = "SOXL";
			Console.WriteLine("Security: " + security);

			_client = new YahooFinanceProvider();
			var dataPoints = _client.GetLast10YearsOfPrices(security);

			DateTime? fromDate = new DateTime(2014, 5, 22);
			Console.WriteLine("Starting from: " + fromDate);
			DateTime? toDate = new DateTime(2029, 1, 1);

			dataPoints = dataPoints.SkipWhile(dp => dp.Date < fromDate).Where(dp => dp.Date < toDate).ToList();

			TestMainStrategy(security, dataPoints);
		}

		private static void TestMainStrategy(string ticker, IList<StockPrice> dataPoints)
		{
			IEnumerable<Strategy> GenerateStrategies()
			{
				int minMA   = 20;
				int maCount = 10;

				for (int maDays = minMA; maDays < minMA + maCount; maDays++)
				{
					for (int emaDays = 7; emaDays <= (maDays + 1) / 2; emaDays += 1)
					{
						yield return new MACrossOverStrategy("SOXL", emaDays, maDays);
					}
				}
			}

			var walkForwardTester = new WalkForwardTester(GenerateStrategies, x => x.last.FinalProfitRatio > x.best.FinalProfitRatio);

			var result = walkForwardTester.Run(dataPoints, 1000, 48, 12, rr => rr.TotalTradeCount > 40, rr => rr.TotalTradeCount >= 6);

			ScottPlotHelper.DrawEquityCurve(result.OOSEquityCurve);
			ScottPlotHelper.ShowHeatMap(result.InSampleHeatMap, false);
		}
	}
}