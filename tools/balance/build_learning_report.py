"""Build the offline review of the ten learning boards from data + real Unity results.
Run after BounceLearningFlowTests and the balance regression test suites.
This report deliberately does not estimate GRP1 cohort/human success rates.
"""
import hashlib
import json
import shutil
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'docs/bounce-learning-v1'
DATA = ROOT / 'Assets/_Project/Bounce/Content/bounce-learning-levels.json'
TITLES = ['첫 조준', '넓은 면과 반사', '좌우 목표 나누기', '벽과 중앙 통로', '짧은 복습',
          '기뢰 첫 만남', '두 묶음의 기뢰', '기뢰 연쇄', '기뢰와 남은 블록', '배운 규칙 마무리']
INTENTS = ['넓은 중앙 목표 6개로 조준과 소모되는 공을 이해합니다.',
           '마름모 배열의 넓은 면에서 튕기는 방향을 살펴봅니다.',
           '떨어진 두 묶음을 보며 조준 방향을 바꿉니다.',
           '중앙 통로와 벽 반사를 실험합니다. 하드 역할은 아닙니다.',
           '작은 중앙 묶음으로 부담을 낮추고 복습합니다.',
           '기뢰 앞을 비워 바로 맞힐 수 있게 합니다. 다른 새 기믹은 없습니다.',
           '높이가 다른 두 묶음에 기뢰를 다시 노출합니다.',
           '나란히 둔 기뢰가 함께 터지는 보상을 경험합니다.',
           '기뢰를 먼저 활용하고 떨어진 일반 블록을 정리합니다.',
           '익숙한 기뢰와 양쪽의 작은 목표로 학습을 마무리합니다.']
COLOURS = ['#82b83f', '#428cec', '#c667b1', '#eab448']


def sha(path):
    # Git normalizes these text assets; keep provenance portable across checkouts.
    return hashlib.sha256(path.read_bytes().replace(b'\r\n', b'\n')).hexdigest()


def board_svg(level):
    parts = ['<svg viewBox="0 0 270 390" role="img" aria-label="레벨 배치">',
             '<rect width="270" height="390" rx="12" fill="#e6f4f2"/>']
    for row, line in enumerate(level['rows']):
        for col, value in enumerate(line):
            if value != '.':
                parts.append(f'<rect x="{col*30+2}" y="{row*30+2}" width="26" height="26" rx="6" fill="{COLOURS[min(int(value)-1,3)]}"/>')
    for spec in level['gimmicks']:
        x,y=spec['col']*30+15,spec['row']*30+15
        parts.append(f'<circle cx="{x}" cy="{y}" r="12" fill="#35516e"/><circle cx="{x}" cy="{y-3}" r="6" fill="#ed7161"/>')
    parts.append('<path d="M125 378 L135 355 L145 378Z" fill="#176c7b"/></svg>')
    return ''.join(parts)


