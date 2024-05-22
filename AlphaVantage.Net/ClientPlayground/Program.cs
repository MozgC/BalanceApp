using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AlphaVantage.Net.Stocks;
using AlphaVantage.Net.Stocks.TimeSeries;

namespace ClientPlayground
{
	class Program
	{
		private string Symbol = "VOO";
		private const string ApiKey = "1";

		static void Main(string[] args)
		{
			var client = new AlphaVantageStocksClient(ApiKey);

			var result = client.RequestDailyTimeSeriesAsync("VOO", TimeSeriesSize.Full, adjusted: true).Result;

			var dataPoints = result.DataPoints.ToList();
			Console.WriteLine(Calc(dataPoints));

			Console.ReadLine();
		}

		private static decimal Calc(IList<StockDataPoint> dataPoints)
		{
			return dataPoints[0].ClosingPrice / dataPoints.Last().ClosingPrice;
		}
	}
}
