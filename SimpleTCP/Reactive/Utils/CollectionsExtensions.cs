using System;
using System.Collections.Generic;
using System.Text;

namespace System.Collections.Generic
{
    public static class CollectionsExtensions
    {
        public static U CreateIfNotExist<T, U>(this Dictionary<T, U> dictionary, T element) where U : new()
        {
            if (dictionary.ContainsKey(element))
            {
                return dictionary[element];
            }
            else
            {
                var z = new U();
                dictionary.Add(element, z);
                return z;
            }

        }

    }
}
