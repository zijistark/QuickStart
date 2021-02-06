using System.Collections.Generic;

using TaleWorlds.Core;

namespace QuickStart
{
    internal static class Util
    {
        public static T RandomPick<T>(this IEnumerable<T> e)
        {
#if STABLE
            return e.GetRandomElement();
#else
            return e.GetRandomElementInefficiently();
#endif
        }
    }
}
