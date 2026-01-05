using NUnit.Framework.Legacy;
using static StrategyTester.WalkForwardTester;

#pragma warning disable NUnit2005

namespace StrategyTester.Tests
{
	public class Tests
	{
		[SetUp]
		public void Setup()
		{
		}

		[Test]
		public void GetInSampleAndOutSampleDataTest()
		{
			// Create stock prices from 2019-01-01 to 2025-11-01 (6 years, 11 months)
			var stockPrices = Enumerable.Range(0,6*12 + 11).Select(i => new StockPrice(new DateTime(2019, 1, 1).AddMonths(i), 1)).ToList();

			var samples = WalkForwardTester.GetInSampleAndOutSampleData(stockPrices, 48, 12).ToList();

			ClassicAssert.AreEqual(2, samples.Count);
			
			void AssetFirstInSampleAndOutOfSample(IList<InSampleAndOutSampleData> s)
			{
				ClassicAssert.AreEqual(48, s[0].InSample.Count);
				ClassicAssert.AreEqual(new DateTime(2019, 1, 1),  s[0].InSample[0].Date);
				ClassicAssert.AreEqual(new DateTime(2022, 12, 1), s[0].InSample.Last().Date);
			
				ClassicAssert.AreEqual(12, s[0].OutOfSample.Count);
				ClassicAssert.AreEqual(new DateTime(2023, 1, 1),  s[0].OutOfSample[0].Date);
				ClassicAssert.AreEqual(new DateTime(2023, 12, 1), s[0].OutOfSample.Last().Date);
			}
			
			AssetFirstInSampleAndOutOfSample(samples);

			ClassicAssert.AreEqual(48, samples[1].InSample.Count);
			ClassicAssert.AreEqual(new DateTime(2020, 1, 1),  samples[1].InSample[0].Date);
			ClassicAssert.AreEqual(new DateTime(2023, 12, 1), samples[1].InSample.Last().Date);
			
			ClassicAssert.AreEqual(23, samples[1].OutOfSample.Count);
			ClassicAssert.AreEqual(new DateTime(2024, 1, 1),  samples[1].OutOfSample[0].Date);
			ClassicAssert.AreEqual(new DateTime(2025, 11, 1), samples[1].OutOfSample.Last().Date);
			
			// Now add December 2025, and we should have 3 samples, with last OOS being Jan 2025 - Dec 2025
			
			stockPrices.Add(new StockPrice(new DateTime(2025, 12, 1), 1));
			
			samples = WalkForwardTester.GetInSampleAndOutSampleData(stockPrices, 48, 12).ToList();
			
			ClassicAssert.AreEqual(3, samples.Count);
			
			AssetFirstInSampleAndOutOfSample(samples);

			void Assert2ndInSampleAndOutOfSample(IList<InSampleAndOutSampleData> s)
			{
				ClassicAssert.AreEqual(48, s[1].InSample.Count);
				ClassicAssert.AreEqual(new DateTime(2020, 1, 1),  s[1].InSample[0].Date);
				ClassicAssert.AreEqual(new DateTime(2023, 12, 1), s[1].InSample.Last().Date);
			
				ClassicAssert.AreEqual(12, s[1].OutOfSample.Count);
				ClassicAssert.AreEqual(new DateTime(2024, 1, 1),  s[1].OutOfSample[0].Date);
				ClassicAssert.AreEqual(new DateTime(2024, 12, 1), s[1].OutOfSample.Last().Date);
			}

			Assert2ndInSampleAndOutOfSample(samples);
				
			ClassicAssert.AreEqual(48, samples[2].InSample.Count);
			ClassicAssert.AreEqual(new DateTime(2021, 1, 1),  samples[2].InSample[0].Date);
			ClassicAssert.AreEqual(new DateTime(2024, 12, 1), samples[2].InSample.Last().Date);
			
			ClassicAssert.AreEqual(12, samples[2].OutOfSample.Count);
			ClassicAssert.AreEqual(new DateTime(2025, 1, 1),  samples[2].OutOfSample[0].Date);
			ClassicAssert.AreEqual(new DateTime(2025, 12, 1), samples[2].OutOfSample.Last().Date);
			
			// Now add Jan 2026, and we should still have 3 samples, with last OOS being Jan 2025 - Jan 2026
			
			stockPrices.Add(new StockPrice(new DateTime(2026, 1, 1), 1));
			
			samples = WalkForwardTester.GetInSampleAndOutSampleData(stockPrices, 48, 12).ToList();
			
			ClassicAssert.AreEqual(3, samples.Count);
			
			AssetFirstInSampleAndOutOfSample(samples);
			Assert2ndInSampleAndOutOfSample(samples);
			
			ClassicAssert.AreEqual(48, samples[2].InSample.Count);
			ClassicAssert.AreEqual(new DateTime(2021, 1, 1),  samples[2].InSample[0].Date);
			ClassicAssert.AreEqual(new DateTime(2024, 12, 1), samples[2].InSample.Last().Date);
			
			ClassicAssert.AreEqual(13, samples[2].OutOfSample.Count);
			ClassicAssert.AreEqual(new DateTime(2025, 1, 1),  samples[2].OutOfSample[0].Date);
			ClassicAssert.AreEqual(new DateTime(2026, 1, 1),  samples[2].OutOfSample.Last().Date);
		}
	}
}
#pragma warning restore NUnit2005