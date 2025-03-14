﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AlphaVantage.Net.Core;
using AlphaVantage.Net.Stocks.BatchQuotes;
using AlphaVantage.Net.Stocks.Parsing;
using AlphaVantage.Net.Stocks.TimeSeries;
using AlphaVantage.Net.Stocks.Utils;
using AlphaVantage.Net.Stocks.Validation;
//using Microsoft.VisualStudio.Threading;
using CodeJam.Threading;

namespace AlphaVantage.Net.Stocks
{
    /// <summary>
    /// Client for Alpha Vantage API (stock data only)
    /// </summary>
    public class AlphaVantageStocksClient
    {
        private readonly string _apiKey;
        private AlphaVantageCoreClient _coreClient;
        private readonly StockDataParser _parser;
        
        public AlphaVantageStocksClient(string apiKey)
        {
            if(string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentNullException(nameof(apiKey));
            
            _apiKey = apiKey;
            _coreClient = new AlphaVantageCoreClient();
            _parser = new StockDataParser();
        }

        public async Task<StockTimeSeries> RequestIntradayTimeSeriesAsync(
            string symbol, 
            IntradayInterval interval = IntradayInterval.SixtyMin, 
            TimeSeriesSize size = TimeSeriesSize.Compact)
        {
            var query = new Dictionary<string, string>()
            {
                {StockApiQueryVars.Symbol, symbol},
                {StockApiQueryVars.IntradayInterval, interval.ConvertToString()},
                {StockApiQueryVars.OutputSize, size.ConvertToString()}
            };
            
            return await RequestTimeSeriesAsync(ApiFunction.TIME_SERIES_INTRADAY, query);
        }

        public async Task<StockTimeSeries> RequestDailyTimeSeriesAsync(
            string symbol, 
            TimeSeriesSize size = TimeSeriesSize.Compact, 
            bool adjusted = false)
        {
            var query = new Dictionary<string, string>()
            {
                {StockApiQueryVars.Symbol, symbol},
                {StockApiQueryVars.OutputSize, size.ConvertToString()}
            };
            
            var function = adjusted ? 
                ApiFunction.TIME_SERIES_DAILY_ADJUSTED : 
                ApiFunction.TIME_SERIES_DAILY;
            
            return await RequestTimeSeriesAsync(function, query);
        }
        
        public async Task<StockTimeSeries> RequestWeeklyTimeSeriesAsync(string symbol, bool adjusted = false)
        {
            var query = new Dictionary<string, string>()
            {
                {StockApiQueryVars.Symbol, symbol},
            };
            
            var function = adjusted ? 
                ApiFunction.TIME_SERIES_WEEKLY_ADJUSTED : 
                ApiFunction.TIME_SERIES_WEEKLY;
            
            return await RequestTimeSeriesAsync(function, query);
        }
        
        public async Task<StockTimeSeries> RequestMonthlyTimeSeriesAsync(string symbol, bool adjusted = false)
        {
            var query = new Dictionary<string, string>()
            {
                {StockApiQueryVars.Symbol, symbol},
            };
            
            var function = adjusted ? 
                ApiFunction.TIME_SERIES_MONTHLY_ADJUSTED : 
                ApiFunction.TIME_SERIES_MONTHLY;

            return await RequestTimeSeriesAsync(function, query);
        }

        public async Task<ICollection<StockQuote>> RequestBatchQuotesAsync(string[] symbols)
        {
            var symbolsString = string.Join(",", symbols);
            
            var query = new Dictionary<string, string>()
            {
                {StockApiQueryVars.BatchSymbols, symbolsString},
            };
                        
            var jObject = await _coreClient.RequestApiAsync(_apiKey, ApiFunction.BATCH_STOCK_QUOTES, query);
            var timeSeries = _parser.ParseStockQuotes(jObject);
            
            return timeSeries;
        }

        private const int requestsPerMinute = 5;
        QueueEx<double> _q = new QueueEx<double>(requestsPerMinute);

        private async Task<StockTimeSeries> RequestTimeSeriesAsync(
            ApiFunction function, 
            Dictionary<string, string> query)
        {
	        WaitIfRequestsLimitReached();

            var jObject = await _coreClient.RequestApiAsync(_apiKey, function, query);

            var timeSeries = _parser.ParseTimeSeries(jObject);
            
            return timeSeries;
        }

        private void WaitIfRequestsLimitReached()
        {
	        double timeOfDayInMs = DateTime.Now.TimeOfDay.TotalMilliseconds;

	        while (_q.TryDequeue(t => t < timeOfDayInMs - 60 * 1000, out double value))
	        {
	        }

	        if (_q.Count < requestsPerMinute)
		        _q.Enqueue(timeOfDayInMs);
	        else
	        {
		        int msToWait = (int) (_q.Peek() + 60 * 1000 - timeOfDayInMs + 5000);
				Thread.Sleep(msToWait);
		        _q.Enqueue(DateTime.Now.TimeOfDay.TotalMilliseconds);
	        }
        }
    }
}