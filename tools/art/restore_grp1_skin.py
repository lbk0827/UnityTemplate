"""Restore GRP1 image styling without replacing local scripts, bindings or fonts.

Only Image serialized properties are copied, matched by stable Unity fileID.
Prefab-instance sprite overrides are copied by (target fileID, propertyPath).
Existing project icons and the approved studio lobby background stay in place.
"""
from pathlib import Path
import re
import json
import subprocess

ROOT = Path('Assets/_Project/Bounce/Content/Imported/KitResources')
SOURCE = Path('D:/grp1_common/Assets/RootBoxResource')
IMAGE = 'fe87c0e1cc204ed48ad3b37840f39efc'

def chunks(text):
    return re.split(r'(?=^--- !u!)', text, flags=re.M)

def uid(chunk):
    m = re.match(r'--- !u!\d+ &(-?\d+)', chunk)
    return m[1] if m else None

def guid(path):
    return re.search(r'^guid: (\w+)', path.read_text(encoding='utf-8-sig'), re.M)[1]

# The old result hierarchy has all authored reward stages / pedestal / lighting.
win = ROOT/'Template/Prefabs/UI/Popup/StageClear/popup_stageclear.prefab'
win.write_bytes(subprocess.check_output(['git','show','d4de32a:'+win.as_posix()]))
protected = {guid(Path('Assets/_Project/Bounce/Content/UI/Toy/HudIcons.png.meta')),
             guid(Path('Assets/_Project/Bounce/Content/UI/Toy/LobbyStudio.png.meta'))}
available = {guid(p) for p in Path('Assets').rglob('*.meta') if p.is_file()}
remap={}
for meta in SOURCE.rglob('*.meta'):
    target_meta=ROOT/meta.relative_to(SOURCE)
    if target_meta.exists():remap[guid(meta)]=guid(target_meta)
fields = ('m_Sprite','m_Type','m_PreserveAspect','m_Color','m_PixelsPerUnitMultiplier')
changes=[]
for target in ROOT.rglob('*.prefab'):
    source=SOURCE/target.relative_to(ROOT)
    if not source.exists(): continue
    source_text=source.read_text(encoding='utf-8-sig')
    source_text=re.sub(r'guid: (\w+)',lambda m:'guid: '+remap.get(m[1],m[1]),source_text)
    original={uid(c):c for c in chunks(source_text) if uid(c)}
    text=target.read_text(encoding='utf-8-sig'); output=[]; count=0
    for block in chunks(text):
        old=original.get(uid(block))
        if old and IMAGE in block and IMAGE in old:
            sprite=re.search(r'^  m_Sprite: (.*)$',block,re.M)
            if sprite and any(g in sprite[1] for g in protected):
                output.append(block);continue
            original_sprite=re.search(r'^  m_Sprite: (.*)$',old,re.M)
            original_guid=re.search(r'guid: (\w+)',original_sprite[1]) if original_sprite else None
            if original_guid and original_guid[1] not in available:
                output.append(block);continue
            for field in fields:
                value=re.search(r'^  '+field+r': (.*)$',old,re.M)
                if value: block=re.sub(r'^  '+field+r': .*$',lambda _: '  '+field+': '+value[1],block,flags=re.M)
            count+=1
        # Nested prefab overrides retain their native image references too.
        if old and block.startswith('--- !u!1001'):
            pattern=r'    - target: (\{[^\n]+\})\n      propertyPath: m_Sprite\n      value:([^\n]*)\n      objectReference: ([^\n]+)'
            refs={m[1]:m[3] for m in re.finditer(pattern,old)}
            def restore(m):
                ref=refs.get(m[1]);g=re.search(r'guid: (\w+)',ref or '')
                if not ref or any(p in m[3] for p in protected) or (g and g[1] not in available):return m[0]
                return m[0][:m[0].rfind('objectReference: ')]+'objectReference: '+ref
            block=re.sub(pattern,restore,block)
        output.append(block)
    result=''.join(output)
    if result!=text:
        target.write_text(result,encoding='utf-8');changes.append({'prefab':str(target),'images':count})
Path('docs/ui-grp1-refresh').mkdir(parents=True,exist_ok=True)
Path('docs/ui-grp1-refresh/source-restoration.json').write_text(json.dumps(changes,ensure_ascii=False,indent=2),encoding='utf-8')
print('Restored image styling in',len(changes),'prefabs')
