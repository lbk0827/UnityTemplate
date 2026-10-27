"""Build the offline level catalog. --accept-run imports a fresh, passing Unity run.

Unity filter: BounceReleaseFlowTests
Results: TestResults/release-final.xml
Rebuild: python tools/balance/build_level_catalog.py [--accept-run]
"""
import argparse
from collections import Counter
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'docs/bounce-level-catalog'
CONTENT = ROOT / 'Assets/_Project/Bounce/Content'
LEARNING = CONTENT / 'bounce-release-levels.json'
LEGACY = CONTENT / 'bounce-levels.json'
NAMES = dict(tube='금속관', cupboard='찬장', panel='이동 패널', mine='기뢰', switch='스위치', door='문', combo='연속 타격', armor='방향 장갑')
RULES = dict(tube='관 조각을 타격할 때마다 1개 제거. 길이만큼 맞혀야 완료. 용광로를 포함해 배치되지만 목표 카운터는 관 1개.',
             cupboard='찬장에 인접한 일반 블록을 맞힐 때 접시 1개 제거. 총 10개. 찬장 자체 직접 타격으로는 제거되지 않음.',
             panel='좌우로 움직이는 반사 장애물. 파괴 목표에 포함하지 않음.',
             mine='맞히면 주변 8칸에 폭발. 인접 기뢰도 연쇄 폭발하며, 기뢰 자체도 파괴 목표 1개.',
             switch='연결 ID가 같은 문을 열어 통로 확보. 파괴 목표가 아님.',
             door='연결된 스위치가 열기 전까지 공을 가로막음. 파괴 목표가 아님.',
             combo='동일한 공으로 두 번 맞혀야 제거. 다른 공의 첫 타격은 이어서 누적되지 않음.',
             armor='표시된 약점 면에서 들어오는 충돌만 유효. 다른 면은 반사.')


def read(path):
    return json.loads(path.read_text(encoding='utf-8'))


def sha(path):
    data=path.read_bytes()
    if path.suffix not in ('.png','.fbx'):data=data.replace(b'\r\n',b'\n')
    return hashlib.sha256(data).hexdigest()


def sources():
    paths = [LEARNING, LEGACY, CONTENT/'Presentation.asset', CONTENT/'IngameConfig.asset',
             ROOT/'ProjectSettings/TimeManager.asset', ROOT/'ProjectSettings/DynamicsManager.asset', ROOT/'ProjectSettings/TagManager.asset',
             ROOT/'ProjectSettings/ProjectVersion.txt',ROOT/'Assets/_Project/Bounce/Scenes/VisualIngame.unity']
    paths += list((ROOT/'Assets/_Project/Bounce/Runtime').rglob('*.cs'))
    paths += [ROOT/f'Assets/_Project/Bounce/Tests/PlayMode/{name}.cs' for name in ['BounceReleaseFlowTests','BounceOpeningPlanner']]
    # Include gameplay collider prefabs and referenced meshes, not only JSON layouts.
    paths += list((CONTENT/'Gimmicks').rglob('*.prefab'))
    text = (CONTENT/'Presentation.asset').read_text(encoding='utf-8')
    block_guids = re.findall(r'guid: ([a-f0-9]+)', text.split('  blocks:')[1].split('  levels:')[0])
    catalog_guids = re.findall(r'guid: ([a-f0-9]+)', (CONTENT/'IngameConfig.asset').read_text())
    metas = {}
    for p in (ROOT/'Assets/_Project/Bounce').rglob('*.meta'):
        match = re.search(r'^guid: (\w+)', p.read_text(encoding='utf-8'), re.M)
        if match:
            metas[match[1]] = Path(str(p)[:-5])
    art_guids=re.findall(r'^  (?:cannon|ball|arenaMaterial|trajectoryMaterial):.*guid: ([a-f0-9]+)',text,re.M)
    queue = block_guids + catalog_guids + art_guids
    seen = set()
    while queue:
        guid = queue.pop()
        if guid in seen or guid not in metas:
            continue
        seen.add(guid)
        p = metas[guid]
        # TMP regenerates font atlas serialization at Editor shutdown; it is not a physics input.
        if any(part.lower() in ('font','fonts') for part in p.parts):continue
        if p.suffix not in ('.prefab', '.asset', '.fbx', '.obj', '.mat', '.png', '.shader'):
            continue
        paths.append(p)
        if p.suffix in ('.prefab', '.asset', '.mat'):
            queue += re.findall(r'guid: ([a-f0-9]+)', p.read_text(encoding='utf-8', errors='replace'))
    return sorted(set(p for p in paths if p.is_file()))


