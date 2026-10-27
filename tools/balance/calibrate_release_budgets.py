"""Calibrate only the requested range from successful discovery records; final replay must follow."""
import argparse, hashlib, json, math
from pathlib import Path
from build_release_levels import fingerprint
ROOT=Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser()
parser.add_argument('--first',type=int,default=1)
parser.add_argument('--last',type=int,default=140)
args=parser.parse_args()
assert 1<=args.first<=args.last<=140
levels=json.loads((ROOT/'Assets/_Project/Bounce/Content/bounce-release-levels.json').read_text(encoding='utf-8'))['levels']
records=json.loads((ROOT/'TestResults/BounceRelease/solutions.json').read_text())['boards']
assert len(records)>=args.last and all(r['cleared'] and r['replayPassed'] for r in records[args.first-1:args.last])
path=Path(__file__).with_name('release-budgets.json')
data=json.loads(path.read_text()) if path.exists() else dict(budgets={},hashes={})
budgets=data['budgets']
audit_path=ROOT/'TestResults/BounceRelease/opening-audit.json'
audit_report=json.loads(audit_path.read_text()) if args.first<=20 else None
audits=audit_report['boards'] if audit_report else []
if audit_report:
    assert audit_report['sourceSha256']==hashlib.sha256((ROOT/'Assets/_Project/Bounce/Content/bounce-release-levels.json').read_bytes()).hexdigest(), 'Stale opening audit'
for i,(level,record) in enumerate(zip(levels,records),1):
    assert record['stage']==i
    if not args.first<=i<=args.last:continue
    shots=record['shotsUsed']
    role=level['difficulty']
    factor,minimum={'도입':(1.5,12),'복습':(1.4,10),'일반':(1.32,8),'도전':(1.2,6),'휴식':(1.6,12)}[role]
    budget=max(shots+minimum,math.ceil(shots*factor))
    if i<=20:
        audit=next(a for a in audits if a['stage']==i)
        assert audit['plannedShots']==shots and audit['recoveryRemaining']==0 and audit['tolerantRemaining']==0
        factor,margin=(1.5,6) if i<=3 else (1.4,5) if role in ('도입','복습') else (1.2,3) if role=='도전' else (1.3,4)
        budget=max(math.ceil(shots*factor),shots+margin,audit['recoveryShots']+2,audit['tolerantShots']+3)
    budgets[str(i)]=budget
    data['hashes'][str(i)]=fingerprint(level)
path.write_text(json.dumps(data,indent=2)+'\n')
print('Calibrated requested range',args.first,'to',args.last)
