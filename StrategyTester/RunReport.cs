namespace StrategyTester
{
	public class RunReport
	{
		public RunReport() { }

		public RunReport(
			string                 strategyName,
			string                 strategyDescriptionAndParameters,
			DateTime               startDate,
			DateTime               endDate,
			decimal                initialInvestment,
			decimal                finalInvestment,
			decimal                finalProfitRatio,
			int                    totalTradeCount,
			decimal                avgTradeCountPerYear,
			decimal                maxDrawdownPercent,
			decimal                returnToDrawdownRatio,
			decimal                profitFactor,
			string                 debug)
		{
			StrategyName                     = strategyName;
			StrategyDescriptionAndParameters = strategyDescriptionAndParameters;
			StartDate                        = startDate;
			EndDate                          = endDate;
			InitialInvestment                = initialInvestment;
			FinalInvestment                  = finalInvestment;
			FinalProfitRatio                 = finalProfitRatio;
			TotalTradeCount                  = totalTradeCount;
			AvgTradeCountPerYear             = avgTradeCountPerYear;
			MaxDrawdownPercent               = maxDrawdownPercent;
			ReturnToDrawdownRatio            = returnToDrawdownRatio;
			ProfitFactor                     = profitFactor;
			Debug                            = debug;
		}
		
		public string   StrategyName;
		public string   StrategyDescriptionAndParameters;
		public DateTime StartDate;
		public DateTime EndDate;

		// If the investment went from $1000 to $1100, it will be 1.1
		public decimal  FinalProfitRatio;

		public decimal  InitialInvestment;
		public decimal  FinalInvestment;

		public int      TotalTradeCount;
		public decimal  AvgTradeCountPerYear;
		public decimal  MaxDrawdownPercent;

		/*
		One of the most important indicators. Calculated as average annualized return divided by max drawdown.
		One way to think about this ratio is "it takes Y risk to make X". In this case, Y is the drawdown, and X is the profit return.
		Obviously, high values of this ratio are better. I generally look for return/drawdown ratios above 2.0, although I will	accept lower values in special circumstances.
		In my experience, I find that ratios above 2.0 will usually produce acceptable results in the real world of trading live.
		*/
		public decimal  ReturnToDrawdownRatio;

		// Gross profits divided by gross losses
		public decimal  ProfitFactor;
		public string   Debug;

		public (decimal x, decimal y) HeatmapKey;
		public decimal                HeatmapValue;

		public void SetHeatmapKeyAndValue((decimal x, decimal y) heatmapKey, decimal heatmapValue)
		{
			HeatmapKey   = heatmapKey;
			HeatmapValue = heatmapValue;
		}
	}
}
