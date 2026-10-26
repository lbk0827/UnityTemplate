"""Build the offline level catalog. --accept-run imports a fresh, passing Unity run.

Unity filter: BounceLearningFlowTests;BounceLearningDifficultyTests
Results: TestResults/level-catalog-play.xml
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
import statistics
import subprocess
import xml.etree.ElementTree as ET

from build_learning_report import TITLES, INTENTS

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'docs/bounce-level-catalog'
CONTENT = ROOT / 'Assets/_Project/Bounce/Content'
LEARNING = CONTENT / 'bounce-learning-levels.json'
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
    return hashlib.sha256(path.read_bytes().replace(b'\r\n', b'\n')).hexdigest()


def sources():
    paths = [LEARNING, LEGACY, CONTENT/'Presentation.asset', CONTENT/'IngameConfig.asset',
             ROOT/'ProjectSettings/TimeManager.asset', ROOT/'ProjectSettings/DynamicsManager.asset', ROOT/'ProjectSettings/TagManager.asset',
             ROOT/'ProjectSettings/ProjectVersion.txt']
    paths += list((ROOT/'Assets/_Project/Bounce/Runtime').rglob('*.cs'))
    paths += [ROOT/f'Assets/_Project/Bounce/Tests/PlayMode/{name}.cs' for name in ['BounceLearningFlowTests', 'BounceLearningDifficultyTests']]
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
    queue = block_guids + catalog_guids
    seen = set()
    while queue:
        guid = queue.pop()
        if guid in seen or guid not in metas:
            continue
        seen.add(guid)
        p = metas[guid]
        if p.suffix not in ('.prefab', '.asset', '.fbx', '.obj'):
            continue
        paths.append(p)
        if p.suffix in ('.prefab', '.asset'):
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
    source = ROOT/'TestResults'
    tests = ET.parse(source/'level-catalog-play.xml').getroot()
    assert tests.attrib['result'] == 'Passed' and int(tests.attrib['failed']) == 0, 'Unity test run failed'
    required = {'TenProductionLearningBoardsClearAndReplayWithoutBoosters',
                'MeasureDirectAimWithZeroTwoAndFiveDegreeErrors',
                'FirstMineGuideShowsTheMineRuleAndPreservesTheBallBudget'}
    passed = {case.attrib['name'] for case in tests.iter('test-case') if case.attrib['result'] == 'Passed'}
    assert required <= passed, 'Missing required runtime checks'
    start = datetime.fromisoformat(tests.attrib['start-time'].replace('Z', '+00:00'))
    start = start.replace(tzinfo=timezone.utc) if start.tzinfo is None else start
    paths = sources()
    assert all(p.stat().st_mtime <= start.timestamp()+2 for p in paths), 'A gameplay source changed after the run started; rerun tests'
    witness = read(source/'BounceLearning/solutions.json')['boards']
    trials = read(source/'BounceDifficulty/trials.json')['trials']
    assert len(witness) == 10 and all(w['replayPassed'] for w in witness)
    assert len(trials) == 250
    evidence = OUT/'evidence'
    evidence.mkdir(parents=True, exist_ok=True)
    for src, dst in [(source/'level-catalog-play.xml', 'test-results.xml'),
                     (source/'BounceLearning/solutions.json', 'solutions.json'),
                     (source/'BounceDifficulty/trials.json', 'trials.json')]:
        assert src.stat().st_mtime >= start.timestamp()-2, f'Stale result file: {src}'
        shutil.copyfile(src, evidence/dst)
    for stage in range(1, 11):
        image = source/f'BounceLearning/level-{stage:02}.png'
        assert image.stat().st_mtime >= start.timestamp()-2, f'Stale screenshot: {image}'
        shutil.copyfile(image, evidence/image.name)
    manifest = dict(verifiedAt=tests.attrib['end-time'], testsPassed=int(tests.attrib['passed']),
                    commit=subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
                    hashNormalization='CRLF normalized to LF',
                    sourceHashes={p.relative_to(ROOT).as_posix(): sha(p) for p in paths})
    (evidence/'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')


def summarize(level, stage, library, routes, trials, fresh):
    blocks = sum('1' <= c <= '9' for row in level['rows'] for c in row)
    gimmicks = level.get('gimmicks') or []
    counts = Counter(g['type'] for g in gimmicks)
    objectives = blocks + sum(g['type'] not in ('panel', 'switch', 'door') for g in gimmicks)
    current = library == 'active'
    record = dict(id=f'{library}-{stage}', stage=stage, library=library, balls=level['balls'], blocks=blocks,
                  objectives=objectives, columns=len(level['rows'][0]), height=len(level['rows']),
                  rows=level['rows'], gimmicks=gimmicks, gimmickCounts=dict(counts),
                  gimmickLabel=' · '.join(f'{NAMES[k]} {n}' for k, n in counts.items()) or '없음',
                  title=TITLES[stage-1] if current and stage <= 10 else f'보관 레벨 {stage:02}',
                  intent=INTENTS[stage-1] if current and stage <= 10 else '이전 배치 및 기믹 검증용 라이브러리. 현재 플레이에 미연결.',
                  grade='학습 D1 목표 · 체감 등급 미확정' if current else '미평가',
                  status='재검증 필요' if current else '클리어 미검증', pressure='미평가', groups=[], route=None)
    if not current or not fresh:
        return record
    route = next(w for w in routes if w['stage'] == stage)
    assert route['objectives'] == objectives and route['budget'] == level['balls']
    assert route['replayPassed'] and route['shots'][-1]['after'] == 0 and len(route['shots']) <= level['balls']
    record.update(status='클리어·재현 확인', route=route, witnessShots=len(route['shots']), spareBalls=level['balls']-len(route['shots']))
    for error in [0, 2, 5]:
        group = [t for t in trials if t['stage'] == stage and t['errorDegrees'] == error]
        assert len(group) == (1 if error == 0 else 12)
        assert all(t['budget'] == level['balls'] and len(t['shots'])+t['ballsLeft'] == t['budget'] for t in group)
        record['groups'].append(dict(error=error, wins=sum(t['cleared'] for t in group), count=len(group),
                                     medianShots=statistics.median(len(t['shots']) for t in group),
                                     medianSeconds=round(statistics.median(t['gameSeconds'] for t in group), 1)))
    zero = record['groups'][0]
    usage = zero['medianShots']/level['balls']
    record['pressure'] = '빠듯' if not zero['wins'] or usage > .75 else '보통' if usage > .5 else '여유'
    record['consumption'] = round(usage*100)
    record['note'] = ('정확한 직선 조준에서도 탄 여유가 적습니다. 중앙 타격만 반복하기보다 반사 경로를 살펴보세요.' if record['pressure'] == '빠듯' else
                      '기뢰 규칙을 이해하고 먼저 맞히면 적은 발사로 정리할 수 있습니다.' if counts.get('mine') else
                      '직선 조준과 자연스러운 반사만으로도 탄 여유가 관찰됐습니다.')
    if any(g['wins'] < g['count'] for g in record['groups'][1:]):
        record['note'] += ' 조준 오차 실험에서 실패 사례가 있어 실수 허용폭을 점검할 필요가 있습니다.'
    return record


def build():
    active = active_source()
    assert active == LEARNING, 'The active library changed; update the learning evidence policy before publishing'
    manifest = read(OUT/'evidence/manifest.json')
    current_hashes = {p.relative_to(ROOT).as_posix(): sha(p) for p in sources()}
    stale = [p for p in set(current_hashes) | set(manifest['sourceHashes']) if current_hashes.get(p) != manifest['sourceHashes'].get(p)]
    routes = read(OUT/'evidence/solutions.json')['boards']
    trials = read(OUT/'evidence/trials.json')['trials']
    records = []
    for library, path in [('active', active), ('archive', LEGACY)]:
        for stage, level in enumerate(read(path)['levels'], 1):
            records.append(summarize(level, stage, library, routes, trials, not stale))
    result = dict(activeSource=active.relative_to(ROOT).as_posix(), archiveSource=LEGACY.relative_to(ROOT).as_posix(),
                  activeCount=len(read(active)['levels']), verifiedAt=manifest['verifiedAt'], fresh=not stale,
                  staleSources=sorted(stale), testedCommit=manifest['commit'], testsPassed=manifest['testsPassed'],
                  names=NAMES, rules=RULES, levels=records)
    (OUT/'catalog.json').write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    template = Path(__file__).with_suffix('.html').read_text(encoding='utf-8')
    data = json.dumps(result, ensure_ascii=False).replace('<', '\\u003c')
    (OUT/'index.html').write_text(template.replace('__CATALOG_DATA__', data), encoding='utf-8')
    print(f'Catalog: {len(records)} layouts; active {result["activeCount"]}; evidence {"fresh" if not stale else "STALE"}')
    for record in records[:10]:
        print(f'L{record["stage"]}: blocks={record["blocks"]}, balls={record["balls"]}, '
              f'direct-aim usage={record.get("consumption", "unverified")}%, replay={bool(record["route"])}')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--accept-run', action='store_true')
    args = parser.parse_args()
    if args.accept_run:
        import_evidence()
    build()
