using System.Collections.Generic;
using UnityEngine;

public static class BrailleDatabase
{
    public static bool[] GetLetterPattern(char c)
    {
        c = char.ToLower(c);

        return c switch
        {
            'a' => new bool[] { true, false, false, false, false, false },
            'b' => new bool[] { true, true, false, false, false, false },
            'c' => new bool[] { true, false, false, true, false, false },
            'd' => new bool[] { true, false, false, true, true, false },
            'e' => new bool[] { true, false, false, false, true, false },
            'f' => new bool[] { true, true, false, true, false, false },
            'g' => new bool[] { true, true, false, true, true, false },
            'h' => new bool[] { true, true, false, false, true, false },
            'i' => new bool[] { false, true, false, true, false, false },
            'j' => new bool[] { false, true, false, true, true, false },
            'k' => new bool[] { true, false, true, false, false, false },
            'l' => new bool[] { true, true, true, false, false, false },
            'm' => new bool[] { true, false, true, true, false, false },
            'n' => new bool[] { true, false, true, true, true, false },
            'o' => new bool[] { true, false, true, false, true, false },
            'p' => new bool[] { true, true, true, true, false, false },
            'q' => new bool[] { true, true, true, true, true, false },
            'r' => new bool[] { true, true, true, false, true, false },
            's' => new bool[] { false, true, true, true, false, false },
            't' => new bool[] { false, true, true, true, true, false },
            'u' => new bool[] { true, false, true, false, false, true },
            'v' => new bool[] { true, true, true, false, false, true },
            'w' => new bool[] { false, true, false, true, true, true },
            'x' => new bool[] { true, false, true, true, false, true },
            'y' => new bool[] { true, false, true, true, true, true },
            'z' => new bool[] { true, false, true, false, true, true },
            _ => new bool[6] // default: nessun puntino alzato
        };
    }

    public static bool[] NumberSign()
    {
        return new bool[] { false, false, true, true, true, true };
    }


    // Trasforma cifra 0-9 in lettera a-j
    public static char DigitToLetter(char d)
    {
        return d switch
        {
            '1' => 'a',
            '2' => 'b',
            '3' => 'c',
            '4' => 'd',
            '5' => 'e',
            '6' => 'f',
            '7' => 'g',
            '8' => 'h',
            '9' => 'i',
            '0' => 'j',
            _ => ' '
        };
    }

    // Encode semplice: string → lista di celle braille
    public static List<bool[]> Encode(string input)
    {
        List<bool[]> result = new List<bool[]>();
        bool numberMode = false;

        foreach (char c in input)
        {
            if (char.IsDigit(c))
            {
                if (!numberMode)
                {
                    // primo numero consecutivo: aggiungi NumberSign
                    result.Add(NumberSign());
                    numberMode = true;
                }
                result.Add(GetLetterPattern(DigitToLetter(c)));
            }
            else
            {
                numberMode = false;
                result.Add(GetLetterPattern(c));
            }
        }

        return result;
    }

    public static char GetRandomLetter()
    {
        char[] letters = "abcdefghijklmnopqrstuvwxyz".ToCharArray();
        return letters[Random.Range(0, letters.Length)];
    }

    public static char GetRandomDigit()
    {
        char[] digits = "0123456789".ToCharArray();
        return digits[Random.Range(0, digits.Length)];
    }
}
