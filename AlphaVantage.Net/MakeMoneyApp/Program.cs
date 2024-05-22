using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AlphaVantage.Net.Stocks;
using AlphaVantage.Net.Stocks.TimeSeries;
using CodeJam.Threading;
using DataModels;
using LinqToDB.Data;

namespace ClientPlayground
{
	class Program
	{
		private const string  ApiKey            = "90W1KG7TQ5U3LVB8";
		private const int     holdingPeriodDays = 30;
		private static object _consoleSync      = new object();

		static void Main(string[] args)
		{
			_client = new AlphaVantageStocksClient(ApiKey);
			// they now made adjusted premium :(
			bool adjusted = false;

			decimal initialInvestment = 49000;

			//Rebalance(initialInvestment, "SPUU", "USD", new DateTime(2015, 1, 1));

			string security = "VOO";
			var res = _client.RequestDailyTimeSeriesAsync(security, TimeSeriesSize.Full, adjusted).Result;
			IList<StockDataPoint> dataPoints = res.DataPoints.Cast<StockDataPoint>().Reverse().ToList();

			var strategies = new InvestmentStrategy[]
			{
				new BuyAndHoldStrategy(),
				//new DcaStrategy(1000),
				new BuyLowSellHighStrategy(90, 10)
			};

			foreach (var investmentStrategy in strategies)
			{
				Console.WriteLine("-------------------------------------");
				Console.WriteLine($"Strategy: {investmentStrategy.Name}");
				var result = investmentStrategy.Execute(initialInvestment, dataPoints);
				Console.WriteLine($"Result: {result.result:C0}");
				//Console.WriteLine($"Log:{Environment.NewLine}{result.log}");
			}
				
			CheckForSharpDrops(dataPoints);


			DateTime? fromDate = new DateTime(2024, 5, 22);
			Console.WriteLine("Starting from: " + fromDate);
			DateTime? toDate = new DateTime(2029, 1, 1);

			dataPoints = dataPoints.SkipWhile(dp => dp.Time < fromDate).Where(dp => dp.Time < toDate).ToList();

			var buyAndHoldResult = CalcBuyAndHold(initialInvestment, dataPoints);
			var avgPercent = GetAvgPercentPerYear(initialInvestment, buyAndHoldResult, GetYears(dataPoints));

			Console.WriteLine("Security: " + security);
			Console.WriteLine($"Buy & Hold: {buyAndHoldResult:C}, Initial investment: {initialInvestment:C}, Avg year %: {avgPercent:N}");

			var (dcaResult, totalInvested, dcaInfo) = TestDCA(dataPoints, 1000, 1000);
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


		private static void Rebalance(decimal amount, string ticker1, string ticker2, DateTime fromDate)
		{
			Console.WriteLine("Tickers: " + ticker1 + " " + ticker2);
			Console.WriteLine("Initial investment: " + amount + " 50/50");

			var res = _client.RequestWeeklyTimeSeriesAsync(ticker1, true).Result;
			IList<StockDataPoint> ticker1Data = res.DataPoints.Cast<StockDataPoint>().Reverse().ToList();
			ticker1Data = ticker1Data.SkipWhile(dp => dp.Time < fromDate).ToList();

			res = _client.RequestWeeklyTimeSeriesAsync(ticker2, true).Result;
			IList<StockDataPoint> ticker2Data = res.DataPoints.Cast<StockDataPoint>().Reverse().ToList();
			ticker2Data = ticker2Data.SkipWhile(dp => dp.Time < fromDate).ToList();

			var ticker1BuyAndHoldResult = CalcBuyAndHold(amount / 2, ticker1Data);
			var ticker2BuyAndHoldResult = CalcBuyAndHold(amount / 2, ticker2Data);

			Console.WriteLine("Buy & Hold: " + (ticker1BuyAndHoldResult + ticker2BuyAndHoldResult));

			decimal ticker1Shares = amount / 2 / ticker1Data[0].ClosingPrice;
			decimal ticker2Shares = amount / 2 / ticker2Data[0].ClosingPrice;

			int month = ticker1Data[0].Time.Month;

			for (int i = 0; i < ticker1Data.Count; i++)
			{
				if (ticker1Data[i].Time.Month != month)
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

		private static void CheckForSharpDrops(IList<StockDataPoint> dataPoints)
		{
			for (int i = dataPoints.Count - 1; i > 15; i--)
			{
				var currentDP = dataPoints[i];
				var weekAgo = dataPoints[i - 5];
				var twoWeeksAgo = dataPoints[i - 10];
				var weekDrop = currentDP.ClosingPrice / weekAgo.ClosingPrice;
				var twoWeeksDrop = currentDP.ClosingPrice / twoWeeksAgo.ClosingPrice;

				if (twoWeeksDrop < 0.85m || weekDrop < 0.9m)
				{
					Console.WriteLine($"Sharp drop: {twoWeeksAgo.Time} {twoWeeksAgo.ClosingPrice} -> {weekAgo.Time} {weekAgo.ClosingPrice} {weekDrop:P}% -> {currentDP.Time} {currentDP.ClosingPrice} {twoWeeksDrop:P}%");
				}
			}
		}

		private static decimal CalcBuyAndHold(decimal initialInvestment, IList<StockDataPoint> dataPoints)
		{
			decimal shares = initialInvestment / dataPoints[0].ClosingPrice;
			decimal initialShares = shares;
			var lastPrice = dataPoints.Last().ClosingPrice;
			return initialShares * lastPrice;
		}

		private static void SaveToDb(string name, IList<StockDataPoint> dataPoints)
		{
			using (var db = new MakeMoneyDB("MakeMoneyDB"))
			{
				db.BulkCopy(dataPoints.Select(dp => new InstrumentPrice
				{ Name = name, Date = dp.Time, ClosingPrice = dp.ClosingPrice }));
			}
		}

		private static void CheckSecurities(string[] mySecurities, AlphaVantageStocksClient client, bool adjusted)
		{
			foreach (var mySecurity in mySecurities)
			{
				try
				{
					Console.WriteLine($"Checking {mySecurity}");

					var res = client.RequestDailyTimeSeriesAsync(mySecurity, TimeSeriesSize.Compact, adjusted).Result;

					IList<StockDataPoint> prices = res.DataPoints.Cast<StockDataPoint>().Reverse().ToList();

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
			IList<StockDataPoint> dataPoints,
			int maDays,
			int emaDays,
			decimal percent)
		{
			var ma = MovingAverage(dataPoints, maDays, dataPoints.Count - 1);
			var ema = ExponentialMovingAverage(dataPoints, emaDays, dataPoints.Count - 1);

			if (ma == null || ema == null)
				return false;

			decimal price = dataPoints.Last().ClosingPrice;

			return price / ma.Value <= (1 - percent / 100) && price < ema.Value;
		}

		private static bool IsAboveMaAndEma(
			IList<StockDataPoint> dataPoints,
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

		private static (decimal result, decimal totalInvested, string info) TestDCA(IList<StockDataPoint> dataPoints, decimal initialInvestment, decimal amountEachMonth)
		{
			decimal cash  = initialInvestment;
			decimal total = initialInvestment;

			decimal shares = cash / dataPoints[0].ClosingPrice;
			var lastBuy = dataPoints[0];

			string debug = InvestmentStrategy.GetBuyingSharesAtForString(dataPoints[0], cash);

			cash = 0;

			foreach (var dp in InvestmentStrategy.GetFirstBusinessDatesOfEachMonth(dataPoints, false))
			{
				shares += amountEachMonth / dp.ClosingPrice;
				total  += amountEachMonth;
				debug += InvestmentStrategy.GetBuyingSharesAtForString(dp, amountEachMonth);
			}

			return (dataPoints.Last().ClosingPrice * shares, total, debug);
		}

		private static (decimal, string) TestStrat(
			IList<StockDataPoint> dataPoints,
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

				bool canSell = dp.Time.Date - lastBuy.Time.Date > TimeSpan.FromDays(holdingPeriodDays);

				if (cash > 0 && !canSell)
					continue;

				var ma = MovingAverage(dataPoints, maDays, i);
				var ema = ExponentialMovingAverage(dataPoints, emaDays, i);

				if (ma == null || ema == null)
					continue;

				// what if it dropped 50% in 1 day, price can be less than MA, should we sell?
				if (shares > 0 && price / ma.Value >= (1 + percentUpFromMa / 100) && price < ema.Value)
				{
					string str = $"{dp.Time:d} Selling {shares:N} shares at {price:C} for  {shares * price:C}\n";
					debug += str;

					cash = shares * price;
					shares = 0;
				}
				else if (cash > 0 && price / ma.Value <= (1 - percentDownFromMa / 100) && price > ema.Value)
				{
					string str = $"{dp.Time:d} Buying {cash / price:N} shares at {price:C} for  {cash:C}\n";
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

		private static double GetYears(IList<StockDataPoint> dataPoints)
		{
			return (dataPoints.Last().Time - dataPoints[0].Time).TotalDays / 365;
		}

		private static double GetAvgPercentPerYear(decimal initialInvestment, decimal finalAmount, double years)
		{
			return Math.Pow((double)(finalAmount / initialInvestment), 1 / years) * 100 - 100;
		}

		private static ConcurrentDictionary<(int periods, int currentDayIndex), decimal> _maDict = new ConcurrentDictionary<(int periods, int currentDayIndex), decimal>();
		private static ConcurrentDictionary<(int periods, int currentDayIndex), decimal> _emaDict = new ConcurrentDictionary<(int periods, int currentDayIndex), decimal>();
		private static AlphaVantageStocksClient _client;

		private static decimal? MovingAverage(IList<StockDataPoint> dataPoints, int periods, int currentDayIndex)
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

		private static decimal? ExponentialMovingAverage(IList<StockDataPoint> dataPoints, int periods, int currentDayIndex)
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

	public abstract class InvestmentStrategy
	{
		public abstract string Name { get; }

		public abstract (decimal result, string log) Execute(decimal initialInvestment, IList<StockDataPoint> dataPoints);

		private static ConcurrentDictionary<(int currentDayIndex, int periodInDays), (decimal minPrice, decimal maxPrice)> _minMaxPrice = new ConcurrentDictionary<(int currentDayIndex, int periodInDays), (decimal minPrice, decimal maxPrice)>();

		public static (decimal minPrice, decimal maxPrice) GetMinAndMaxPriceForPeriod(IList<StockDataPoint> dataPoints, int currentDayIndex, int periodInDays)
		{
			if (_minMaxPrice.TryGetValue((currentDayIndex, periodInDays), out var res))
				return res;

			decimal min = dataPoints[currentDayIndex].ClosingPrice;
			decimal max = dataPoints[currentDayIndex].ClosingPrice;

			int prevDay = currentDayIndex - 1;

			while (prevDay >= 0 && (dataPoints[currentDayIndex].Time.Date - dataPoints[prevDay].Time.Date).Days < periodInDays)
			{
				decimal price = dataPoints[prevDay].ClosingPrice;

				if (price > max)
					max = price;

				if (price < min)
					min = price;
				
				prevDay--;
			}
			
			return _minMaxPrice[(currentDayIndex, periodInDays)] = (min, max);
		}

		public static IEnumerable<StockDataPoint> GetFirstBusinessDatesOfEachMonth(IList<StockDataPoint> dps, bool includeFirstDataPoint)
		{
			DateTime? date = null;

			foreach (var dpx in dps)
			{
				if (date == null)
				{
					date = dpx.Time.Date;

					if (includeFirstDataPoint)
						yield return dpx;
					
					continue;
				}
				
				if (date.Value.Month == dpx.Time.Month)
					continue;

				date = dpx.Time.Date;
				yield return dpx;
			}
		}

		public static string GetBuyingSharesAtForString(StockDataPoint dp, decimal cash)
		{
			return $"{dp.Time:d} Buying {cash / dp.ClosingPrice:N} shares at {dp.ClosingPrice:C} for  {cash:C}\n";
		}
		public static string GetSellingSharesAtForString(StockDataPoint dp, decimal shares)
		{
			return $"{dp.Time:d} Selling {shares:N} shares at {dp.ClosingPrice:C} for {shares*dp.ClosingPrice:C}\n";
		}
	}

	public class BuyAndHoldStrategy : InvestmentStrategy
	{
		public override string Name => "Buy & Hold";

		public override (decimal result, string log) Execute(decimal initialInvestment, IList<StockDataPoint> dataPoints)
		{
			decimal shares        = initialInvestment / dataPoints[0].ClosingPrice;
			decimal initialShares = shares;
			var lastPrice         = dataPoints[^1].ClosingPrice;

			return (initialShares * lastPrice, null);
		}
	}

	public class DcaStrategy : InvestmentStrategy
	{
		private decimal _amountEachMonth;
		public override string Name => "Dollar-Cost Averaging";

		public DcaStrategy(decimal amountEachMonth)
		{
			_amountEachMonth = amountEachMonth;
		}

		public override (decimal result, string log) Execute(decimal initialInvestment, IList<StockDataPoint> dataPoints)
		{
			decimal cash = initialInvestment;
			decimal total = initialInvestment;

			decimal shares = cash / dataPoints[0].ClosingPrice;
			var lastBuy = dataPoints[0];

			string debug = GetBuyingSharesAtForString(dataPoints[0], cash);

			cash = 0;

			foreach (var dp in GetFirstBusinessDatesOfEachMonth(dataPoints, false))
			{
				shares += _amountEachMonth / dp.ClosingPrice;
				total += _amountEachMonth;
				debug += GetBuyingSharesAtForString(dp, _amountEachMonth);
			}

			return (dataPoints.Last().ClosingPrice * shares, debug);
		}
	}

	public class BuyLowSellHighStrategy : InvestmentStrategy
	{
		public override string Name => $"Buy low & sell high ({_periodInDays} days, {_percentDiff}%)";

		private readonly int     _periodInDays;
		private readonly decimal _percentDiff;
		
		public BuyLowSellHighStrategy(int periodInDays, decimal percentDiff)
		{
			_periodInDays = periodInDays;
			_percentDiff  = percentDiff;
		}

		public override (decimal result, string log) Execute(decimal initialInvestment, IList<StockDataPoint> dataPoints)
		{
			string debug = InvestmentStrategy.GetBuyingSharesAtForString(dataPoints[0], initialInvestment);
			
			decimal paidPrice = dataPoints[0].ClosingPrice;
			decimal shares    = initialInvestment / paidPrice;
			decimal cash      = 0;
			
			for (int i = 1; i < dataPoints.Count; i++)
			{
				decimal price = dataPoints[i].ClosingPrice;

				if (shares > 0 && price >= paidPrice * (1 + _percentDiff / 100))
				{
					debug += InvestmentStrategy.GetSellingSharesAtForString(dataPoints[i], shares);
					cash = shares * price;
					shares = 0;
				}
				else if (shares == 0)
				{
					var (min, max) = GetMinAndMaxPriceForPeriod(dataPoints, i, _periodInDays);

					decimal maxBuyPrice = (max - min) / 10 + min;

					if (price <= maxBuyPrice)
					{
						debug += InvestmentStrategy.GetBuyingSharesAtForString(dataPoints[i], cash);
						shares = cash / price;
						cash   = 0;
					}
				}
			}

			decimal result = cash > 0
				? cash
				: shares * dataPoints[^1].ClosingPrice;

			return (result, debug);
		}
	}


}
