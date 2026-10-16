"""Port common popup visuals without importing RootBox runtime dependencies.

Requires PyYAML. Existing project GUIDs/art are reused; unknown behaviours become
reference-only VisualBindings. Run from repository root, then PopupImportSetup.Run.
"""
from pathlib import Path
import re, json, shutil
import yaml

SOURCE = Path('D:/grp1_common')
DEST = Path('Assets/_Project/Bounce/Content/Imported')
BINDING = 'bb010001000000000000000000000003'
NAMES = {'popup_quit', 'popup_retry', 'popup_heart', 'popup_unlockgimmick',
         'popup_unlockbooster', 'popup_usebooster', 'popup_stagecelebration',
         'popup_reward', 'popup_profile'}

def index(roots):
    result = {}
    for root in roots:
        for meta in root.rglob('*.meta'):
            match = re.search(r'^guid: (\w+)', meta.read_text(encoding='utf-8-sig'), re.M)
            if match:
                result[match[1]] = Path(str(meta)[:-5])
    return result

source = index([SOURCE / 'Assets', SOURCE / 'Library/PackageCache'])
target = index([Path('Assets'), Path('Packages'), Path('Library/PackageCache')])
copied = []
manifest = Path('docs/common-popups-import.json')
previous = json.loads(manifest.read_text(encoding='utf-8')) if manifest.exists() else []
remap = {'607c770462e2bba4ebcb0aa019e38d42': '4e29b1a8efbd4b44bb3f3716e73f07ff',
         'ba53d1fef9c407143ae4117bbe0544d6': 'f4688fdb7df04437aeb418b961361dc5'}

def references(value, key=''):
    if isinstance(value, dict):
        if 'fileID' in value:
            if value['fileID'] and not value.get('guid'):
                yield {'key': key, 'value': value}
        else:
            for k, v in value.items():
                if not k.startswith('m_'):
                    yield from references(v, f'{key}.{k}' if key else k)
    elif isinstance(value, list):
        for i, v in enumerate(value):
            yield from references(v, f'{key}.{i}')

def convert(text):
    chunks = re.split(r'(?=^--- !u!)', text, flags=re.M)
    for i, chunk in enumerate(chunks):
        match = re.search(r'm_Script:.*guid: (\w+)', chunk)
        if not match:
            continue
        guid = match[1]
        if guid in remap:
            chunks[i] = chunk.replace(guid, remap[guid])
            continue
        path = source.get(guid, target.get(guid, Path('unknown')))
        # Only Unity's UI and TMP components retain behaviour. Sequences are
        # replaced by the local unscaled popup transition, not package callbacks.
        if ('com.unity.ugui' in str(path) or 'com.unity.textmeshpro' in str(path)):
            continue
        data = yaml.safe_load(chunk[chunk.index('\n')+1:])['MonoBehaviour']
        entries = list(references(data))
        prefix = chunk[:chunk.index('  m_Script:')]
        chunks[i] = prefix + '  m_Script: {fileID: 11500000, guid: '+BINDING+', type: 3}\n  m_Name: \n  m_EditorClassIdentifier: \n  role: '+path.stem+'\n' + yaml.safe_dump({'entries':entries},sort_keys=False,default_flow_style=False).replace('\n', '\n  ').join(['  ', '']).rstrip()+'\n'
    return ''.join(chunks)

def copy(guid):
    if guid in target or guid.startswith('0000000000000000'):
        return
    src = source.get(guid)
    if not src or not src.is_file() or src.suffix in {'.cs', '.dll', '.asmdef', '.ttf', '.otf'}:
        return
    relative = (str(src.relative_to(SOURCE / 'Assets')).replace('RootBoxResource', 'KitResources')
                if src.is_relative_to(SOURCE/'Assets') else 'CommonPackages/'+src.name)
    dest = DEST / relative
    if dest.exists() and Path(str(dest)+'.meta').exists():
        existing = re.search(r'^guid: (\w+)',Path(str(dest)+'.meta').read_text(encoding='utf-8-sig'),re.M)[1]
        remap[guid] = existing
        target[guid] = dest
        return
    if src.suffix == '.asset' and 'Font' not in str(src):
        return
    target[guid] = dest
    dest.parent.mkdir(parents=True, exist_ok=True)
    if src.suffix in {'.prefab', '.mat', '.asset', '.controller', '.anim'}:
        text = src.read_text(encoding='utf-8-sig')
        if src.suffix == '.prefab':
            text = convert(text)
        dest.write_text(text, encoding='utf-8')
        for ref in set(re.findall(r'guid: (\w+)', text)):
            copy(ref)
    else:
        shutil.copyfile(src, dest)
    shutil.copyfile(str(src)+'.meta', str(dest)+'.meta')
    copied.append(dest.as_posix())

for path in (SOURCE/'Assets/RootBoxResource/Template/Prefabs/UI/Popup').rglob('*.prefab'):
    if path.stem in NAMES:
        guid = re.search(r'guid: (\w+)',Path(str(path)+'.meta').read_text())[1]
        copy(guid)
for name in copied:
    path = Path(name)
    if path.suffix in {'.prefab', '.mat', '.asset'}:
        text = path.read_text(encoding='utf-8')
        for old, new in remap.items():
            text = text.replace(old, new)
        path.write_text(text, encoding='utf-8')
manifest.write_text(json.dumps(sorted(set(previous+copied)), indent=2),encoding='utf-8')
print(f'Imported {len(copied)} visual assets')
