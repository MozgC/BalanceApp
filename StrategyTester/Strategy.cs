namespace StrategyTester
{
	public abstract class Strategy
	{
		public    string      Name;
		public    string      Description;
		public    string      ParametersDescription;

		public    string      Ticker;
		public    decimal     InitialCash       = 1000;
		public    decimal     InitialInvestment = 1000;

		public    decimal     FinalInvestment;
		public    int         HoldingPeriodDays = 30;

		public    int         TotalTradeCount;
		public    decimal     MaxDrawdownPercent;

		public    string      Debug = "";

		protected decimal     Cash;

		protected decimal     Shares;
		protected StockPrice? LastBuy;

		public    decimal     Peak;
		public    decimal     GrossProfit;
		public    decimal     GrossLoss;

		public IList<StockPrice> DataPoints;

		public virtual (decimal x, decimal y) GetHeatmapKey()
		{
			return (0, 0);
		}

		public virtual Func<RunReport, decimal> GetHeatmapValue()
		{
			return _ => 0;
		}

		protected Strategy(string ticker, string name, string description, string parametersDescription)
		{
			Ticker                   = ticker;
			Name                     = name;
			Description              = description;
			ParametersDescription    = parametersDescription;
		}

		// Optionally in the derived class, propose how to generate multiple strategies with different parameters
		public virtual IEnumerable<Strategy> GenerateStrategies(string ticker)
		{
			return [];
		}
		
		private void Initialize(IList<StockPrice> dataPoints, bool buyOnFirstDay, decimal initialInvestment, decimal initialCash, decimal initialShares, StockPrice? lastBuy)
		{
			DataPoints        = dataPoints;
			InitialCash       = initialCash;
			InitialInvestment = initialInvestment;
			Shares            = initialShares;
			LastBuy           = lastBuy;

			Cash               = InitialCash;
			Peak               = InitialInvestment;
			Debug              = "";
			TotalTradeCount    = 0;
			MaxDrawdownPercent = 0;
			GrossProfit        = 0;
			GrossLoss          = 0;

			if (Cash > 0 && buyOnFirstDay)
			{
				Shares      = Cash / DataPoints[0].ClosingPrice;
				LastBuy     = DataPoints[0];
				Debug       = InvestmentStrategy.GetBuyingSharesAtForString(DataPoints[0], Cash);
				Cash        = 0;
			}
		}
		
		protected abstract bool CalcDailyParametersAndDecideIfCanBuyOrSell(int currentIndex);

		public RunReport Run(
			IList<StockPrice> dataPoints, 
			bool              buyOnFirstDay, 
			decimal           initialInvestment = 1000, // either cash or shares value at start
			decimal           initialCash       = 1000, 
			decimal           initialShares     = 0, 
			StockPrice?       lastBuy           = null)
		{
			Initialize(dataPoints, buyOnFirstDay, initialInvestment, initialCash, initialShares, lastBuy);

			for (int i = 0; i < DataPoints.Count; i++)
			{
				var dp = dataPoints[i];

				if (Shares > 0 && Shares * dp.ClosingPrice > Peak)
					Peak = Shares * dp.ClosingPrice;
				
				decimal currentInvestment = Cash > 0 ? Cash : Shares * dp.ClosingPrice;

				decimal currentDrawdownFromPeak = (Peak - currentInvestment) / Peak * 100;

				if (currentDrawdownFromPeak > MaxDrawdownPercent)
					MaxDrawdownPercent = currentDrawdownFromPeak;

				if (HoldingPeriodDays > 0 && Shares > 0 && dp.Date.Date - LastBuy.Date.Date < TimeSpan.FromDays(HoldingPeriodDays))
					continue;
					
				if (!CalcDailyParametersAndDecideIfCanBuyOrSell(i))
					continue;

				if (Shares > 0 && ShouldExit())
				{
					Sell(dp);
				}
				// if price is below  MA and above EMA - buy
				else if (Cash > 0 && ShouldEnter())
				{
					Buy(dp);
				}
			}

			var lastPrice = DataPoints.Last().ClosingPrice;

			FinalInvestment = Cash > 0 ? Cash : Shares * lastPrice;

			var avgPercent = Helpers.GetAvgPercentPerYear(initialInvestment, FinalInvestment, Helpers.GetYears(DataPoints));
			Debug += $"Avg year %: {avgPercent:N}";

			decimal profitFactor = GrossLoss == 0 ? 9999m : GrossProfit / GrossLoss;

			var returnToDrawdownRatio = MaxDrawdownPercent <= 0.0001m  // treat near-zero as zero
				? avgPercent > 0
					? 9999m
					: 0m
				: (decimal) avgPercent / MaxDrawdownPercent;

			decimal years = (decimal) (DataPoints.Last().Date - DataPoints[0].Date).TotalDays / 365;

			var runReport = new RunReport(
				Name,
				Description,
				DataPoints[0].Date,
				DataPoints.Last().Date,
				initialInvestment,
				FinalInvestment,
				Cash,
				Shares,
				FinalInvestment / InitialInvestment,
				LastBuy,
				TotalTradeCount,
				TotalTradeCount / years,
				MaxDrawdownPercent,
				returnToDrawdownRatio,
				profitFactor,
				Debug);

			runReport.SetHeatmapKeyAndValue(GetHeatmapKey(), GetHeatmapValue()(runReport));

			return runReport;
		}

		protected void Buy(StockPrice dp)
		{
			string str = $"{dp.Date:d} Buying {Cash / dp.ClosingPrice:N} shares at {dp.ClosingPrice:C} for  {Cash:C}\n";
			Debug += str;

			Shares  = Cash / dp.ClosingPrice;
			Cash    = 0;
			LastBuy = dp;

			TotalTradeCount++;
		}

		protected void Sell(StockPrice dp)
		{
			string str = $"{dp.Date:d} Selling {Shares:N} shares at {dp.ClosingPrice:C} for  {Shares * dp.ClosingPrice:C}\n";
			Debug += str;

			Cash = Shares * dp.ClosingPrice;

			decimal tradeProfitAndLoss = Cash - Shares * LastBuy.ClosingPrice;

			if (tradeProfitAndLoss > 0)
				GrossProfit += tradeProfitAndLoss;
			else if (tradeProfitAndLoss < 0)
				GrossLoss -= tradeProfitAndLoss; // grossLoss increases by absolute loss

			Shares = 0;

			TotalTradeCount++;
		}

		protected abstract bool ShouldEnter();
		protected abstract bool ShouldExit();
	}
}