using System.Collections.Generic;

namespace MakeMoneyApp;

public interface IStockPriceProvider
{
	IList<StockPrice> GetLast10YearsOfPrices(string ticker);
}