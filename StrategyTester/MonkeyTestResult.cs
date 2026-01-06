namespace StrategyTester;

public class MonkeyTestResult
{
	public decimal RandomEnter90PercentFinalInvestment;
	public decimal RandomExit90PercentFinalInvestment;
	public decimal RandomEnterAndExit90PercentFinalInvestment;

	public MonkeyTestResult(decimal randomEnter90PercentFinalInvestment, decimal randomExit90PercentFinalInvestment, decimal randomEnterAndExit90PercentFinalInvestment)
	{
		RandomEnter90PercentFinalInvestment        = randomEnter90PercentFinalInvestment;
		RandomExit90PercentFinalInvestment         = randomExit90PercentFinalInvestment;
		RandomEnterAndExit90PercentFinalInvestment = randomEnterAndExit90PercentFinalInvestment;
	}
}