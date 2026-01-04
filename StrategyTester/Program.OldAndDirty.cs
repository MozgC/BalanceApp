using DataModels;
using JetBrains.Annotations;
using LinqToDB.Data;

namespace StrategyTester
{
	partial class Program
	{
		private const string               _alphaVantage      = "90W1KG7TQ5U3LVB8";

		static void OldMain(string[] args)
		{
			List<StockPrice> dataPoints = new ();
			string security = "SOXL";
			
			//IStockPriceProvider provider2 = new AlphaVantageNetProvider(_alphaVantage);
			//var data2 = provider2.GetLast10YearsOfPrices(security);
			//Rebalance(initialInvestment, "SPUU", "USD", new DateTime(2015, 1, 1));
			decimal initialInvestment = 49000;

			var strategies = new InvestmentStrategy[]
			{
				new SaveCashInvestWhenDropStrategy(0, 1000, 45, 660),
				//new BuyAndHoldStrategy(initialInvestment),
				new DcaStrategy(0, 1000),
				//new BuyLowSellHighStrategy(initialInvestment, 90, 10)
			};

			foreach (var investmentStrategy in strategies)
			{
				Console.WriteLine("-------------------------------------");
				Console.WriteLine($"Strategy: {investmentStrategy.Name}");
				Console.WriteLine($"Description: {investmentStrategy.Description}");
				var result = investmentStrategy.Execute(dataPoints);
				Console.WriteLine($"Result: {result.result:C0}");
				Console.WriteLine($"Log:{Environment.NewLine}{result.log}");
			}

			CheckForSharpDrops(dataPoints);

			var buyAndHoldResult = CalcBuyAndHold(initialInvestment, dataPoints);
			var avgPercent = Helpers.GetAvgPercentPerYear(initialInvestment, buyAndHoldResult, Helpers.GetYears(dataPoints));

			Console.WriteLine("Security: " + security);
			Console.WriteLine($"Buy & Hold: {buyAndHoldResult:C}, Initial investment: {initialInvestment:C}, Avg year %: {avgPercent:N}");

			var (dcaResult, totalInvested, dcaInfo) = TestDca(dataPoints, 1000, 1000);
			Console.WriteLine($"DCA result: {dcaResult:C}. Total invested: {totalInvested:C}, Resulting % of original investment: %{dcaResult / totalInvested * 100m:N2}");

			decimal record = 0;

			if (IsBelowMaAndEma(security, dataPoints, 27, 9, 5))
				Console.WriteLine("Below MA & EMA!");

			Console.WriteLine("Done");
			Console.ReadLine();
		}
		
				private static object _syncObj = new();

		[UsedImplicitly]
		private static void Rebalance(decimal amount, string ticker1, string ticker2, DateTime fromDate)
		{
			Console.WriteLine("Tickers: " + ticker1 + " " + ticker2);
			Console.WriteLine("Initial investment: " + amount + " 50/50");

			var ticker1Data = _client.GetLast10YearsOfPrices(ticker1);
			ticker1Data = ticker1Data.SkipWhile(dp => dp.Date < fromDate).ToList();

			var ticker2Data = _client.GetLast10YearsOfPrices(ticker2);
			ticker2Data = ticker2Data.SkipWhile(dp => dp.Date < fromDate).ToList();

			var ticker1BuyAndHoldResult = CalcBuyAndHold(amount / 2, ticker1Data);
			var ticker2BuyAndHoldResult = CalcBuyAndHold(amount / 2, ticker2Data);

			Console.WriteLine("Buy & Hold: " + (ticker1BuyAndHoldResult + ticker2BuyAndHoldResult));

			decimal ticker1Shares = amount / 2 / ticker1Data[0].ClosingPrice;
			decimal ticker2Shares = amount / 2 / ticker2Data[0].ClosingPrice;

			int month = ticker1Data[0].Date.Month;

			for (int i = 0; i < ticker1Data.Count; i++)
			{
				if (ticker1Data[i].Date.Month != month)
				{
					// rebalance
					decimal ticker1Amount = ticker1Shares * ticker1Data[i].ClosingPrice;
					decimal ticker2Amount = ticker2Shares * ticker2Data[i].ClosingPrice;

					decimal amountToSell = Math.Max(ticker1Amount, ticker2Amount) - (ticker1Amount + ticker2Amount) / 2;

					if (ticker1Amount > ticker2Amount * 1.2m)
					{
						ticker1Shares -= amountToSell / ticker1Data[i].ClosingPrice;
						ticker2Shares += amountToSell / ticker2Data[i].ClosingPrice;
					}
					else if (ticker2Amount > ticker1Amount * 1.1m)
					{
						ticker1Shares += amountToSell / ticker1Data[i].ClosingPrice;
						ticker2Shares -= amountToSell / ticker2Data[i].ClosingPrice;
					}
				}
			}

			decimal total = ticker1Shares * ticker1Data.Last().ClosingPrice + ticker2Shares * ticker2Data.Last().ClosingPrice;

			Console.WriteLine("With monthly rebalancing: " + total);
		}

