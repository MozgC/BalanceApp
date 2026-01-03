using System.Collections.Concurrent;

namespace StrategyTester;

public abstract class InvestmentStrategy
{
	protected readonly decimal _initialInvestment;
	public abstract string Name { get; }
	public virtual string Description => Name;

	public InvestmentStrategy(decimal initialInvestment)
	{
		_initialInvestment = initialInvestment;
	}

	public abstract (decimal result, string log) Execute(IList<StockPrice> dataPoints);

	private static ConcurrentDictionary<(int currentDayIndex, int periodInDays), (decimal minPrice, decimal maxPrice)> _minMaxPrice = new ConcurrentDictionary<(int currentDayIndex, int periodInDays), (decimal minPrice, decimal maxPrice)>();

	public static (decimal minPrice, decimal maxPrice) GetMinAndMaxPriceForPeriod(IList<StockPrice> dataPoints, int currentDayIndex, int periodInDays)
	{
		if (_minMaxPrice.TryGetValue((currentDayIndex, periodInDays), out var res))
			return res;

		decimal min = dataPoints[currentDayIndex].ClosingPrice;
		decimal max = dataPoints[currentDayIndex].ClosingPrice;

		int prevDay = currentDayIndex - 1;

		while (prevDay >= 0 && (dataPoints[currentDayIndex].Date.Date - dataPoints[prevDay].Date.Date).Days < periodInDays)
		{
			decimal price = dataPoints[prevDay].ClosingPrice;

			if (price > max)
				max = price;

			if (price < min)
				min = price;

			prevDay--;
		}

		return _minMaxPrice[(currentDayIndex, periodInDays)] = (min, max);
	}

	public static IEnumerable<(StockPrice dp, int index)> GetFirstBusinessDatesOfEachMonth(IList<StockPrice> dps, bool includeFirstDataPoint)
	{
		DateTime? date = null;

		for (var i = 0; i < dps.Count; i++)
		{
			var dpx = dps[i];
			if (date == null)
			{
				date = dpx.Date.Date;

				if (includeFirstDataPoint)
					yield return (dpx, i);

				continue;
			}

			if (date.Value.Month == dpx.Date.Month)
				continue;

			date = dpx.Date.Date;
			yield return (dpx, i);
		}
	}

	public static string GetBuyingSharesAtForString(StockPrice dp, decimal cash)
	{
		return $"{dp.Date:d} Buying {cash / dp.ClosingPrice:N} shares at {dp.ClosingPrice:C} for  {cash:C}\n";
	}
	public static string GetSellingSharesAtForString(StockPrice dp, decimal shares)
	{
		return $"{dp.Date:d} Selling {shares:N} shares at {dp.ClosingPrice:C} for {shares * dp.ClosingPrice:C}\n";
	}
}