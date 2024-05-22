using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using AlphaVantage.Net.Stocks.TimeSeries;

namespace ClientPlayground;

public abstract class InvestmentStrategy
{
	protected readonly decimal _initialInvestment;
	public abstract string Name { get; }
	public virtual string Description => Name;

	public InvestmentStrategy(decimal initialInvestment)
	{
		_initialInvestment = initialInvestment;
	}

	public abstract (decimal result, string log) Execute(IList<StockDataPoint> dataPoints);

	private static ConcurrentDictionary<(int currentDayIndex, int periodInDays), (decimal minPrice, decimal maxPrice)> _minMaxPrice = new ConcurrentDictionary<(int currentDayIndex, int periodInDays), (decimal minPrice, decimal maxPrice)>();

	public static (decimal minPrice, decimal maxPrice) GetMinAndMaxPriceForPeriod(IList<StockDataPoint> dataPoints, int currentDayIndex, int periodInDays)
	{
		if (_minMaxPrice.TryGetValue((currentDayIndex, periodInDays), out var res))
			return res;

		decimal min = dataPoints[currentDayIndex].ClosingPrice;
		decimal max = dataPoints[currentDayIndex].ClosingPrice;

		int prevDay = currentDayIndex - 1;

		while (prevDay >= 0 && (dataPoints[currentDayIndex].Time.Date - dataPoints[prevDay].Time.Date).Days < periodInDays)
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

	public static IEnumerable<(StockDataPoint dp, int index)> GetFirstBusinessDatesOfEachMonth(IList<StockDataPoint> dps, bool includeFirstDataPoint)
	{
		DateTime? date = null;

		for (var i = 0; i < dps.Count; i++)
		{
			var dpx = dps[i];
			if (date == null)
			{
				date = dpx.Time.Date;

				if (includeFirstDataPoint)
					yield return (dpx, i);

				continue;
			}

			if (date.Value.Month == dpx.Time.Month)
				continue;

			date = dpx.Time.Date;
			yield return (dpx, i);
		}
	}

	public static string GetBuyingSharesAtForString(StockDataPoint dp, decimal cash)
	{
		return $"{dp.Time:d} Buying {cash / dp.ClosingPrice:N} shares at {dp.ClosingPrice:C} for  {cash:C}\n";
	}
	public static string GetSellingSharesAtForString(StockDataPoint dp, decimal shares)
	{
		return $"{dp.Time:d} Selling {shares:N} shares at {dp.ClosingPrice:C} for {shares * dp.ClosingPrice:C}\n";
	}
}