def active_source():
    text = (CONTENT/'Presentation.asset').read_text(encoding='utf-8')
    guid = re.search(r'  levels:.*guid: (\w+)', text).group(1)
    for path in CONTENT.glob('*.json'):
        if f'guid: {guid}' in path.with_suffix('.json.meta').read_text():
            return path
    raise ValueError('Cannot resolve Presentation.levels to a JSON library')


def import_evidence():
    source=ROOT/'TestResults'
    tests=ET.parse(source/'release-final.xml').getroot()
    assert tests.attrib['result']=='Passed' and int(tests.attrib['failed'])==0
    required={'EveryReleasedStageClearsAndReplaysWithinBudget','FirstMineGuideIsAtFourteenAndCampaignEndDoesNotChargeAHeart','RecordedMineIntroductionAlsoClearsWithAutomaticPhysics','OpeningChoicesCompareNearestShotsAndRecoverFromAlternateOpening'}
    assert required <= {c.attrib['name'] for c in tests.iter('test-case') if c.attrib['result']=='Passed'}
    start=datetime.fromisoformat(tests.attrib['start-time'].replace('Z','+00:00'))
    if start.tzinfo is None:start=start.replace(tzinfo=timezone.utc)
    paths=sources()
    assert all(p.stat().st_mtime<=start.timestamp()+2 for p in paths),'Gameplay source changed during the run'
    report=read(source/'BounceRelease/solutions.json')
    rows=read(active_source())['levels'];records=report['boards']
    assert len(records)==len(rows) and {r['stage'] for r in records}==set(range(1,len(rows)+1))
    for level,r in zip(rows,records):
        assert r['cleared'] and r['replayPassed'] and r['remaining']==0
        assert r['budget']==level['balls'] and r['shotsUsed']==len(r['shots'])<=r['budget']
    audit=read(source/'BounceRelease/opening-audit.json')
    assert audit['sourceSha256']==hashlib.sha256(active_source().read_bytes()).hexdigest()
    assert [a['stage'] for a in audit['boards']]==list(range(1,21))
    for a in audit['boards']:
        r=records[a['stage']-1]
        assert a['plannedShots']==r['shotsUsed'] and a['recoveryRemaining']==0
        assert a['recoveryShots']<=r['budget'] and len(a['openingDamage'])==5
        assert a['tolerantRemaining']==0 and a['tolerantShots']<=r['budget']
        assert a['openingDamage'][2]==r['shots'][0]['before']-r['shots'][0]['after']
    evidence=OUT/'evidence';evidence.mkdir(parents=True,exist_ok=True)
    for src,dst in [(source/'release-final.xml','release-test-results.xml'),(source/'BounceRelease/solutions.json','release-solutions.json'),(source/'BounceRelease/opening-audit.json','opening-audit.json')]:
        assert src.stat().st_mtime>=start.timestamp()-2
        shutil.copyfile(src,evidence/dst)
    for r in records:
        if not r.get('screenshot'):continue
        assert re.fullmatch(r'level-\d{3}\.png',r['screenshot'])
        src=source/'BounceRelease'/r['screenshot'];assert src.stat().st_mtime>=start.timestamp()-2
        shutil.copyfile(src,evidence/src.name)
    manifest=dict(verifiedAt=tests.attrib['end-time'],testsPassed=int(tests.attrib['passed']),
                  commit=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),
                  hashNormalization='Text CRLF normalized to LF; binary unchanged',
                  sourceHashes={p.relative_to(ROOT).as_posix():sha(p) for p in paths})
    (evidence/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

def summarize(level,stage,library,routes,fresh):
    blocks=sum('1'<=c<='9' for row in level['rows'] for c in row)
    gimmicks=level.get('gimmicks') or [];counts=Counter(g['type'] for g in gimmicks)
    objectives=blocks+sum(g['type'] not in ('panel','switch','door') for g in gimmicks)
    current=library=='active'
    record=dict(id=f'{library}-{stage}',stage=stage,library=library,balls=level['balls'],blocks=blocks,
                objectives=objectives,columns=len(level['rows'][0]),height=level.get('gridRows') or 13,
                arenaColumns=11 if current else len(level['rows'][0]),arenaRows=16 if current else level.get('gridRows') or 13,
                rows=level['rows'],gimmicks=gimmicks,gimmickCounts=dict(counts),
                gimmickLabel=' · '.join(f'{NAMES[k]} {n}' for k,n in counts.items()) or '없음',
                title=level.get('title') or f'보관 레벨 {stage:02}',intent=level.get('intent') or '보관 중인 기믹 배치. 현재 출시 순서에 미연결.',
                chapter=level.get('chapter') or '보관',designDifficulty=level.get('difficulty') or '미평가',
                introduction=level.get('introduction') or '',
                grade=(level.get('difficulty','일반')+' 설계 · 사람 난이도 미평가') if current else '미평가',
                status='재검증 필요' if current else '클리어 미검증',pressure='미평가',groups=[],route=None)
    if not current or not fresh:return record
    route=next(r for r in routes if r['stage']==stage)
    assert route['objectives']==objectives and route['budget']==level['balls']
    assert route['replayPassed'] and route['cleared'] and len(route['shots'])<=level['balls']
    used=len(route['shots']);usage=used/level['balls']
    record.update(status='클리어·재현 확인',route=route,witnessShots=used,spareBalls=level['balls']-used,
                  consumption=round(usage*100),pressure='여유' if usage<=.65 else '보통' if usage<=.8 else '빠듯',
                  screenshot=route.get('screenshot') or '')
    return record

def build():
    active=active_source();assert active==LEARNING
    manifest=read(OUT/'evidence/manifest.json')
    hashes={p.relative_to(ROOT).as_posix():sha(p) for p in sources()}
    stale=[p for p in set(hashes)|set(manifest['sourceHashes']) if hashes.get(p)!=manifest['sourceHashes'].get(p)]
    path=OUT/'evidence/release-solutions.json'
    routes=read(path)['boards'] if path.exists() else []
    fresh=not stale and len(routes)==len(read(active)['levels'])
    records=[]
    for library,path in [('active',active),('archive',LEGACY)]:
        records.extend(summarize(l,i,library,routes,fresh) for i,l in enumerate(read(path)['levels'],1))
    if fresh:
        for audit in read(OUT/'evidence/opening-audit.json')['boards']:
            records[audit['stage']-1]['openingAudit']=audit
    colors=[]
    for suffix in ['G','B','P','Y','5','6','7','8','9']:
        text=(CONTENT/f'RippleBox/Ripple_{suffix}.mat').read_text(encoding='utf-8')
        rgb=re.search(r'_BaseColor: \{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+)',text).groups()
        colors.append('#'+''.join(f'{round(float(v)*255):02x}' for v in rgb))
    result=dict(activeSource=active.relative_to(ROOT).as_posix(),archiveSource=LEGACY.relative_to(ROOT).as_posix(),
                activeCount=len(read(active)['levels']),verifiedAt=manifest['verifiedAt'],fresh=fresh,
                motifCount=len({l['title'].split('·')[0].strip() for l in read(active)['levels']}),
                staleSources=sorted(stale),testedCommit=manifest['commit'],testsPassed=manifest['testsPassed'],
                names=NAMES,rules=RULES,levels=records,colors=colors,
                unlocks=[dict(stage=s,type=t) for t,s in [('mine',14),('tube',21),('cupboard',41),('panel',61),('switch',81),('armor',101),('combo',121)]])
    (OUT/'catalog.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    template=Path(__file__).with_suffix('.html').read_text(encoding='utf-8')
    data=json.dumps(result,ensure_ascii=False).replace('<','\\u003c')
    (OUT/'index.html').write_text(template.replace('__CATALOG_DATA__',data).replace('__MOTIF_COUNT__',str(result['motifCount'])),encoding='utf-8')
    print(f'Catalog: {len(records)} layouts; {result["activeCount"]} release stages; evidence {"fresh" if fresh else "STALE"}')

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--accept-run',action='store_true');args=parser.parse_args()
    if args.accept_run:import_evidence()
    build()
