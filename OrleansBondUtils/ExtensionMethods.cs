using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrleansBondUtils
{
    public static class ExtensionMethods
    {
        public static T[] ToArray<T>(this ArraySegment<T> A)
        {
            var Result = new T[A.Count];
            Array.Copy(A.Array, A.Offset, Result, 0, A.Count);
            return Result;
        }
    }
}
