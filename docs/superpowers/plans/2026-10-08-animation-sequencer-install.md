# Animation Sequencer + DOTween 설치 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** UnityTemplate에 공개 MIT Animation Sequencer(0.5.5)와 DOTween free(1.3.030)를 설치하고, sf의 팝업 열림/닫힘 시퀀스 프리팹 2개를 GUID 리맵해 가져온 뒤 EditMode 테스트와 배치모드 컴파일로 검증한다.

**Architecture:** Animation Sequencer는 OpenUPM 스코프 레지스트리로 설치한다(업데이트 가능). DOTween은 UPM 패키지가 없으므로 공식 zip의 unitypackage를 풀어 `Assets/Plugins/Demigiant/DOTween/`에 원본 .meta(GUID 보존)와 함께 넣고, `DOTween.Modules.asmdef`와 `DOTweenSettings.asset`을 직접 만든다. 업스트림 패키지는 `DOTWEEN_ENABLED` 정의가 없으면 전체가 컴파일에서 빠지므로 ProjectSettings의 Scripting Define에 `DOTWEEN_ENABLED;TMP_ENABLED`를 추가한다. sf 프리팹은 컨트롤러 스크립트 GUID만 다르고(`a3dbb958…`→`c10d422b…`) SerializeReference 타입명은 동일하므로 텍스트 치환으로 리맵한다.

**Tech Stack:** Unity 6000.3.21f1, OpenUPM, `com.brunomikoski.animationsequencer` 0.5.5 (MIT), DOTween 1.3.030 free, NUnit EditMode, Unity 배치모드.

**배경 사실 (조사 완료):**
- OpenUPM 최신 0.5.5, git 커밋 `1ed78c4c69ccf295d02098da2ae31e44a5dd16fa`, `unity: 2021.3`. `com.demigiant.dotween`은 OpenUPM에 **없다**.
- 업스트림 asmdef는 `DOTween.Modules`, `Unity.TextMeshPro`, `UniTask`를 참조. versionDefines로 `com.demigiant.dotween`→`DOTWEEN_ENABLED`, `com.unity.textmeshpro`→`TMP_ENABLED`를 걸지만 우리 프로젝트엔 둘 다 패키지로 없으므로 둘 다 수동 define 필요. (`UNITASK_ENABLED`는 `com.cysharp.unitask`가 있으니 자동.)
- 업스트림 에디터 스크립트 `AnimationSequencerSetupHelper`는 로드 시 `DOTween.Modules` 어셈블리가 보이면 현재 빌드 타깃 그룹에만 `DOTWEEN_ENABLED`를 추가한다. 결정성을 위해 Standalone/Android/iPhone 세 그룹에 미리 넣는다.
- DOTween 공식 zip은 스크래치패드에 내려받아 풀어 두었다: `C:\Users\DG-250~1\AppData\Local\Temp\claude\d--UnityTemplate\70291d59-c7ab-4bd6-b23a-d4f43b5fe629\scratchpad\dotween_dl\pkg\<guid>\{asset,asset.meta,pathname}`. DOTween.dll GUID `a811bde74b26b53498b4f6d872b09b6d`(sf와 동일).
- 1.3.030 Modules 마커: UI/Sprite/Physics/Physics2D/Audio는 기본 컴파일(`#if !DOTWEEN_NOxxx`), UIToolkit/EPOOutline은 기본 제외. `DOTweenModuleUI.cs`가 `UnityEngine.UI`를 쓰므로 asmdef에 명시 참조한다.
- sf 프리팹 `SQ_Popup_Open/Close`(각 136줄)는 컨트롤러 1개 + DOTweenAnimationStep 1개(FadeCanvasGroup + Scale). 다른 외부 참조 없음. 메타 GUID `2ad1d76ef800ec64792cc432a6bc52f2`, `ae14d485131a412418a506f5df7ea5af`는 템플릿과 충돌 없음.
- sf 포크 전용 필드(`enable`, `isResetToFromState`, `skipResetOnDisable`, `fromAlpha`, `fromLocalScale`, `findCamera`, `delayBySiblingOrder` 등)는 업스트림에 없어 로드 시 무시된다. 핵심 필드(`delay, flowType, target, duration, loopCount, loopType, actions, direction, ease, isRelative, alpha, scale, axisConstraint`)는 이름이 같다.
- 템플릿 베이스라인은 배치모드 컴파일 통과(에러 0). 프로젝트가 에디터에 열려 있지 않아야 한다.
- `Assets/_Project/Bounce/Tests/EditMode/BK.Kit.EditModeTests.asmdef`가 유일한 EditMode 테스트 어셈블리. 테스트는 `#if UNITY_EDITOR` 가드 불필요(asmdef가 Editor 전용).