		private static void CheckForSharpDrops(IList<StockPrice> dataPoints)
		{
			for (int i = dataPoints.Count - 1; i > 15; i--)
			{
				var currentDP    = dataPoints[i];
				var weekAgo      = dataPoints[i - 5];
				var twoWeeksAgo  = dataPoints[i - 10];
				var weekDrop     = currentDP.ClosingPrice / weekAgo.ClosingPrice;
				var twoWeeksDrop = currentDP.ClosingPrice / twoWeeksAgo.ClosingPrice;

				if (twoWeeksDrop < 0.85m || weekDrop < 0.9m)
				{
					Console.WriteLine($"Sharp drop: {twoWeeksAgo.Date} {twoWeeksAgo.ClosingPrice} -> {weekAgo.Date} {weekAgo.ClosingPrice} {weekDrop:P}% -> {currentDP.Date} {currentDP.ClosingPrice} {twoWeeksDrop:P}%");
				}
			}
		}

		private static decimal CalcBuyAndHold(decimal initialInvestment, IList<StockPrice> dataPoints)
		{
			decimal shares = initialInvestment / dataPoints[0].ClosingPrice;
			decimal initialShares = shares;
			var lastPrice = dataPoints.Last().ClosingPrice;
			return initialShares * lastPrice;
		}

		[UsedImplicitly]
		private static void SaveToDb(string name, IList<StockPrice> dataPoints)
		{
			using (var db = new StrategyTesterDB("MakeMoneyDB"))
			{
				db.BulkCopy(dataPoints.Select(dp => new InstrumentPrice
					{ Name = name, Date = dp.Date, AdjustedClosingPrice = dp.ClosingPrice }));
			}
		}

		[UsedImplicitly]
		private static void CheckSecurities(string[] mySecurities, bool adjusted)
		{
			foreach (var mySecurity in mySecurities)
			{
				try
				{
					Console.WriteLine($"Checking {mySecurity}");

					var prices = _client.GetLast10YearsOfPrices(mySecurity);

					if (IsBelowMaAndEma(mySecurity, prices, 27, 9, 5))
						Console.WriteLine($"{mySecurity} is below MA & EMA!");

					if (IsAboveMaAndEma(mySecurity, prices, 27, 9, 5))
						Console.WriteLine($"{mySecurity} is above MA & EMA!");

					Thread.Sleep(3000);
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Exception with {mySecurity}: " + ex.Message);
				}
			}
		}

		private static bool IsBelowMaAndEma(
			string ticker,
			IList<StockPrice> dataPoints,
			int maDays,
			int emaDays,
			decimal percent)
		{
			var ma  = IndicatorFunctions.MovingAverage(ticker, dataPoints, maDays, dataPoints.Count - 1);
			var ema = IndicatorFunctions.ExponentialMovingAverage(ticker, dataPoints, emaDays, dataPoints.Count - 1);

			if (ma == null || ema == null)
				return false;

			decimal price = dataPoints.Last().ClosingPrice;

			return price / ma.Value <= (1 - percent / 100) && price < ema.Value;
		}

		private static bool IsAboveMaAndEma(
			string ticker,
			IList<StockPrice> dataPoints,
			int maDays,
			int emaDays,
			decimal percent)
		{
			var ma = IndicatorFunctions.MovingAverage(ticker, dataPoints, maDays, dataPoints.Count - 1);
			var ema = IndicatorFunctions.ExponentialMovingAverage(ticker, dataPoints, emaDays, dataPoints.Count - 1);

			if (ma == null || ema == null)
				return false;

			decimal price = dataPoints.Last().ClosingPrice;

			return price / ma.Value >= (1 + percent / 100) && price > ema.Value;
		}

