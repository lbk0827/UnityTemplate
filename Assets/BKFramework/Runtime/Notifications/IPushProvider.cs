using System;
using System.Collections.Generic;

namespace BK.Notifications
{
    /// <summary>
    /// Produces the notifications a feature wants delivered while the app is in the background.
    /// Called on every background transition, so providers compute from current state and never cache.
    /// </summary>
    public interface IPushProvider
    {
        IEnumerable<NotificationRequest> Build(DateTime nowLocal);
    }
}
