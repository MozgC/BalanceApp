using System.Collections.Concurrent;
using CodeJam.Threading;

namespace StrategyTester;

public class MonkeyTester
{
	public MonkeyTestResult Run(IList<StockPrice> dataPoints, Strategy strategy)
	{
		ConcurrentBag<RunReport> randomEntryReports        = new();
		ConcurrentBag<RunReport> randomExitReports         = new();
		ConcurrentBag<RunReport> randomEntryAndExitReports = new();

		const int numberOfRuns = 8000;
				
		ParallelExtensions.RunInParallel(Enumerable.Range(0, numberOfRuns), i =>
		{
			var strat = FastCloner.FastCloner.ShallowClone(strategy);
				
			int x = Random.Shared.Next();
			strat.OverrideEntryLogic(() => x % 5 == 0);

			randomEntryReports.Add(strat.Run(dataPoints, true));
		});
		
		strategy.OverrideEntryLogic(null);

		ParallelExtensions.RunInParallel(Enumerable.Range(0, numberOfRuns), i =>
		{
			var strat = FastCloner.FastCloner.ShallowClone(strategy);
			
			int x = Random.Shared.Next();
			strat.OverrideExitLogic(() => x % 5 == 0);

			randomExitReports.Add(strat.Run(dataPoints, true));
		});
		
		ParallelExtensions.RunInParallel(Enumerable.Range(0, numberOfRuns), i =>
		{
			var strat = FastCloner.FastCloner.ShallowClone(strategy);
			
			int x = Random.Shared.Next();
			strat.OverrideEntryLogic(() => x % 5 == 0);
			int y = Random.Shared.Next();
			strat.OverrideExitLogic(() => y % 5 == 0);

			randomEntryAndExitReports.Add(strat.Run(dataPoints, true));
		});
		
		const int ninetyPercentRuns = (int)(numberOfRuns * 0.9m);

		return new MonkeyTestResult(
			randomEntryReports       .OrderBy(x => x.FinalInvestment).Skip(ninetyPercentRuns).First().FinalInvestment,
			randomExitReports        .OrderBy(x => x.FinalInvestment).Skip(ninetyPercentRuns).First().FinalInvestment,
			randomEntryAndExitReports.OrderBy(x => x.FinalInvestment).Skip(ninetyPercentRuns).First().FinalInvestment);
	}
}