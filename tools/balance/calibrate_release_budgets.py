"""Author budget overrides from a complete successful discovery run; final replay must follow."""
import json, math
from pathlib import Path
from build_release_levels import fingerprint
ROOT=Path(__file__).resolve().parents[2]
levels=json.loads((ROOT/'Assets/_Project/Bounce/Content/bounce-release-levels.json').read_text(encoding='utf-8'))['levels']
records=json.loads((ROOT/'TestResults/BounceRelease/solutions.json').read_text())['boards']
assert len(records)==len(levels) and all(r['cleared'] and r['replayPassed'] for r in records)
budgets={}
for i,(level,record) in enumerate(zip(levels,records),1):
    assert record['stage']==i
    shots=record['shotsUsed'];blocks=sum(c.isdigit() for row in level['rows'] for c in row)
    role=level['difficulty']
    factor,minimum={'도입':(1.5,12),'복습':(1.4,10),'일반':(1.32,8),'도전':(1.2,6),'휴식':(1.6,12)}[role]
    budget=max(shots+minimum,math.ceil(shots*factor))
    if i<=3:budget=max(budget,blocks+5)
    elif i<=13:budget=max(budget,math.ceil(blocks*.9)+6)
    budgets[str(i)]=budget
data=dict(budgets=budgets,hashes={str(i):fingerprint(l) for i,l in enumerate(levels,1)})
Path(__file__).with_name('release-budgets.json').write_text(json.dumps(data,indent=2)+'\n')
print('Calibrated',len(budgets),'budgets:',min(budgets.values()),'to',max(budgets.values()))
