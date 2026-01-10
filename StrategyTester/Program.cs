using System.Diagnostics;

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

			var walkForwardResult = WalkForwardTestOnMovingAveragesCrossoverStrategy(security, initialInvestment, dataPoints);
			
			Console.WriteLine(Environment.NewLine + "Buy & Hold:");
			decimal buyAndHold = CalcBuyAndHold(initialInvestment, dataPoints.SkipWhile(x => x.Date <= walkForwardResult.OOSRunReports[0].StartDate).ToList());
			Console.WriteLine($"{buyAndHold:C}");

			Console.WriteLine(Environment.NewLine + "Starting monkey tests..." + Environment.NewLine);
			
			var monkeyTester = new MonkeyTester();
			var monkeyTestResults = monkeyTester.Run(dataPoints.SkipWhile(x => x.Date < walkForwardResult.OOSRunReports[0].StartDate).ToList(), walkForwardResult.OOSRunReports.Last().Strategy);
			
			Console.WriteLine("Monkey test results:" + Environment.NewLine);
			Console.WriteLine($"90% of monkey tests with random entry returned below {monkeyTestResults.RandomEnter90PercentFinalInvestment:C}");
			Console.WriteLine($"90% of monkey tests with random exit returned below {monkeyTestResults.RandomExit90PercentFinalInvestment:C}");
			Console.WriteLine($"90% of monkey tests with random entry and exit returned below {monkeyTestResults.RandomEnterAndExit90PercentFinalInvestment:C}");
			
			Console.WriteLine();
			
			Console.WriteLine($"Is our strategy better than 90% of monkey test runs with random entry? - {walkForwardResult.FinalInvestment > monkeyTestResults.RandomEnter90PercentFinalInvestment}");
			Console.WriteLine($"Is our strategy better than 90% of monkey test runs with random exit? - {walkForwardResult.FinalInvestment > monkeyTestResults.RandomExit90PercentFinalInvestment}");
			Console.WriteLine($"Is our strategy better than 90% of monkey test runs with random entry and exit? - {walkForwardResult.FinalInvestment > monkeyTestResults.RandomEnterAndExit90PercentFinalInvestment}");
			
			const int monteCarloSimulations = 2500;
			Console.Write(Environment.NewLine + $"Starting Monte Carlo Simulations ({monteCarloSimulations} permutations)..." + Environment.NewLine);

			var mctResults = MonteCarloTester.Run(walkForwardResult, dataPoints, monteCarloSimulations);

			Console.Write(Environment.NewLine + "Results:" + Environment.NewLine);

			foreach (var mctResult in mctResults)
			{
				Console.WriteLine($"OOS Period: {mctResult.OriginalOOSReport.StartDate:Y} - {mctResult.OriginalOOSReport.EndDate:Y}");
				Console.WriteLine($"Median Drawdown: {mctResult.MedianDrawdown/100:P}");
				Console.WriteLine($"Median Profit Ratio: {mctResult.MedianProfitRatio:N}");
				Console.WriteLine($"Median Return to Drawdown ratio: {mctResult.MedianReturnToDrawdownRatio:N}");
				Console.WriteLine($"Probability of making money: {mctResult.ProbabilityOfMakingMoney:P}");
				
				Console.WriteLine();
			}

			ScottPlotHelper.DrawEquityCurve(security, walkForwardResult.OOSRunsPassedOOSFilterCount / (decimal)walkForwardResult.OOSRunReports.Count, walkForwardResult.OOSEquityCurve);
			ScottPlotHelper.ShowHeatMap(walkForwardResult.InSampleHeatMap, false);

			Console.Read();
		}

		private static WalkForwardResult WalkForwardTestOnMovingAveragesCrossoverStrategy(string ticker, decimal initialInvestment, IList<StockPrice> dataPoints)
		{
			IEnumerable<Strategy> GenerateStrategies()
			{
				int minMA   = 20;
				int maCount = 20;

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

			return result;
		}
	}
}