using AlphaVantage.Net.Core.Client;
using AlphaVantage.Net.Stocks;
using AlphaVantage.Net.Common.Size;
using AlphaVantage.Net.Stocks.Client;
using AlphaVantage.Net.Common.Intervals;

namespace StrategyTester
{
	/// <summary>
	/// Currently free access to AlphaVantage does not adjust for stock splits, so it becomes almost useless,
	/// or only if you use the last portion of the result, after the last stock split
	/// </summary>
	public class AlphaVantageNetProvider : IStockPriceProvider
	{
		private StocksClient _client;

		public AlphaVantageNetProvider(string apiKey)
		{
			_client = new AlphaVantageClient(apiKey).Stocks();
		}
		
		public IList<StockPrice> GetLast10YearsOfPrices(string ticker)
		{
			StockTimeSeries res = _client.GetTimeSeriesAsync(ticker, Interval.Daily, OutputSize.Full, isAdjusted: true).Result;

			return res.DataPoints.Reverse().Select(x => new StockPrice(x.Time, x.ClosingPrice)).ToList();
		}
	}
}