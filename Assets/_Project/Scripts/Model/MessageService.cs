using System.Threading;
using Cysharp.Threading.Tasks;
using BK.Localization;
using BK.UI;

namespace Project
{
    /// <summary>안내/확인/토스트를 한 줄로 띄우는 진입점. 팝업 프리팹 주소는 여기서만 알면 됩니다.</summary>
    public sealed class MessageService
    {
        private readonly IUIService _ui;
        private readonly ILocalizationService _loc;
        private ToastView _toast;

        public MessageService(IUIService ui, ILocalizationService loc)
        {
            _ui = ui;
            _loc = loc;
        }

        public async UniTask ShowAsync(string title, string body, CancellationToken cancellationToken = default)
        {
            var popup = await _ui.OpenAsync<MessagePopup, MessageArgs>(
                MessagePopup.Address, new MessageArgs(title, body, _loc.Get("common.ok")), cancellationToken);
            await popup.ResultAsync;
        }

        public async UniTask<bool> ConfirmAsync(string title, string body, CancellationToken cancellationToken = default)
        {
            var popup = await _ui.OpenAsync<MessagePopup, MessageArgs>(
                MessagePopup.Address,
                new MessageArgs(title, body, _loc.Get("common.ok"), _loc.Get("common.cancel")),
                cancellationToken);
            return await popup.ResultAsync;
        }

        public void Toast(string message) => ToastAsync(message).Forget();

        private async UniTask ToastAsync(string message)
        {
            // 토스트 뷰는 첫 사용 때 열어 두고 이후 재사용합니다. 씬 전환으로 파괴됐으면 다시 엽니다.
            if (_toast == null || !_toast.IsOpen)
                _toast = await _ui.OpenAsync<ToastView>(ToastView.Address);
            _toast.Enqueue(message);
        }
    }
}
