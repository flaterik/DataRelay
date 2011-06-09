namespace MySpace.BerkeleyDb.Configuration
{
	/// <summary>
	/// Interface for use in creating settings
	/// </summary>
	public interface ITimerConfig
	{
		bool Enabled { get; set; }
		int Interval { get; set; }
	}
}
