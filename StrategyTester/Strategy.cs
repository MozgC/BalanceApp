namespace StrategyTester
{
	public abstract class Strategy
	{
		public    string     Name;
		public    string     DescriptionAndParameters;
			      		     
		public    string     Ticker;
		public    decimal    InitialInvestment = 1000;
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

		public Strategy(string ticker, IList<StockPrice> dataPoints, string name, string descriptionAndParameters)
		{
			Ticker                   = ticker;
			Name                     = name;
			DescriptionAndParameters = descriptionAndParameters;
			DataPoints               = dataPoints;
		}

		protected virtual void Initialize(bool buyOnFirstDay)
		{
			Cash        = InitialInvestment;
			Peak        = InitialInvestment;

			if (buyOnFirstDay)
			{
				Shares      = Cash / DataPoints[0].ClosingPrice;
				LastBuy     = DataPoints[0];
				Debug       = InvestmentStrategy.GetBuyingSharesAtForString(DataPoints[0], Cash);
				Cash        = 0;
			}
		}

		protected abstract bool CalcDailyParametersAndDecideIfCanBuyOrSell(StockPrice dp);

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
	}
}
