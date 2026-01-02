using System;
using System.Collections.Generic;

namespace AlphaVantage.Net.Core
{
	public class QueueEx<T> : Queue<T>
	{
		public QueueEx(int capacity) : base(capacity) { }

		public bool TryDequeue(Predicate<T> valueCheck, out T value)
		{
			if (this.Count > 0 && valueCheck(this.Peek()))
			{
				value = this.Dequeue();
				return true;
			}

			value = default(T);
			return false;
		}
	}
}
