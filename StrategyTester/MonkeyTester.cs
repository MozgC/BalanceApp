namespace StrategyTester;

public class MonkeyTester
{
	public MonkeyTestResult Run(IList<StockPrice> dataPoints, Strategy strategy)
	{
		List<RunReport> randomEntryReports        = new();
		List<RunReport> randomExitReports         = new();
		List<RunReport> randomEntryAndExitReports = new();
				
		for (int i = 0; i < 8000; i++)
		{
			int x = i;
			strategy.OverrideEntryLogic(() => x % 30 == 0);

			randomEntryReports.Add(strategy.Run(dataPoints, true));
		}
			
		strategy.OverrideEntryLogic(null);
			
		for (int i = 0; i < 8000; i++)
		{
			int x = i;
			strategy.OverrideExitLogic(() => x % 30 == 0);

			randomExitReports.Add(strategy.Run(dataPoints, true));
		}

		for (int i = 0; i < 8000; i++)
		{
			int x = i;
			strategy.OverrideEntryLogic(() => x % 30 == 0);
			strategy.OverrideExitLogic(() => x % 30 == 0);

			randomEntryAndExitReports.Add(strategy.Run(dataPoints, true));
		}

		return new MonkeyTestResult(
			randomEntryReports       .OrderBy(x => x.FinalInvestment).Skip(7200).First().FinalInvestment,
			randomExitReports        .OrderBy(x => x.FinalInvestment).Skip(7200).First().FinalInvestment,
			randomEntryAndExitReports.OrderBy(x => x.FinalInvestment).Skip(7200).First().FinalInvestment);
	}
}