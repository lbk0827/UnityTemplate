"""Deterministic launch campaign. Run this, then BK Kit > Balance > Apply Release Campaign.

Budgets in release-budgets.json are measured overrides, never inferred human win rates.
"""
import json,hashlib,math
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
CONTENT=ROOT/'Assets/_Project/Bounce/Content'
UNLOCKS={'mine':14,'tube':21,'cupboard':41,'panel':61,'switch':81,'armor':101,'combo':121}
NAMES={'mine':'기뢰','tube':'금속관','cupboard':'찬장','panel':'이동 판','switch':'스위치·문','armor':'방향 장갑','combo':'연속 타격'}
CHAPTERS=['산호 해안','과수원','바다 공방','도자기 정원','움직이는 항구','비밀 수문','장갑 섬','메아리 성']

def shapes():
    old=json.loads((CONTENT/'bounce-learning-levels.json').read_text(encoding='utf-8'))['levels']
    result=[]
    for l in old:
        a=[list(r) for r in l['rows']]
        for g in l.get('gimmicks',[]): a[g['row']][g['col']]='5' if '딸기' in l['title'] else '8'
        occupied=[(x,y) for y,r in enumerate(a) for x,c in enumerate(r) if c!='.']
        x0,x1=min(x for x,y in occupied),max(x for x,y in occupied)
        y0,y1=min(y for x,y in occupied),max(y for x,y in occupied)
        result.append((l['title'],[''.join(r[x0:x1+1]) for r in a[y0:y1+1]]))
    extra=[
      ('황금 별',["......4......",".....444.....",".....444.....","4444444444444",".44444444444.","..444444444..","...4444444...","..444444444..","..444...444..",".444.....444."]),
      ('행운 클로버',["...111.111...","..111111111..","..118111811..","...1111111...",".11111111111.","1111111111111","1181111111811","1111111111111",".11111111111.","...11.1.11...","......1......","......11....."]),
      ('산호 물고기',[".....6666......","...66666666....","..6666666666..6",".66886666666666","666986666666666","666886666666666",".66666666666666","..6666666666..6","...66666666....",".....6666......"]),
      ('체리 한 쌍',[".......11......","......1111.....",".....11911.....","....99..99.....","...9......9....",".55555..55555..","55585555558555.","55585555558555.","55555555555555.",".55555..55555..","..555....555..."]),
      ('보랏빛 포도',["....11111....","...1119111...","......9......","..3333.3333..",".33833333833.",".33333333333.","..333333333..","...3333333...","....33333....",".....333.....","......3......"]),
      ('레몬 조각',["....44444....","..444444444..",".44888888844.","4484448444844","4844848444484","4884888884884","4844484844484","4484448444844",".44888888844.","..444444444..","....44444...."]),
      ('무지개 우산',["......9......","...3333333...","..333444333..",".33344444333.","3334447444333","3344477744433","3444777774443","3.4.7.7.7.4.3","......9......","......9......","...9..9......","...9999......"]),
      ('열기구',["....33333....","..333444333..",".33344444333.","3334447444333","3334447444333","3334447444333",".33344444333.","..333444333..","...3333333...","....9...9....","....9...9....","....66666....","....68886....","....66666...."]),
      ('붉은 버섯',["....55555....","..555885555..",".55588855555.","5555585558855","5585555558855","5585555555555","5555555555555","....88888....","....88888....","....88888....","...8888888...","..888888888.."]),
      ('초록 나무',[".....111.....","...1111111...","..111111111..",".11118111111.","1111181111111","1111111111111",".11111111111.","..111111111..",".....999.....",".....999.....",".....999.....","....99999....","...1111111..."]),
      ('아이스크림',["....33333....","..333333333..",".33833333333.","3338833333333","3333333333333","3333333333333",".44444444444.",".46446446444.","..446446444..","...4446444...","....44444....",".....444.....","......4......"]),
      ('따뜻한 찻잔',["...8..8..8.....","...8..8..8.....","..8..8..8......","77777777777....","78888888887777.","777777777777.77","777777777777.77","777777777777.77",".7777777777777.","..777777777....","88888888888888."]),
      ('황금 왕관',["4.....4.....4","44...444...44","444.44444.444","4444444444444","4484443444844","4444444444444","4444444444444","4444444444444",".99999999999."]),
      ('산호 게',["66.........66","666.......666","6666.....6666",".66..9.9..66.","..666666666..",".66666666666.","6666666666666","6666666666666",".66666666666.",".66.66.66.66.","66..66.66..66"]),
      ('보라 문어',["....33333....","..333333333..",".33833338333.","3339333393333","3333333333333","3333333333333",".33333333333.","..333333333..",".33.33333.33.","33..33.33..33","33.33...33.33",".333.....333."]),
      ('바다 조개',["......3......","...3333333...","..338383833..",".33838383833.","3338383838333","3383838383833","3338383838333",".33383838333.","..333838333..","...3333333...","....88888...."]),
      ('등대',["....55555....","...5555555...","....88888....","....89998....","....89998....","...8888888...","...5555555...","...5555555...","...8888888...","...8889888...","...5559555...","..555595555..","..888898888..",".88888888888.","7777777777777"]),
      ('고양이',["33.......33","333.....333","3333...3333","33333333333","33833333833","33933333933","33333333333",".333393333.","..3333333..","..3333333..",".333333333.","33333333333","3333...3333"]),
      ('작은 집',["......5......",".....555.....","....55555....","...5555555...","..555555555..",".55555555555.","5555555555555","..888888888..","..822888228..","..822888228..","..888898888..","..888898888..","..888898888..","..999999999.."]),
      ('음표',["....999999999","....999999999","....99.....99","....99.....99","....99.....99","....99.....99","....99.....99","..3333...3333",".33333..33333","333333.333333","33333..33333.",".333....333.."]),
      ('보석',["...7777777...","..778878877..",".77887887877.","7777777777777",".27772777277.","..277277277..","...2727277...","....27777....",".....277.....","......2......"]),
      ('바다 닻',[".....777.....","....77877....","....77877....",".....777.....","......7......","..777777777..","......7......","......7......","7.....7.....7","77....7....77","777...7...777",".77777777777.","..777777777..","....77777...."]),
      ('눈꽃',["7.....7.....7",".7....7....7.","..7...7...7..","...7..7..7...","....7.7.7....",".....777.....","7777777777777",".....777.....","....7.7.7....","...7..7..7...","..7...7...7..",".7....7....7.","7.....7.....7"]),
      ('튤립 화분',["..3...3...3..","..33.333.33..","..333333333..","..338333333..","..333333333..","...3333333...","....33333....","......1......","..11..1..11..","...1111111...","...6666666...","....68886....","....66666....",".....666....."]),
      ('선물 상자',["...33...33...","..3333.3333..","..333333333..","...3333333...","4444433344444","4888833388884","4888833388884","3333333333333","3333333333333","4888833388884","4888833388884","4444433344444"]),
    ]
    result.extend(extra)
    order=[0,1,3,10,11,13,14,12,5,4,8,15,2,6,7,16,17,18,19,20,21,22,23,24,25,26,27,28,29,30,31,32,33,34,9]
    portrait=[]
    for i in order:
        name,art=result[i]
        # Fish and whale swim upward; keep square cells and the complete silhouette.
        if i in (2,12):
            art=[''.join(row[x] for row in reversed(art)) for x in range(len(art[0]))]
        width=len(art[0]);height=len(art)
        new_width=min(11,width);new_height=min(11,height)
        if new_width%2==0:new_width-=1
        art=[''.join(art[min(height-1,int((y+.5)*height/new_height))][min(width-1,int((x+.5)*width/new_width))]
                     for x in range(new_width)) for y in range(new_height)]
        # Preserve thin motifs as readable clusters after reducing the authored footprint.
        cells=[list(row) for row in art]
        while sum(c!='.' for row in cells for c in row)<44:
            choices=[(x,y) for y in range(1,new_height-1) for x in range(1,new_width-1)
                     if cells[y][x]=='.' and sum(cells[y+dy][x+dx]!='.' for dx,dy in [(-1,0),(1,0),(0,-1),(0,1)])>=2]
            if not choices:break
            x,y=min(choices,key=lambda p:(p[0]-new_width/2)**2+(p[1]-new_height/2)**2)
            cells[y][x]=next(cells[y+dy][x+dx] for dx,dy in [(-1,0),(1,0),(0,-1),(0,1)] if cells[y+dy][x+dx]!='.')
        art=[''.join(row) for row in cells]
        portrait.append((name,art))
    return portrait

