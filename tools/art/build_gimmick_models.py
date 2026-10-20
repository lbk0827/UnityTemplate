"""Author editable Blender models and export Unity-coordinate meshes.
Run: blender --background --python tools/art/build_gimmick_models.py -- <output-dir>
Coordinates deliberately use Unity's Y-up convention, with unchanged mesh winding.
No external models, textures or generators are required.
"""
import bpy, math, json, sys
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree

OUT = Path(sys.argv[sys.argv.index('--') + 1]); OUT.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
groups = {}; current = None
COLORS = dict(violet='6436BD', lilac='A779ED', plum='39265D', ink='202E49', steel='819CBC', silver='D1E3F3', gold='EBAA36', lightgold='FFE5A1', teal='137D88', aqua='66E1E2', red='EE5542', wood='A44F28', honey='E3A15A', cream='FFF2D5', white='F7FBFF', blue='329CC4', fire='FFFFFF', darkwood='633726')
def mat(key):
    name='Enamel_'+key
    m=bpy.data.materials.get(name)
    if not m:
        m=bpy.data.materials.new(name)
        h=COLORS[key]; rgb=[int(h[i:i+2],16)/255 for i in (0,2,4)]
        m.diffuse_color=(*rgb,1)
    return m
def group(name):
    global current
    current=name; groups[name]=[]
def finish(o, color, bevel=0):
    o.data.materials.append(mat(color)); groups[current].append(o)
    bpy.context.view_layer.objects.active=o
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        b=o.modifiers.new('Soft manufactured edges','BEVEL'); b.width=bevel; b.segments=4
        bpy.ops.object.modifier_apply(modifier=b.name)
    for p in o.data.polygons: p.use_smooth=True
    if bevel:
        w=o.modifiers.new('Weighted face normals','WEIGHTED_NORMAL'); w.keep_sharp=True; w.weight=50
        bpy.ops.object.modifier_apply(modifier=w.name)
    return o
def box(name,p,s,color,bevel=.04,rot=0):
    bpy.ops.mesh.primitive_cube_add(size=1,location=p); o=bpy.context.object; o.name=name; o.scale=s
    o.rotation_euler[1]=math.radians(rot)
    return finish(o,color,min(bevel,min(s)*.45))
def sphere(name,p,s,color,segments=24,rings=12):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments,ring_count=rings,radius=.5,location=p)
    o=bpy.context.object; o.name=name;o.scale=s;return finish(o,color)
def cylinder(name,p,r,depth,color,axis='Y',bevel=.025,vertices=32):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=depth,location=p)
    o=bpy.context.object;o.name=name
    o.rotation_euler[0 if axis=='Y' else 1]=math.pi/2 if axis!='Z' else 0
    return finish(o,color,min(bevel,depth*.35))
def torus(name,p,r,t,color,axis='Y'):
    bpy.ops.mesh.primitive_torus_add(major_segments=32,minor_segments=8,location=p,major_radius=r,minor_radius=t)
    o=bpy.context.object;o.name=name
    o.rotation_euler[0 if axis=='Y' else 1]=math.pi/2 if axis!='Z' else 0
    return finish(o,color)
def screw(p,r=.035):
    cylinder('Inset rivet',p,r,.025,'lightgold',vertices=16,bevel=.008)
    box('Screw slot',(p[0],p[1]+.015,p[2]),(r*1.15,.008,.009),'wood',.002)
def plate():
    # A turned porcelain dish: raised lip, sloped bowl and foot ring, not stacked cylinders.
    profile=[(0,-.025),(.12,-.025),(.16,-.01),(.22,.022),(.24,.055),(.238,.075),(.223,.088),(.203,.073),(.18,.035),(.14,.014),(0,.014)]
    verts=[]; faces=[]; n=48
    for r,y in profile:
        verts.extend([(r*math.cos(i*2*math.pi/n),y,r*math.sin(i*2*math.pi/n)) for i in range(n)])
    for j in range(len(profile)-1):
        for i in range(n): faces.append(((j+1)*n+i,(j+1)*n+(i+1)%n,j*n+(i+1)%n,j*n+i))
    me=bpy.data.meshes.new('Thrown porcelain');me.from_pydata(verts,[],faces);me.update()
    o=bpy.data.objects.new('Concave porcelain dish',me);bpy.context.collection.objects.link(o);finish(o,'white')
    torus('Blue glaze rim',(0,.079,0),.219,.009,'blue')
    torus('Fine gold rim',(0,.088,0),.23,.004,'gold')
    torus('Bowl glaze accent',(0,.022,0),.145,.006,'blue')
    for i in range(8):
        a=i*math.pi/4
        petal=sphere('Painted petal',(math.cos(a)*.061,.018,math.sin(a)*.061),(.034,.005,.065),'blue',12,6);petal.rotation_euler[1]=-a+math.pi/2
    cylinder('Flower heart',(0,.019,0),.025,.005,'gold',bevel=.001)

