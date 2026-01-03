using System.Collections.Generic;
using System.Linq;
using StrategyTester;

namespace StrategyTester;

public class DcaStrategy : InvestmentStrategy
{
	private decimal _amountEachMonth;
	public override string Name => "Dollar-Cost Averaging";

	public override string Description => $"{Name}, Initial investment: {_initialInvestment:C}, Amount to invest each month: {_amountEachMonth:C}";

	public DcaStrategy(decimal initialInvestment, decimal amountEachMonth) : base(initialInvestment)
	{
		_amountEachMonth = amountEachMonth;
	}

	public override (decimal result, string log) Execute(IList<StockPrice> dataPoints)
	{
		decimal cash  = _initialInvestment;
		decimal total = _initialInvestment;

		decimal shares = cash / dataPoints[0].ClosingPrice;
		var lastBuy = dataPoints[0];

		string debug = GetBuyingSharesAtForString(dataPoints[0], cash);

		cash = 0;

		foreach (var x in GetFirstBusinessDatesOfEachMonth(dataPoints, false))
		{
			shares += _amountEachMonth / x.dp.ClosingPrice;
			total += _amountEachMonth;
			debug += GetBuyingSharesAtForString(x.dp, _amountEachMonth);
		}

		return (dataPoints.Last().ClosingPrice * shares, debug);
	}
}