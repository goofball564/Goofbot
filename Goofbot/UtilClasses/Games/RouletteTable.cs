namespace Goofbot.UtilClasses;

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
            if (this.lastSpinResultBackValue < 0)
            {
                return "00";
            }
            else
            {
                return this.lastSpinResultBackValue.ToString();
            }
        }
    }

    public RouletteColor Color
    {
        get
        {
            if ((this.lastSpinResultBackValue >= 1 && this.lastSpinResultBackValue <= 10) || (this.lastSpinResultBackValue >= 19 && this.lastSpinResultBackValue <= 28))
            {
                return this.lastSpinResultBackValue % 2 == 0 ? RouletteColor.Black : RouletteColor.Red;
            }
            else if ((this.lastSpinResultBackValue >= 11 && this.lastSpinResultBackValue <= 18) || (this.lastSpinResultBackValue >= 29 && this.lastSpinResultBackValue <= 36))
            {
                return this.lastSpinResultBackValue % 2 == 0 ? RouletteColor.Red : RouletteColor.Black;
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
            if (this.lastSpinResultBackValue <= 0)
            {
                return 0;
            }
            else
            {
                int column = this.lastSpinResultBackValue % 3;
                return column > 0 ? column : 3;
            }
        }
    }

    public int Dozen
    {
        get
        {
            return Math.Max(Convert.ToInt32(Math.Ceiling(this.lastSpinResultBackValue / 12.0)), 0);
        }
    }

    public bool High
    {
        get
        {
            return this.lastSpinResultBackValue >= 19;
        }
    }

    public bool Low
    {
        get
        {
            return this.lastSpinResultBackValue <= 18 && this.lastSpinResultBackValue >= 1;
        }
    }

    public bool Even
    {
        get
        {
            return this.lastSpinResultBackValue > 0 && this.lastSpinResultBackValue % 2 == 0;
        }
    }

    public bool Odd
    {
        get
        {
            return this.lastSpinResultBackValue > 0 && this.lastSpinResultBackValue % 2 == 1;
        }
    }

    public bool TopLine
    {
        get
        {
            return this.lastSpinResultBackValue <= 3;
        }
    }

    public int Street
    {
        get
        {
            return Math.Max(Convert.ToInt32(Math.Ceiling(this.lastSpinResultBackValue / 3.0)), 0);
        }
    }

    public void Spin()
    {
        this.lastSpinResultBackValue = RandomNumberGenerator.GetInt32(38) - 1;
    }
}
