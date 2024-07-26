// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

/// <summary>World map speed in yards/angle per second</summary>
public record struct Speed
{
    public static readonly Speed Zero = new(0);
    /// <summary>Yards per Second</summary>
    public float PerSec;

    /// <summary>Yards per Millisecond</summary>
    public float PerMS => PerSec / Time.MillisecondsInSecond;

    /// <summary>Is used for <see cref="Framework.Constants.SpellAttr9.SpecialDelayCalculation">special case</see></summary>
    public Milliseconds AsDelayMS => (Milliseconds)(PerSec * Time.MillisecondsInSecond);

    /// <summary>Is used for <see cref="Framework.Constants.SpellAttr9.SpecialDelayCalculation">special case</see></summary>
    public Seconds AsDelaySec => (Seconds)PerSec;

    public static explicit operator Speed(double YardsPerSecond)
    {
        return new(YardsPerSecond);
    }

    public static Speed operator -(Speed speed)
    {
        return new(-speed.PerSec);
    }

    public Speed(double YardsPerSecond)
    {
        PerSec = (float)YardsPerSecond;
    }

    public override string ToString()
    {
        return PerSec.ToString();
    }

    public static implicit operator float(Speed speed) => speed.PerSec;
}