---

### Task 1: 패키지 매니페스트와 Scripting Define

**Files:**
- Modify: `Packages/manifest.json`
- Modify: `ProjectSettings/ProjectSettings.asset` (`scriptingDefineSymbols: {}` 줄, 833행 부근)

- [x] **Step 1: manifest에 의존성과 스코프 추가 (Edit 툴, 수동 삽입)**

파일은 CRLF이고 의존성이 알파벳순이 아니므로 스크립트로 재정렬하지 않는다. 두 곳만 Edit 한다.

`"com.annulusgames.lit-motion": "2.0.2",` 바로 다음 줄에:

```json
    "com.brunomikoski.animationsequencer": "0.5.5",
```

`scopedRegistries[0].scopes` 배열의 `"com.github-glitchenzo"` 뒤에:

```json
        "com.github-glitchenzo",
        "com.brunomikoski"
```

`git diff --stat Packages/manifest.json` → 변경 2~3줄만.

- [x] **Step 2: Scripting Define 추가**

`ProjectSettings/ProjectSettings.asset`에서

```yaml
  scriptingDefineSymbols: {}
```

를

```yaml
  scriptingDefineSymbols:
    Android: DOTWEEN_ENABLED;TMP_ENABLED
    Standalone: DOTWEEN_ENABLED;TMP_ENABLED
    iPhone: DOTWEEN_ENABLED;TMP_ENABLED
```

로 바꾼다 (Edit 툴, 2-space 들여쓰기 유지).

- [x] **Step 3: 커밋은 Task 2와 합친다**

이 시점은 `DOTWEEN_ENABLED`는 켜졌는데 `DOTween.Modules`가 없어 패키지 어셈블리가 스킵되는 중간 상태다. Task 2 Step 5에서 함께 커밋한다.

---

### Task 2: DOTween 1.3.030 설치

**Files:**
- Create: `Assets/Plugins/Demigiant/DOTween/**` (unitypackage의 27개 항목, 원본 .meta 유지)
- Create: `Assets/Plugins/Demigiant/DOTween/Modules/DOTween.Modules.asmdef` (+ `.meta`)
- Create: `Assets/Plugins/Demigiant/DOTween/Resources/DOTweenSettings.asset` (+ `.meta`, 폴더 `.meta`)

- [x] **Step 1: unitypackage 내용을 .meta와 함께 복사**

```bash
python -I - <<'EOF'
import os,shutil
S=r'C:\Users\DG-250~1\AppData\Local\Temp\claude\d--UnityTemplate\70291d59-c7ab-4bd6-b23a-d4f43b5fe629\scratchpad\dotween_dl\pkg'
R=r'D:\UnityTemplate'
n=0
for d in os.listdir(S):
    dd=os.path.join(S,d)
    pn=os.path.join(dd,'pathname')
    if not os.path.isfile(pn): continue
    rel=open(pn,encoding='utf-8').read().split('\n')[0]
    dst=os.path.join(R,rel.replace('/',os.sep))
    if os.path.isfile(os.path.join(dd,'asset')):
        os.makedirs(os.path.dirname(dst),exist_ok=True)
        shutil.copyfile(os.path.join(dd,'asset'),dst)
    else:
        os.makedirs(dst,exist_ok=True)
    shutil.copyfile(os.path.join(dd,'asset.meta'),dst+'.meta')
    n+=1
print('copied',n)
EOF
```

Expected: `copied 28` (파일 24 + 폴더 4). `find Assets/Plugins/Demigiant -type f | wc -l` → 52.

unitypackage에는 `Assets/Plugins/Demigiant` 폴더 항목이 없으므로 `Assets/Plugins/Demigiant.meta`를 아래 폴더 meta 템플릿(새 GUID)으로 직접 만든다. `Assets/Plugins.meta`는 이미 있다.

- [x] **Step 2: Modules asmdef 작성**

`Assets/Plugins/Demigiant/DOTween/Modules/DOTween.Modules.asmdef`:

