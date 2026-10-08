# 프레임워크 기반: 세이브 서비스 · 옵션 · Back 입력 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** sf RootBox의 PlayerDataManager / GameOptionManager / EscapeManager 패턴을 BKFramework 스타일로 재구현한다. 범용 `ISaveService`(슬롯별 JSON, 원자적 쓰기, 백업 복구, 파손 보존, 버전 마이그레이션, 더티 기반 주기 저장), 그 위의 `IOptionsService`(R3 반응형 옵션, 부팅 언어 공급), 그리고 `IUIService.HandleBackRequest`를 실제로 호출하는 `BackInputDriver`를 추가하고 Bounce의 자체 Escape 폴링을 그 경로로 통일한다.

**Architecture:** 새 레이어 두 개를 추가한다. `BK.Save`(Core에만 의존, JsonUtility 사용, Newtonsoft 없음)와 `BK.Options`(Save + R3). 레이어 규칙은 `Core ← Save ← Options`, `UI`는 `Scene`을 참조해도 된다(규칙상 하향). sf의 리플렉션 필드 스캔 대신 `Get<T>()` 지연 로드 + 타입명 파일명을 쓴다. 암호화는 이번 범위에서 제외한다(로드맵에 기록). Bounce의 `LocalSaveStore`/`PlayerProgress`는 PlayMode 테스트 40여 곳이 직접 참조하므로 이번엔 건드리지 않고, 로비 재구현(마일스톤 G)에서 교체한다.

**Tech Stack:** Unity 6000.3.21f1, VContainer 1.19, UniTask, R3 1.3.1, Input System(`activeInputHandler: 2` = Both), NUnit EditMode, 배치모드 검증.

**검증 명령 (공통):**

```bash
UNITY="/c/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Unity.exe"
SP="C:/Users/DG-250~1/AppData/Local/Temp/claude/d--UnityTemplate/70291d59-c7ab-4bd6-b23a-d4f43b5fe629/scratchpad"
# 컴파일만
"$UNITY" -batchmode -nographics -quit -projectPath "D:/UnityTemplate" -logFile "$SP/c.log"; echo exit=$?; grep -E "error CS" "$SP/c.log" | sort -u
# EditMode 테스트 (필터는 클래스명)
"$UNITY" -batchmode -nographics -projectPath "D:/UnityTemplate" -runTests -testPlatform EditMode -testFilter <Class> -testResults "$SP/t.xml" -logFile "$SP/t.log"; echo exit=$?
python -I -c "import xml.etree.ElementTree as E;r=E.parse(r'$SP/t.xml').getroot();print(r.attrib['total'],r.attrib['passed'],r.attrib['failed']);[print(t.attrib['result'],t.attrib['name'],(t.find('failure/message').text if t.find('failure/message') is not None else '')[:200]) for t in r.iter('test-case') if t.attrib['result']!='Passed']"
```

컴파일 에러 시 `-runTests`는 결과 XML을 만들지 않고 exit 1로 끝난다. 그때는 로그의 `error CS`를 본다.

**.meta 규칙:** 새 `.cs`는 MonoImporter meta, `.asmdef`는 AssemblyDefinitionImporter meta, 폴더는 folderAsset meta를 함께 만든다(GUID는 `uuid.uuid4().hex`). 아래 헬퍼를 각 Task에서 재사용한다:

```bash
# 사용: addmeta <path>  (폴더/cs/asmdef 자동 판별)
addmeta() { python -I - "$1" <<'EOF'
import sys,os,uuid
p=sys.argv[1]; g=uuid.uuid4().hex
if os.path.isdir(p): body='folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
elif p.endswith('.asmdef'): body='AssemblyDefinitionImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
else: body='MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
m=p+'.meta'
if not os.path.exists(m): open(m,'w',encoding='utf-8',newline='\n').write('fileFormatVersion: 2\nguid: %s\n%s'%(g,body))
EOF
}
```

---

### Task 1: 프레임워크 EditMode 테스트 어셈블리

**Files:**
- Create: `Assets/BKFramework/Tests/EditMode/BK.Framework.EditModeTests.asmdef` (+ meta, 폴더 meta)

- [ ] **Step 1: asmdef 작성**

```json
{
  "name": "BK.Framework.EditModeTests",
  "rootNamespace": "BK.Tests",
  "references": [
    "BK.Core",
    "BK.Save",
    "BK.UI",
    "BK.Scene",
    "BK.Assets",
    "R3.Unity",
    "VContainer",
    "UniTask",
    "UnityEngine.TestRunner",
    "UnityEditor.TestRunner"
  ],
  "includePlatforms": ["Editor"],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": true,
  "precompiledReferences": ["nunit.framework.dll", "R3.dll"],
  "autoReferenced": false,
  "defineConstraints": ["UNITY_INCLUDE_TESTS"],
  "versionDefines": [],
  "noEngineReferences": false
}
```

`BK.Save`는 Task 2에서 만들어지므로 그 전까지 이 asmdef는 "missing reference" 경고가 난다. Task 2 완료 전에는 컴파일을 돌리지 않는다. `BK.Options`는 Task 4에서 references에 추가한다. (R3 코어는 asmdef가 아니라 패키지 안의 프리컴파일 `R3.dll`이다. `overrideReferences: true`인 이 테스트 어셈블리는 `precompiledReferences`에 `R3.dll`을 명시해야 하고, `overrideReferences: false`인 런타임 asmdef는 자동으로 참조한다.)

