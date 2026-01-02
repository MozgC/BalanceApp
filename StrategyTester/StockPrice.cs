using System;

namespace StrategyTester
{
	public class StockPrice
	{
		public DateTime Date;
		public decimal  ClosingPrice;

		public StockPrice() { }

		public StockPrice(DateTime date, decimal closingPrice)
		{
			Date         = date;
			ClosingPrice = closingPrice;
		}

	}
}