group('Furnace')
box('Cast violet housing',(0,-.035,0),(.91,.78,.9),'violet',.15)
box('Dark lower plinth',(0,-.31,0),(.94,.15,.92),'plum',.06)
box('Raised lavender bezel',(0,.31,0),(.87,.24,.86),'lilac',.14)
box('Silver inset rim',(0,.416,0),(.73,.065,.73),'silver',.11)
box('Recessed soot chamber',(0,.448,0),(.65,.035,.65),'plum',.10)
for z in [-.20,0,.20]:
    box('Vent lip',(0,.463,z),(.55,.045,.15),'gold',.07)
    box('Vent cavity',(0,.488,z),(.48,.018,.105),'ink',.05)
for x in [-.32,.32]:
    for z in [-.32,.32]: screw((x,.45,z),.025)
cylinder('Intake collar',(.44,0,0),.39,.14,'plum','X')
torus('Polished intake edge',(.52,0,0),.33,.045,'silver','X')
for x in [-.45,.45]:
    for z in [-.23,.23]: box('Side cooling slot',(x,-.05,z),(.025,.26,.09),'plum',.03)
group('FurnaceGlow')
for z in [-.20,0,.20]: box('Molten orange vent',(0,.501,z),(.43,.025,.075),'fire',.035)
torus('Intake heat',(.525,0,0),.285,.022,'fire','X')

group('Pipe')
cylinder('Rolled steel body',(0,0,0),.342,.98,'steel','X',.045)
for x in [-.44,.44]:
    cylinder('Compression flange',(x,0,0),.388,.095,'ink','X',.025)
    torus('Rounded silver flange',(x,0,0),.36,.038,'silver','X')
    for a in [math.pi/4,3*math.pi/4,5*math.pi/4,7*math.pi/4]:
        cylinder('Flange bolt',(x+.05,.295*math.sin(a),.295*math.cos(a)),.028,.018,'gold','X',.006,12)
cylinder('Bore recess',(.496,0,0),.275,.008,'ink','X',.002)
torus('Pipe lip',(.5,0,0),.286,.025,'silver','X')
box('Longitudinal polished seam',(0,.335,0),(.75,.018,.035),'silver',.008)

group('Cabinet')
box('Rounded walnut cabinet',(1,-.16,-.5),(2.94,.54,1.94),'wood',.11)
box('Dark recessed back',(1,.118,-.5),(2.67,.05,1.67),'darkwood',.07)
for z in [-.94,-.06]: box('Shelf backing',(1,.152,z),(2.64,.04,.76),'honey',.045)
for x in [-.40,2.40]:
    box('Carved stile',(x,.22,-.5),(.18,.5,1.91),'honey',.045)
    box('Stile raised moulding',(x,.485,-.5),(.075,.045,1.72),'cream',.02)
for z in [.42,-1.42]:
    box('Crown moulding',(1,.28,z),(2.96,.48,.16),'honey',.055)
    box('Crown cream lip',(1,.535,z),(2.78,.055,.065),'cream',.025)
box('Middle shelf',(1,.28,-.5),(2.7,.35,.13),'wood',.035)
box('Shelf front moulding',(1,.465,-.5),(2.69,.055,.12),'honey',.025)
for x in [-.39,2.39]:
    for z in [-1.34,.34]: screw((x,.505,z),.036)
for z in [-.11,-1.06]:
    for x in [-.1,.45,1,1.55,2.1]: box('Plate support',(x,.21,z),(.27,.09,.13),'wood',.035)
group('Plate');plate()

group('Panel')
box('Deep teal casting',(0,0,0),(1.97,.79,.39),'teal',.13)
box('Rounded golden bumper',(0,.05,-.15),(1.98,.65,.16),'gold',.075)
box('Padded upper rim',(0,.40,0),(1.96,.14,.40),'lightgold',.06)
box('Recessed enamel',(0,.475,0),(1.60,.025,.24),'teal',.065)
for side in [-1,1]:
    for j in [-1,1]:
        box('Movement chevron',(side*.50,.498,j*.045),(.20,.025,.045),'aqua',.018,rot=side*j*35)
    screw((side*.84,.48,0))
    cylinder('Brass carriage wheel',(side*.69,-.34,0),.13,.37,'ink','Z',.025)
