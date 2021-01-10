using System;
using System.Collections.Generic;
using System.Text;

namespace NegativeEncoder.Utils
{
    public static class DeepCompare
    {
        public static bool EqualsDeep1<T>(T a, T b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;

            var props = typeof(T).GetProperties();
            foreach (var p in props)
            {
                if (!p.GetValue(a).Equals(p.GetValue(b)))
                {
                    return false;
                }
            }

            return true;
        }

        public static T CloneDeep1<T>(T o) where T : class, new()
        {
            if (o == null) return null;

            var props = typeof(T).GetProperties();

            var result = new T();

            foreach (var p in props)
            {
                var v = p.GetValue(o);
                p.SetValue(result, v);
            }

            return result;
        }
    }
}
