using System;

namespace BK.Assets
{
    /// <summary>
    /// Addressable address wrapped in a struct so asset identity is a distinct type
    /// rather than a bare string floating through call sites.
    /// </summary>
    [Serializable]
    public readonly struct AssetKey : IEquatable<AssetKey>
    {
        public readonly string Address;

        public AssetKey(string address) => Address = address;

        public bool IsValid => !string.IsNullOrEmpty(Address);

        public static implicit operator AssetKey(string address) => new AssetKey(address);

        public bool Equals(AssetKey other) => string.Equals(Address, other.Address, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is AssetKey other && Equals(other);
        public override int GetHashCode() => Address?.GetHashCode() ?? 0;
        public override string ToString() => Address ?? "<invalid>";
    }
}
