using System;

namespace MySpace.Common.HelperObjects
{
    public static class CacheHelper
    {
        public static int GeneratePrimaryId(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return 1;
            }

            if (bytes.Length >= 4)
            {
                int value = BitConverter.ToInt32(bytes, 0);
                if (value == Int32.MinValue) // To prevent Math.Abs exception
                {
                    return 1;
                }
                return Math.Abs(value);
            }

            return Math.Abs((int)bytes[0]);
        }
    }
}
