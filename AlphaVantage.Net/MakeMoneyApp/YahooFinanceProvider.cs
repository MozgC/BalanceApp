using System;
using OoplesFinance.YahooFinanceAPI.Enums;
using OoplesFinance.YahooFinanceAPI;
using System.Collections.Generic;
using System.Linq;

namespace MakeMoneyApp
{
	public class YahooFinanceProvider : IStockPriceProvider
	{
		private YahooClient _yahooClient = new YahooClient();

		public IList<StockPrice> GetLast10YearsOfPrices(string ticker)
		{
			var data = _yahooClient.GetChartInfoAsync(ticker, TimeRange._10Years, TimeInterval._1Day).Result;

			var data2 = _yahooClient.GetHistoricalDataAsync(ticker, DataFrequency.Daily, DateTime.Today.AddYears(-10),
				DateTime.Today.AddDays(1), true).Result;

			var data3 = _yahooClient.GetStockSplitDataAsync(ticker, DataFrequency.Daily, DateTime.Today.AddYears(-10),
				DateTime.Today.AddDays(1), true).Result;

			return data.DateList.Select((t, i) => new StockPrice(t, (decimal) data.CloseList[i])).ToList();
		}
	}
}
