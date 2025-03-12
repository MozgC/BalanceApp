using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ClientPlayground;
using CodeJam.Threading;
using DataModels;
using JetBrains.Annotations;
using LinqToDB.Data;

namespace MakeMoneyApp
{
	class Program
	{
		private const string               _alphaVantage      = "90W1KG7TQ5U3LVB8";
		private const int                  _holdingPeriodDays = 30;
		private static readonly object     _consoleSync       = new object();
		private static IStockPriceProvider _client;

		static void Main(string[] args)
		{
			string security = "LABU";
			Console.WriteLine("Security: " + security);

			_client = new YahooFinanceProvider();
			var dataPoints = _client.GetLast10YearsOfPrices(security);

			//IStockPriceProvider provider2 = new AlphaVantageNetProvider(_alphaVantage);
			//var data2 = provider2.GetLast10YearsOfPrices(security);

			decimal initialInvestment = 49000;

			//Rebalance(initialInvestment, "SPUU", "USD", new DateTime(2015, 1, 1));

			DateTime? fromDate = new DateTime(2014, 5, 22);
			Console.WriteLine("Starting from: " + fromDate);
			DateTime? toDate = new DateTime(2029, 1, 1);

			dataPoints = dataPoints.SkipWhile(dp => dp.Date < fromDate).Where(dp => dp.Date < toDate).ToList();

			decimal bestResult      = 0;
			string  bestDescription = "";
			string  bestFullLog     = "";
			
			for (int priceDrop = 5; priceDrop < 50; priceDrop += 5)
			{
				for (int periodDays = 90; periodDays < 365 * 5; periodDays += 30)
				{
					var strategy = new SaveCashInvestWhenDropStrategy(0, 1000, priceDrop, periodDays);
					Console.WriteLine($"Description: {strategy.Description}");
					var result = strategy.Execute(dataPoints);
					Console.WriteLine($"Result: {result.result:C0}");

					if (result.result > bestResult)
					{
						bestResult      = result.result;
						bestDescription = strategy.Description;
						bestFullLog     = result.log;
					}
				}
			}
			
			Console.WriteLine("Best strategy: " + bestDescription);
			Console.WriteLine($"Result: {bestResult:C}");
			Console.WriteLine("Full log:");
			Console.WriteLine(bestFullLog);

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
			var avgPercent = GetAvgPercentPerYear(initialInvestment, buyAndHoldResult, GetYears(dataPoints));

			Console.WriteLine("Security: " + security);
			Console.WriteLine($"Buy & Hold: {buyAndHoldResult:C}, Initial investment: {initialInvestment:C}, Avg year %: {avgPercent:N}");

			var (dcaResult, totalInvested, dcaInfo) = TestDca(dataPoints, 1000, 1000);
			Console.WriteLine($"DCA result: {dcaResult:C}. Total invested: {totalInvested:C}, Resulting % of original investment: %{dcaResult/totalInvested*100m:N2}");

			decimal record = 0;

			ParallelExtensions.RunInParallel(Enumerable.Range(15, 186), Environment.ProcessorCount - 1, maDays =>
			{
				for (int emaDays = 7; emaDays <= (maDays + 1) / 2; emaDays += 1)
					for (int upPercent = 2; upPercent <= 40; upPercent++)
					//for (int downPercent = 2; downPercent <= 40; downPercent++)
					{
						int downPercent = upPercent;
						var (result, debug) = TestStrat(dataPoints, maDays, emaDays, upPercent, upPercent);

						if (result > record)
						{
							record = result;

							lock (_consoleSync)
							{
								Console.WriteLine(
													$"New record found: {record} params: {maDays} {emaDays} {upPercent} {downPercent}");

								Console.WriteLine(debug);
							}
						}
					}
			});

			if (IsBelowMaAndEma(dataPoints, 27, 9, 5))
				Console.WriteLine("Below MA & EMA!");
	
			Console.WriteLine("Done");
			Console.ReadLine();
		}

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
			using (var db = new MakeMoneyDB("MakeMoneyDB"))
			{
				db.BulkCopy(dataPoints.Select(dp => new InstrumentPrice
				{ Name = name, Date = dp.Date, ClosingPrice = dp.ClosingPrice }));
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

					if (IsBelowMaAndEma(prices, 27, 9, 5))
						Console.WriteLine($"{mySecurity} is below MA & EMA!");

					if (IsAboveMaAndEma(prices, 27, 9, 5))
						Console.WriteLine($"{mySecurity} is above MA & EMA!");

					ClearMaAndEmaDict();

					Thread.Sleep(3000);
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Exception with {mySecurity}: " + ex.Message);
				}
			}
		}

		private static void ClearMaAndEmaDict()
		{
			_maDict.Clear();
			_emaDict.Clear();
		}

		private static bool IsBelowMaAndEma(
			IList<StockPrice> dataPoints,
			int maDays,
			int emaDays,
			decimal percent)
		{
			var ma  = MovingAverage(dataPoints, maDays, dataPoints.Count - 1);
			var ema = ExponentialMovingAverage(dataPoints, emaDays, dataPoints.Count - 1);

			if (ma == null || ema == null)
				return false;

			decimal price = dataPoints.Last().ClosingPrice;

			return price / ma.Value <= (1 - percent / 100) && price < ema.Value;
		}

		private static bool IsAboveMaAndEma(
			IList<StockPrice> dataPoints,
			int maDays,
			int emaDays,
			decimal percent)
		{
			var ma = MovingAverage(dataPoints, maDays, dataPoints.Count - 1);
			var ema = ExponentialMovingAverage(dataPoints, emaDays, dataPoints.Count - 1);

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

		private static (decimal, string) TestStrat(
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

			string debug = InvestmentStrategy.GetBuyingSharesAtForString(dataPoints[0], cash);

			cash = 0;

			for (int i = 0; i < dataPoints.Count; i++)
			{
				var dp = dataPoints[i];
				decimal price = dp.ClosingPrice;

				bool canSell = dp.Date.Date - lastBuy.Date.Date > TimeSpan.FromDays(_holdingPeriodDays);

				if (cash > 0 && !canSell)
					continue;

				var ma = MovingAverage(dataPoints, maDays, i);
				var ema = ExponentialMovingAverage(dataPoints, emaDays, i);

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
				}
				// if price is below  MA and above EMA - buy
				else if (cash > 0 && price / ma.Value <= (1 - percentDownFromMa / 100) && price > ema.Value)
				{
					string str = $"{dp.Date:d} Buying {cash / price:N} shares at {price:C} for  {cash:C}\n";
					debug += str;

					shares = cash / price;
					cash = 0;
					lastBuy = dp;
				}
			}

			var lastPrice = dataPoints.Last().ClosingPrice;

			if (cash == 0)
				cash = shares * lastPrice;

			var years = GetYears(dataPoints);
			var avgPercent = GetAvgPercentPerYear(initialInvestment, cash, years);
			debug += $"Avg year %: {avgPercent:N}";

			return (cash, debug);
		}

		private static double GetYears(IList<StockPrice> dataPoints)
		{
			return (dataPoints.Last().Date - dataPoints[0].Date).TotalDays / 365;
		}

		private static double GetAvgPercentPerYear(decimal initialInvestment, decimal finalAmount, double years)
		{
			return Math.Pow((double)(finalAmount / initialInvestment), 1 / years) * 100 - 100;
		}

		private static ConcurrentDictionary<(int periods, int currentDayIndex), decimal> _maDict = new ConcurrentDictionary<(int periods, int currentDayIndex), decimal>();
		private static ConcurrentDictionary<(int periods, int currentDayIndex), decimal> _emaDict = new ConcurrentDictionary<(int periods, int currentDayIndex), decimal>();

		private static decimal? MovingAverage(IList<StockPrice> dataPoints, int periods, int currentDayIndex)
		{
			if (currentDayIndex < periods) return null;

			if (_maDict.TryGetValue((periods, currentDayIndex), out decimal res))
				return res;

			decimal sum = 0;

			for (int i = currentDayIndex; i > currentDayIndex - periods; i--)
			{
				sum += dataPoints[i].ClosingPrice;
			}

			return _maDict[(periods, currentDayIndex)] = sum / periods;
		}

		private static decimal? ExponentialMovingAverage(IList<StockPrice> dataPoints, int periods, int currentDayIndex)
		{
			if (currentDayIndex < periods) return null;

			if (_emaDict.TryGetValue((periods, currentDayIndex), out decimal res))
				return res;

			var alpha = 2 / (decimal)(periods + 1);
			decimal ema = dataPoints[currentDayIndex - periods + 1].ClosingPrice;

			for (int i = currentDayIndex - periods + 1; i <= currentDayIndex; i++)
			{
				ema = alpha * dataPoints[i].ClosingPrice + (1 - alpha) * ema;
			}	

			return _emaDict[(periods, currentDayIndex)] = ema;
		}

	}
}
