using System.Collections.Concurrent;
using LinqToDB;

namespace StrategyTester;

public class WalkForwardResult
{
	public IList<RunReport>                                            OOSRunReports;
	public List<(DateTime, decimal)>                                   OOSEquityCurve;
	public ConcurrentDictionary<(decimal x, decimal y), List<decimal>> InSampleHeatMap;
	public int                                                         OOSRunsPassedOOSFilterCount;
	
	public decimal FinalInvestment => OOSRunReports.Last().FinalInvestment;

	public decimal PercentOfOOSRunsPassedFilter => OOSRunsPassedOOSFilterCount / OOSRunReports.Count;

	public WalkForwardResult(
		IList<RunReport>                                            ooSRunReports, 
		List<(DateTime, decimal)>                                   ooSEquityCurve, 
		ConcurrentDictionary<(decimal x, decimal y), List<decimal>> iSHeatMap, 
		int                                                         ooSRunsPassedOOSFilterCount)
	{
		OOSRunReports               = ooSRunReports;
		OOSEquityCurve              = ooSEquityCurve;
		InSampleHeatMap             = iSHeatMap;
		OOSRunsPassedOOSFilterCount = ooSRunsPassedOOSFilterCount;
	}
}