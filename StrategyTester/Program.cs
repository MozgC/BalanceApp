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

			string security = "VOO";
			Console.WriteLine("Security: " + security);

			_client = new YahooFinanceProvider();
			var dataPoints = _client.GetLast10YearsOfPrices(security);

			DateTime? fromDate = new DateTime(2014, 5, 22);
			Console.WriteLine("Starting from: " + fromDate);
			DateTime? toDate = new DateTime(2029, 1, 1);

			dataPoints = dataPoints.SkipWhile(dp => dp.Date < fromDate).Where(dp => dp.Date < toDate).ToList();

			var result = WalkForwardTestOnMovingAveragesCrossoverStrategy(security, dataPoints);
			
			Console.WriteLine(Environment.NewLine + "Buy & Hold:");
			decimal buyAndHold = CalcBuyAndHold(1000, dataPoints.SkipWhile(x => x.Date <= result.OOSRunReports[0].StartDate).ToList());
			Console.WriteLine($"{buyAndHold:C}");

			Console.Read();
		}

		private static WalkForwardResult WalkForwardTestOnMovingAveragesCrossoverStrategy(string ticker, IList<StockPrice> dataPoints)
		{
			IEnumerable<Strategy> GenerateStrategies()
			{
				int minMA   = 20;
				int maCount = 181;

				for (int maDays = minMA; maDays < minMA + maCount; maDays++)
				{
					for (int emaDays = 7; emaDays <= (maDays + 1) / 2; emaDays += 1)
					{
						yield return new MACrossOverStrategy(ticker, emaDays, maDays, true) { HoldingPeriodDays = 30 };
					}
				}
			}

			var walkForwardTester = new WalkForwardTester(GenerateStrategies, x => x.last.FinalProfitRatio > x.best.FinalProfitRatio);

			var result = walkForwardTester.Run(dataPoints, 1000, 48, 12, rr => rr.TotalTradeCount >= 20, rr => rr.TotalTradeCount >= 6);

			ScottPlotHelper.DrawEquityCurve(ticker, result.OOSRunsPassedOOSFilterCount / (decimal)result.OOSRunReports.Count, result.OOSEquityCurve);
			ScottPlotHelper.ShowHeatMap(result.InSampleHeatMap, false);

			return result;
		}
	}
}