def main():
    levels = json.loads(DATA.read_text(encoding='utf-8'))['levels']
    solutions_path = ROOT/'TestResults/BounceLearning/solutions.json'
    solutions = json.loads(solutions_path.read_text(encoding='utf-8'))
    assert len(levels) == len(solutions['boards']) == 10
    suites=[]
    for name in ['bounce-learning-edit-final.xml','bounce-learning-play-final.xml']:
        p=ROOT/'TestResults'/name
        result=ET.parse(p).getroot().attrib
        assert result['result']=='Passed' and int(result['failed'])==0, name
        suites.append(dict(file=name,passed=int(result['passed']),failed=int(result['failed']),skipped=int(result['skipped'])))
    OUT.mkdir(parents=True,exist_ok=True)
    rows=[]; cards=[]
    for stage,(level,route) in enumerate(zip(levels,solutions['boards']),1):
        assert route['stage']==stage and route['budget']==level['balls'] and route['replayPassed']
        assert route['shots'][-1]['after']==0 and len(route['shots'])<=level['balls']
        blocks=sum(c!='.' for line in level['rows'] for c in line)
        row=dict(stage=stage,role='learning',sourceGrade=None,targetGrade=1,family='first',
                 title=TITLES[stage-1],intent=INTENTS[stage-1],seed=20261021,
                 balls=level['balls'],blocks=blocks,mines=len(level['gimmicks']),
                 measuredRates=None,trials=None,launchSuccessRate=None,
                 cohortStatus='미측정: 조준형 봇 프로필·조준 오차·템포 미확정',
                 witnessShots=len(route['shots']),remainingBalls=level['balls']-len(route['shots']),replayPassed=True)
        rows.append(row)
        image=f'level-{stage:02}.png'
        shutil.copyfile(ROOT/'TestResults/BounceLearning'/image, OUT/image)
        cards.append(f'''<article><div class="heading"><span class="number">{stage:02}</span><div><h2>{row['title']}</h2><span class="tag">학습 · 목표 D1</span></div></div>
          <div class="board">{board_svg(level)}<div><p>{row['intent']}</p>
          <dl><dt>발사 예산</dt><dd>{level['balls']}개</dd><dt>일반 블록 / 기뢰</dt><dd>{blocks} / {len(level['gimmicks'])}</dd>
          <dt>검증 경로 사용 공</dt><dd>{len(route['shots'])}개</dd><dt>재실행</dt><dd>클리어 확인</dd></dl>
          <a href="{image}" target="_blank">실제 플레이 화면 ↗</a></div></div>
          <details><summary>검증한 발사 각도 보기</summary><p>{', '.join(str(s['angle'])+'°' for s in route['shots'])}</p><small>정면 0°, 오른쪽 +. 최적해·사람 행동을 뜻하지 않습니다.</small></details></article>''')
    provenance = [DATA, ROOT/'Assets/_Project/Bounce/Editor/BounceLearningGenerator.cs',
                  ROOT/'Assets/_Project/Bounce/Tests/PlayMode/BounceLearningFlowTests.cs',
                  ROOT/'Assets/_Project/Bounce/Content/IngameConfig.asset',
                  ROOT/'Assets/_Project/Bounce/Runtime/Presentation/BounceModule.cs',
                  ROOT/'Assets/_Project/Bounce/Runtime/Presentation/BounceGimmicks.cs',
                  ROOT/'ProjectSettings/ProjectVersion.txt', ROOT/'ProjectSettings/TimeManager.asset']
    record=dict(date='2026-10-21',version='bounce-learning-v1',seed=20261021,
                unityVersion=(ROOT/'ProjectSettings/ProjectVersion.txt').read_text().splitlines()[0].split(': ',1)[1],
                references=['docs/Balance_설계_적용_가이드.html','docs/밸런스_기준_한눈에_보기.html'],
                decisions=['1~10 모두 D1 목표 학습 구간. L4·L9에 강제 허들 없음.',
                           'L6 기뢰 단독 도입, L7~L10 반복. 색상은 내구도가 아님.',
                           '이동 패널·장갑·연속 타격 등 복합 기믹은 이번 학습 10판에서 제외.',
                           '고정 시드의 제약 템플릿 생성. 기존 22판은 별도 검증 카탈로그로 보존.',
                           '1차 물리 검증에서 L4 12/14발 사용을 확인해 예산을 18발로 완화.',
                           '기존 재시작은 같은 배치. 별도 재도전 변형·구제·보상 변경 없음.'],
                botDraft=dict(profileId='bounce-aim-draft-v1',confirmedBy=None,
                              move='공 1발 조준·발사부터 배수 또는 20초 수명 종료까지',
                              legal='런타임 ±75° 연속 조준. 유한 각도 탐색은 그 근사이며 모든 합법 입력이 아님.',
                              novice='제안: 화면에서 직접 보이는 가까운 목표를 먼저 조준',
                              intermediate='제안: 1발 뒤 결과 예측',expert='제안: 2발 뒤 결과 예측',
                              randomPick=dict(intermediate=.173932,expert=.090005),
                              weights=dict(novice=0,intermediate=.5,expert=.5),
                              unresolved=['조준 오차 분포','동시 발사·발사 간격 모델','예측 후보 이산화','평가와 동점 처리','대표 D2~D5 눈금 검증'],
                              information='숨김 정보 없음',excluded=['booster','continue','cross-trial-learning']),
                validation=dict(suites=suites,clearedBoards=10,replayedBoards=10,
                                method='실제 BounceModule.Fire / FixedUpdate와 Unity 충돌체. 부스터 없이 발견한 각도열을 새 보드에서 재실행.',
                                search='0.5도 간격 ±75도, 실제 SphereCast와 최대 8회 벽 반사로 도달 목표를 찾는 진단 탐색. 정식 GRP1 봇 아님.',
                                limitations=['클리어 경로 검증은 성공률 추정·최적해 증명이 아님.',
                                             '정식 초보·일반·고수 예비 200~1,000회와 독립 10,000회 측정은 수행하지 않음.',
                                             'D1 80~100%는 설계 목표 가설. 사람 승률 및 모바일 실기 난도 미검증.',
                                             '이번 10판은 학습 전용이므로 D2~D5 서열·하드/회복 리듬을 검증하지 않음.',
                                             '기존 런타임의 카탈로그 순환 유지: 11레벨부터 이 10개 배치가 반복됨.']),
                hashNormalization='Text bytes with CRLF normalized to LF',
                hashes={p.relative_to(ROOT).as_posix():sha(p) for p in provenance},
                rows=rows)
    (OUT/'balance-report.json').write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    shutil.copyfile(solutions_path, OUT/'solutions.json')
    shutil.copyfile(ROOT/'TestResults/BounceLearning/guide-mine.png', OUT/'guide-mine.png')
    for suite in suites: shutil.copyfile(ROOT/'TestResults'/suite['file'],OUT/suite['file'])
    count=sum(s['passed'] for s in suites)
    page='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Bounce · 학습 레벨 1–10</title><style>
