using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Project
{
    /// <summary>
    /// 짧은 안내 문구를 순서대로 보여줍니다. 큐가 비면 스스로 닫혀 뒤로가기 스택을 막지 않습니다.
    /// 다음 토스트는 MessageService 가 다시 엽니다.
    /// </summary>
    public sealed class ToastView : ProjectViewBase
    {
        public const string Address = "UI/Popups/Toast";

        [SerializeField] private CanvasGroup _bubble;
        [SerializeField] private Text _text;
        [SerializeField] private float _showSeconds = 1.2f;

        private readonly Queue<string> _queue = new();
        private bool _draining;

        public override UniTask OnInitializeAsync(CancellationToken cancellationToken)
        {
            _bubble.alpha = 0f;
            return base.OnInitializeAsync(cancellationToken);
        }

        public void Enqueue(string message)
        {
            _queue.Enqueue(message);
            if (!_draining)
                DrainAsync(destroyCancellationToken).Forget();
        }

        private async UniTask DrainAsync(CancellationToken cancellationToken)
        {
            _draining = true;
            try
            {
                while (_queue.Count > 0)
                {
                    _text.text = _queue.Dequeue();
                    await BK.Presentation.Motions.FadeAsync(_bubble, 1f, 0.12f, cancellationToken);
                    await UniTask.Delay(TimeSpan.FromSeconds(_showSeconds), DelayType.UnscaledDeltaTime, cancellationToken: cancellationToken);
                    await BK.Presentation.Motions.FadeAsync(_bubble, 0f, 0.15f, cancellationToken);
                }
            }
            finally
            {
                _draining = false;
            }

            if (_queue.Count == 0 && IsOpen)
                CloseAsync().Forget();
        }
    }
}
