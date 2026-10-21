"""Review actual Unity aim-error trials without assigning human or GRP1 cohort rates."""
import hashlib
import html
import json
import shutil
import statistics
import xml.etree.ElementTree as ET
from pathlib import Path

from build_learning_report import TITLES, INTENTS

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'docs/bounce-learning-playtest'


def main():
    source = ROOT/'TestResults/BounceDifficulty/trials.json'
    data = json.loads(source.read_text(encoding='utf-8'))
    tests = ROOT/'TestResults/bounce-difficulty.xml'
    test_run = ET.parse(tests).getroot().attrib
    assert test_run['result'] == 'Passed' and int(test_run['failed']) == 0
    assert len(data['trials']) == 250
    levels = json.loads((ROOT/'Assets/_Project/Bounce/Content/bounce-learning-levels.json').read_text())['levels']
    witnesses = json.loads((ROOT/'TestResults/BounceLearning/solutions.json').read_text())['boards']
    assert len(witnesses) == 10 and all(w['replayPassed'] for w in witnesses)
    rows = []
    for stage in range(1, 11):
        groups = []
        for error in [0, 2, 5]:
            trials = [t for t in data['trials'] if t['stage'] == stage and t['errorDegrees'] == error]
            assert len(trials) == (1 if error == 0 else 12)
            assert all(t['budget'] == levels[stage-1]['balls'] for t in trials)
            assert all(len(t['shots'])+t['ballsLeft'] == t['budget'] for t in trials)
            wins = [t for t in trials if t['cleared']]
            groups.append(dict(errorDegrees=error, trials=len(trials), clears=len(wins),
                               noDirectTarget=sum(t['noDirectTarget'] for t in trials),
                               medianShots=statistics.median(len(t['shots']) for t in trials),
                               clearedMedianShots=statistics.median(len(t['shots']) for t in wins) if wins else None,
                               medianGameSeconds=round(statistics.median(t['gameSeconds'] for t in trials), 2),
                               meanRemainingObjectives=round(statistics.mean(t['remaining'] for t in trials), 2)))
        rows.append(dict(stage=stage, title=TITLES[stage-1], intent=INTENTS[stage-1],
                         budget=levels[stage-1]['balls'], witnessShots=len(witnesses[stage-1]['shots']), groups=groups))
    OUT.mkdir(parents=True, exist_ok=True)
    provenance = ['Assets/_Project/Bounce/Tests/PlayMode/BounceLearningDifficultyTests.cs',
                  'Assets/_Project/Bounce/Tests/PlayMode/BounceLearningFlowTests.cs',
                  'Assets/_Project/Bounce/Content/bounce-learning-levels.json',
                  'Assets/_Project/Bounce/Content/IngameConfig.asset',
                  'Assets/_Project/Bounce/Runtime/Presentation/BounceModule.cs',
                  'Assets/_Project/Bounce/Runtime/Presentation/BounceGimmicks.cs',
                  'ProjectSettings/TimeManager.asset', 'ProjectSettings/ProjectVersion.txt']
    review = [
        '1레벨은 넓은 중앙 목표로 발사와 공 소모를 익히는 판입니다. 첫 입력의 이해가 목적입니다.',
        f"2레벨은 반사를 알아보는 판이지만, 정확한 직선 조준에서 {rows[1]['budget']-rows[1]['groups'][0]['medianShots']:g}발만 남았습니다. 반사를 활용하지 못하는 플레이를 고려해 공 12발 시안을 재검증할 가치가 있습니다.",
        '3·4레벨은 좌우 묶음과 중앙 통로로 자연스러운 연속 충돌을 만들었습니다. 직선 조준에서도 반사 이득이 발생하므로 배치가 복잡해 보여도 공 압박은 크지 않았습니다.',
        f"5레벨은 짧은 복습이 목적이지만 정확한 조준에서 {rows[4]['groups'][0]['medianShots']:g}/{rows[4]['budget']}발을 사용했습니다. 복습판의 실수 허용을 위해 공 12발 시안을 우선 검토합니다.",
        '6레벨은 기뢰의 효과를 한 번에 이해시키고, 7레벨은 서로 다른 위치의 기뢰를 다시 겨냥하게 만듭니다. 기뢰 우선 정책은 이미 규칙을 이해한 상황입니다.',
        '8레벨은 한 번의 타격이 연쇄 폭발로 확대되는 보상 체험입니다. 클리어 압박보다 규칙의 확실한 전달을 택한 쉬운 판입니다.',
        '9레벨은 기뢰로 큰 묶음을 지운 뒤 남은 일반 블록으로 전환하고, 10레벨은 중앙 기뢰와 양쪽 목표를 조합해 학습을 마무리합니다.',
        '중앙을 정확히 맞히면 공이 되돌아오고, 조금 빗맞으면 반사로 더 많이 맞히기도 합니다. 오차가 커질수록 반드시 어려워진다고 해석할 수 없습니다.',
        '현재 곡선에서는 2·5레벨의 여유가 상대적으로 적고 기뢰 도입 뒤 난도가 낮아집니다. 초반 학습이라는 목적에는 맞지만, 매 레벨 점점 어려워지는 구성은 아닙니다.'
    ]
    for trial in data['trials']:
        if not trial['cleared']:
            empty_shots=sum(s['before']==s['after'] for s in trial['shots'])
            review.append(f"실패 사례: {trial['stage']}레벨, ±{trial['errorDegrees']:g}도, 시드 {trial['seed']}. "
                          f"{len(trial['shots'])}발 중 {empty_shots}발은 목표를 줄이지 못했고, 마지막에 {trial['remaining']}개가 남았습니다.")
    summary = dict(kind=data['kind'], date='2026-10-21', policy=data['policy'], timing=data['timing'],
                   totalTrials=250, liveClearAndReplayBoards=10, testCasesPassed=int(test_run['passed']),
                   seed=data['seed'], pairedNoiseAcrossErrorMagnitudes=True,
                   limitations=['자동 발사 테스트이며 사람이 마우스로 플레이한 기록이 아님.',
                                '±2도·±5도는 진단용 가정이며 초보·일반·고수의 실제 조준 오차로 보정하지 않음.',
                                '조건당 12회는 소규모 민감도 관찰. 정식 예비·최종 측정이나 유저 승률로 해석하지 않음.',
                                '보이는 기뢰를 우선 공격하므로 기뢰 규칙을 이해한 상황을 가정함.',
                                '공이 끝날 때까지 기다리며 동시 발사·조작 실수·이해도·판단 시간·모바일 입력은 미검증.',
                                '비행 시간은 판단·조작 시간을 제외하며 사람의 전체 플레이 시간이 아님.',
                                '직선 조준 정책이 실패해도 레벨이 불가능하다는 뜻은 아님. 별도 반사 경로의 실제 클리어·재현을 확인함.',
                                '이번 분석에서 스테이지 배치와 공 예산은 수정하지 않음.'],
                   hashNormalization='Text bytes with CRLF normalized to LF',
                   hashes={p: hashlib.sha256((ROOT/p).read_bytes().replace(b'\r\n',b'\n')).hexdigest() for p in provenance},
                   review=review, rows=rows)
    (OUT/'summary.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    shutil.copyfile(source, OUT/'trials.json')
    shutil.copyfile(tests, OUT/'test-results.xml')
    shutil.copyfile(ROOT/'TestResults/BounceLearning/solutions.json', OUT/'clear-and-replay.json')
    table = []
    for row in rows:
        zero, small, large = row['groups']
        precise = f"{'성공' if zero['clears'] else '실패'} · {zero['medianShots']:g}/{row['budget']}발"
        table.append(f"<tr><td>{row['stage']}</td><td><b>{row['title']}</b><br>{row['intent']}</td>"
                     f"<td>{row['witnessShots']}/{row['budget']}발</td><td>{precise}</td>"
                     f"<td>{small['clears']}/12</td><td>{large['clears']}/12</td>"
                     f"<td>{zero['medianGameSeconds']:g}초</td></tr>")
    page = '''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Bounce 학습 10레벨 · 플레이 진단</title><style>
*{box-sizing:border-box}body{margin:0;background:#f3f6fa;color:#20344b;font:16px/1.7 system-ui,"Malgun Gothic",sans-serif}main{max-width:1380px;margin:auto;padding:36px 24px}h1{font-size:32px;line-height:1.3}h2{font-size:22px;margin-top:32px}.note{background:#fff7e3;border-left:4px solid #deb257;border-radius:8px;padding:18px}.table{overflow-x:auto}table{border-collapse:collapse;width:100%;background:#fff;font-size:14px}th,td{text-align:left;padding:14px;border-bottom:1px solid #dce5ed}th{background:#dcefeb;white-space:nowrap}td:first-child{font-size:20px;font-weight:700}td:nth-child(2){min-width:260px;max-width:410px}td:nth-child(n+3){white-space:nowrap}a{color:#176e94}li{margin:8px 0}.review{display:grid;grid-template-columns:repeat(2,1fr);gap:16px}.review p{background:white;border-radius:12px;padding:18px;margin:0}@media(max-width:720px){main{padding:20px 16px}h1{font-size:26px}.review{grid-template-columns:1fr}}
</style><main><p>DEVELOP-BOUNCE · 2026.10.21</p><h1>클리어 가능한가, 조준이 빗나가도 괜찮은가?</h1>
<p>실제 Unity 게임에서 발사·반사·기뢰·배수·승패 처리를 실행했습니다. 250회 조준 진단과 별도 10판의 클리어 경로 재검증·재실행 결과입니다. 부스터·이어하기는 사용하지 않았습니다.</p>
<div class="note"><b>실제 게임을 실행한 자동 플레이 평가입니다.</b> 인간의 체감 난도나 유저 승률을 실측한 결과는 아닙니다. 12회 중 성공 수는 해당 조준 정책과 오차 가정에 한정됩니다. D1 등급을 확정하지 않습니다.</div>
<h2>스테이지별 제작 의도와 실행 결과</h2><p>반사 경로는 기존 탐색 방식으로 확인한 한 가지 해법입니다. 직선 조준은 보이는 기뢰 우선, 그다음 가까운 목표 중앙을 겨냥하며 반사를 미리 계산하지 않습니다. 오차 조건에서도 매 발사 후 현재 판을 다시 보고 목표를 정합니다.</p>
<div class="table"><table><thead><tr><th>레벨</th><th>제작 의도</th><th>반사 포함 경로<br>사용/지급 공</th><th>정확한 직선 조준<br>1회 결과</th><th>±2도<br>성공/시도</th><th>±5도<br>성공/시도</th><th>오차 0도<br>공 비행 시간</th></tr></thead><tbody>TABLE</tbody></table></div>
<p>오차 0도는 정상 속도, ±2도·±5도는 시간 배율 12로 실행했습니다. 물리 고정 간격은 변경하지 않았습니다. 비행 시간에는 사람이 생각하거나 조작하는 시간이 포함되지 않습니다.</p>
<h2>제작 의도와 비교한 평가</h2><div class="review">REVIEW</div>
<h2>판단할 때 주의할 점</h2><ul>LIMITATIONS</ul>
<p><a href="summary.json">조건별 집계·출처</a> · <a href="trials.json">250회 원본 발사 기록</a> · <a href="clear-and-replay.json">클리어·재현 경로</a> · <a href="test-results.xml">Unity 검사 결과</a> · <a href="../bounce-learning-v1/index.html">배치와 실제 화면 보기</a></p>
</main></html>'''
    page=page.replace('TABLE',''.join(table)).replace('REVIEW',''.join('<p>'+html.escape(s)+'</p>' for s in review)).replace('LIMITATIONS',''.join('<li>'+html.escape(s)+'</li>' for s in summary['limitations']))
    (OUT/'index.html').write_text(page,encoding='utf-8')
    for row in rows:
        print(row['stage'], 'route',row['witnessShots'], 'budget',row['budget'], [(g['errorDegrees'],g['clears'],g['trials'],g['medianShots']) for g in row['groups']])


if __name__=='__main__': main()
