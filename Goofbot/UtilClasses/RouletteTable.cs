namespace Goofbot.UtilClasses;

using System;
using System.Security.Cryptography;

internal class RouletteTable
{
    private int lastSpinResult = 0;

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
            if (this.lastSpinResult < 0)
            {
                return "00";
            }
            else
            {
                return this.lastSpinResult.ToString();
            }
        }
    }

    public RouletteColor Color
    {
        get
        {
            if ((this.lastSpinResult >= 1 && this.lastSpinResult <= 10) || (this.lastSpinResult >= 19 && this.lastSpinResult <= 28))
            {
                return this.lastSpinResult % 2 == 0 ? RouletteColor.Black : RouletteColor.Red;
            }
            else if ((this.lastSpinResult >= 11 && this.lastSpinResult <= 18) || (this.lastSpinResult >= 29 && this.lastSpinResult <= 36))
            {
                return this.lastSpinResult % 2 == 0 ? RouletteColor.Red : RouletteColor.Black;
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
            if (this.lastSpinResult <= 0)
            {
                return 0;
            }
            else
            {
                int column = this.lastSpinResult % 3;
                return column > 0 ? column : 3;
            }
        }
    }

    public int Dozen
    {
        get
        {
            return Math.Max(Convert.ToInt32(Math.Ceiling(this.lastSpinResult / 12.0)), 0);
        }
    }

    public bool High
    {
        get
        {
            return this.lastSpinResult >= 19;
        }
    }

    public bool Low
    {
        get
        {
            return this.lastSpinResult <= 18 && this.lastSpinResult >= 1;
        }
    }

    public bool Even
    {
        get
        {
            return this.lastSpinResult > 0 && this.lastSpinResult % 2 == 0;
        }
    }

    public bool Odd
    {
        get
        {
            return this.lastSpinResult > 0 && this.lastSpinResult % 2 == 1;
        }
    }

    public bool TopLine
    {
        get
        {
            return this.lastSpinResult <= 3;
        }
    }

    public int Street
    {
        get
        {
            return Math.Max(Convert.ToInt32(Math.Ceiling(this.lastSpinResult / 3.0)), 0);
        }
    }

    public void Spin()
    {
        this.lastSpinResult = RandomNumberGenerator.GetInt32(38) - 1;
    }
}