		private static (decimal result, decimal totalInvested, string info) TestDca(IList<StockPrice> dataPoints, decimal initialInvestment, decimal amountEachMonth)
		{
			decimal cash  = initialInvestment;
			decimal total = initialInvestment;

			decimal shares = cash / dataPoints[0].ClosingPrice;

			string debug = InvestmentStrategy.GetBuyingSharesAtForString(dataPoints[0], cash);

			foreach (var x in InvestmentStrategy.GetFirstBusinessDatesOfEachMonth(dataPoints, false))
			{
				shares += amountEachMonth / x.dp.ClosingPrice;
				total  += amountEachMonth;
				debug += InvestmentStrategy.GetBuyingSharesAtForString(x.dp, amountEachMonth);
			}

			return (dataPoints.Last().ClosingPrice * shares, total, debug);
		}

		private static (decimal FinalCash, string Debug, int TradeCount, decimal MaxDrawdown, decimal ProfitFactor) MainStrategy(
			string ticker,
			IList<StockPrice> dataPoints,
			int maDays,
			int emaDays,
			decimal percentUpFromMa,
			decimal percentDownFromMa)
		{
			decimal initialInvestment = 1000;
			decimal cash = initialInvestment;

			decimal shares = cash / dataPoints[0].ClosingPrice;
			var lastBuy = dataPoints[0];

			decimal peak = initialInvestment;
			decimal maxDrawdown = 0m;
			decimal grossProfit = 0m;
			decimal grossLoss = 0m;
			int winningTrades = 0;
			int losingTrades = 0;
			decimal entryPrice = 0m;

			string debug = InvestmentStrategy.GetBuyingSharesAtForString(dataPoints[0], cash);

			cash = 0;
			int tradeCount = 0;

			for (int i = 0; i < dataPoints.Count; i++)
			{
				var dp = dataPoints[i];
				decimal price = dp.ClosingPrice;

				bool canSell = dp.Date.Date - lastBuy.Date.Date > TimeSpan.FromDays(_holdingPeriodDays);

				if (cash > 0 && !canSell)
					continue;

				var ma = IndicatorFunctions.MovingAverage(ticker, dataPoints, maDays, i);
				var ema = IndicatorFunctions.ExponentialMovingAverage(ticker, dataPoints, emaDays, i);

				if (ma == null || ema == null)
					continue;

				// what if it dropped 50% in 1 day, price can be less than MA, should we sell?
				// if price is above Simple MA and below EMA - sell
				if (shares > 0 && price / ma.Value >= (1 + percentUpFromMa / 100) && price < ema.Value)
				{
					string str = $"{dp.Date:d} Selling {shares:N} shares at {price:C} for  {shares * price:C}\n";
					debug += str;

					cash = shares * price;
					shares = 0;

					tradeCount++;

					decimal pnl = (price / entryPrice - 1m);
					if (pnl > 0)
					{
						grossProfit += cash * pnl;  // или просто pnl в процентах
						winningTrades++;
					}
					else
					{
						grossLoss -= cash * pnl;   // grossLoss всегда положительный
						losingTrades++;
					}

					// Обновление просадки
					decimal currentEquity = cash > 0 ? cash : shares * price;
					if (currentEquity > peak) peak = currentEquity;
					decimal dd = (peak - currentEquity) / peak;
					if (dd > maxDrawdown) maxDrawdown = dd;
				}
				// if price is below  MA and above EMA - buy
				else if (cash > 0 && price / ma.Value <= (1 - percentDownFromMa / 100) && price > ema.Value)
				{
					string str = $"{dp.Date:d} Buying {cash / price:N} shares at {price:C} for  {cash:C}\n";
					debug += str;

					shares = cash / price;
					cash = 0;
					lastBuy = dp;

					tradeCount++;
					entryPrice = price;
				}
			}

			var lastPrice = dataPoints.Last().ClosingPrice;

			if (cash == 0)
				cash = shares * lastPrice;

			var years = Helpers.GetYears(dataPoints);
			var avgPercent = Helpers.GetAvgPercentPerYear(initialInvestment, cash, years);
			debug += $"Avg year %: {avgPercent:N}";

			decimal profitFactor = grossLoss == 0 ? 999m : grossProfit / grossLoss;

			return (cash, debug, tradeCount, maxDrawdown, profitFactor);
		}
	}
}
