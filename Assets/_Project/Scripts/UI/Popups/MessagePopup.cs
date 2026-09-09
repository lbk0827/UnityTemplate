using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Project
{
    /// <summary>메시지/확인 팝업 인자. 두 번째 버튼 텍스트가 비어 있으면 1버튼 팝업.</summary>
    public readonly struct MessageArgs
    {
        public readonly string Title;
        public readonly string Body;
        public readonly string PrimaryLabel;
        public readonly string SecondaryLabel;

        public MessageArgs(string title, string body, string primaryLabel, string secondaryLabel = null)
        {
            Title = title;
            Body = body;
            PrimaryLabel = primaryLabel;
            SecondaryLabel = secondaryLabel;
        }

        public bool IsConfirm => !string.IsNullOrEmpty(SecondaryLabel);
    }

    /// <summary>
    /// 1버튼 안내 / 2버튼 확인 공용 팝업. System 레이어에 올라가 다른 팝업 위에 뜹니다.
    /// 결과는 <see cref="ResultAsync"/> 로 기다립니다. 닫기/배경/뒤로가기는 false.
    /// </summary>
    public sealed class MessagePopup : PopupViewBase, BK.UI.IUIView<MessageArgs>
    {
        public const string Address = "UI/Popups/Message";

        [SerializeField] private Text _title;
        [SerializeField] private Text _body;
        [SerializeField] private Button _primaryButton;
        [SerializeField] private Text _primaryLabel;
        [SerializeField] private Button _secondaryButton;
        [SerializeField] private Text _secondaryLabel;

        private MessageArgs _args;
        private readonly UniTaskCompletionSource<bool> _result = new();

        public UniTask<bool> ResultAsync => _result.Task;

        public void SetArgs(MessageArgs args) => _args = args;

        public override UniTask OnInitializeAsync(CancellationToken cancellationToken)
        {
            _title.text = _args.Title;
            _body.text = _args.Body;
            _primaryLabel.text = _args.PrimaryLabel;
            _secondaryButton.gameObject.SetActive(_args.IsConfirm);
            if (_args.IsConfirm)
                _secondaryLabel.text = _args.SecondaryLabel;

            _primaryButton.onClick.AddListener(() => Resolve(true));
            _secondaryButton.onClick.AddListener(() => Resolve(false));
            return base.OnInitializeAsync(cancellationToken);
        }

        private void Resolve(bool value)
        {
            _result.TrySetResult(value);
            RequestClose();
        }

        protected override UniTask PlayCloseAsync(CancellationToken cancellationToken)
        {
            // 버튼이 아닌 경로(X, 배경, 뒤로가기)로 닫히면 false 로 확정됩니다.
            _result.TrySetResult(false);
            return base.PlayCloseAsync(cancellationToken);
        }
    }
}
