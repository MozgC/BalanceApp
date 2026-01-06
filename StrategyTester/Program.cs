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
			decimal initialInvestment = 1000;
			Console.WriteLine($"Initial investment: {initialInvestment:C}");

			_client = new YahooFinanceProvider();
			var dataPoints = _client.GetLast10YearsOfPrices(security);

			DateTime? fromDate = new DateTime(2014, 5, 22);
			Console.WriteLine("Starting from: " + fromDate);
			DateTime? toDate = new DateTime(2029, 1, 1);

			dataPoints = dataPoints.SkipWhile(dp => dp.Date < fromDate).Where(dp => dp.Date < toDate).ToList();

			var result = WalkForwardTestOnMovingAveragesCrossoverStrategy(security, initialInvestment, dataPoints);
			
			Console.WriteLine(Environment.NewLine + "Buy & Hold:");
			decimal buyAndHold = CalcBuyAndHold(initialInvestment, dataPoints.SkipWhile(x => x.Date <= result.OOSRunReports[0].StartDate).ToList());
			Console.WriteLine($"{buyAndHold:C}");

			Console.WriteLine(Environment.NewLine + "Starting monkey tests..." + Environment.NewLine);
			
			var monkeyTester = new MonkeyTester();
			var monkeyTestResults = monkeyTester.Run(dataPoints.SkipWhile(x => x.Date < result.OOSRunReports[0].StartDate).ToList(), result.OOSRunReports.Last().Strategy);
			
			Console.WriteLine("Monkey test results:" + Environment.NewLine);
			Console.WriteLine($"90% of monkey tests with random entry returned below {monkeyTestResults.RandomEnter90PercentFinalInvestment:C}");
			Console.WriteLine($"90% of monkey tests with random exit returned below {monkeyTestResults.RandomExit90PercentFinalInvestment:C}");
			Console.WriteLine($"90% of monkey tests with random entry and exit returned below {monkeyTestResults.RandomEnterAndExit90PercentFinalInvestment:C}");
			
			Console.WriteLine();
			
			Console.WriteLine($"Is our strategy better than 90% of monkey test runs with random entry? - {result.FinalInvestment > monkeyTestResults.RandomEnter90PercentFinalInvestment}");
			Console.WriteLine($"Is our strategy better than 90% of monkey test runs with random exit? - {result.FinalInvestment > monkeyTestResults.RandomExit90PercentFinalInvestment}");
			Console.WriteLine($"Is our strategy better than 90% of monkey test runs with random entry and exit? - {result.FinalInvestment > monkeyTestResults.RandomEnterAndExit90PercentFinalInvestment}");

			Console.Read();
		}

		private static WalkForwardResult WalkForwardTestOnMovingAveragesCrossoverStrategy(string ticker, decimal initialInvestment, IList<StockPrice> dataPoints)
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

			var result = walkForwardTester.Run(dataPoints, initialInvestment, 48, 12, rr => rr.TotalTradeCount >= 30, rr => rr.TotalTradeCount >= 6);

			ScottPlotHelper.DrawEquityCurve(ticker, result.OOSRunsPassedOOSFilterCount / (decimal)result.OOSRunReports.Count, result.OOSEquityCurve);
			ScottPlotHelper.ShowHeatMap(result.InSampleHeatMap, false);

			return result;
		}
	}
}