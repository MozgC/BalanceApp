using System.Collections.Concurrent;
using System.Diagnostics;
using CodeJam.Threading;

namespace StrategyTester;

public static class MonteCarloTester
{
	/// <summary>
	/// Thread-safe addition to a double value using lock-free CAS (Compare-And-Swap) loop.
	/// </summary>
	/// <param name="increment">The value to add</param>
	/// <param name="total">Reference to the double value you want to increment atomically</param>
	public static void Add(double increment, ref double total)
	{
		double original;
		double newValue;

		do
		{
			original = total;                    // Current value (volatile read is implicit here)
			newValue = original + increment;
		}
		while (Interlocked.CompareExchange(ref total, newValue, original) != original);
	}
	
	public static IList<MonteCarloResult> Run(WalkForwardResult wfr, IList<StockPrice> dataPoints, int monteCarloSimulations = 1000)
	{
		var       results    = new List<MonteCarloResult>();
		RunReport prevReport = null;
		
		foreach (var oosRunReport in wfr.OOSRunReports)
		{
			decimal maxDrawdowns = 0;
			decimal profitRatios = 0;
			int     madeMoney = 0;
			decimal returnToDrawdownRatios = 0;

			var dps = dataPoints.SkipWhile(x => x.Date < oosRunReport.StartDate).TakeWhile(x => x.Date <= oosRunReport.EndDate).ToList();

			var x = GenerateMonteCarloShuffles(dps, monteCarloSimulations).ToList();

			ParallelExtensions.RunInParallel(x, shuffledDataPoints =>
			{
				var s           = oosRunReport.Strategy.ShallowClone();
				var mcRunReport = s.Run(shuffledDataPoints, false, oosRunReport.InitialInvestment, oosRunReport.InitialCash, oosRunReport.InitialShares, oosRunReport.InitialShares > 0? prevReport?.LastBuy : null);

				lock (results)
				{
					maxDrawdowns           += mcRunReport.MaxDrawdownPercent;
					profitRatios           += mcRunReport.FinalProfitRatio;
					madeMoney              += mcRunReport.FinalProfitRatio > 1 ? 1 : 0;
					returnToDrawdownRatios += mcRunReport.ReturnToDrawdownRatio;
				}
			});

			results.Add(new MonteCarloResult
			{
				OriginalOOSReport           = oosRunReport,
				MedianDrawdown              = maxDrawdowns / monteCarloSimulations,
				MedianProfitRatio           = profitRatios / monteCarloSimulations,
				ProbabilityOfMakingMoney    = (decimal)madeMoney / monteCarloSimulations,
				MedianReturnToDrawdownRatio = returnToDrawdownRatios / monteCarloSimulations
			});
			
			prevReport = oosRunReport;
		}

		return results;
	}
	
	public static IEnumerable<List<StockPrice>> GenerateMonteCarloShuffles(
		IEnumerable<StockPrice> source,
		int simulations = 1000)
	{
		var data = source.ToArray();
		var rnd  = new Random();

		var dailyReturns = new List<decimal>(data.Length) { 1 };

		for (int i = 1; i < data.Length; i++)
		{
			dailyReturns.Add(data[i].ClosingPrice / data[i-1].ClosingPrice);
		}

		for (int i = 0; i < simulations; i++)
		{
			var shuffledReturns = dailyReturns.OrderBy(_ => rnd.NextDouble()).ToList();
			var shuffledPrices  = new List<decimal>(data.Length);

			decimal lastPrice = data[0].ClosingPrice;

			for (int j = 0 ; j < data.Length; j++)
			{
				shuffledPrices.Add(lastPrice * shuffledReturns[j]);
				lastPrice *= shuffledReturns[j];
			}

			var shuffled = shuffledPrices
				.Zip(data.Select(x => x.Date), (price, date) => new StockPrice(date, price))
				.ToList();

			yield return shuffled;
		}
	}
}