- [ ] **Step 2: 폴더/asmdef meta 생성** (`addmeta` 사용). 커밋은 Task 2와 함께.

---

### Task 2: BK.Save — 파일 계층 (SaveFile)

**Files:**
- Create: `Assets/BKFramework/Runtime/Save/BK.Save.asmdef`
- Create: `Assets/BKFramework/Runtime/Save/SaveData.cs`
- Create: `Assets/BKFramework/Runtime/Save/SaveFile.cs`
- Test: `Assets/BKFramework/Tests/EditMode/SaveFileTests.cs`

- [ ] **Step 1: asmdef**

```json
{
  "name": "BK.Save",
  "rootNamespace": "BK.Save",
  "references": ["BK.Core", "VContainer", "UniTask"],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

- [ ] **Step 2: SaveData.cs**

```csharp
using System;
using UnityEngine;

namespace BK.Save
{
    /// <summary>
    /// One persisted slot. Subclasses are plain [Serializable] field bags that
    /// JsonUtility can round-trip. The service owns loading and writing; a slot
    /// only says what version it is, how to upgrade older files, and whether a
    /// loaded instance is sane.
    /// </summary>
    [Serializable]
    public abstract class SaveData
    {
        /// <summary>Version stamped into the file. Managed by the service.</summary>
        public int version;

        [NonSerialized] private bool _dirty;

        /// <summary>Version the code expects. Bump when fields change meaning.</summary>
        public abstract int CurrentVersion { get; }

        public bool IsDirty => _dirty;
        public void MarkDirty() => _dirty = true;
        internal void ClearDirty() => _dirty = false;

        /// <summary>Called once when no usable file exists. Defaults are already applied.</summary>
        public virtual void OnCreate() { }

        /// <summary>
        /// Called after an older file was populated into this instance. Fields the old
        /// file did not have keep their defaults; fix up semantics here.
        /// </summary>
        public virtual void OnMigrate(int fromVersion) { }

        /// <summary>Return false to treat a loaded file as corrupt (it is preserved, not deleted).</summary>
        public virtual bool Validate() => true;
    }
}
```

- [ ] **Step 3: 실패 테스트 — SaveFileTests.cs**

```csharp
using System.IO;
using BK.Save;
using NUnit.Framework;

namespace BK.Tests
{
    public sealed class SaveFileTests
    {
        private string _dir;
        private string Path(string n) => System.IO.Path.Combine(_dir, n);

        [SetUp] public void SetUp() { _dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "BKSaveTests", System.Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        [Test]
        public void WriteAtomicRotatesPreviousFileIntoBackup()
        {
            SaveFile.WriteAtomic(Path("a.json"), "{\"version\":1}");
            SaveFile.WriteAtomic(Path("a.json"), "{\"version\":2}");
            Assert.That(File.ReadAllText(Path("a.json")), Is.EqualTo("{\"version\":2}"));
            Assert.That(File.ReadAllText(Path("a.json.bak")), Is.EqualTo("{\"version\":1}"));
            Assert.That(File.Exists(Path("a.json.tmp")), Is.False);
        }

        [Test]
        public void ReadUsablePrefersPrimaryThenBackup()
        {
            SaveFile.WriteAtomic(Path("a.json"), "{\"version\":1}");
            SaveFile.WriteAtomic(Path("a.json"), "{\"version\":2}");
            Assert.That(SaveFile.ReadUsable(Path("a.json"), out var corrupt), Is.EqualTo("{\"version\":2}"));
            Assert.That(corrupt, Is.False);

            File.WriteAllText(Path("a.json"), "{\"version\":");
            Assert.That(SaveFile.ReadUsable(Path("a.json"), out corrupt), Is.EqualTo("{\"version\":1}"));
            Assert.That(corrupt, Is.True);
        }

        [Test]
        public void ReadUsableReturnsNullWhenNothingParses()
        {
            File.WriteAllText(Path("a.json"), "nope");
            File.WriteAllText(Path("a.json.bak"), "{\"noVersion\":true}");
            Assert.That(SaveFile.ReadUsable(Path("a.json"), out var corrupt), Is.Null);
            Assert.That(corrupt, Is.True);
        }

        [Test]
        public void PreserveCorruptMovesFileAsideInsteadOfDeleting()
        {
            File.WriteAllText(Path("a.json"), "nope");
            var preserved = SaveFile.PreserveCorrupt(Path("a.json"));
            Assert.That(File.Exists(Path("a.json")), Is.False);
            Assert.That(preserved, Does.StartWith(Path("a.json.corrupt-")));
            Assert.That(File.ReadAllText(preserved), Is.EqualTo("nope"));
        }

        [Test]
        public void TryReadVersionRejectsInvalidJsonAndMissingVersion()
        {
            Assert.That(SaveFile.TryReadVersion("{\"version\":3}", out var v), Is.True);
            Assert.That(v, Is.EqualTo(3));
            Assert.That(SaveFile.TryReadVersion("{\"x\":3}", out _), Is.False);
            Assert.That(SaveFile.TryReadVersion("{", out _), Is.False);
            Assert.That(SaveFile.TryReadVersion("", out _), Is.False);
        }
    }
}
```

- [ ] **Step 4: SaveFile.cs 구현**

```csharp
using System;
using System.IO;
using UnityEngine;
using BK.Core.Diagnostics;

namespace BK.Save
{
    /// <summary>
    /// Crash-safe file primitives for save slots. A write goes to a temp file and is
    /// renamed over the target while the previous target becomes the backup, so at any
    /// instant either a valid primary or a valid backup exists. A primary that reads
    /// but does not parse is "corrupt": it is moved aside, never deleted, because it
    /// is the only evidence for diagnosing what went wrong.
    /// </summary>
    public static class SaveFile
    {
        public const string BackupExtension = ".bak";
        public const string TempExtension = ".tmp";
        public const string CorruptExtension = ".corrupt-";
        public const string Category = "Save";

