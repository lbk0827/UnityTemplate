using System;
using R3;
using BK.Save;

namespace BK.Options
{
    /// <inheritdoc cref="IOptionsService"/>
    public sealed class OptionsService : IOptionsService, IDisposable
    {
        private readonly OptionsData _data;
        private readonly CompositeDisposable _subscriptions = new();

        public ReactiveProperty<bool> Music { get; }
        public ReactiveProperty<bool> Sfx { get; }
        public ReactiveProperty<bool> Haptics { get; }
        public ReactiveProperty<string> Language { get; }

        public OptionsService(ISaveService saves, string defaultLanguage)
        {
            _data = saves.Get<OptionsData>();
            Music = new ReactiveProperty<bool>(_data.music);
            Sfx = new ReactiveProperty<bool>(_data.sfx);
            Haptics = new ReactiveProperty<bool>(_data.haptics);
            Language = new ReactiveProperty<string>(string.IsNullOrEmpty(_data.language) ? defaultLanguage : _data.language);

            Music.Skip(1).Subscribe(v => { _data.music = v; _data.MarkDirty(); }).AddTo(_subscriptions);
            Sfx.Skip(1).Subscribe(v => { _data.sfx = v; _data.MarkDirty(); }).AddTo(_subscriptions);
            Haptics.Skip(1).Subscribe(v => { _data.haptics = v; _data.MarkDirty(); }).AddTo(_subscriptions);
            Language.Skip(1).Subscribe(v => { _data.language = v ?? ""; _data.MarkDirty(); }).AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
            Music.Dispose();
            Sfx.Dispose();
            Haptics.Dispose();
            Language.Dispose();
        }
    }
}
