using System.Collections.Concurrent;
using ClientPlayground;
using CodeJam.Threading;
using DataModels;
using JetBrains.Annotations;
using LinqToDB.Data;
using ScottPlot;
using ScottPlot.WinForms;

namespace StrategyTester
{
	class Program
	{
		private const string               _alphaVantage      = "90W1KG7TQ5U3LVB8";
		private const int                  _holdingPeriodDays = 30;
		private static readonly object     _consoleSync       = new object();
		private static IStockPriceProvider _client;

		[STAThread]
		static void Main(string[] args)
		{
			Console.OutputEncoding = System.Text.Encoding.UTF8;

			// На всякий случай (иногда нужно и это)
			Console.InputEncoding = System.Text.Encoding.UTF8;

			string security = "SOXL";
			Console.WriteLine("Security: " + security);

			//IStockPriceProvider provider2 = new AlphaVantageNetProvider(_alphaVantage);
			//var data2 = provider2.GetLast10YearsOfPrices(security);

			_client = new YahooFinanceProvider();
			var dataPoints = _client.GetLast10YearsOfPrices(security);


			decimal initialInvestment = 49000;

			//Rebalance(initialInvestment, "SPUU", "USD", new DateTime(2015, 1, 1));

			DateTime? fromDate = new DateTime(2014, 5, 22);
			Console.WriteLine("Starting from: " + fromDate);
			DateTime? toDate = new DateTime(2029, 1, 1);

			dataPoints = dataPoints.SkipWhile(dp => dp.Date < fromDate).Where(dp => dp.Date < toDate).ToList();

			TestMainStrategy2(dataPoints);

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
			

			if (IsBelowMaAndEma(dataPoints, 27, 9, 5))
				Console.WriteLine("Below MA & EMA!");
	
			Console.WriteLine("Done");
			Console.ReadLine();
		}
	
		private static void ShowHeatMap(Dictionary<(int x, int y), List<decimal>> heatMap, bool labelValues)
		{
			/*
			heatMap = new Dictionary<(int x, int y), List<decimal>>();
			heatMap[(0, 4)] = new List<decimal>() { 1 };
			heatMap[(0, 5)] = new List<decimal>() { 2 };
			heatMap[(0, 6)] = new List<decimal>() { 3 };
			heatMap[(1, 4)] = new List<decimal>() { 4 };
			heatMap[(1, 5)] = new List<decimal>() { 5 };
			heatMap[(1, 6)] = new List<decimal>() { 6 };
			heatMap[(2, 4)] = new List<decimal>() { 7 };
			heatMap[(2, 5)] = new List<decimal>() { 8 };
			heatMap[(2, 6)] = new List<decimal>() { 9 };
			*/
			
			var maSet =  heatMap.Keys.Select(k => k.x).Distinct().OrderBy(x => x);
			var emaSet = heatMap.Keys.Select(k => k.y).Distinct().OrderBy(x => x);

			int[] xValues = maSet.ToArray();       // например: [30, 35, 40, ..., 150]
			int[] yValues = emaSet.ToArray(); // например: [6, 7, 8, ..., 22]

			int cols = xValues.Length;       // высота = количество разных maDays
			int rows = yValues.Length;  // ширина = количество разных процентов

			double[,] data = new double[rows, cols];
			
			for (int row = 0; row < rows; row++)
			{
				int ema = yValues[row];

				for (int col = 0; col < cols; col++)
				{
					int ma = xValues[col];

					if (heatMap.TryGetValue((ma, ema), out var list) && list.Count > 0)
					{
						data[yValues.Length - 1 - row, col] = (double)list.Average();
					}
					else
					{
						data[yValues.Length - 1 - row, col] = double.NaN; // пустые ячейки
					}
				}
			}

			var plot = new ScottPlot.Plot();

			var hm = plot.Add.Heatmap(data);
			hm.Colormap = new ScottPlot.Colormaps.Turbo();
			hm.Smooth = true;

			if (labelValues)
			{
				for (int y = 0; y < data.GetLength(0); y++)
				for (int x = 0; x < data.GetLength(1); x++)
				{
					Coordinates coordinates = new(x, y);
					string cellLabel = data[yValues.Length - 1 - y, x].ToString("0.0");
					var text = plot.Add.Text(cellLabel, coordinates);
					text.Alignment = Alignment.MiddleCenter;
					text.LabelFontSize = 30;
					text.LabelFontColor = Colors.White;
				}
			}

			// axis titles
			plot.Title("HeatMap");
			plot.XLabel("MA Days");
			plot.YLabel("EMA Days");

			// map indices -> your X/Y values using manual ticks
			double[] xPositions = Enumerable.Range(0, xValues.Length)
				.Select(i => (double)i)
				.ToArray();

			double[] yPositions = Enumerable.Range(0, yValues.Length)
				.Select(i => (double)i)
				.ToArray();

			plot.Axes.Bottom.SetTicks(
				xPositions,
				xValues.Select(v => v.ToString()).ToArray()
			);

			plot.Axes.Left.SetTicks(
				yPositions,
				yValues.Select(v => v.ToString()).ToArray()
			);

			// optional but keeps the heatmap tightly framed with half-cell padding
			plot.Axes.SetLimits(left: -0.5, right: cols - 0.5, bottom: -0.5, top: rows - 0.5);

			var cb = plot.Add.ColorBar(hm);
			cb.Label = "Profit ratio";
			///cb.LabelStyle.FontSize = 24;

			FormsPlotViewer.Launch(plot);
		}

