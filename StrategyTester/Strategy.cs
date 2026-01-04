namespace StrategyTester
{
	public abstract class Strategy
	{
		public    string     Name;
		public    string     Description;
		public    string     ParametersDescription;
			      		     
		public    string     Ticker;
		public    decimal    InitialInvestment = 1000;
		public    decimal    EndInvestment;
		public    int        HoldingPeriodDays = 30;
			      		     
		public    int        TotalTradeCount;
		public    decimal    MaxDrawdownPercent;
				   	   	     
		public    string     Debug = "";

		protected decimal    Cash;

		protected decimal    Shares;
		protected StockPrice LastBuy;

		public    decimal    Peak;
		public    decimal    GrossProfit;
		public    decimal    GrossLoss;

		public IList<StockPrice> DataPoints;

		public virtual (decimal x, decimal y) GetHeatmapKey()
		{
			return (0, 0);
		}

		public virtual Func<RunReport, decimal> GetHeatmapValue()
		{
			return r => 0;
		}
		
		public Strategy(string ticker, string name, string description, string parametersDescription)
		{
			Ticker                   = ticker;
			Name                     = name;
			Description              = description;
			ParametersDescription    = parametersDescription;
		}
		
		public RunReport Run(IList<StockPrice> dataPoints, decimal initialInvestment = 1000)
		{
			DataPoints        = dataPoints;
			InitialInvestment = initialInvestment;

			Initialize();
			
			for (int i = 0; i < DataPoints.Count; i++)
			{
				if (!CalcDailyParametersAndDecideIfCanBuyOrSell(i))
					continue;

				if (Shares > 0 && ShouldExit())
				{
					Sell(DataPoints[i]);
				}
				// if price is below  MA and above EMA - buy
				else if (Cash > 0 && ShouldEnter())
				{
					Buy(DataPoints[i]);
				}
			}

			var lastPrice = DataPoints.Last().ClosingPrice;

			if (Cash == 0)
				Cash = Shares * lastPrice;
			
			EndInvestment = Cash;

			var avgPercent = Helpers.GetAvgPercentPerYear(InitialInvestment, Cash, Helpers.GetYears(DataPoints));
			Debug += $"Avg year %: {avgPercent:N}";

			decimal profitFactor = GrossLoss == 0 ? 9999m : GrossProfit / GrossLoss;

			var returnToDrawdownRatio = MaxDrawdownPercent <= 0.0001m  // treat near-zero as zero
				? avgPercent > 0 
					? 9999m 
					: 0m 
				: (decimal) avgPercent / MaxDrawdownPercent;

			var runReport = new RunReport(
				Name,
				Description,
				DataPoints[0].Date,
				DataPoints.Last().Date,
				InitialInvestment,
				EndInvestment,
				Cash / InitialInvestment,
				TotalTradeCount,
				TotalTradeCount / (decimal) (DataPoints.Last().Date - DataPoints[0].Date).TotalDays / 365,
				MaxDrawdownPercent,
				returnToDrawdownRatio,
				profitFactor,
				Debug);
			
			runReport.SetHeatmapKeyAndValue(GetHeatmapKey(), GetHeatmapValue()(runReport));

			return runReport;
		}

		protected abstract void Initialize();
		
		protected virtual void Initialize(bool buyOnFirstDay)
		{
			Cash               = InitialInvestment;
			Peak               = InitialInvestment;
			Debug              = "";
			TotalTradeCount    = 0;
			MaxDrawdownPercent = 0;
			Shares             = 0;
			LastBuy            = null;
			GrossProfit        = 0;
			GrossLoss          = 0;

			if (buyOnFirstDay)
			{
				Shares      = Cash / DataPoints[0].ClosingPrice;
				LastBuy     = DataPoints[0];
				Debug       = InvestmentStrategy.GetBuyingSharesAtForString(DataPoints[0], Cash);
				Cash        = 0;
			}
		}

		protected abstract bool CalcDailyParametersAndDecideIfCanBuyOrSell(int currentIndex);

		protected void Buy(StockPrice dp)
		{
			decimal price;
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

			decimal tradeProfitAndLoss = Cash - (Shares * LastBuy.ClosingPrice);

			if (tradeProfitAndLoss > 0)
				GrossProfit += tradeProfitAndLoss;
			else if (tradeProfitAndLoss < 0)
				GrossLoss -= tradeProfitAndLoss; // grossLoss increases by absolute loss

			Shares = 0;

			TotalTradeCount++;

			// Update max drawdown
			if (Cash > Peak)
				Peak = Cash;
					
			decimal currentDrawdownFromPeak = (Peak - Cash) / Peak * 100;
					
			if (currentDrawdownFromPeak > MaxDrawdownPercent)
				MaxDrawdownPercent = currentDrawdownFromPeak;
		}

		protected abstract bool ShouldEnter();
		protected abstract bool ShouldExit();
	}
}
