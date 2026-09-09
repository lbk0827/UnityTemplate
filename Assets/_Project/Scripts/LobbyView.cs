using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.UI;
using UnityEngine;
using VContainer;
using BK.Data;
using BK.Localization;
using BK.UI;

namespace Project
{
    /// <summary>
    /// 프레임워크 각 계층이 실제로 붙는지 보여주는 검증용 뷰.
    /// 로컬라이제이션 문자열과 테이블 데이터를 조합해 화면에 찍습니다.
    /// </summary>
    public sealed class LobbyView : UIViewBase
    {
        [SerializeField] private Text _title;
        [SerializeField] private Text _body;

        private ILocalizationService _localization;
        private ITableService _tables;

        [Inject]
        public void Construct(ILocalizationService localization, ITableService tables)
        {
            _localization = localization;
            _tables = tables;
        }

        public override UniTask OnInitializeAsync(CancellationToken cancellationToken)
        {
            var table = _tables.Get<int, SampleRow>();
            var first = table.Get(1);

            _title.text = _localization.Get("lobby.title");
            _body.text = _localization.Format("lobby.body", _localization.Get(first.NameKey), first.Value);

            return base.OnInitializeAsync(cancellationToken);
        }
    }
}
