using CodeJam.Threading;
using System.Collections.Concurrent;

namespace StrategyTester
{
	public class WalkForwardTester
	{
		private readonly Func<IEnumerable<Strategy>>                  _getStrategiesWithDifferentParams;
		private readonly Func<(RunReport best, RunReport last), bool> _areLastResultsBetter;
		
		public WalkForwardTester(
			Func<IEnumerable<Strategy>>                  getGetStrategiesWithDifferentParamsWithDifferentParams, 
			Func<(RunReport best, RunReport last), bool> areLastResultsBetter)
		{
			_getStrategiesWithDifferentParams  = getGetStrategiesWithDifferentParamsWithDifferentParams;
			_areLastResultsBetter              = areLastResultsBetter;
		}

		public WalkForwardResult Run(
			IList<StockPrice>      allData, 
			decimal                initialInvestment, 
			int                    inSampleMonths, 
			int                    outOfSampleMonths, 
			Func<RunReport, bool>  iisFilter,
			Func<RunReport, bool>? oosFilter = null)
		{
			var equityCurve                 = new List<(DateTime, decimal)>();
			List<RunReport> oosReports      = new ();
			var heatMap                     = new ConcurrentDictionary<(decimal x, decimal y), List<decimal>>();
			int ooSRunsPassedOOSFilterCount = 0;
			decimal initialShares           = 0;
			decimal initialCash             = initialInvestment;
			StockPrice? lastBuy             = null;
			bool firstOosRun                = true;
			
			Console.Write(Environment.NewLine + "Beginning Walk-Forward..." + Environment.NewLine);

			foreach (var inSampleAndOutSampleData in GetInSampleAndOutSampleData(allData, inSampleMonths, outOfSampleMonths))
			{
				if (equityCurve.Count == 0)
					equityCurve.Add((inSampleAndOutSampleData.OutOfSample[0].Date, initialInvestment));
				
				Console.WriteLine(Environment.NewLine + $"Optimization from {inSampleAndOutSampleData.InSample[0].Date:yyyy-MM} till {inSampleAndOutSampleData.InSample.Last().Date:yyyy-MM}...");
				
				var (bestStrategy, _) = FindBestStrategyOnInSample(inSampleAndOutSampleData.InSample, iisFilter, heatMap);

				if (bestStrategy == null)
					throw new Exception("No best strategy has been found, most likely all results failed the InSample filter.");
				
				Console.WriteLine($"Best strategy: {bestStrategy.ParametersDescription}");

				var oosRunReport  = bestStrategy.Run(inSampleAndOutSampleData.OutOfSample, firstOosRun, initialInvestment, initialCash, initialShares, lastBuy);

				bool passedOOSFilter = oosFilter == null || oosFilter(oosRunReport);
				
				if (passedOOSFilter)
					ooSRunsPassedOOSFilterCount++;
				
				// next OOS run will continue from current OOS run's final investment
				initialCash       = oosRunReport.FinalCash;
				initialShares     = oosRunReport.FinalShares;
				lastBuy           = oosRunReport.LastBuy;
				initialInvestment = oosRunReport.FinalInvestment;

				oosReports .Add(oosRunReport);
				equityCurve.Add((inSampleAndOutSampleData.OutOfSample.Last().Date, oosRunReport.FinalInvestment));
				firstOosRun = false;
				
				Console.WriteLine($"OOS run: {oosRunReport.InitialInvestment:C} -> {oosRunReport.FinalInvestment:C}, Passed OOS Filter = {passedOOSFilter}");
			}

			return new WalkForwardResult(oosReports, equityCurve, heatMap, ooSRunsPassedOOSFilterCount);
		}
		
		public class InSampleAndOutSampleData
		{
			public IList<StockPrice> InSample    { get; }
			public IList<StockPrice> OutOfSample { get; }
			
			public InSampleAndOutSampleData(IList<StockPrice> inSample, IList<StockPrice> outOfSample)
			{
				InSample    = inSample;
				OutOfSample = outOfSample;
			}
		}

		public static IEnumerable<InSampleAndOutSampleData> GetInSampleAndOutSampleData(
			IList<StockPrice> allData,
			int               inSampleMonths, 
			int               outOfSampleMonths)
		{
			var monthlyGroups = allData
				.GroupBy(x => new { x.Date.Year, x.Date.Month })
				.OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
				.Select (g => g.OrderBy(x => x.Date).ToList())
				.ToList ();
			
			for (int oosStartMonth = inSampleMonths; oosStartMonth + outOfSampleMonths <= monthlyGroups.Count; oosStartMonth += outOfSampleMonths)
			{
				int inSampleEndMonth  = oosStartMonth;  // End of In-Sample

				// On last iteration use a longer OOS period.
				// For example, if now is February 2026 and we have 48 months IS period & 12 months OOS period,
				// then the last InSample period should be January 2021 - December 2024,
				// and the last OOS period should be January 2025 - February 2026 (14 months).
				bool lastIteration = oosStartMonth + 2 * outOfSampleMonths > monthlyGroups.Count;

				int oosEndMonth = lastIteration
					? monthlyGroups.Count
					: oosStartMonth + outOfSampleMonths;

				// Prepare InSample data

				// Unanchored:
				// var isData = monthlyGroups.Take(isEndMonth).SelectMany(x => x).ToList();

				// Anchored:
				var isData = monthlyGroups.Skip(oosStartMonth - inSampleMonths).Take(inSampleMonths).SelectMany(x => x).ToList();

				// OOS test
				var oosData = monthlyGroups
					.Skip(inSampleEndMonth)
					.Take(oosEndMonth - inSampleEndMonth)
					.SelectMany(x => x)
					.ToList();

				yield return new InSampleAndOutSampleData(isData, oosData);
			}
		}

		public (Strategy?, RunReport?) FindBestStrategyOnInSample(
			IList<StockPrice> inSample,
			Func<RunReport, bool> iisFilter,
			ConcurrentDictionary<(decimal x, decimal y), List<decimal>> heatmap)
		{
			RunReport? bestRunReport = null;
			Strategy?  bestStrategy  = null;
			var bestLock             = new object();

			ParallelExtensions.RunInParallel(
				_getStrategiesWithDifferentParams(), 
				//Environment.ProcessorCount - 1, 
				1, // 1 thread for testing
				strategy =>
				{
					var runReport = strategy.Run(inSample, true);

					if (!iisFilter(runReport))
						return;

					// Safely get-or-create the list in the concurrent dictionary and lock it when mutating it.
					var list = heatmap.GetOrAdd(runReport.HeatmapKey, _ => new List<decimal>());
					lock (list)
					{
						list.Add(runReport.HeatmapValue);
					}

					// Update the best candidate under a lock to make read/compare/write atomic.
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
