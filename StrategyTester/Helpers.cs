namespace StrategyTester
{
	public static class Helpers
	{
		public static double GetYears(IList<StockPrice> dataPoints)
		{
			return (dataPoints.Last().Date - dataPoints[0].Date).TotalDays / 365;
		}

		public static double GetAvgPercentPerYear(decimal initialInvestment, decimal finalAmount, double years)
		{
			return Math.Pow((double)(finalAmount / initialInvestment), 1 / years) * 100 - 100;
		}
	}
}