        [Serializable] private sealed class VersionProbe { public int version = -1; }

        /// <summary>True when <paramref name="json"/> is valid JSON that carries a non-negative version.</summary>
        public static bool TryReadVersion(string json, out int version)
        {
            version = -1;
            if (string.IsNullOrWhiteSpace(json))
                return false;
            try
            {
                var probe = JsonUtility.FromJson<VersionProbe>(json);
                if (probe == null || probe.version < 0)
                    return false;
                version = probe.version;
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>
        /// Returns the first parseable JSON among primary and backup, or null.
        /// <paramref name="primaryCorrupt"/> is set when the primary existed but did not parse.
        /// </summary>
        public static string ReadUsable(string path, out bool primaryCorrupt)
        {
            primaryCorrupt = false;
            if (File.Exists(path))
            {
                var json = TryRead(path);
                if (json != null && TryReadVersion(json, out _))
                    return json;
                primaryCorrupt = true;
            }

            var backup = path + BackupExtension;
            if (File.Exists(backup))
            {
                var json = TryRead(backup);
                if (json != null && TryReadVersion(json, out _))
                {
                    BKLog.Warn(Category, $"primary unusable, recovered from backup: {Path.GetFileName(path)}");
                    return json;
                }
            }

            return null;
        }

        /// <summary>Writes temp → rename, rotating the old primary into the backup.</summary>
        public static void WriteAtomic(string path, string json)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var temp = path + TempExtension;
            File.WriteAllText(temp, json);
            if (File.Exists(path))
                File.Replace(temp, path, path + BackupExtension);
            else
                File.Move(temp, path);
        }

        /// <summary>Moves a corrupt file to <c>name.corrupt-yyyyMMddHHmmss</c>. Returns the new path or null.</summary>
        public static string PreserveCorrupt(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return null;
                var target = path + CorruptExtension + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                File.Move(path, target);
                return target;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                BKLog.Warn(Category, $"could not preserve corrupt file {path}: {exception.Message}");
                return null;
            }
        }

        private static string TryRead(string path)
        {
            try { return File.ReadAllText(path); }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                BKLog.Warn(Category, $"read failed {path}: {exception.Message}");
                return null;
            }
        }
    }
}
```

`BKLog.Warn(string category, string message)` 시그니처는 `Assets/BKFramework/Runtime/Core/Diagnostics/BKLog.cs`에서 확인한다. 카테고리가 상수 문자열이면 `"Save"`를 그대로 쓴다.

- [ ] **Step 5: meta 생성 → 컴파일 → 테스트 (SaveFileTests 5/5)**

`addmeta`를 Save 폴더, asmdef, 각 .cs, 테스트 .cs에 적용.

- [ ] **Step 6: 커밋**

```bash
git add Assets/BKFramework/Tests Assets/BKFramework/Runtime/Save Assets/BKFramework/Runtime/Save.meta
git commit -m "feat(save): add crash-safe save file primitives with framework EditMode tests"
```

---

### Task 3: BK.Save — SaveService

**Files:**
- Create: `Assets/BKFramework/Runtime/Save/ISaveService.cs`
- Create: `Assets/BKFramework/Runtime/Save/SaveService.cs`
- Create: `Assets/BKFramework/Runtime/Save/SaveFlushDriver.cs`
- Test: `Assets/BKFramework/Tests/EditMode/SaveServiceTests.cs`

- [ ] **Step 1: ISaveService.cs**

```csharp
namespace BK.Save
{
    /// <summary>
    /// Typed save slots. One file per <see cref="SaveData"/> subclass, loaded on first
    /// access and written back when dirty: periodically, on pause, on quit, on dispose,
    /// or when <see cref="Flush"/> is called.
    /// </summary>
    public interface ISaveService
    {
        /// <summary>Loads (once) and returns the slot. Never null; a missing or corrupt file yields defaults.</summary>
        T Get<T>() where T : SaveData, new();

        /// <summary>Writes every dirty slot. Returns false if any write failed; those slots stay dirty.</summary>
        bool Flush();

