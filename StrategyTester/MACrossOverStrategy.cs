namespace StrategyTester
{
	public class MACrossOverStrategy : Strategy
	{
		private readonly int _emaDays;
		private readonly int _maDays;
		private decimal? _currentMA;
		private decimal? _maPrev;
		private decimal? _currentEMA;
		private decimal? _emaPrev;

		public MACrossOverStrategy(string ticker, IList<StockPrice> dataPoints, int emaDays, int maDays) 
			: base(
				ticker, 
				dataPoints, 
				"Simple MA & EMA crossover strategy", 
				$"If {emaDays} EMA crosses over below {maDays} MA - we sell. If {emaDays} EMA crosses over above {maDays} - we buy.")
		{
			_emaDays = emaDays;
			_maDays  = maDays;
		}

		protected override bool CalcDailyParametersAndDecideIfCanBuyOrSell(int currentIndex)
		{
			var dp = DataPoints[currentIndex];
			decimal price = dp.ClosingPrice;

			bool canSell = HoldingPeriodDays == 0 || dp.Date.Date - LastBuy.Date.Date > TimeSpan.FromDays(HoldingPeriodDays);

			if (Cash > 0 && !canSell)
				return false;

			_currentMA  = IndicatorFunctions.MovingAverage(Ticker, DataPoints, _maDays, currentIndex);
			_maPrev     = IndicatorFunctions.MovingAverage(Ticker, DataPoints, _maDays, currentIndex - 1);
			_currentEMA = IndicatorFunctions.ExponentialMovingAverage(Ticker, DataPoints, _emaDays, currentIndex);
			_emaPrev    = IndicatorFunctions.ExponentialMovingAverage(Ticker, DataPoints, _emaDays, currentIndex - 1);

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
