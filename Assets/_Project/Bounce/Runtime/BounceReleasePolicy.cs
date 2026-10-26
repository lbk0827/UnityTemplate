using System;
using System.Linq;

namespace BK.Kit
{
    public static class BounceReleasePolicy
    {
        public static readonly string[] Families = { "mine", "tube", "cupboard", "panel", "switch", "armor", "combo" };
        public static readonly int[] Introductions = { 14, 21, 41, 61, 81, 101, 121 };
        public static int UnlockStage(string type)
        {
            int index=Array.IndexOf(Families,type=="door"?"switch":type);
            return index<0?int.MaxValue:Introductions[index];
        }
        public static bool IsAllowed(string type,int stage)=>stage>=UnlockStage(type);
        public static string IntroducedAt(int stage)=>Families.FirstOrDefault(t=>UnlockStage(t)==stage)??"";
    }
}