        bool HasUnsavedChanges { get; }
    }
}
```

- [ ] **Step 2: 실패 테스트 — SaveServiceTests.cs**

```csharp
using System.IO;
using BK.Save;
using NUnit.Framework;
using UnityEngine;

namespace BK.Tests
{
    public sealed class SaveServiceTests
    {
        private string _dir;
        [SetUp] public void SetUp() => _dir = Path.Combine(Path.GetTempPath(), "BKSaveTests", System.Guid.NewGuid().ToString("N"));
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        [System.Serializable]
        private sealed class Progress : SaveData
        {
            public override int CurrentVersion => 2;
            public int level = 1;
            public int gold;
            public bool music = true, sfx = true;
            public bool created;
            public int migratedFrom = -1;
            public override void OnCreate() => created = true;
            public override void OnMigrate(int fromVersion)
            {
                migratedFrom = fromVersion;
                if (fromVersion < 2) sfx = music; // v1 had a single flag
            }
            public override bool Validate() => level >= 1 && gold >= 0;
        }

        private string File_ => Path.Combine(_dir, "Progress.json");

        [Test]
        public void GetCreatesDefaultsAndFlushWritesVersionedJson()
        {
            var saves = new SaveService(_dir);
            var p = saves.Get<Progress>();
            Assert.That(p.created, Is.True);
            Assert.That(p.version, Is.EqualTo(2));
            Assert.That(saves.HasUnsavedChanges, Is.True, "a freshly created slot is dirty so the file appears on first flush");
            Assert.That(saves.Flush(), Is.True);
            Assert.That(SaveFile.TryReadVersion(File.ReadAllText(File_), out var v) && v == 2, Is.True);
            Assert.That(saves.HasUnsavedChanges, Is.False);
            Assert.That(ReferenceEquals(saves.Get<Progress>(), p), Is.True, "same instance on repeated Get");
        }

        [Test]
        public void DirtySlotRoundTripsAndKeepsBackup()
        {
            var saves = new SaveService(_dir);
            var p = saves.Get<Progress>();
            saves.Flush();
            p.level = 5; p.gold = 120; p.MarkDirty();
            Assert.That(saves.Flush(), Is.True);

            var again = new SaveService(_dir).Get<Progress>();
            Assert.That(again.level, Is.EqualTo(5));
            Assert.That(again.gold, Is.EqualTo(120));
            Assert.That(again.created, Is.False);
            Assert.That(File.Exists(File_ + SaveFile.BackupExtension), Is.True);
        }

        [Test]
        public void UnchangedSlotIsNotRewritten()
        {
            var saves = new SaveService(_dir);
            saves.Get<Progress>(); saves.Flush();
            var stamp = File.GetLastWriteTimeUtc(File_);
            File.SetLastWriteTimeUtc(File_, stamp.AddMinutes(-5));
            Assert.That(saves.Flush(), Is.True);
            Assert.That(File.GetLastWriteTimeUtc(File_), Is.EqualTo(stamp.AddMinutes(-5)));
        }

        [Test]
        public void OlderFileIsMigratedAndMarkedDirty()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(File_, "{\"version\":1,\"level\":9,\"music\":false}");
            var saves = new SaveService(_dir);
            var p = saves.Get<Progress>();
            Assert.That(p.level, Is.EqualTo(9));
            Assert.That(p.migratedFrom, Is.EqualTo(1));
            Assert.That(p.sfx, Is.False);
            Assert.That(p.version, Is.EqualTo(2));
            Assert.That(p.IsDirty, Is.True);
        }

        [Test]
        public void CorruptPrimaryFallsBackToBackupAndPreservesEvidence()
        {
            var saves = new SaveService(_dir);
            var p = saves.Get<Progress>(); saves.Flush();
            p.level = 7; p.MarkDirty(); saves.Flush();
            File.WriteAllText(File_, "{\"version\":2,\"level\":");
            var again = new SaveService(_dir).Get<Progress>();
            Assert.That(again.level, Is.EqualTo(1), "backup holds the save before level=7");
            Assert.That(Directory.GetFiles(_dir, "Progress.json.corrupt-*"), Has.Length.EqualTo(1));
        }

