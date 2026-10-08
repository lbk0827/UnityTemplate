using System;

namespace BK.Notifications
{
    /// <summary>One local notification to deliver at a local wall-clock time. Ids must be stable per purpose.</summary>
    public readonly struct NotificationRequest
    {
        public readonly int Id;
        public readonly string Title;
        public readonly string Body;
        public readonly DateTime FireAtLocal;

        public NotificationRequest(int id, string title, string body, DateTime fireAtLocal)
        {
            Id = id;
            Title = title;
            Body = body;
            FireAtLocal = fireAtLocal;
        }

        public override string ToString() => $"#{Id} {FireAtLocal:yyyy-MM-dd HH:mm} {Title}";
    }
}