```json
{
    "name": "DOTween.Modules",
    "references": [
        "UnityEngine.UI"
    ],
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

`.meta` (GUID는 새로 생성: `python -c "import uuid;print(uuid.uuid4().hex)"`):

```yaml
fileFormatVersion: 2
guid: <새 GUID>
AssemblyDefinitionImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
```

- [x] **Step 3: DOTweenSettings.asset 작성**

`Assets/Plugins/Demigiant/DOTween/Resources/DOTweenSettings.asset` — sf의 것을 기반으로 하되 `storeSettingsLocation: 1`(DOTween 폴더 안 Resources), `textMeshProEnabled: 0`, `createASMDEF: 1`:

```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 16995157, guid: a811bde74b26b53498b4f6d872b09b6d, type: 3}
  m_Name: DOTweenSettings
  m_EditorClassIdentifier: 
  useSafeMode: 1
  safeModeOptions:
    logBehaviour: 2
    nestedTweenFailureBehaviour: 0
  timeScale: 1
  unscaledTimeScale: 1
  useSmoothDeltaTime: 0
  maxSmoothUnscaledTime: 0.15
  rewindCallbackMode: 0
  showUnityEditorReport: 0
  logBehaviour: 0
  drawGizmos: 1
  defaultRecyclable: 0
  defaultAutoPlay: 3
  defaultUpdateType: 0
  defaultTimeScaleIndependent: 0
  defaultEaseType: 6
  defaultEaseOvershootOrAmplitude: 1.70158
  defaultEasePeriod: 0
  defaultAutoKill: 1
  defaultLoopType: 0
  debugMode: 0
  debugStoreTargetId: 1
  showPreviewPanel: 1
  storeSettingsLocation: 1
  modules:
    showPanel: 0
    audioEnabled: 1
    physicsEnabled: 1
    physics2DEnabled: 1
    spriteEnabled: 1
    uiEnabled: 1
    textMeshProEnabled: 0
    tk2DEnabled: 0
    deAudioEnabled: 0
    deUnityExtendedEnabled: 0
    epoOutlineEnabled: 0
  createASMDEF: 1
  showPlayingTweens: 0
  showPausedTweens: 0
