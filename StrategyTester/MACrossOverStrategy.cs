namespace StrategyTester
{
	public class MACrossOverStrategy : Strategy
	{
		private readonly int _emaDays;
		private readonly int _maDays;
		
		MACrossOverStrategy(string ticker, IList<StockPrice> dataPoints, int emaDays, int maDays) 
			: base(
				ticker, 
				dataPoints, 
				"Simple MA & EMA crossover strategy", 
				$"If {emaDays} EMA crosses over below {maDays} MA - we sell. If {emaDays} EMA crosses over above {maDays} - we buy.")
		{
			_emaDays = emaDays;
			_maDays  = maDays;
		}

		protected override bool CalcDailyParametersAndDecideIfCanBuyOrSell(StockPrice dp)
		{
			decimal price = dp.ClosingPrice;

			bool canSell = HoldingPeriodDays == 0 || dp.Date.Date - LastBuy.Date.Date > TimeSpan.FromDays(HoldingPeriodDays);

			if (Cash > 0 && !canSell)
				return false;

			//todo:
			int i = 10;
			
			var ma      = IndicatorFunctions.MovingAverage(Ticker, DataPoints, _maDays, i);
			var maPrev  = IndicatorFunctions.MovingAverage(Ticker, DataPoints, _maDays, i - 1);
			var ema     = IndicatorFunctions.ExponentialMovingAverage(Ticker, DataPoints, _emaDays, i);
			var emaPrev = IndicatorFunctions.ExponentialMovingAverage(Ticker, DataPoints, _emaDays, i - 1);

			if (ma == null || ema == null)
				return false;

			return true;
		}

		public RunReport Run()
		{
			Initialize(true);

			for (int i = 0; i < DataPoints.Count; i++)
			{
				var dp        = DataPoints[i];
				decimal price = dp.ClosingPrice;

				bool canSell = HoldingPeriodDays == 0 || dp.Date.Date - LastBuy.Date.Date > TimeSpan.FromDays(HoldingPeriodDays);

				if (Cash > 0 && !canSell)
					continue;

				var ma      = IndicatorFunctions.MovingAverage(Ticker, DataPoints, _maDays, i);
				var maPrev  = IndicatorFunctions.MovingAverage(Ticker, DataPoints, _maDays, i - 1);
				var ema     = IndicatorFunctions.ExponentialMovingAverage(Ticker, DataPoints, _emaDays, i);
				var emaPrev = IndicatorFunctions.ExponentialMovingAverage(Ticker, DataPoints, _emaDays, i - 1);

				if (ma == null || ema == null)
					continue;

				if (Shares > 0 && emaPrev > maPrev && ema < ma)
				{
					Sell(dp);
				}
				// if price is below  MA and above EMA - buy
				else if (Cash > 0 && emaPrev < maPrev && ema > ma)
				{
					Buy(dp);
				}
			}

			var lastPrice = DataPoints.Last().ClosingPrice;

			if (Cash == 0)
				Cash = Shares * lastPrice;

			var avgPercent = Helpers.GetAvgPercentPerYear(InitialInvestment, Cash, Helpers.GetYears(DataPoints));
			Debug += $"Avg year %: {avgPercent:N}";

			decimal profitFactor = GrossLoss == 0 ? 9999m : GrossProfit / GrossLoss;

			var returnToDrawdownRatio = MaxDrawdownPercent <= 0.0001m  // treat near-zero as zero
				? avgPercent > 0 
					? 9999m 
					: 0m 
				: (decimal) avgPercent / MaxDrawdownPercent;

			return new RunReport(
				Name,
				DescriptionAndParameters,
				DataPoints[0].Date,
				DataPoints.Last().Date,
				Cash / InitialInvestment,
				TotalTradeCount,
				TotalTradeCount / (decimal)(DataPoints.Last().Date - DataPoints[0].Date).TotalDays / 365,
				MaxDrawdownPercent,
				returnToDrawdownRatio,
				profitFactor,
				Debug);
		}

		public bool IsEnter()
		{
			return true;
		}
	}
}
