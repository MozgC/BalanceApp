using System.Collections.Generic;

namespace StrategyTester;

/// <summary>
/// An interface representing a stock price history information so that we could switch different providers, like Yahoo Finance or AlphaVantage
/// </summary>
public interface IStockPriceProvider
{
	IList<StockPrice> GetLast10YearsOfPrices(string ticker);
}