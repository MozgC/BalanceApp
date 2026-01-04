namespace StrategyTester
{
	public class MACrossOverStrategy : Strategy
	{
		public readonly int EmaDays;
		public readonly int MaDays;
		private decimal? _currentMA;
		private decimal? _maPrev;
		private decimal? _currentEMA;
		private decimal? _emaPrev;

		public MACrossOverStrategy(string ticker, int emaDays, int maDays) 
			: base(
				ticker, 
				"Simple MA & EMA crossover strategy", 
				$"If {emaDays} EMA crosses over below {maDays} MA - we sell. If {emaDays} EMA crosses over above {maDays} - we buy.",
				$"EMA Days = {emaDays}, MA Days = {maDays}")
		{
			EmaDays = emaDays;
			MaDays  = maDays;
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
			decimal price = dp.ClosingPrice;

			bool canSell = HoldingPeriodDays == 0 || dp.Date.Date - LastBuy.Date.Date > TimeSpan.FromDays(HoldingPeriodDays);

			if (Cash > 0 && !canSell)
				return false;

			_currentMA  = IndicatorFunctions.MovingAverage(Ticker, DataPoints, MaDays, currentIndex);
			_maPrev     = IndicatorFunctions.MovingAverage(Ticker, DataPoints, MaDays, currentIndex - 1);
			_currentEMA = IndicatorFunctions.ExponentialMovingAverage(Ticker, DataPoints, EmaDays, currentIndex);
			_emaPrev    = IndicatorFunctions.ExponentialMovingAverage(Ticker, DataPoints, EmaDays, currentIndex - 1);

			if (_currentMA == null || _currentEMA == null)
				return false;

			return true;
		}

		protected override void Initialize()
		{
			Initialize(true);
		}

		protected override bool ShouldEnter()
		{
			return _emaPrev < _maPrev && _currentEMA > _currentMA;
		}
		
		protected override bool ShouldExit()
		{
			return _emaPrev > _maPrev && _currentEMA < _currentMA;
		}
	}
}