```

`.meta`:

```yaml
fileFormatVersion: 2
guid: <새 GUID>
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
```

`Resources` 폴더 `.meta`:

```yaml
fileFormatVersion: 2
guid: <새 GUID>
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
```

- [x] **Step 4: 배치모드 컴파일로 검증**

```bash
"/c/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Unity.exe" -batchmode -nographics -quit -projectPath "D:/UnityTemplate" -logFile "<scratchpad>/compile_task2.log"; echo exit=$?
grep -cE "error CS" "<scratchpad>/compile_task2.log"
ls /d/UnityTemplate/Library/PackageCache | grep brunomikoski
grep -E "DOTWEEN_ENABLED" /d/UnityTemplate/ProjectSettings/ProjectSettings.asset
```

Expected: `exit=0`, 에러 0, `com.brunomikoski.animationsequencer@…` 폴더 존재, define 유지. 로그에서 `Animation Sequencer` 셋업 헬퍼 메시지를 확인해도 좋다.

`DOTWEEN_NOUI` 류 정의가 없으니 UI 모듈이 컴파일된다. 만약 `UnityEngine.UI` 참조 오류가 나면 asmdef의 references를 확인한다.

- [x] **Step 5: 커밋**

```bash
git add Packages/manifest.json ProjectSettings/ProjectSettings.asset Assets/Plugins/Demigiant Assets/Plugins/Demigiant.meta
git status --short   # 남는 미추적 파일이 없어야 한다
git commit -m "build: install Animation Sequencer 0.5.5 and vendor DOTween 1.3.030"
```

---

### Task 3: EditMode 테스트로 설치 확인

**Files:**
- Modify: `Assets/_Project/Bounce/Tests/EditMode/BK.Kit.EditModeTests.asmdef` (references에 `BrunoMikoski.AnimationSequencer`, `DOTween.Modules` 추가)
- Create: `Assets/_Project/Bounce/Tests/EditMode/AnimationSequencerInstallTests.cs` (+ `.meta`, MonoImporter)

- [x] **Step 1: 실패하는 테스트 작성**

```csharp
using BrunoMikoski.AnimationSequencer;
using DG.Tweening;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class AnimationSequencerInstallTests
{
    [Test]
    public void DOTweenAndSequencerTypesResolve()
    {
        Assert.That(DOTween.Version, Does.StartWith("1.3."));
        var go = new GameObject("seq");
        try
        {
            var controller = go.AddComponent<AnimationSequencerController>();
            Assert.That(controller, Is.Not.Null);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [TestCase("Assets/_Project/UI/Sequences/SQ_Popup_Open.prefab")]
    [TestCase("Assets/_Project/UI/Sequences/SQ_Popup_Close.prefab")]
    public void ImportedPopupSequencePrefabsKeepTheirSteps(string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab, Is.Not.Null, path);
        var controller = prefab.GetComponent<AnimationSequencerController>();
        Assert.That(controller, Is.Not.Null, "controller script GUID not remapped");
        Assert.That(controller.AnimationSteps.Length, Is.EqualTo(1));
        Assert.That(controller.AnimationSteps[0], Is.TypeOf<DOTweenAnimationStep>());
        Assert.That(((DOTweenAnimationStep)controller.AnimationSteps[0]).Actions.Length, Is.EqualTo(2));
    }
}
```

(0.5.5 확인됨: `AnimationSequencerController.AnimationSteps`, `DOTweenAnimationStep.Actions` 모두 public 프로퍼티.)

asmdef 최종 references:

```json
  "references": [
    "BK.Kit.Runtime", "BK.UI", "BK.Scene", "BK.Assets", "BK.Composition",
    "UniTask", "VContainer", "BK.Data",
    "BrunoMikoski.AnimationSequencer", "DOTween.Modules"
  ],
```

`.meta`:

```yaml
fileFormatVersion: 2
guid: <새 GUID>
MonoImporter:
  externalObjects: {}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {instanceID: 0}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
```

- [x] **Step 2: 테스트 실행 → 프리팹 테스트 2개 실패 확인**

```bash
"/c/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Unity.exe" -batchmode -nographics -projectPath "D:/UnityTemplate" -runTests -testPlatform EditMode -testFilter AnimationSequencerInstallTests -testResults "<scratchpad>/tests_task3a.xml" -logFile "<scratchpad>/tests_task3a.log"; echo exit=$?
python -I -c "import xml.etree.ElementTree as E;r=E.parse(r'<scratchpad>/tests_task3a.xml').getroot();print(r.attrib['total'],r.attrib['passed'],r.attrib['failed'])"
```

Expected: `3 1 2` (타입 테스트 통과, 프리팹 2개는 파일 없음으로 실패). exit 코드는 실패 시 2 또는 3.

- [x] **Step 3: 커밋 (테스트만)**

```bash
git add Assets/_Project/Bounce/Tests/EditMode
git commit -m "test: verify Animation Sequencer install and imported popup sequences"
```

---

### Task 4: sf 팝업 시퀀스 프리팹 GUID 리맵 이식

**Files:**
- Create: `Assets/_Project/UI/Sequences/SQ_Popup_Open.prefab` (+ `.meta`), `SQ_Popup_Close.prefab` (+ `.meta`), 폴더 `.meta`
- Create: `docs/animation-sequencer.json` (출처·리맵 기록)

- [x] **Step 1: 리맵 복사**

```bash
python -I - <<'EOF'
import os,shutil,uuid
SRC=r'D:\ngfe_sf\Assets\Application\Bundles\UI\Common'
DST=r'D:\UnityTemplate\Assets\_Project\UI\Sequences'
FORK='a3dbb958357cc5b4f9bf0b1b2226c664'; UP='c10d422b52559da41aa9676770a8fc65'
os.makedirs(DST,exist_ok=True)
for n in ['SQ_Popup_Open','SQ_Popup_Close']:
    t=open(os.path.join(SRC,n+'.prefab'),encoding='utf-8').read()
    assert t.count(FORK)==1, n
    open(os.path.join(DST,n+'.prefab'),'w',encoding='utf-8',newline='\n').write(t.replace(FORK,UP))
    shutil.copyfile(os.path.join(SRC,n+'.prefab.meta'),os.path.join(DST,n+'.prefab.meta'))
m=DST+'.meta'
if not os.path.exists(m):
    open(m,'w',encoding='utf-8',newline='\n').write('fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'%uuid.uuid4().hex)
print('ok')
EOF
grep -c c10d422b52559da41aa9676770a8fc65 /d/UnityTemplate/Assets/_Project/UI/Sequences/*.prefab
```

Expected: 각 프리팹 1회.

- [x] **Step 2: 테스트 실행 → 3개 통과**

Task 3 Step 2 명령을 `tests_task4`로 다시 실행. Expected: `3 3 0`, exit=0.

실패 시 로그에서 `Unknown managed type referenced` 또는 `AnimationSteps` 길이 0을 찾는다. 길이 0이면 SerializeReference 타입명(`asm: BrunoMikoski.AnimationSequencer`)이 설치된 어셈블리명과 다른 것이므로 `Library/PackageCache/.../BrunoMikoski.AnimationSequencer.asmdef`의 `name`을 확인한다.

- [x] **Step 3: 출처 기록**

`docs/animation-sequencer.json`:

```json
{
  "package": {
    "name": "com.brunomikoski.animationsequencer",
    "version": "0.5.5",
    "license": "MIT",
    "source": "https://github.com/brunomikoski/Animation-Sequencer",
    "commit": "1ed78c4c69ccf295d02098da2ae31e44a5dd16fa",
    "installedVia": "OpenUPM scoped registry (scope com.brunomikoski)"
  },
  "dotween": {
    "version": "1.3.030",
    "edition": "free",
    "source": "https://dotween.demigiant.com/downloads/DOTween_1_3_030.zip",
    "license": "https://dotween.demigiant.com/license.php",
    "path": "Assets/Plugins/Demigiant/DOTween",
    "notes": "Modules asmdef and DOTweenSettings.asset were authored by hand instead of the Utility Panel. Settings are stored in DOTween/Resources (storeSettingsLocation=1). Do not regenerate the asmdef from the Utility Panel: it would drop the hand-written UnityEngine.UI reference."
  },
  "defines": ["DOTWEEN_ENABLED", "TMP_ENABLED"],
  "importedFromSf": {
    "source": "D:/ngfe_sf/Assets/Application/Bundles/UI/Common",
    "files": ["SQ_Popup_Open.prefab", "SQ_Popup_Close.prefab"],
    "target": "Assets/_Project/UI/Sequences",
    "guidRemap": {"a3dbb958357cc5b4f9bf0b1b2226c664": "c10d422b52559da41aa9676770a8fc65"},
    "droppedForkOnlyFields": ["enable", "isResetToFromState", "skipResetOnDisable", "fromAlpha", "fromLocalScale", "findCamera", "delayBySiblingOrder", "firstDelay", "oneTimeDelay", "findChild"],
    "notPorted": "sf의 DoubleU 포크(0.5.20) 전용 액션(LayoutElement/MaterialProperty/Renderer/ParticleSystemParam/Spine/TMP_TextColor)과 Recipe/Sequence 프리팹은 RootBox UIElements·scopeduiservice에 묶여 있어 로비 재구현 마일스톤(G)에서 다룬다."
  }
}
```

- [x] **Step 4: 커밋**

```bash
git add Assets/_Project/UI/Sequences Assets/_Project/UI/Sequences.meta docs/animation-sequencer.json
git status --short
git commit -m "assets: import popup open/close sequences from sf with remapped sequencer GUID"
```

---

### Task 5: 문서 갱신

**Files:**
- Modify: `CLAUDE.md` Stack 표 (`| Tween / sequencing | LitMotion |` 행)

- [x] **Step 1: Stack 표 갱신**

```markdown
| Tween / sequencing | LitMotion (코드 트윈), Animation Sequencer 0.5.5 + DOTween 1.3.030 free (프리팹 UI 연출) |
```

바로 아래 Rules 섹션 끝에 한 줄 추가:

```markdown
- Animation Sequencer는 `DOTWEEN_ENABLED;TMP_ENABLED` define이 있어야 컴파일된다. DOTween은 `Assets/Plugins/Demigiant/DOTween`에 벤더링(출처 `docs/animation-sequencer.json`). 코드에서 트윈이 필요하면 LitMotion, 디자이너가 프리팹에서 조립하는 연출은 Animation Sequencer.
```

- [x] **Step 2: 전체 EditMode 테스트 한 번 더 실행 후 커밋**

```bash
"/c/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Unity.exe" -batchmode -nographics -projectPath "D:/UnityTemplate" -runTests -testPlatform EditMode -testResults "<scratchpad>/tests_all.xml" -logFile "<scratchpad>/tests_all.log"; echo exit=$?
```

Expected: exit=0, failed 0.

```bash
git add CLAUDE.md
git commit -m "docs: record Animation Sequencer and DOTween setup"
```
