using System.Collections.Generic;
using AlphaVantage.Net.Stocks.TimeSeries;

namespace ClientPlayground;

public class BuyLowSellHighStrategy : InvestmentStrategy
{
	public override string Name => $"Buy low & sell high ({_periodInDays} days, {_percentDiff}%)";

	private readonly int     _periodInDays;
	private readonly decimal _percentDiff;
		
	public BuyLowSellHighStrategy(decimal initialInvestment, int periodInDays, decimal percentDiff) : base(initialInvestment)
	{
		_periodInDays = periodInDays;
		_percentDiff  = percentDiff;
	}

	public override (decimal result, string log) Execute(IList<StockDataPoint> dataPoints)
	{
		string debug = InvestmentStrategy.GetBuyingSharesAtForString(dataPoints[0], _initialInvestment);
			
		decimal paidPrice = dataPoints[0].ClosingPrice;
		decimal shares    = _initialInvestment / paidPrice;
		decimal cash      = 0;
			
		for (int i = 1; i < dataPoints.Count; i++)
		{
			decimal price = dataPoints[i].ClosingPrice;

			if (shares > 0 && price >= paidPrice * (1 + _percentDiff / 100))
			{
				debug += InvestmentStrategy.GetSellingSharesAtForString(dataPoints[i], shares);
				cash = shares * price;
				shares = 0;
			}
			else if (shares == 0)
			{
				var (min, max) = GetMinAndMaxPriceForPeriod(dataPoints, i, _periodInDays);

				decimal maxBuyPrice = (max - min) / 10 + min;

				if (price <= maxBuyPrice)
				{
					debug += InvestmentStrategy.GetBuyingSharesAtForString(dataPoints[i], cash);
					shares = cash / price;
					cash   = 0;
				}
			}
		}

		decimal result = cash > 0
			? cash
			: shares * dataPoints[^1].ClosingPrice;

		return (result, debug);
	}
}