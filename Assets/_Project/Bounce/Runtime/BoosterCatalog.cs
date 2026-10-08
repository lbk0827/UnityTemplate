using System;
using System.Collections.Generic;

namespace BK.Kit
{
    public enum BoosterKind { Missile, ExtraBall, Bomb, Laser }
    public sealed class BoosterOffer
    {
        public readonly BoosterKind Kind;
        public readonly string Title, Description, ButtonKey;
        public readonly int Price;
        public BoosterOffer(BoosterKind kind,string title,string description,string buttonKey,int price)
        {Kind=kind;Title=title;Description=description;ButtonKey=buttonKey;Price=price;}
    }
    public static class BoosterCatalog
    {
        public const int ExtraBallAmount=5;
        public static readonly IReadOnlyList<BoosterOffer> All=Array.AsReadOnly(new[]
        {
            new BoosterOffer(BoosterKind.Missile,"Missile","Remove one strong block","MissileIngameButton",75),
            new BoosterOffer(BoosterKind.ExtraBall,"Extra Balls","Add 5 balls","ExtraBallIngameButton",50),
            new BoosterOffer(BoosterKind.Bomb,"Bomb","Clear a 3 x 3 area","BombIngameButton",100),
            new BoosterOffer(BoosterKind.Laser,"Laser","Clear one column","LaserIngameButton",125)
        });
        public static BoosterOffer Find(BoosterKind kind)
        {foreach(var offer in All)if(offer.Kind==kind)return offer;return null;}
    }
}
