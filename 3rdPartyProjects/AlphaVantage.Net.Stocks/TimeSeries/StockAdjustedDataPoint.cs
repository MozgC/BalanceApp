using System;

namespace AlphaVantage.Net.Stocks.TimeSeries
{
    /// <summary>
    /// Represent single adjusted element of time series
    /// </summary>
    public sealed class StockAdjustedDataPoint : StockDataPoint
    {
        public decimal AdjustedClosingPrice {get; set;}

        public override decimal ClosingPrice
        {
	        get => AdjustedClosingPrice;
	        set => AdjustedClosingPrice = value;
        }

        public decimal DividendAmount {get; set;}
        
        public decimal? SplitCoefficient { get; set; }
    }
}