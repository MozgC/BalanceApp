using CodeJam.Threading;
using System.Collections.Concurrent;

namespace StrategyTester
{
	public class WalkForwardTester
	{
		private readonly Func<IEnumerable<Strategy>>                  _getStrategiesWithDifferentParams;
		private readonly Func<(RunReport best, RunReport last), bool> _areLastResultsBetter;
		
		public static WalkForwardTester CreateWalkForwardTesterExample()
		{
			IEnumerable<Strategy> GenerateStrategies()
			{
				int minMA   = 20;
				int maCount = 50;
				
				for (int maDays = minMA; maDays < minMA + maCount; maDays++)
				{
					for (int emaDays = 7; emaDays <= (maDays + 1) / 2; emaDays += 1)
					{
						yield return new MACrossOverStrategy("SOXL", emaDays, maDays);
					}
				}
			}
			
			return new WalkForwardTester(GenerateStrategies, x => x.last.FinalProfitRatio > x.best.FinalProfitRatio);
		}

		public WalkForwardTester(
			Func<IEnumerable<Strategy>>                  getGetStrategiesWithDifferentParamsWithDifferentParams, 
			Func<(RunReport best, RunReport last), bool> areLastResultsBetter)
		{
			_getStrategiesWithDifferentParams  = getGetStrategiesWithDifferentParamsWithDifferentParams;
			_areLastResultsBetter              = areLastResultsBetter;
		}

		public WalkForwardResult Run(
			IList<StockPrice>     allData, 
			decimal               initialInvestment, 
			int                   inSampleMonths, 
			int                   outOfSampleMonths, 
			Func<RunReport, bool> iisFilter,
			Func<RunReport, bool> oosFilter = null)
		{
			var monthlyGroups = allData
				.GroupBy(x => new { x.Date.Year, x.Date.Month })
				.OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
				.Select(g => g.OrderBy(x => x.Date).ToList())
				.ToList();

			var equityCurve            = new List<(DateTime, decimal)> { (allData[0].Date, initialInvestment) };
			List<RunReport> oosReports = new ();

			var heatMap = new ConcurrentDictionary<(decimal x, decimal y), List<decimal>>();

			int ooSRunsPassedOOSFilterCount = 0;
				
			for (int oosStartMonth = inSampleMonths; oosStartMonth < monthlyGroups.Count; oosStartMonth += outOfSampleMonths)
			{
				int isEndMonth  = oosStartMonth;  // End of In-Sample
				int oosEndMonth = Math.Min(oosStartMonth + outOfSampleMonths, monthlyGroups.Count);

				// Prepare InSample data
				var isData = monthlyGroups.Take(isEndMonth).SelectMany(x => x).ToList();

				var (bestStrategy, bestRunReport) = FindBestStrategyOnInSample(isData, iisFilter, heatMap);
				
				Console.WriteLine($"Optimization from {monthlyGroups[0][0].Date:yyyy-MM} till {monthlyGroups[isEndMonth-1][0].Date:yyyy-MM} → best strategy: {bestStrategy.ParametersDescription}");
				
				// OOS test
				var oosData = monthlyGroups.Skip(isEndMonth).Take(oosEndMonth - isEndMonth).SelectMany(x => x).ToList();
				
				var oosRunReport  = bestStrategy.Run(oosData, initialInvestment);

				if (oosFilter != null && oosFilter(oosRunReport))
					ooSRunsPassedOOSFilterCount++;
				
				// next OOS run will continue from current OOS run's final investment
				initialInvestment = oosRunReport.FinalInvestment;
				oosReports .Add(oosRunReport);
				equityCurve.Add((oosData.Last().Date, oosRunReport.FinalInvestment));
			}

			return new WalkForwardResult(oosReports, equityCurve, heatMap, ooSRunsPassedOOSFilterCount);
		}

		public (Strategy, RunReport) FindBestStrategyOnInSample(
			IList<StockPrice> inSample,
			Func<RunReport, bool> iisFilter,
			ConcurrentDictionary<(decimal x, decimal y), List<decimal>> heatmap)
		{
			RunReport bestRunReport = null;
			Strategy  bestStrategy  = null;
			var bestLock            = new object();

			ParallelExtensions.RunInParallel(_getStrategiesWithDifferentParams(), Environment.ProcessorCount - 1, strategy =>
			{
				var runReport = strategy.Run(inSample);

				if (!iisFilter(runReport))
					return;

				// Safely get-or-create the list in the concurrent dictionary and lock it when mutating it.
				var list = heatmap.GetOrAdd(runReport.HeatmapKey, _ => new List<decimal>());
				lock (list)
				{
					list.Add(runReport.HeatmapValue);
				}

				// Update best candidate under a lock to make read/compare/write atomic.
				lock (bestLock)
				{
					if (bestRunReport == null || _areLastResultsBetter((bestRunReport, runReport)))
					{
						bestRunReport = runReport;
						bestStrategy  = strategy;
					}
				}
			});

			return (bestStrategy, bestRunReport);
		}
	}
}
