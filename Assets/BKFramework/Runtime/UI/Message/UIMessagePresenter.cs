using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using BK.Assets;

namespace BK.UI
{
    /// <summary>Presents messages through the UI service on the System layer.</summary>
    public sealed class UIMessagePresenter : IMessagePresenter
    {
        private readonly IUIService _ui;
        private readonly AssetKey _popupKey;
        private readonly AssetKey _toastKey;
        private readonly float _toastSeconds;

        public UIMessagePresenter(IUIService ui, AssetKey popupKey, AssetKey toastKey, float toastSeconds)
        {
            _ui = ui;
            _popupKey = popupKey;
            _toastKey = toastKey;
            _toastSeconds = toastSeconds;
        }

        public async UniTask<bool> ShowAsync(MessageRequest request, CancellationToken cancellationToken)
        {
            var view = await _ui.OpenAsync<MessagePopupView, MessageRequest>(_popupKey, request, cancellationToken);
            try { return await view.Result.AttachExternalCancellation(cancellationToken); }
            finally { await _ui.CloseAsync(view); }
        }

        public async UniTask ShowToastAsync(string text, CancellationToken cancellationToken)
        {
            var view = await _ui.OpenAsync<ToastView, string>(_toastKey, text, cancellationToken);
            try { await UniTask.Delay(TimeSpan.FromSeconds(_toastSeconds), ignoreTimeScale: true, cancellationToken: cancellationToken); }
            finally { await _ui.CloseAsync(view); }
        }
    }
}
