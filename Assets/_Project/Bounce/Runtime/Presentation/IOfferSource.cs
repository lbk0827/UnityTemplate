using System.Collections.Generic;

namespace BK.Kit
{
    // Boundary for lobby offers. Replace LocalOfferSource with a server-backed implementation (Supabase, Firebase, ...)
    // without touching LobbyOffersView; the view only reads steps and calls TryClaim/TryPurchase.
    public interface IOfferSource
    {
        IReadOnlyList<OfferStep> Steps(OfferKind kind);
        int CurrentStep(OfferKind kind);
        WelcomeDealOffer WelcomeDeal { get; }
        bool TryClaim(OfferKind kind,int step,out string message);
        bool TryPurchase(OfferKind kind,int step,out string message);
    }

    // Offline kit: static catalog, progress in the local save, no payments.
    public sealed class LocalOfferSource : IOfferSource
    {
        private readonly KitApp app;
        public LocalOfferSource(KitApp app) => this.app=app;
        public IReadOnlyList<OfferStep> Steps(OfferKind kind) => OfferCatalog.Steps(kind);
        public int CurrentStep(OfferKind kind) => app.OfferStep(kind);
        public WelcomeDealOffer WelcomeDeal => OfferCatalog.WelcomeDeal;
        public bool TryClaim(OfferKind kind,int step,out string message) => app.TryClaimOfferStep(kind,step,out message);
        public bool TryPurchase(OfferKind kind,int step,out string message)
        {
            message="Payments are not connected in this offline kit.\nConnect a store backend to enable purchases.";return false;
        }
    }
}
