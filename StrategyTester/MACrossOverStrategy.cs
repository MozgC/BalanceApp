namespace StrategyTester
{
	public class MACrossOverStrategy : Strategy
	{
		public readonly int EmaDays;
		public readonly int MaDays;
		private readonly bool _allowSellingAtLoss;
		private decimal? _currentMA;
		private decimal? _maPrev;
		private decimal? _currentEMA;
		private decimal? _emaPrev;
		private decimal _price;

		public MACrossOverStrategy(string ticker, int emaDays, int maDays, bool allowSellingAtLoss = true) 
			: base(
				ticker, 
				"Simple MA & EMA crossover strategy", 
				$"If {emaDays} days EMA crosses over below {maDays} days MA - we sell. If {emaDays} days EMA crosses over above {maDays} days MA- we buy.",
				$"EMA Days = {emaDays}, MA Days = {maDays}")
		{
			EmaDays = emaDays;
			MaDays  = maDays;
			_allowSellingAtLoss = allowSellingAtLoss;
		}
		
		public override (decimal x, decimal y) GetHeatmapKey()
		{
			return (MaDays, EmaDays);
		}

		public override Func<RunReport, decimal> GetHeatmapValue()
		{
			return r => r.FinalProfitRatio;
		}

		protected override bool CalcDailyParametersAndDecideIfCanBuyOrSell(int currentIndex)
		{
			var dp = DataPoints[currentIndex];
			_price = dp.ClosingPrice;

			_currentMA  = IndicatorFunctions.MovingAverage(Ticker, DataPoints, MaDays, currentIndex);
			_maPrev     = IndicatorFunctions.MovingAverage(Ticker, DataPoints, MaDays, currentIndex - 1);
			_currentEMA = IndicatorFunctions.ExponentialMovingAverage(Ticker, DataPoints, EmaDays, currentIndex);
			_emaPrev    = IndicatorFunctions.ExponentialMovingAverage(Ticker, DataPoints, EmaDays, currentIndex - 1);

			if (_currentMA == null || _currentEMA == null)
				return false;

			return true;
		}

		protected override bool ShouldEnter()
		{
			return _emaPrev < _maPrev && _currentEMA > _currentMA;
		}
		
		protected override bool ShouldExit()
		{
			if (!_allowSellingAtLoss && LastBuy != null && _price < LastBuy.ClosingPrice)
				return false;
				
			return _emaPrev > _maPrev && _currentEMA < _currentMA;
		}

		public override IEnumerable<Strategy> GenerateStrategies(string ticker)
		{
			int minMA   = 20;
			int maCount = 35;

			for (int maDays = minMA; maDays < minMA + maCount; maDays++)
			{
				for (int emaDays = 7; emaDays <= (maDays + 1) / 2; emaDays += 1)
				{
					yield return new MACrossOverStrategy(ticker, emaDays, maDays);
				}
			}
		}
	}
}
