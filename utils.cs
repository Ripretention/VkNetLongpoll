using System;

namespace Utils
{
	class Math
	{
		static public int getRandomInt()
		{
			Random rnd = new Random();
			return rnd.Next(1, 46116868);
		}
	}
	class Time
	{
		static public int getUnixTime()
		{
			return (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
		}
	}
}
