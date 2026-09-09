using System;
using UnityEngine;
using BK.Data;

namespace Project
{
    public enum ProfileItemKind
    {
        Avatar,
        Frame,
    }

    /// <summary>아바타/프레임 한 종.</summary>
    [Serializable]
    public struct ProfileItemRow : ITableRow<int>
    {
        [SerializeField] private int _id;
        [SerializeField] private ProfileItemKind _kind;
        [SerializeField] private Color _color;
        [SerializeField] private Sprite _sprite;

        public int Id => _id;
        public ProfileItemKind Kind => _kind;
        public Color Color => _color;
        public Sprite Sprite => _sprite;

        public ProfileItemRow(int id, ProfileItemKind kind, Color color, Sprite sprite = null)
        {
            _id = id;
            _kind = kind;
            _color = color;
            _sprite = sprite;
        }
    }

    [CreateAssetMenu(fileName = "ProfileItemTable", menuName = "Project/Profile Item Table")]
    public sealed class ProfileItemTable : TableAsset<int, ProfileItemRow> { }
}
