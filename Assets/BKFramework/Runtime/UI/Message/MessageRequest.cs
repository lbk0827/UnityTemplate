namespace BK.UI
{
    public sealed class MessageRequest
    {
        public string Title;
        public string Body;
        public string ConfirmText;

        /// <summary>Null means a single-button message.</summary>
        public string CancelText;

        /// <summary>No background tap / back to dismiss.</summary>
        public bool Forced;

        public bool IsConfirm => CancelText != null;
    }
}