group('Rail')
box('Track',(0,0,0),(1,1,1),'teal',.15)
group('RailStop')
cylinder('Stop pedestal',(0,0,0),.115,.17,'gold')
cylinder('Bolt cap',(0,.10,0),.08,.04,'lightgold',vertices=6,bevel=.01)

group('Mine')
sphere('Rounded navy mine',(0,0,0),(.76,.76,.76),'ink')
torus('Equatorial brass seam',(0,0,0),.374,.026,'gold')
torus('Trigger socket',(0,.32,0),.18,.038,'silver')
cylinder('Red pressure cap',(0,.368,0),.15,.12,'red',bevel=.045)
torus('Trigger cap trim',(0,.40,0),.145,.009,'lightgold')
for i in range(8):
    a=i*math.pi/4;d=Vector((math.sin(a),0,math.cos(a)))
    o=cylinder('Radial detonator',tuple(d*.37),.065,.19,'gold',axis='Z',bevel=.025,vertices=24)
    o.rotation_mode='QUATERNION';o.rotation_quaternion=Vector((0,0,1)).rotation_difference(d)
    sphere('Detonator tip',tuple(d*.455),(.11,.11,.11),'lightgold',16,8)
for x in [-.12,.12]: screw((x,.30,-.18),.023)

group('SwitchBase')
box('Switch mounting plate',(0,-.20,0),(.92,.25,.92),'ink',.14)
cylinder('Machined switch socket',(0,-.02,0),.40,.24,'teal',bevel=.045)
torus('Socket gold bezel',(0,.11,0),.335,.04,'gold')
torus('Recess shadow',(0,.115,0),.29,.025,'ink')
for x in [-.33,.33]:
    for z in [-.33,.33]: screw((x,-.06,z),.025)
for state,color,y in [('SwitchOff','red',.16),('SwitchOn','aqua',.10)]:
    group(state);cylinder('Domed button',(0,y,0),.278,.15,color,bevel=.06)
    torus('Button inner lip',(0,y+.075,0),.235,.009,'cream')
    torus('Power symbol',(0,y+.081,0),.075,.009,'cream')
    box('Power symbol stem',(0,y+.085,.07),(.025,.012,.11),'cream',.008)

group('GateFrame')
for x in [-.40,.40]:
    box('Iron gate pillar',(x,0,0),(.18,1.08,.40),'teal',.055)
    box('Pillar foot',(x,-.42,0),(.24,.15,.44),'ink',.04)
    box('Pillar capital',(x,.43,0),(.24,.18,.44),'gold',.045)
box('Arch lintel',(0,.55,0),(.97,.24,.44),'gold',.08)
box('Enamel header',(0,.68,0),(.65,.035,.26),'teal',.07)
for x in [-.36,.36]: screw((x,.69,0),.03)
group('GateBars')
for x in [-.23,0,.23]: box('Polished gate bar',(x,-.02,0),(.115,.99,.17),'lightgold',.04)
box('Crossbar',(0,-.20,-.04),(.70,.10,.24),'gold',.035)
box('Lock escutcheon',(0,.05,-.13),(.27,.32,.08),'gold',.06)
group('GateLamp');sphere('Open indicator',(0,.73,0),(.16,.08,.16),'aqua')

group('ComboClosed')
box('Base plinth',(0,-.30,0),(.91,.20,.91),'plum',.09)
for x in [-.225,.225]:
    box('Rounded split shell',(x,.04,0),(.425,.74,.88),'violet',.10)
    box('Raised shell panel',(x,.43,0),(.32,.065,.68),'lilac',.055)
    box('Panel inset',(x,.465,0),(.235,.025,.53),'violet',.045)
box('Gold centre lock',(0,.455,0),(.13,.06,.32),'gold',.045)
for z in [-.28,.28]: box('Latch',(0,.445,z),(.16,.08,.12),'gold',.025)
group('ComboOpen')
box('Base plinth',(0,-.30,0),(.91,.20,.91),'plum',.09)
sphere('Exposed aqua core',(0,.06,0),(.49,.62,.49),'aqua')
torus('Core collar',(0,-.12,0),.26,.04,'gold')
for x in [-.36,.36]:
    box('Retracted shell',(x,0,0),(.17,.6,.84),'violet',.07)
    box('Shell highlight',(x,.32,0),(.16,.065,.70),'lilac',.03)
