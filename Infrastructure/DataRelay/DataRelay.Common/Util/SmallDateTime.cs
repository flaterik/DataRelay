using System;

namespace MySpace.DataRelay.Common.Util
{

	// ------------------------------------------------------------ 
	//  Sample Values
	// ------------------------------------------------------------ 
	// Min					Granularity		Max						
	// ----   				-----------		---						
	// 1/1/1900 12:00:00 AM	Minute			1/24/5983 2:07:00 AM	
	//						Second			1/20/1968 3:14:07 AM	
	// 1/1/2000 12:00:00 AM	Minute			1/23/6083 2:07:00 AM	
	//						Second			1/19/2068 3:14:07 AM	
	// ------------------------------------------------------------ 

	public class SmallDateTime
	{
		// Consts
		public static readonly bool SecondGranularity = true;
		public static readonly DateTime MinValue = new DateTime(2000, 1, 1);
		public static readonly DateTime MaxValue;

		#region Internal Contants
		private static readonly long MinValueInt64;
		private static readonly long MaxValueInt64;
		private static readonly long DeltaInt64;

		private static readonly int MinValueInt32;
		private static readonly int MaxValueInt32;
		private static readonly int DeltaInt32;

		private static readonly long INTERNAL_EPOCH;
		#endregion
		
		public static readonly int LOST_ACCURACY_TICKS;
		
		// DataMember
		private DateTime fullDateTime;
		
		// Static constructor
		static SmallDateTime()
		{			
			MaxValue = (SecondGranularity ? MinValue.AddSeconds(Int32.MaxValue) : MinValue.AddMinutes(Int32.MaxValue));

			MinValueInt64 = MinValue.Ticks;
			MaxValueInt64 = MaxValue.Ticks;
			DeltaInt64 = MaxValueInt64 - MinValueInt64;

			MinValueInt32 = 0;
			MaxValueInt32 = Int32.MaxValue;
			DeltaInt32 = MaxValueInt32 - MinValueInt32;

			INTERNAL_EPOCH = Convert.ToInt64((SecondGranularity ? new TimeSpan(MinValue.Ticks).TotalSeconds : new TimeSpan(MinValue.Ticks).TotalMinutes));
			LOST_ACCURACY_TICKS = (int)(DeltaInt64 / DeltaInt32);
		}

		// Constructors
		public SmallDateTime()
		{
			fullDateTime = MinValue;
		}
		public SmallDateTime(SmallDateTime smallDttm)
		{
			this.fullDateTime = smallDttm.FullDateTime;
		}
		public SmallDateTime(DateTime dttm)
		{
			this.FullDateTime = dttm;
		}
		public SmallDateTime(long ticks)
		{
			this.TicksInt64 = ticks;					
		}
		public SmallDateTime(int ticks)
		{
			this.TicksInt32 = ticks;
		}
		
		// Properties
		public long TicksInt64
		{
			get
			{
				return fullDateTime.Ticks;
			}
			set
			{
				ValidateInt64Ticks(value);
				this.fullDateTime = new DateTime(Normailize(value));	
			}
		}
		public int TicksInt32
		{
			get
			{
				return GetInt32Tics(this.fullDateTime);
			}
			set
			{
				ValidateInt32Ticks(value);
				this.fullDateTime = GetDateTime(value);
			}
		}
		public DateTime FullDateTime 
		{
			get
			{
				return fullDateTime;
			}
			set
			{
				Validate(value);
				this.fullDateTime = new DateTime(Normailize(value.Ticks));
			}
		}

		// Util
		private static void Validate(DateTime dttm)
		{
			if (dttm < MinValue || dttm > MaxValue)
			{
				throw new Exception("SmallDateTime cannot represent this date: " + dttm.ToString());
			}
		}
		private static void ValidateInt64Ticks(long tics)
		{
			if (tics < MinValueInt64 || tics > MaxValueInt64)
			{
				throw new Exception("SmallDateTime cannot represent this date (tics - int64): " + tics.ToString());
			}
		}
		private static void ValidateInt32Ticks(int tics)
		{
			if (tics < MinValueInt32 || tics > MaxValueInt32)
			{
				throw new Exception("SmallDateTime cannot represent this date (tics - int32): " + tics.ToString());
			}
		}
		private static long Normailize(long ticks)
		{
			long ticks2 = LOST_ACCURACY_TICKS * (ticks / LOST_ACCURACY_TICKS);
			return ticks2;
		}
		private static long GetTotalTimeUnits(DateTime dttm)
		{		
			return Convert.ToInt64((SecondGranularity ? new TimeSpan(dttm.Ticks).TotalSeconds : new TimeSpan(dttm.Ticks).TotalMinutes));
		}
		private static int GetInt32Tics(DateTime dttm)
		{
			return (int)(GetTotalTimeUnits(dttm) - INTERNAL_EPOCH);
		}
		private static DateTime GetDateTime(int ticks)
		{
			return (SecondGranularity ? MinValue.AddSeconds(ticks) : MinValue.AddMinutes(ticks));
		}
        
        public override string ToString()
        {
            return FullDateTime.ToString();    
        }
	}
}
