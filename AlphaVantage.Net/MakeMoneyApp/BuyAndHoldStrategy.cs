﻿using System.Collections.Generic;
using AlphaVantage.Net.Stocks.TimeSeries;

namespace ClientPlayground;

public class BuyAndHoldStrategy : InvestmentStrategy
{
	public override string Name => "Buy & Hold";

	public BuyAndHoldStrategy(decimal initialInvestment) : base(initialInvestment)
	{
	}

	public override (decimal result, string log) Execute(IList<StockDataPoint> dataPoints)
	{
		decimal shares        = _initialInvestment / dataPoints[0].ClosingPrice;
		decimal initialShares = shares;
		var lastPrice         = dataPoints[^1].ClosingPrice;

		return (initialShares * lastPrice, null);
	}
}