		private static object _syncObj = new();

		/*
		private static void TestMainStrategy(IList<StockPrice> dataPoints)
		{
			decimal record = 0;

			ParallelExtensions.RunInParallel(Enumerable.Range(15, 186), Environment.ProcessorCount - 1, maDays =>
			{
				for (int emaDays = 7; emaDays <= (maDays + 1) / 2; emaDays += 1)
				for (int upPercent = 2; upPercent <= 15; upPercent++) 
				//for (int downPercent = 2; downPercent <= 40; downPercent++)
				{
					int downPercent = upPercent;
					var (result, debug, trades) = MainStrategy(dataPoints, maDays, emaDays, upPercent, upPercent);
					//var (result, debug) = testStratFunc(dataPoints, maDays, emaDays, upPercent, upPercent);

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
		}
		*/

		private static void TestStrategy2(IList<StockPrice> dataPoints)
		{
			decimal record = 0;

			ParallelExtensions.RunInParallel(Enumerable.Range(15, 186), Environment.ProcessorCount - 1, maDays =>
			{
				for (int emaDays = 7; emaDays <= (maDays + 1) / 2; emaDays += 1)
				{
					var (result, debug) = Strategy2(dataPoints, maDays, emaDays);

					if (result > record)
					{
						record = result;

						lock (_consoleSync)
						{
							Console.WriteLine(
								$"New record found: {record} params: {maDays} {emaDays}");

							Console.WriteLine(debug);
						}
					}
				}
			});
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

		private static (decimal FinalCash, string Debug, int TradeCount, decimal MaxDrawdown, decimal ProfitFactor) MainStrategy(
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

			var years = GetYears(dataPoints);
			var avgPercent = GetAvgPercentPerYear(initialInvestment, cash, years);
			debug += $"Avg year %: {avgPercent:N}";

			decimal profitFactor = grossLoss == 0 ? 999m : grossProfit / grossLoss;
			
			return (cash, debug, tradeCount, maxDrawdown, profitFactor);
		}
		
		private static (decimal finalProfitRatio, string Debug, int TradeCount, decimal MaxDrawdown, decimal ProfitFactor) MainStrategy2(
			IList<StockPrice> dataPoints,
			int maDays,
			int emaDays)
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

				var ma = MovingAverage(dataPoints, maDays, i);
				var maPrev = MovingAverage(dataPoints, maDays, i - 1);
				var ema = ExponentialMovingAverage(dataPoints, emaDays, i);
				var emaPrev = ExponentialMovingAverage(dataPoints, emaDays, i - 1);

				if (ma == null || ema == null)
					continue;

				if (shares > 0 && emaPrev > maPrev && ema < ma)
				{
					string str = $"{dp.Date:d} Selling {shares:N} shares at {price:C} for  {shares * price:C}\n";
					debug += str;

					cash = shares * price;
					shares = 0;

					tradeCount++;
					
					decimal pnl = (price / lastBuy.ClosingPrice - 1m);
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
					if (cash > peak) peak = cash;
					decimal currentDrawdownFromPeak = (peak - cash) / peak;
					if (currentDrawdownFromPeak > maxDrawdown) maxDrawdown = currentDrawdownFromPeak;
				}
				// if price is below  MA and above EMA - buy
				else if (cash > 0 && emaPrev < maPrev && ema > ma)
				{
					string str = $"{dp.Date:d} Buying {cash / price:N} shares at {price:C} for  {cash:C}\n";
					debug += str;

					shares = cash / price;
					cash = 0;
					lastBuy = dp;

					tradeCount++;
				}
			}

