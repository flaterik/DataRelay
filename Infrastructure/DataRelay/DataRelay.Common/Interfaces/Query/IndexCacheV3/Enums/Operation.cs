namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
	public enum Operation : byte
	{
		LessThan,
		GreaterThan,
		LessThanEquals,
		GreaterThanEquals,
		Equals,
		NotEquals,
        BitwiseComplement,
        BitwiseAND,
        BitwiseOR,
        BitwiseXOR,
        BitwiseShiftLeft,
        BitwiseShiftRight
	}
}