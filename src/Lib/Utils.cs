using System;

public class Utils
{
    public static int[] GenerateRandomIntArray(int size)
    {
        Random rand = new();
        int[] array = new int[size];

        for (int i = 0; i < size; i++)
        {
            var randNum = rand.Next(Config.BlockedTileIndexMin, Config.BlockedTileIndexMax);
            array[i] = randNum;
        }

        return array;
    }
}