			var lastPrice = dataPoints.Last().ClosingPrice;

			if (cash == 0)
				cash = shares * lastPrice;

			var years = GetYears(dataPoints);
			var avgPercent = GetAvgPercentPerYear(initialInvestment, cash, years);
			debug += $"Avg year %: {avgPercent:N}";

			decimal profitFactor = grossLoss == 0 ? 999m : grossProfit / grossLoss;
			
			return (cash / initialInvestment, debug, tradeCount, maxDrawdown, profitFactor);
		}
		
		private static void TestMainStrategy2(IList<StockPrice> dataPoints)
		{
			const int minMA = 25;
			const int maCount = 75;
		    // Сначала сортируем данные по дате (на всякий случай)
		    dataPoints = dataPoints.OrderBy(dp => dp.Date).ToList();

		    // Группируем данные по месяцам для Walk-Forward
		    var monthlyGroups = dataPoints.GroupBy(x => new { x.Date.Year, x.Date.Month })
		                                  .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
		                                  .Select(g => g.OrderBy(x => x.Date).ToList())
		                                  .ToList();

		    // Параметры для Walk-Forward
		    const int minInSampleMonths = 48; // 4 года
		    const int oosMonths = 12;         // 1 год OOS
		    const int stepMonths = 12;        // Шаг переоптимизации 1 год

		    double capital = 1000.0;  // Начальный капитал
		    var equityCurve = new List<double>();
		    var allOosResults = new List<(int maDays, int emaDays, decimal oosProfitPercent, int trades)>(); // Для анализа

		    // Heatmap данные: словарь для всех комбинаций (maDays, upPercent) -> средний result по emaDays
		    var heatMap = new ConcurrentDictionary<(int ma, int ema), List<decimal>>();

		    for (int oosStartMonth = minInSampleMonths; oosStartMonth < monthlyGroups.Count; oosStartMonth += stepMonths)
		    {
		        int isEndMonth = oosStartMonth;  // Конец In-Sample
		        int oosEndMonth = Math.Min(oosStartMonth + oosMonths, monthlyGroups.Count);

		        // Собираем In-Sample данные
		        var isData = monthlyGroups.Take(isEndMonth).SelectMany(x => x).ToList();

		        // Брутфорс на In-Sample с фильтрами против переобучения
		        decimal record = decimal.MinValue;
		        int bestMa = 0, bestEma = 0;
		        string bestDebug = "";
		        int bestTrades = 0;

		        ParallelExtensions.RunInParallel(Enumerable.Range(minMA, maCount), Environment.ProcessorCount - 1, maDays =>
		        {
		            for (int emaDays = 7; emaDays <= (maDays + 1) / 2; emaDays += 1)
		            {
		                var (finalProfitRatio, debug, tradeCount, maxDrawdown, profitFactor) = MainStrategy2(isData, maDays, emaDays);

		                // Фильтры против переобучения
		                if (tradeCount >= 20 &&                
		                    finalProfitRatio > 1.5m &&        // Минимум 50% прибыли 
		                    maxDrawdown < 0.6m && 
		                    profitFactor > 1.6m)   
		                {
			                var key = (maDays, emaDays);
			                if (!heatMap.ContainsKey(key))
				                heatMap[key] = new List<decimal>();
			                heatMap[key].Add(finalProfitRatio);

			                lock (_syncObj)
		                    {
		                        if (finalProfitRatio > record)
		                        {
		                            record = finalProfitRatio;
		                            bestMa = maDays;
		                            bestEma = emaDays;
		                            bestDebug = debug;
		                            bestTrades = tradeCount;
		                        }
		                    }
		                }
		            }
		        });

		        Console.WriteLine($"Оптимизация от {monthlyGroups[0][0].Date:yyyy-MM} до {monthlyGroups[isEndMonth-1][0].Date:yyyy-MM} → лучшие: MA={bestMa} EMA={bestEma} (сделок: {bestTrades})");

				// OOS тест
		        var oosData = monthlyGroups.Skip(isEndMonth).Take(oosEndMonth - isEndMonth).SelectMany(x => x).ToList();
		        
		        ParallelExtensions.RunInParallel(Enumerable.Range(minMA, maCount), Environment.ProcessorCount - 1, maDays =>
		        {
			        for (int emaDays = 7; emaDays <= (maDays + 1) / 2; emaDays += 1)
			        {
				        var (finalProfitRatio, debug, tradeCount, maxDrawdown, profitFactor) = MainStrategy2(oosData, maDays, emaDays);

				        // Фильтры против переобучения
				        if (tradeCount >= 6 &&                
				            finalProfitRatio > 1.1m && 
				            maxDrawdown < 60m && 
				            profitFactor > 1.6m)   
				        {
					        var key = (maDays, emaDays);
					        if (!heatMap.ContainsKey(key))
						        heatMap[key] = new List<decimal>();
					        heatMap[key].Add(finalProfitRatio);

					        lock (_syncObj)
					        {
						        if (finalProfitRatio > record)
						        {
							        record = finalProfitRatio;
							        bestMa = maDays;
							        bestEma = emaDays;
							        bestDebug = debug;
							        bestTrades = tradeCount;
						        }
					        }
				        }
			        }
		        });

		        equityCurve.Add(capital);
		    }

		    // В конце: вывод equity curve
		    Console.WriteLine("\nEquity Curve:");
		    foreach (var eq in equityCurve)
		        Console.WriteLine(eq.ToString("F2"));
		    
		    ShowHeatMap(heatMap.ToDictionary(), false);
		}
		