        [Test]
        public void FailedValidationIsTreatedAsCorrupt()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(File_, "{\"version\":2,\"level\":3,\"gold\":-1}");
            var p = new SaveService(_dir).Get<Progress>();
            Assert.That(p.gold, Is.Zero);
            Assert.That(p.level, Is.EqualTo(1));
            Assert.That(p.created, Is.True);
            Assert.That(Directory.GetFiles(_dir, "Progress.json.corrupt-*"), Has.Length.EqualTo(1));
        }

        [Test]
        public void DisposeFlushesDirtySlots()
        {
            var saves = new SaveService(_dir);
            var p = saves.Get<Progress>();
            p.gold = 42; p.MarkDirty();
            saves.Dispose();
            Assert.That(new SaveService(_dir).Get<Progress>().gold, Is.EqualTo(42));
        }
    }
}
```

- [ ] **Step 3: SaveService.cs 구현**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using BK.Core.Diagnostics;

namespace BK.Save
{
    /// <inheritdoc cref="ISaveService"/>
    public sealed class SaveService : ISaveService, IDisposable
    {
        private readonly string _directory;
        private readonly Dictionary<Type, SaveData> _slots = new();
        private readonly object _gate = new();
        private SaveFlushDriver _driver;
        private bool _disposed;

        /// <param name="directory">Folder for slot files. Tests pass a temp folder.</param>
        /// <param name="flushIntervalSeconds">Periodic flush cadence while playing. 0 disables the driver.</param>
        public SaveService(string directory, float flushIntervalSeconds = 5f)
        {
            _directory = directory;
            FlushIntervalSeconds = flushIntervalSeconds;
        }

        /// <summary>Default production folder.</summary>
        public static string DefaultDirectory => Path.Combine(Application.persistentDataPath, "Save");

        public float FlushIntervalSeconds { get; }

        public bool HasUnsavedChanges
        {
            get
            {
                lock (_gate)
                {
                    foreach (var slot in _slots.Values)
                        if (slot.IsDirty) return true;
                    return false;
                }
            }
        }

        public T Get<T>() where T : SaveData, new()
        {
            lock (_gate)
            {
                if (_slots.TryGetValue(typeof(T), out var existing))
                    return (T)existing;

                var loaded = Load<T>();
                _slots.Add(typeof(T), loaded);
                EnsureDriver();
                return loaded;
            }
        }

        public bool Flush()
        {
            var allOk = true;
            lock (_gate)
            {
                foreach (var pair in _slots)
                {
                    var slot = pair.Value;
                    if (!slot.IsDirty) continue;
                    try
                    {
                        SaveFile.WriteAtomic(PathFor(pair.Key), JsonUtility.ToJson(slot, Application.isEditor));
                        slot.ClearDirty();
                    }
                    catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                    {
                        // Stays dirty: the next flush retries. Loss is delayed, not silent.
                        BKLog.Error(SaveFile.Category, $"save failed {pair.Key.Name}: {exception.Message}");
                        allOk = false;
                    }
                }
            }
            return allOk;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Flush();
            if (_driver != null)
                UnityEngine.Object.Destroy(_driver.gameObject);
        }

        private string PathFor(Type type) => Path.Combine(_directory, type.Name + ".json");

        private T Load<T>() where T : SaveData, new()
        {
            var path = PathFor(typeof(T));
            var json = SaveFile.ReadUsable(path, out var primaryCorrupt);
            if (primaryCorrupt)
                BKLog.Error(SaveFile.Category, $"corrupt save {typeof(T).Name}, preserved at {SaveFile.PreserveCorrupt(path) ?? "(failed)"}");

            var data = new T();
            if (json != null && TryApply(data, json, out var migrated))
            {
                if (migrated) data.MarkDirty();
                return data;
            }

            if (json != null)
            {
                // Parsed but failed validation or population: same policy as corrupt.
                BKLog.Error(SaveFile.Category, $"invalid save {typeof(T).Name}, preserved at {SaveFile.PreserveCorrupt(path) ?? "(failed)"}");
                data = new T();
            }

            data.version = data.CurrentVersion;
            data.OnCreate();
            data.MarkDirty();
            return data;
        }

        private static bool TryApply<T>(T data, string json, out bool migrated) where T : SaveData
        {
            migrated = false;
            if (!SaveFile.TryReadVersion(json, out var fileVersion))
                return false;
            try { JsonUtility.FromJsonOverwrite(json, data); }
            catch (ArgumentException) { return false; }

            if (fileVersion < data.CurrentVersion)
            {
                data.OnMigrate(fileVersion);
                data.version = data.CurrentVersion;
                migrated = true;
            }
            return data.Validate();
        }

        private void EnsureDriver()
        {
            if (_driver != null || FlushIntervalSeconds <= 0f || !Application.isPlaying)
                return;
            _driver = SaveFlushDriver.Create(this);
        }
    }
}
```

주의: `BKLog.Error` 시그니처 확인(Task 2 Step 4와 같은 방식). `JsonUtility.ToJson(obj, prettyPrint)`의 두 번째 인자는 에디터에서만 들여쓰기.

- [ ] **Step 4: SaveFlushDriver.cs**

```csharp
using UnityEngine;

namespace BK.Save
{
    /// <summary>
    /// Play-mode only helper that flushes dirty slots on an interval, on pause and on
    /// quit. Created by <see cref="SaveService"/>; never add it to a scene by hand.
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class SaveFlushDriver : MonoBehaviour
    {
        private SaveService _service;
        private float _nextFlush;

        public static SaveFlushDriver Create(SaveService service)
        {
            var go = new GameObject("[BK.Save]") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            var driver = go.AddComponent<SaveFlushDriver>();
            driver._service = service;
            driver._nextFlush = Time.unscaledTime + service.FlushIntervalSeconds;
            return driver;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextFlush) return;
            _nextFlush = Time.unscaledTime + _service.FlushIntervalSeconds;
            if (_service.HasUnsavedChanges) _service.Flush();
        }

        private void OnApplicationPause(bool paused) { if (paused) _service.Flush(); }
        private void OnApplicationQuit() => _service.Flush();
    }
}
```

- [ ] **Step 5: meta → 컴파일 → 테스트 (SaveServiceTests 7/7 + SaveFileTests 5/5)**

- [ ] **Step 6: 커밋**

