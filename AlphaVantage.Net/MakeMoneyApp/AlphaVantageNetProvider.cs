using OoplesFinance.YahooFinanceAPI.Enums;
using OoplesFinance.YahooFinanceAPI;
using System.Collections.Generic;
using System.Linq;
using AlphaVantage.Net.Stocks.TimeSeries;
using AlphaVantage.Net.Stocks;
using System;

namespace MakeMoneyApp
{
	/// <summary>
	/// Currently free access to AlphaVantage does not adjust for stock splits, so it becomes almost useless,
	/// or only if you use the last portion of the result, after the last stock split
	/// </summary>
	public class AlphaVantageNetProvider : IStockPriceProvider
	{
		private AlphaVantageStocksClient _client;

		public AlphaVantageNetProvider(string apiKey)
		{
			_client = new AlphaVantageStocksClient(apiKey);
		}
		
		public IList<StockPrice> GetLast10YearsOfPrices(string ticker)
		{
			var res = _client.RequestDailyTimeSeriesAsync(ticker, TimeSeriesSize.Full, false).Result;

			return res.DataPoints.Cast<StockDataPoint>().Reverse().Select(x => new StockPrice(x.Time, x.ClosingPrice)).ToList();
		}
	}
}