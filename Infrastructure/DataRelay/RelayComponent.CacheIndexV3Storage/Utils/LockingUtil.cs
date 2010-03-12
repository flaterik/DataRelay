using System;
using System.Collections.Generic;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Utils
{
    internal class LockingUtil
    {
        #region Data Members

        internal object[] LockerObjects
        {
            get; private set;
        }

        private const int DEFAULT_LOCK_MULTIPLIER = 8;

        internal static LockingUtil Instance { get; private set; }

        #endregion

        #region Ctors

        private LockingUtil(){}

        static LockingUtil()
        {
            Instance = new LockingUtil();
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets the lock.
        /// </summary>
        /// <param name="primaryId">The primary id.</param>
        /// <returns></returns>
        internal object GetLock(int primaryId)
        {
            return LockerObjects[primaryId % LockerObjects.Length];
        }

        /// <summary>
        /// Initializes the locker objects.
        /// </summary>
        /// <param name="lockMultiplier">The lock multiplier.</param>
        /// <param name="numClustersInGroup">The num clusters in group.</param>
        internal void InitializeLockerObjects(int lockMultiplier, int numClustersInGroup)
        {
            int procCountBasedLockerObjectNum = Environment.ProcessorCount * (lockMultiplier > 0 && lockMultiplier < 1000 ? lockMultiplier : DEFAULT_LOCK_MULTIPLIER);
            int lockerObjectsNum = GetNextPrimeNumber(Math.Max(numClustersInGroup, procCountBasedLockerObjectNum));
            LockerObjects = new object[lockerObjectsNum];
            for (int i = 0; i < lockerObjectsNum; i++)
            {
                LockerObjects[i] = new object();
            }
            LoggingUtil.Log.InfoFormat("Using {0} locks to synchronize access to indicies", lockerObjectsNum);
        }

        /// <summary>
        /// Gets the next prime number.
        /// </summary>
        /// <param name="number">The number.</param>
        /// <returns></returns>
        private static int GetNextPrimeNumber(int number)
        {
            int retVal;
            if (number < 2)
            {
                retVal = 2;
            }
            else
            {
                List<int> primesSoFar = new List<int> { 2 };

                for (int i = 3; ; i += 2)
                {
                    if (IsPrime(i, primesSoFar))
                    {
                        primesSoFar.Add(i);
                    }

                    if (primesSoFar[primesSoFar.Count - 1] > number)
                    {
                        retVal = primesSoFar[primesSoFar.Count - 1];
                        break;
                    }
                }
            }
            return retVal;
        }

        /// <summary>
        /// Determines whether the specified n is prime.
        /// </summary>
        /// <param name="n">The n.</param>
        /// <param name="primesSoFar">The primes so far.</param>
        /// <returns>
        /// 	<c>true</c> if the specified n is prime; otherwise, <c>false</c>.
        /// </returns>
        private static bool IsPrime(int n, List<int> primesSoFar)
        {
            // Take all prime nos. <= square root of n and we look for first among them that divides n evenly
            // if such a divisor exists then n is not prime else it is   

            int squareRoot = (int)Math.Sqrt(n);
            for (int i = 0; i < primesSoFar.Count && primesSoFar[i] <= squareRoot; i++)
            {
                if (n % primesSoFar[i] == 0)
                    return false;
            }
            return true;
        }

        #endregion
    }
}