```bash
git add Assets/BKFramework/Runtime/Save Assets/BKFramework/Tests
git commit -m "feat(save): add SaveService with lazy typed slots, migration and periodic flush"
```

---

### Task 4: BK.Options — 반응형 옵션

**Files:**
- Create: `Assets/BKFramework/Runtime/Options/BK.Options.asmdef`
- Create: `Assets/BKFramework/Runtime/Options/OptionsData.cs`
- Create: `Assets/BKFramework/Runtime/Options/IOptionsService.cs`
- Create: `Assets/BKFramework/Runtime/Options/OptionsService.cs`
- Modify: `Assets/BKFramework/Tests/EditMode/BK.Framework.EditModeTests.asmdef` (references에 `BK.Options` 추가)
- Test: `Assets/BKFramework/Tests/EditMode/OptionsServiceTests.cs`

- [ ] **Step 1: asmdef**

```json
{
  "name": "BK.Options",
  "rootNamespace": "BK.Options",
  "references": ["BK.Core", "BK.Save", "R3.Unity", "VContainer", "UniTask"],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

- [ ] **Step 2: 실패 테스트 — OptionsServiceTests.cs**

```csharp
using System.IO;
using BK.Options;
using BK.Save;
using NUnit.Framework;

namespace BK.Tests
{
    public sealed class OptionsServiceTests
    {
        private string _dir;
        [SetUp] public void SetUp() => _dir = Path.Combine(Path.GetTempPath(), "BKSaveTests", System.Guid.NewGuid().ToString("N"));
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        [Test]
        public void DefaultsAreOnAndLanguageFallsBackToDefault()
        {
            using var options = new OptionsService(new SaveService(_dir), "ko");
            Assert.That(options.Music.Value, Is.True);
            Assert.That(options.Sfx.Value, Is.True);
            Assert.That(options.Haptics.Value, Is.True);
            Assert.That(options.Language.Value, Is.EqualTo("ko"));
        }

        [Test]
        public void ChangesPersistThroughTheSaveService()
        {
            var saves = new SaveService(_dir);
            using (var options = new OptionsService(saves, "en"))
            {
                options.Music.Value = false;
                options.Language.Value = "ja";
                Assert.That(saves.HasUnsavedChanges, Is.True);
                saves.Flush();
            }
            using var reloaded = new OptionsService(new SaveService(_dir), "en");
            Assert.That(reloaded.Music.Value, Is.False);
            Assert.That(reloaded.Sfx.Value, Is.True);
            Assert.That(reloaded.Language.Value, Is.EqualTo("ja"));
        }
    }
}
```

- [ ] **Step 3: 구현**

`OptionsData.cs`:

```csharp
using System;
using BK.Save;

namespace BK.Options
{
    [Serializable]
    public sealed class OptionsData : SaveData
    {
        public override int CurrentVersion => 1;
        public bool music = true;
        public bool sfx = true;
        public bool haptics = true;
        /// <summary>Empty means "use the project default".</summary>
        public string language = "";
    }
}
```

`IOptionsService.cs`:

```csharp
using R3;

