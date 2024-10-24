﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using System;

public class RandomHelper
{
    private static readonly Random rand;

    static RandomHelper()
    {
        rand = new Random();
    }

    /// <summary>
    /// Returns a random number between 0.0 and 1.0.
    /// </summary>
    /// <returns></returns>
    public static float NextSingle()
    {
        return rand.NextSingle();
    }

    /// <summary>
    /// Returns a nonnegative random number.
    /// </summary>
    /// <returns></returns>
    public static int Rand32()
    {
        return rand.Next();
    }

    /// <summary>
    /// Returns a nonnegative random number less than the specified maximum.
    /// </summary>
    /// <param name="maxValue"></param>
    /// <returns></returns>
    public static int Rand32(dynamic maxValue)
    {
        return rand.Next(maxValue);
    }

    /// <summary>
    /// Returns a random number within a specified range.
    /// </summary>
    /// <param name="minValue"></param>
    /// <param name="maxValue"></param>
    /// <returns></returns>
    public static int IRand(int minValue, int maxValue)
    {
        return rand.Next(minValue, maxValue);
    }

    public static long IRand64(long minValue, long maxValue)
    {
        return rand.NextInt64(minValue, maxValue);
    }

    public static uint URand(uint minValue, uint maxValue)
    {
        return (uint)rand.Next((int)minValue, (int)maxValue);
    }

    public static TimeSpan TRand(TimeSpan minValue, TimeSpan maxValue)
    {
        return new TimeSpan(rand.NextInt64(minValue.Ticks, maxValue.Ticks));
    }

    public static float FRand(float min, float max)
    {
        Cypher.Assert(max >= min);
        return (float)(rand.NextDouble() * (max - min) + min);
    }

    /// <summary>
    /// Returns true if rand.Next less then chance
    /// </summary>
    /// <param name="chancePercent"></param>
    /// <returns></returns>
    public static bool randChance(float chancePercent)
    {
        return chancePercent > randPercent();
    }

    public static double randPercent()
    {
        return rand.NextDouble() * 100.0;
    }

    /// <summary>
    /// Fills the elements of a specified array of bytes with random numbers.
    /// </summary>
    /// <param name="buffer"></param>
    public static void NextBytes(byte[] buffer)
    {
        rand.NextBytes(buffer);
    }

    public static byte[] GetRandomBytes(int length)
    {
        byte[] buffer = new byte[length];
        rand.NextBytes(buffer);
        return buffer;
    }

    public static T RAND<T>(params T[] args)
    {
        int randIndex = IRand(0, args.Length - 1);

        return args[randIndex];
    }

    public static TimeSpan RandTime(TimeSpan min, TimeSpan max)
    {
        TimeSpan diff = max - min;
        Cypher.Assert(diff >= TimeSpan.Zero);
        Cypher.Assert(diff <= Milliseconds.MaxValue);
        return min + TimeSpan.FromTicks(IRand64(0, diff.Ticks));
    }
}