def fingerprint(level):
    data={k:level[k] for k in ['rows','gimmicks','gridRows','difficulty']}
    return hashlib.sha256(json.dumps(data,sort_keys=True,separators=(',',':')).encode()).hexdigest()

def build(count=140):
    library=shapes()
    budgets_path=Path(__file__).with_name('release-budgets.json')
    budgets=json.loads(budgets_path.read_text()) if budgets_path.exists() else {}
    levels=[]
    for stage in range(1,count+1):
        # 35 motifs; interleave themes across chapters instead of repeating ten-level loops.
        index=(stage-1)%len(library);variant=(stage-1)//len(library)
        name,art=library[index]
        intro=next((t for t,s in UNLOCKS.items() if s==stage),'')
        available=[t for t,s in UNLOCKS.items() if s<=stage]
        chapter=sum(stage>=s for s in UNLOCKS.values())
        lesson=intro or (available[-1] if available and stage-UNLOCKS[available[-1]]<=2 else '')
        role='도입' if intro else '복습' if lesson else '도전' if stage%10==0 else '휴식' if stage%10 in (1,6) and stage>13 else '일반'
        margin=0
        width=max(map(len,art))+2*margin
        height=min(16,max(13,len(art)+4+variant%2))
        # Dense, legible source art; each later encounter has a different interior route.
        cells=[['.']*width for _ in range(height)]
        for y,row in enumerate(art):
            for x,c in enumerate(row):
                cells[y+1][x+margin]=c
        if variant:
            for y in range(3,len(art)-1):
                for x in range(margin+2,width-margin-2):
                    if all(cells[y+dy][x+dx]!='.' for dx,dy in [(0,0),(-1,0),(1,0),(0,-1),(0,1)]) and (x+2*y+variant)% (8+variant)==0:
                        cells[y][x]='.'
        # The thin anchor has no solid interior to carve; open its lower return lane.
        if index==30 and variant==3:
            cells[len(art)-2][width//2]='.'
        specs=[];ring_cells=set()
        def occupied(x,y):return any((x,y) in footprint(g) for g in specs)
        def add(g):
            for x,y in footprint(g):
                if not(0<=x<width and 0<=y<height) or occupied(x,y) or (x,y) in ring_cells:return False
            for x,y in footprint(g):cells[y][x]='.'
            specs.append(g);return True
        selected=[]
        if lesson:selected=[lesson]
        elif available and role!='휴식':
            selected=[available[(stage+variant)%len(available)]]
            if len(available)>1:selected.append(available[(stage+variant+1)%len(available)])
            if chapter>=4 and stage%3==0:selected.append('mine')
        selected=list(dict.fromkeys(selected))
        if stage==count:selected=list(UNLOCKS)
        # Reserve the cupboard and its full ten-cell ring before placing embedded targets.
        if 'cupboard' in selected:
            found=False
            for y in range(3,max(4,len(art)-2)):
                for x in range(2,width-4):
                    ring=[(x+i,y-1) for i in range(3)]+[(x+i,y+2) for i in range(3)]+[(x-1,y),(x-1,y+1),(x+3,y),(x+3,y+1)]
                    if all(cells[b][a]!='.' for a,b in ring):
                        add({'type':'cupboard','col':x,'row':y});found=True;break
                if found:break
            if not found:
                x=width//2-1;y=height-5
                for a in range(x-1,x+4):
                    for b in range(y-1,y+3):cells[b][a]='8'
                add({'type':'cupboard','col':x,'row':y})
        ring_cells=set()
        for g in specs:
            if g['type']=='cupboard':
                for x,y in footprint(g):
                    for dx,dy in [(-1,0),(1,0),(0,-1),(0,1)]:ring_cells.add((x+dx,y+dy))
        if 'mine' in selected:
            choices=[(x,y) for y in range(2,len(art)) for x in range(2,width-2)
                     if cells[y][x]!='.' and (x,y) not in ring_cells and not occupied(x,y)]
            if choices:
                x,y=choices[(stage*7)%len(choices)];add({'type':'mine','col':x,'row':y})
                if not lesson and stage%4==0:
                    x,y=choices[(stage*7+len(choices)//2)%len(choices)];add({'type':'mine','col':x,'row':y})
        if 'tube' in selected:
            # Dedicated lower lane, clear furnace + 2..4 segments.
            add({'type':'tube','col':2,'row':height-3,'length':2 if lesson else 3+stage%2})
        if 'panel' in selected:
            specs.append({'type':'panel','col':width//2,'row':height-1,'width':1.5 if lesson else 2,'travel':1,'speed':.65 if lesson else .8})
        if 'switch' in selected:
            add({'type':'switch','col':width-2,'row':height-3,'link':'A'})
            add({'type':'door','col':width//2,'row':height-2,'link':'A'})
        if 'armor' in selected:
            add({'type':'armor','col':width//2,'row':height-3,'weakSide':0 if lesson else stage%2})
        if 'combo' in selected:
            # An open upper pocket gives the same ball room to return via the top/side walls.
            x=width//2
            for y in range(0,4):
                for a in range(max(0,x-2),min(width,x+3)):
                    if not occupied(a,y) and (a,y) not in ring_cells:cells[y][a]='.'
            add({'type':'combo','col':x,'row':2})
        blocks=sum(c!='.' for r in cells for c in r)
        budget=blocks+sum(g.get('length',1) for g in specs if g['type'] in ('tube','armor','combo'))+12
        intent=(f'{NAMES[intro]}의 규칙을 먼저 익히는 도입 레벨.' if intro else
                '앞에서 배운 규칙을 단독으로 반복하며 조준을 익힙니다.' if lesson else
                '기믹 없이 사물의 윤곽과 반사 경로를 즐기는 휴식 레벨.' if role=='휴식' else
                '이미 배운 기믹을 조합해 통로를 열고 사물의 안쪽을 공략합니다.' if specs else
                '넓은 면과 외곽 반사를 차례로 익히는 일반 블록 레벨.')
        level=dict(title=f'{name} · {variant+1}',intent=intent,chapter=CHAPTERS[chapter],difficulty=role,
             introduction=intro,gridRows=height,balls=budget,rows=[''.join(r) for r in cells],gimmicks=specs)
        if stage<=20:
            from build_opening_levels import opening
            level=opening(stage,level)
        if budgets.get('hashes',{}).get(str(stage))==fingerprint(level):
            level['balls']=budgets['budgets'][str(stage)]
        levels.append(level)
    (CONTENT/'bounce-release-levels.json').write_text(json.dumps(dict(campaign=True,levels=levels),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    motifs=len({level['title'].split('·')[0].strip() for level in levels})
    print(f'{len(levels)} release levels / {motifs} motifs')
    for t,s in UNLOCKS.items():print(s,t)

def footprint(g):
    x,y=g['col'],g['row'];t=g['type']
    if t=='cupboard':return [(x+a,y+b) for a in range(3) for b in range(2)]
    if t=='tube':return [(x+a,y) for a in range(-1,g.get('length',3))]
    if t=='panel':return []
    return [(x,y)]

if __name__=='__main__':build()