namespace BK.Options
{
    /// <summary>
    /// Player-facing settings as reactive properties. Writing a value persists it
    /// through <see cref="BK.Save.ISaveService"/>; audio/haptic systems subscribe to apply it.
    /// </summary>
    public interface IOptionsService
    {
        ReactiveProperty<bool> Music { get; }
        ReactiveProperty<bool> Sfx { get; }
        ReactiveProperty<bool> Haptics { get; }
        /// <summary>Language code. Never empty: the project default is substituted.</summary>
        ReactiveProperty<string> Language { get; }
    }
}
```

`OptionsService.cs`:

```csharp
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
            Music.Dispose(); Sfx.Dispose(); Haptics.Dispose(); Language.Dispose();
        }
    }
}
```

R3의 `Skip(1)`은 `Observable` 확장이므로 `ReactiveProperty`에서 바로 호출 가능. `AddTo(CompositeDisposable)`은 R3 기본 제공.

- [ ] **Step 4: 테스트 asmdef에 `BK.Options` 추가, meta → 컴파일 → 테스트 (OptionsServiceTests 2/2)**

- [ ] **Step 5: 커밋**

```bash
git add Assets/BKFramework/Runtime/Options Assets/BKFramework/Runtime/Options.meta Assets/BKFramework/Tests
git commit -m "feat(options): add reactive OptionsService persisted through BK.Save"
```

---

### Task 5: BackInputDriver (BK.UI)

**Files:**
- Modify: `Assets/BKFramework/Runtime/UI/BK.UI.asmdef` (references에 `BK.Scene` 추가)
- Create: `Assets/BKFramework/Runtime/UI/BackInputDriver.cs`
- Test: `Assets/BKFramework/Tests/EditMode/BackInputDriverTests.cs`

- [ ] **Step 1: 실패 테스트**

```csharp
using System.Threading;
using BK.Assets;
using BK.Scene;
using BK.UI;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace BK.Tests
{
    public sealed class BackInputDriverTests
    {
        private sealed class FakeUI : IUIService
        {
            public int BackRequests;
            public bool HandleBackRequest() { BackRequests++; return true; }
            public UniTask<TView> OpenAsync<TView>(AssetKey key, CancellationToken ct = default) where TView : class, IUIView => throw new System.NotSupportedException();
            public UniTask<TView> OpenAsync<TView, TArgs>(AssetKey key, TArgs args, CancellationToken ct = default) where TView : class, IUIView<TArgs> => throw new System.NotSupportedException();
            public UniTask CloseAsync(IUIView view, CancellationToken ct = default) => UniTask.CompletedTask;
            public UniTask CloseTopAsync(UILayer layer, CancellationToken ct = default) => UniTask.CompletedTask;
            public UniTask CloseAllAsync(UILayer layer, CancellationToken ct = default) => UniTask.CompletedTask;
            public IUIView Peek(UILayer layer) => null;
        }

        private sealed class FakeScenes : ISceneService
        {
            public bool IsTransitioning { get; set; }
            public ISceneScope ActiveScene => null;
            public UniTask<ISceneScope> TransitionToAsync(AssetKey key, System.IProgress<float> progress = null, CancellationToken ct = default) => throw new System.NotSupportedException();
            public UniTask<ISceneScope> LoadAdditiveAsync(AssetKey key, System.IProgress<float> progress = null, CancellationToken ct = default) => throw new System.NotSupportedException();
            public UniTask UnloadAsync(ISceneScope scope, CancellationToken ct = default) => UniTask.CompletedTask;
        }

        [Test]
        public void PressRoutesToUIServiceOncePerPress()
        {
            var ui = new FakeUI(); var scenes = new FakeScenes();
            var pressed = false;
            var driver = new BackInputDriver(ui, scenes, () => pressed);
            driver.Tick();
            Assert.That(ui.BackRequests, Is.Zero);
            pressed = true; driver.Tick();
            Assert.That(ui.BackRequests, Is.EqualTo(1));
        }

        [Test]
        public void PressIsIgnoredWhileSceneTransitionsOrWhenSuspended()
        {
            var ui = new FakeUI(); var scenes = new FakeScenes { IsTransitioning = true };
            var driver = new BackInputDriver(ui, scenes, () => true);
            driver.Tick();
            Assert.That(ui.BackRequests, Is.Zero);
            scenes.IsTransitioning = false;
            driver.Suspended = true; driver.Tick();
            Assert.That(ui.BackRequests, Is.Zero);
            driver.Suspended = false; driver.Tick();
            Assert.That(ui.BackRequests, Is.EqualTo(1));
        }
    }
}
```

- [ ] **Step 2: 구현**

```csharp
using System;
using UnityEngine.InputSystem;
using VContainer.Unity;
using BK.Scene;

namespace BK.UI
{
    /// <summary>
    /// The only place that reads the back key. Escape on desktop and the Android back
    /// button (which Unity reports as Escape) both route to
    /// <see cref="IUIService.HandleBackRequest"/>, topmost layer first. Nothing fires
    /// while a scene transition is in flight or while <see cref="Suspended"/> is set.
    /// </summary>
    public sealed class BackInputDriver : ITickable
    {
        private readonly IUIService _ui;
        private readonly ISceneService _scenes;
        private readonly Func<bool> _wasPressedThisFrame;

        /// <summary>Game code sets this during its own modal flows (e.g. a transition it drives itself).</summary>
        public bool Suspended { get; set; }

        public BackInputDriver(IUIService ui, ISceneService scenes)
            : this(ui, scenes, ReadKeyboard) { }

        public BackInputDriver(IUIService ui, ISceneService scenes, Func<bool> wasPressedThisFrame)
        {
            _ui = ui;
            _scenes = scenes;
            _wasPressedThisFrame = wasPressedThisFrame;
        }

        public void Tick()
        {
            if (Suspended || _scenes.IsTransitioning || !_wasPressedThisFrame())
                return;
            _ui.HandleBackRequest();
        }

        private static bool ReadKeyboard()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
        }
    }
}
```

VContainer는 등록된 생성자 중 `[Inject]`가 없으면 **매개변수가 가장 많은 public 생성자**를 고르므로 `Func<bool>`이 미등록이면 해석 실패한다. 두 생성자 중 DI용에 `[Inject]`를 붙인다:

```csharp
        [VContainer.Inject]
        public BackInputDriver(IUIService ui, ISceneService scenes)
```

- [ ] **Step 3: BK.UI asmdef에 `BK.Scene` 추가, meta → 컴파일 → 테스트 (BackInputDriverTests 2/2)**

- [ ] **Step 4: 커밋**

```bash
git add Assets/BKFramework/Runtime/UI
git add Assets/BKFramework/Tests
git commit -m "feat(ui): add BackInputDriver that routes Escape/back to IUIService"
```

---

### Task 6: 컴포지션 배선 + Bounce Escape 통일

**Files:**
- Modify: `Assets/BKFramework/Runtime/Composition/BK.Composition.asmdef` (references에 `BK.Save`, `BK.Options`)
- Modify: `Assets/BKFramework/Runtime/Composition/AppLifetimeScope.cs`
- Modify: `Assets/_Project/Bounce/Runtime/Presentation/ImportedSceneView.cs` (Update 제거, OnBackRequested로 이동)

- [ ] **Step 1: AppLifetimeScope 등록**

`using BK.Save; using BK.Options;` 추가. `builder.Register<SceneService>...` 다음에:

```csharp
            builder.Register<ISaveService>(_ => new SaveService(SaveService.DefaultDirectory), Lifetime.Singleton);
            builder.Register<IOptionsService>(container => new OptionsService(
                container.Resolve<ISaveService>(), _settings.DefaultLanguage), Lifetime.Singleton);