for z in [-.35,.35]: box('Open latch',(0,.08,z),(.16,.28,.15),'gold',.03)

group('Armor')
box('Armored core',(0,-.02,0),(.81,.76,.81),'ink',.10)
box('Back armor',(0,.02,.35),(.94,.90,.23),'steel',.07)
for x in [-.36,.36]:
    box('Shoulder armor',(x,.02,-.015),(.23,.90,.88),'steel',.07)
    box('Shoulder highlight',(x,.485,-.015),(.13,.055,.69),'silver',.025)
    for z in [-.28,.26]: screw((x,.52,z),.028)
box('Recessed top enamel',(0,.38,-.04),(.51,.07,.60),'teal',.08)
box('Red vulnerable pad',(0,.01,-.404),(.51,.59,.08),'red',.06)
box('Weak edge lip',(0,.40,-.35),(.51,.075,.13),'red',.04)
for side in [-1,1]: box('Directional chevron',(side*.09,.443,-.08),(.08,.035,.29),'lightgold',.025,rot=side*40)

group('Shard')
box('Glazed porcelain fragment',(0,0,0),(.13,.045,.10),'white',.012)
box('Blue glaze',(0,.024,0),(.075,.008,.07),'blue',.008)

# The pilot has its own editable source and UV-baked exports. Other gimmicks stay untouched.
if '--pilot' in sys.argv:
    sys.path.insert(0,str(Path(__file__).resolve().parent))
    from gimmick_pilot import build_and_export
    build_and_export(globals())
    sys.exit(0)

# Store an editable collection for every functional part in the source blend.
for name,objects in groups.items():
    coll=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(coll)
    for o in objects:
        for c in list(o.users_collection): c.objects.unlink(o)
        coll.objects.link(o)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'GimmickModels.blend'))

def linear(c): return c/12.92 if c<=.04045 else ((c+.055)/1.055)**2.4
deps=bpy.context.evaluated_depsgraph_get(); result=[]
for name,objects in groups.items():
    vertices=[]; normals=[]; colors=[]; triangles=[]; polys=[]; raw=[]
    for o in objects:
        ev=o.evaluated_get(deps);me=ev.to_mesh();me.calc_loop_triangles();m=o.matrix_world
        offset=len(raw);raw.extend([m@v.co for v in me.vertices]);polys.extend([tuple(offset+i for i in p.vertices) for p in me.polygons])
        ev.to_mesh_clear()
    bvh=BVHTree.FromPolygons(raw,polys)
    # Short-range ambient occlusion baked in vertex alpha, keeping the asset texture-free.
    for o in objects:
        ev=o.evaluated_get(deps);me=ev.to_mesh();me.calc_loop_triangles();m=o.matrix_world;nm=m.to_3x3().inverted().transposed()
        rgb=[linear(c) for c in o.data.materials[0].diffuse_color[:3]]
        cache={}
        for tri in me.loop_triangles:
            ids=[]
            for li in tri.loops:
                vi=me.loops[li].vertex_index;p=m@me.vertices[vi].co;n=(nm@me.corner_normals[li].vector).normalized()
                key=(vi,tuple(round(x,4) for x in n))
                if key not in cache:
                    tangent=n.cross(Vector((0,0,1)) if abs(n.z)<.9 else Vector((1,0,0))).normalized();bit=n.cross(tangent)
                    occluded=0
                    for k in range(8):
                        a=k*2.399963;z=.3+.65*(k+.5)/8;r=math.sqrt(1-z*z);d=n*z+tangent*(math.cos(a)*r)+bit*(math.sin(a)*r)
                        hit=bvh.ray_cast(p+n*.003,d,.23)
                        if hit[0] is not None: occluded+=1
                    ao=1-.48*occluded/8
                    cache[key]=len(vertices)//3
                    vertices.extend(round(x,6) for x in p);normals.extend(round(x,6) for x in n);colors.extend([*map(lambda x:round(x,6),rgb),round(ao,4)])
                ids.append(cache[key])
            triangles.extend(ids)
        ev.to_mesh_clear()
    data=dict(name=name,vertices=vertices,normals=normals,colors=colors,triangles=triangles)
    (OUT/(name+'.json')).write_text(json.dumps(data,separators=(',',':')),encoding='utf-8')
    result.append(dict(name=name,vertices=len(vertices)//3,triangles=len(triangles)//3))
    print('EXPORTED',result[-1],flush=True)
(OUT/'manifest.json').write_text(json.dumps(result,indent=2))