		private static (decimal, string) Strategy2(
			IList<StockPrice> dataPoints,
			int maDays,
			int emaDays)
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
				var maPrevDay = MovingAverage(dataPoints, maDays, i - 1);
				var ema = ExponentialMovingAverage(dataPoints, emaDays, i);
				var emaPrevDay = ExponentialMovingAverage(dataPoints, emaDays, i - 1);

				if (ma == null || ema == null)
					continue;

				// if EMA goes below MA - sell
				if (ema < ma && emaPrevDay > maPrevDay)
				{
					string str = $"{dp.Date:d} Selling {shares:N} shares at {price:C} for  {shares * price:C}\n";
					debug += str;

					cash = shares * price;
					shares = 0;
				}
				// if EMA goes above MA - sell
				else if (ema > ma && emaPrevDay < maPrevDay)
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

		public static decimal? ExponentialMovingAverage(IList<StockPrice> dataPoints, int periods, int currentDayIndex)
		{
			if (currentDayIndex < periods - 1) return null;  // Need at least 'periods' points for SMA seed

			var alpha = 2m / (periods + 1m);

			// Allocate array for all EMAs up to currentDayIndex (0-based indexing)
			var emas = new decimal[currentDayIndex + 1];

			// Step 1: Seed the first EMA as SMA of first 'periods' closing prices
			decimal sma = 0m;
			for (int i = 0; i < periods; i++)
			{
				sma += dataPoints[i].ClosingPrice;
			}
			emas[periods - 1] = sma / periods;  // EMA at index periods-1

			var sma2 = MovingAverage(dataPoints, periods, dataPoints.Count - 1);

			// Step 2: Recurse forward from seed to currentDayIndex
			for (int i = periods; i <= currentDayIndex; i++)
			{
				emas[i] = alpha * dataPoints[i].ClosingPrice + (1 - alpha) * emas[i - 1];
			}

			return emas[currentDayIndex];
		}

	}
}