```

`RegisterBootSteps`의 LocalizationBootStep을 옵션 언어로:

```csharp
            builder.Register<IBootStep>(container => new LocalizationBootStep(
                container.Resolve<ILocalizationService>(),
                container.Resolve<IOptionsService>().Language.Value), Lifetime.Singleton);
```

`builder.RegisterEntryPoint<AppBootstrapper>();` 다음에:

```csharp
            builder.RegisterEntryPoint<BackInputDriver>().AsSelf();
```

(`AsSelf()`로 게임 코드가 `BackInputDriver.Suspended`를 만질 수 있게 한다.)

VContainer `Register<ISaveService>(Func<IObjectResolver, ISaveService>, Lifetime)`로 등록한 구현체가 `IDisposable`이면 컨테이너 Dispose 시 함께 Dispose된다 → 종료 시 Flush.

- [ ] **Step 2: ImportedSceneView Escape 통일**

`Update()` 메서드(366–377행)를 삭제하고 `OnBackRequested`(52행)를 다음으로 교체:

```csharp
        public bool OnBackRequested()
        {
            // Consumed in every case: this view drives its own settings/result back flow
            // and must never be popped by the framework.
            if(app==null || app.IsLoading)return true;
            if(dialog!=null){dialog.Close();return true;}
            if(offersView!=null && offersView.IsOpen){offersView.Close();return true;}
            if(resultShown)
            {
                var reward=popup==null?null:popup.GetComponent<ClearRewardView>();
                if(reward!=null)reward.Collect();else app.GoToLobby();
            }
            else if(popup!=null)ClosePopup();else OpenSettings();
            return true;
        }
```

`using UnityEngine;`의 `Input` 사용이 사라졌는지 확인(다른 곳에서 `Input`을 쓰면 유지).

- [ ] **Step 3: 컴파일 → 전체 EditMode 테스트 → PlayMode 테스트**

```bash
"$UNITY" -batchmode -nographics -projectPath "D:/UnityTemplate" -runTests -testPlatform EditMode -testResults "$SP/all_edit.xml" -logFile "$SP/all_edit.log"; echo exit=$?
"$UNITY" -batchmode -nographics -projectPath "D:/UnityTemplate" -runTests -testPlatform PlayMode -testResults "$SP/all_play.xml" -logFile "$SP/all_play.log"; echo exit=$?
```

Expected: EditMode 16 + 16 = 32 통과(기존 16 + SaveFile 5 + SaveService 7 + Options 2 + Back 2). PlayMode는 기존 통과 수와 동일(VisualFlowTests/OfferFlowTests). PlayMode가 배치모드에서 그래픽 없이 실패하면 `-nographics`를 빼고 다시 시도한다.

- [ ] **Step 4: 커밋**

```bash
git add Assets/BKFramework/Runtime/Composition Assets/_Project/Bounce/Runtime/Presentation/ImportedSceneView.cs
git commit -m "feat: wire save, options and back input into the app scope; route Bounce back through IUIService"
```

---

### Task 7: 문서

**Files:**
- Modify: `CLAUDE.md` (Layout 블록에 `Save/`, `Options/` 추가, Rules 레이어 줄 갱신)
- Modify: `docs/superpowers/plans/2026-10-08-sf-port-roadmap.md` (B 행 완료, 암호화 미포함 메모)

- [ ] **Step 1: CLAUDE.md**

Layout 블록의 `Core/` 줄 다음에:

```
  Save/      typed save slots: atomic write, .bak recovery, .corrupt-* preservation, version migration
  Options/   reactive player options (music/sfx/haptics/language) persisted through Save
```

Rules 첫 줄을:

```
- Framework layers depend downward only: `Core` ← `Save` ← `Options`, `Core` ← `Assets` ← `Scene`/`Data` ← `UI`/`Localization`; `UI` may use `Scene`.
```

Rules에 추가:

```
- Back/Escape는 `BackInputDriver` 한 곳에서만 읽는다. 뷰는 `IUIView.OnBackRequested`로 소비하고, 게임 코드는 `Input.GetKeyDown(KeyCode.Escape)`를 직접 폴링하지 않는다.
- 세이브는 `ISaveService.Get<T>()`로 슬롯을 받고 변경 후 `MarkDirty()`. 파일은 `persistentDataPath/Save/<Type>.json`. 암호화는 아직 없다.
```

- [ ] **Step 2: 로드맵 B 행 → `완료`, 비고에 "암호화(sf PlayerDataCrypto 상당)는 미포함, 필요 시 SaveFile에 바이트 계층 추가". 커밋:**

```bash
git add CLAUDE.md docs/superpowers/plans
git commit -m "docs: record save/options/back layers"
```
