using System;
using UnityEngine;
using BK.Data;

namespace Project
{
    /// <summary>템플릿 검증용 샘플 행. 실제 게임에서는 삭제하고 자기 테이블로 교체합니다.</summary>
    [Serializable]
    public struct SampleRow : ITableRow<int>
    {
        [SerializeField] private int _id;
        [SerializeField] private string _nameKey;
        [SerializeField] private int _value;

        public int Id => _id;
        public string NameKey => _nameKey;
        public int Value => _value;

        public SampleRow(int id, string nameKey, int value)
        {
            _id = id;
            _nameKey = nameKey;
            _value = value;
        }
    }

    [CreateAssetMenu(fileName = "SampleTable", menuName = "Project/Sample Table")]
    public sealed class SampleTable : TableAsset<int, SampleRow> { }
}
