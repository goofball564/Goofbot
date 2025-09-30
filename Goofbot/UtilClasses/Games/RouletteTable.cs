namespace Goofbot.UtilClasses.Games;

using System;
using System.Security.Cryptography;

internal class RouletteTable
{
    private int lastSpinResultBackValue = 0;

    public enum RouletteColor
    {
        Red,
        Black,
        Green,
    }

    public string LastSpinResult
    {
        get
        {
            if (lastSpinResultBackValue < 0)
            {
                return "00";
            }
            else
            {
                return lastSpinResultBackValue.ToString();
            }
        }
    }

    public RouletteColor Color
    {
        get
        {
            if (lastSpinResultBackValue >= 1 && lastSpinResultBackValue <= 10 || lastSpinResultBackValue >= 19 && lastSpinResultBackValue <= 28)
            {
                return lastSpinResultBackValue % 2 == 0 ? RouletteColor.Black : RouletteColor.Red;
            }
            else if (lastSpinResultBackValue >= 11 && lastSpinResultBackValue <= 18 || lastSpinResultBackValue >= 29 && lastSpinResultBackValue <= 36)
            {
                return lastSpinResultBackValue % 2 == 0 ? RouletteColor.Red : RouletteColor.Black;
            }
            else
            {
                return RouletteColor.Green;
            }
        }
    }

    // Returns 0 if last spin was 00 or 0
    public int Column
    {
        get
        {
            if (lastSpinResultBackValue <= 0)
            {
                return 0;
            }
            else
            {
                int column = lastSpinResultBackValue % 3;
                return column > 0 ? column : 3;
            }
        }
    }

    public int Dozen
    {
        get
        {
            return Math.Max(Convert.ToInt32(Math.Ceiling(lastSpinResultBackValue / 12.0)), 0);
        }
    }

    public bool High
    {
        get
        {
            return lastSpinResultBackValue >= 19;
        }
    }

    public bool Low
    {
        get
        {
            return lastSpinResultBackValue <= 18 && lastSpinResultBackValue >= 1;
        }
    }

    public bool Even
    {
        get
        {
            return lastSpinResultBackValue > 0 && lastSpinResultBackValue % 2 == 0;
        }
    }

    public bool Odd
    {
        get
        {
            return lastSpinResultBackValue > 0 && lastSpinResultBackValue % 2 == 1;
        }
    }

    public bool TopLine
    {
        get
        {
            return lastSpinResultBackValue <= 3;
        }
    }

    public int Street
    {
        get
        {
            return Math.Max(Convert.ToInt32(Math.Ceiling(lastSpinResultBackValue / 3.0)), 0);
        }
    }

    public void Spin()
    {
        lastSpinResultBackValue = RandomNumberGenerator.GetInt32(38) - 1;
    }
}