*{box-sizing:border-box}body{margin:0;background:#f3f6fa;color:#20344b;font:16px/1.7 system-ui,"Malgun Gothic",sans-serif}main{max-width:1200px;margin:auto;padding:40px 24px}h1{font-size:38px;line-height:1.25;margin:12px 0}h2{font-size:21px;margin:0}.eyebrow{color:#247b86;font-weight:700;letter-spacing:.06em}.lead{max-width:850px}.note{padding:18px 24px;border-left:4px solid #ddaf4e;background:#fff9e9;border-radius:8px;margin:24px 0}.metrics{display:flex;gap:16px;flex-wrap:wrap}.metrics div{background:#e0f0ed;border-radius:12px;padding:16px 24px}.metrics b{display:block;font-size:28px}.grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:22px;margin-top:28px}article{background:white;padding:24px;border-radius:18px;box-shadow:0 4px 20px #16384e08}.heading{display:flex;align-items:center;gap:16px}.number{font-size:34px;color:#29828b;font-weight:800}.tag{font-size:13px;color:#527482}.board{display:grid;grid-template-columns:42% 1fr;gap:20px;margin:18px 0}.board svg{width:100%;max-height:270px}.board p{margin:0 0 16px}dl{display:grid;grid-template-columns:1fr auto;font-size:14px;gap:4px}dd{margin:0;font-weight:700}a{color:#176e94}details{border-top:1px solid #e4ebef;padding-top:12px;font-size:14px}summary{cursor:pointer}section{margin:32px 0}code{background:#e6edf2;padding:2px 6px;border-radius:4px;overflow-wrap:anywhere}li{margin:8px 0}@media(max-width:760px){main{padding:24px 16px}h1{font-size:29px}.grid{grid-template-columns:1fr}article{padding:18px}.board{gap:14px}.metrics div{flex:1;padding:12px}.metrics b{font-size:23px}}@media print{.grid{grid-template-columns:1fr 1fr}article{break-inside:avoid;box-shadow:none;border:1px solid #ddd}details{display:none}}
</style><main><div class="eyebrow">DEVELOP-BOUNCE · 2026.10.21 · 학습 시안 v1</div>
<h1>조준을 익히고,<br>기뢰로 한 번에 터뜨리기</h1>
<p class="lead">GRP1 문서의 1~13 학습 원칙을 적용한 첫 10개 레벨입니다. 판은 9×13으로 유지하고, 목표 분산·통로·기뢰 배치·공 여유로 경험을 바꿉니다. L4·L9에도 하드 표시나 강제 허들을 넣지 않았습니다.</p>
<div class="metrics"><div><b>10 / 10</b>실제 물리 클리어</div><div><b>10 / 10</b>동일 경로 재실행</div><div><b>TEST_COUNT개</b>자동 검사 통과</div><div><b>L6 → L10</b>기뢰 도입·반복</div></div>
<div class="note"><strong>D1은 제작 목표이며 검증된 승률이 아닙니다.</strong><br>조준형 봇의 오차·템포가 아직 확정되지 않았습니다. 아래 공 사용량은 발견한 클리어 경로 한 개의 기록입니다. 일반·고수 50:50 성공률이나 사람 승률로 해석하지 않습니다.</div>
<div class="grid">CARDS</div>
<section><h2>확인한 것과 남은 것</h2><p>실제 게임의 발사·반사·기뢰 연쇄·배수·승리 조건으로 부스터 없이 클리어하고, 새 보드에서 같은 각도열을 재실행했습니다. 첫 검증에서 4레벨은 14발 중 12발을 사용해 예산을 18발로 늘렸습니다.</p><p>초보·일반·고수 봇 프로필, 조준 오차와 발사 템포, 예비 200~1,000회 및 독립 10,000회 측정은 후속 밸런스 작업입니다. 전부 학습 목표인 이번 10판만으로 D2~D5를 가르는 눈금을 확정하지 않습니다.</p></section>
<section><h2>프로젝트에서 사용하기</h2><ul><li>플레이 데이터: <code>Assets/_Project/Bounce/Content/bounce-learning-levels.json</code></li><li>Unity 실행(Play) 중 <code>BK Kit → Level Editor</code>에서 1~10레벨을 선택하고 <code>저장하고 플레이</code>. 이 버튼은 로컬 샌드박스 진행도와 부스터 수량을 변경합니다.</li><li>동일 시안 재생성: <code>BK Kit → Balance → Generate Learning Levels 1-10</code>. 고정 시드 20261021이며 수동 편집한 학습 데이터를 덮어씁니다.</li><li>기존 <code>bounce-levels.json</code>의 22개 배치는 회귀 검사용으로 보존했습니다.</li><li>현재 런타임의 순환 규칙을 유지해 11레벨부터 학습 10개 배치가 반복됩니다. 후속 레벨은 별도 제작 대상입니다.</li></ul></section>
<p><a href="balance-report.json">설계·검증 JSON</a> · <a href="solutions.json">발사 경로 원본</a> · <a href="guide-mine.png">첫 기뢰 안내 화면</a></p>
<p>근거: <a href="../Balance_설계_적용_가이드.html">Balance 설계·적용 가이드</a> §3.1·3.3·3.8·4·5 및 <a href="../밸런스_기준_한눈에_보기.html">밸런스 기준 한눈에 보기</a>. 참고 문서의 공통 기준과 이번 게임별 제작 가정을 구분했습니다.</p></main></html>'''
    page=page.replace('TEST_COUNT',str(count)).replace('CARDS',''.join(cards))
    (OUT/'index.html').write_text(page,encoding='utf-8')
    print(f'Report: {OUT}/index.html; {count} tests; 10 replayed boards')


if __name__=='__main__': main()
