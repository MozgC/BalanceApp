using System;
using System.Collections.Generic;
using System.Linq;
using StrategyTester;

namespace StrategyTester;

/// <summary>
/// Each month we save X dollars, when the stock price drops by Y percent from the maximum in Z days, we invest all cash
/// </summary>
public class SaveCashInvestWhenDropStrategy : InvestmentStrategy
{
	private decimal      _amountEachMonth;
	private readonly int _priceDropPercent;
	private readonly int _compareToMaxPriceInDaysPeriod;
	
	public override string Name => "SaveCashInvestWhenDropStrategy";

	public override string Description =>
		$"{Name}, Initial Investment: {_initialInvestment:C}, Amount to save each month: {_amountEachMonth:C}, Min Price Drop Percent: {_priceDropPercent}%, Compare to maximum price in {_compareToMaxPriceInDaysPeriod} days.";

	public SaveCashInvestWhenDropStrategy(decimal initialInvestment, decimal amountEachMonth, int priceDropPercent, int compareToMaxPriceInDaysPeriod) : base(initialInvestment)
	{
		_amountEachMonth               = amountEachMonth;
		_priceDropPercent              = priceDropPercent;
		_compareToMaxPriceInDaysPeriod = compareToMaxPriceInDaysPeriod;
	}

	public override (decimal result, string log) Execute(IList<StockPrice> dataPoints)
	{
		decimal cash   =  _initialInvestment;
		decimal shares = cash / dataPoints[0].ClosingPrice;
		string  debug  = "";
		
		if (cash > 0)
			debug += GetBuyingSharesAtForString(dataPoints[0], cash);

		cash = 0;

		foreach (var x in GetFirstBusinessDatesOfEachMonth(dataPoints, false))
		{
			cash += _amountEachMonth;
			
			if (x.dp.Date.Subtract(TimeSpan.FromDays(_compareToMaxPriceInDaysPeriod)) > dataPoints[0].Date)
			{
				var minmax  = GetMinAndMaxPriceForPeriod(dataPoints, x.index, _compareToMaxPriceInDaysPeriod);
				var percent = (decimal) (100 - _priceDropPercent) / 100;
				
				if (x.dp.ClosingPrice <= minmax.maxPrice * percent)
				{
					shares += cash / x.dp.ClosingPrice;
					debug  += GetBuyingSharesAtForString(x.dp, cash);
					cash   = 0;
				}
			}
		}

		return (dataPoints.Last().ClosingPrice * shares + cash, debug);
	}
}