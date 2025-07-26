using System;

public static class ArrayExtensions {
    public static T[] Flatten<T>(this T[][] array) {
        T[] flat = new T[array[0].Length + array[1].Length];

        int index = 0;
        for (int i = 0; i < 2; ++i)
            foreach (T element in array[i]) {
                flat[index] = element;
                index++;
            }
            
        return flat;
    }
}
