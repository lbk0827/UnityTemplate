using System;
using System.Collections.Generic;
using System.Text;

namespace BK.Kit
{
    public enum OfferKind { WelcomeDeal, EndlessOffer, EndlessGift }

    public sealed class OfferReward
    {
        public const string GoldId="Gold";
        public readonly string ItemId;
        public readonly int Count;
        public OfferReward(string itemId,int count) {ItemId=itemId;Count=count;}
        public bool IsGold=>ItemId==GoldId;
        public bool TryGetBooster(out BoosterKind kind)=>Enum.TryParse(ItemId,out kind) && BoosterCatalog.Find(kind)!=null;
        public static OfferReward Gold(int count)=>new OfferReward(GoldId,count);
        public static OfferReward Booster(BoosterKind kind,int count)=>new OfferReward(kind.ToString(),count);
    }

    // One card of a step-based offer. Paid steps only display a price in this offline kit; see IOfferSource.
    public sealed class OfferStep
    {
        public readonly int Step;
        public readonly bool Paid;
        public readonly string Price;
        public readonly IReadOnlyList<OfferReward> Rewards;
        public OfferStep(int step,bool paid,string price,params OfferReward[] rewards)
        {Step=step;Paid=paid;Price=price;Rewards=Array.AsReadOnly(rewards);}
    }

    public sealed class WelcomeDealOffer
    {
        public readonly string Title, Price;
        public readonly int BonusPercent;
        public readonly IReadOnlyList<OfferReward> Rewards;
        public WelcomeDealOffer(string title,string price,int bonusPercent,params OfferReward[] rewards)
        {Title=title;Price=price;BonusPercent=bonusPercent;Rewards=Array.AsReadOnly(rewards);}
    }

    // Local RND placeholder data. A server-backed IOfferSource can replace it later; this stays the offline fallback.
    public static class OfferCatalog
    {
        public static readonly IReadOnlyList<OfferStep> EndlessOffer=Array.AsReadOnly(new[]
        {
            new OfferStep(0,false,null,OfferReward.Gold(100)),
            new OfferStep(1,true,"$0.99",OfferReward.Gold(500),OfferReward.Booster(BoosterKind.Missile,1)),
            new OfferStep(2,false,null,OfferReward.Booster(BoosterKind.Bomb,1)),
            new OfferStep(3,true,"$2.99",OfferReward.Gold(2000),OfferReward.Booster(BoosterKind.Laser,2))
        });
        public static readonly IReadOnlyList<OfferStep> EndlessGift=Array.AsReadOnly(new[]
        {
            new OfferStep(0,false,null,OfferReward.Gold(50)),
            new OfferStep(1,false,null,OfferReward.Booster(BoosterKind.ExtraBall,1)),
            new OfferStep(2,true,"$0.99",OfferReward.Gold(300),OfferReward.Booster(BoosterKind.Missile,1)),
            new OfferStep(3,false,null,OfferReward.Gold(100)),
            new OfferStep(4,true,"$1.99",OfferReward.Gold(800),OfferReward.Booster(BoosterKind.Bomb,2)),
            new OfferStep(5,false,null,OfferReward.Booster(BoosterKind.Laser,1)),
            new OfferStep(6,true,"$4.99",OfferReward.Gold(3000),OfferReward.Booster(BoosterKind.ExtraBall,3))
        });
        public static readonly WelcomeDealOffer WelcomeDeal=new WelcomeDealOffer("Welcome Deal","$4.99",300,
            OfferReward.Gold(3000),OfferReward.Booster(BoosterKind.Missile,3),OfferReward.Booster(BoosterKind.ExtraBall,3),
            OfferReward.Booster(BoosterKind.Bomb,2),OfferReward.Booster(BoosterKind.Laser,2));

        public static IReadOnlyList<OfferStep> Steps(OfferKind kind)
        {
            switch(kind) {case OfferKind.EndlessOffer:return EndlessOffer;case OfferKind.EndlessGift:return EndlessGift;default:return Array.Empty<OfferStep>();}
        }
        public static string Describe(IReadOnlyList<OfferReward> rewards)
        {
            var text=new StringBuilder();
            foreach(var reward in rewards)
            {
                if(text.Length>0)text.Append(", ");
                if(reward.IsGold)text.Append(reward.Count).Append(" Gold");
                else text.Append(reward.TryGetBooster(out var kind)?BoosterCatalog.Find(kind).Title:reward.ItemId).Append(" x").Append(reward.Count);
            }
            return text.ToString();
        }
    }

    // Pure progress mutation so EditMode tests can cover it; KitApp adds state checks and persistence.
    public static class OfferClaim
    {
        public static bool CanClaim(PlayerProgress progress,OfferKind kind,int step,out OfferStep data,out string message)
        {
            data=null;message="";var steps=OfferCatalog.Steps(kind);
            if(steps.Count==0){message="Not a step offer.";return false;}
            if(progress.Step(kind)>=steps.Count){message="All rewards collected.";return false;}
            if(step!=progress.Step(kind)){message="Collect the current step first.";return false;}
            data=steps[step];
            if(data.Paid){message="Payments are not connected in this offline kit.";return false;}
            return true;
        }
        public static bool Apply(PlayerProgress progress,OfferKind kind,int step,out string message)
        {
            if(!CanClaim(progress,kind,step,out var data,out message))return false;
            foreach(var reward in data.Rewards)
            {
                if(reward.IsGold)progress.gold+=Math.Min(reward.Count,int.MaxValue-progress.gold);
                else if(reward.TryGetBooster(out var booster))progress.SetCount(booster,progress.Count(booster)+Math.Min(reward.Count,int.MaxValue-progress.Count(booster)));
            }
            progress.SetStep(kind,step+1);
            message="Collected "+OfferCatalog.Describe(data.Rewards);return true;
        }
    }
}
