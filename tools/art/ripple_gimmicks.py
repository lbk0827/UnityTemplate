"""Remaining gameplay props in the approved Ripple Box art language.
Run build_gimmick_models.py -- <output> --ripple-gimmicks.
Authored parts remain editable in the blend; exports use Box-compatible UV atlases.
"""
import bpy, math, json
from mathutils import Vector
import gimmick_pilot
import ripple_box


def build_and_export(api):
    globals().update({k:api[k] for k in ('OUT','COLORS','groups','group','box','sphere','cylinder','torus','finish')})
    for obj in list(bpy.data.objects):bpy.data.objects.remove(obj,do_unlink=True)
    groups.clear()
    ripple_box.finish=finish
    COLORS.update(
        navy='35516E', deep='213B50', softteal='368D99', seaglass='71C8CA',
        ivory='F5E7CB', brass='D7A653', creamlight='FFF3DC',
        coral='ED7161', coraldeep='A74743', violetcase='855AC2',
        violetlid='A885D6', violetshade='563C83', pearl='CBDDEA',
        bluegray='829FB6', glaze='3288AE')

    group('Panel')
    box('Soft teal bumper',(0,0,0),(1.97,.78,.39),'softteal',.14)
    slab('Cream cushion',(0,.015,-.19),(1.91,.12,.57),.16,.045,'ivory',front=True)
    slab('Inset lid shadow',(0,.394,0),(1.75,.055,.33),.13,.02,'deep')
    slab('Enamel direction panel',(0,.425,0),(1.63,.045,.265),.11,.016,'softteal')
    for side in [-1,1]:
        wave('Travel arrow',[(side*.40,.457,-.073),(side*.51,.457,0),(side*.40,.457,.073)],.021,'creamlight')
        cylinder('Recessed carriage wheel',(side*.68,-.31,0),.12,.29,'deep','Z',.025)
    group('Rail')
    box('Rounded travel guide',(0,0,0),(1,1,1),'navy',.17)
    group('RailStop')
    cylinder('Cushioned rail stop',(0,0,0),.115,.17,'bluegray',bevel=.038)
    cylinder('Pearl end cap',(0,.087,0),.084,.055,'pearl',bevel=.018)

    group('Mine')
    sphere('Pillow navy shell',(0,-.035,0),(.77,.74,.77),'navy',32,16)
    torus('Recessed equator',(0,-.035,0),.379,.014,'deep')
    # Four broad bumpers keep the silhouette legible without a crown of tiny spikes.
    for i in range(4):
        a=math.pi/4+i*math.pi/2;d=Vector((math.sin(a),0,math.cos(a)))
        o=cylinder('Soft radial bumper',tuple(d*.365),.088,.17,'bluegray','Z',.035,24)
        o.rotation_mode='QUATERNION';o.rotation_quaternion=Vector((0,0,1)).rotation_difference(d)
        sphere('Pearl bumper cap',tuple(d*.428),(.14,.14,.14),'pearl',16,8)
    cylinder('Trigger seat',(0,.288,0),.225,.085,'deep',bevel=.025)
    torus('Ivory pressure bezel',(0,.328,0),.20,.026,'ivory')
    cylinder('Coral pressure dome',(0,.365,0),.168,.14,'coral',bevel=.045)
    # A broad ring identifies the trigger at phone size.
    torus('Pressure face ring',(0,.436,0),.106,.012,'creamlight')

    group('SwitchBase')
    box('Pillow mounting case',(0,-.13,0),(.93,.41,.93),'softteal',.145)
    slab('Inset cream socket',(0,.083,0),(.80,.065,.80),.15,.023,'ivory')
    cylinder('Button travel well',(0,.135,0),.32,.06,'deep',bevel=.018)
    torus('Soft button collar',(0,.155,0),.324,.024,'brass')
    for name,color,y in [('SwitchOff','coral',.215),('SwitchOn','seaglass',.155)]:
        group(name)
        cylinder('Rounded pressure face',(0,y,0),.282,.16,color,bevel=.052,vertices=48)
        # Open power arc and stem, rather than an ambiguous closed ring.
        wave('Power arc',[(.087*math.sin(a),y+.083,.087*math.cos(a))
             for a in [math.radians(43+i*274/32) for i in range(33)]],.014,'creamlight')
        wave('Power stem',[(0,y+.084,.035),(0,y+.084,.135)],.015,'creamlight')

    group('GateFrame')
    for x in [-.395,.395]:
        box('Soft portal pillar',(x,-.035,0),(.205,.98,.40),'softteal',.08)
        box('Pearl pillar foot',(x,-.43,0),(.255,.15,.44),'bluegray',.055)
        slab('Inset pillar face',(x,-.035,-.205),(.10,.035,.70),.045,.012,'seaglass',front=True)
    box('Rounded bridge',(0,.49,0),(.99,.29,.45),'ivory',.13)
    slab('Teal header inlay',(0,.642,0),(.73,.04,.31),.13,.014,'softteal')
    wave('Header ripple',[(x,.665,.018*math.sin(x*math.pi/.19)) for x in [-.22+i*.44/24 for i in range(25)]],.014,'creamlight')
    group('GateBars')
    for x in [-.21,0,.21]:
        box('Rounded cream bar',(x,-.02,0),(.115,.92,.155),'ivory',.053)
    box('Gate crossbeam',(0,-.18,-.025),(.66,.13,.20),'brass',.06)
    slab('Lock face',(0,.07,-.10),(.29,.09,.34),.10,.03,'ivory',front=True)
    cylinder('Keyhole',(0,.10,-.151),.039,.012,'deep','Z',.004,24)
    box('Keyhole slot',(0,.058,-.154),(.035,.075,.014),'deep',.006)
    group('GateLamp')
    sphere('Open signal pearl',(0,.681,0),(.18,.06,.18),'seaglass',24,12)

    group('ComboClosed')
    box('Rounded violet body',(0,-.025,0),(.94,.89,.94),'violetcase',.145)
    slab('Lid shadow',(0,.419,0),(.79,.045,.79),.14,.016,'violetshade')
    for x in [-.182,.182]:
        slab('Split inset lid',(x,.451,0),(.346,.065,.70),.105,.024,'violetlid')
        cylinder('Two-hit pearl',(x,.489,0),.065,.025,'ivory',bevel=.008,vertices=24)
    # A single seam continues across the face, echoing the opening animation.
    slab('Front lid insert',(0,-.04,-.466),(.69,.035,.44),.10,.012,'violetlid',front=True)
    box('Front split',(0,-.04,-.487),(.028,.39,.015),'violetshade',.005)
    group('ComboOpen')
    box('Core cradle',(0,-.32,0),(.90,.19,.90),'violetshade',.08)
    for x in [-.36,.36]:
        box('Retracted soft shell',(x,-.015,0),(.19,.66,.86),'violetcase',.085)
        slab('Opened lid',(x,.332,0),(.18,.045,.69),.075,.015,'violetlid')
    cylinder('Pearl core seat',(0,-.155,0),.255,.10,'ivory',bevel=.03)
    sphere('Exposed sea-glass core',(0,.095,0),(.47,.61,.47),'seaglass',32,16)
    torus('Core ripple',(0,.10,0),.238,.012,'creamlight')

    group('Armor')
    box('Deep armored body',(0,-.025,0),(.84,.79,.84),'navy',.14)
    box('Soft rear guard',(0,.015,.34),(.94,.90,.25),'bluegray',.115)
    for x in [-.355,.355]:
        box('Rounded shoulder',(x,.015,-.01),(.25,.90,.89),'bluegray',.115)
        slab('Pearl shoulder insert',(x,.472,-.01),(.145,.035,.66),.067,.012,'pearl')
    slab('Recessed direction lid',(0,.381,-.045),(.49,.08,.61),.12,.027,'softteal')
    slab('Coral weak face',(0,-.025,-.429),(.48,.055,.57),.12,.02,'coral',front=True)
    slab('Weak edge cue',(0,.412,-.332),(.48,.045,.13),.06,.015,'coral')
    wave('Weak-side chevron',[(-.13,.431,.08),(0,.431,-.11),(.13,.431,.08)],.03,'creamlight')

    group('Shard')
    # A curved sector of the pilot's porcelain dish, with a continuous blue rolled lip.
    n=12;verts=[]
    for y in [-.022,.022]:
        verts.append((0,y,0))
        verts.extend((.14*math.cos(a),y,.14*math.sin(a)) for a in [-.5+i*1.0/n for i in range(n+1)])
    stride=n+2
    faces=[tuple(range(stride-1,-1,-1)),tuple(range(stride,2*stride))]
    faces.extend((i,(i+1)%stride,(i+1)%stride+stride,i+stride) for i in range(stride))
    mesh=bpy.data.meshes.new('Porcelain sector');mesh.from_pydata(verts,[],faces);mesh.update()
    obj=bpy.data.objects.new('Curved ceramic chip',mesh);bpy.context.collection.objects.link(obj);finish(obj,'ivory',.008)
    wave('Blue glazed rim',[(.137*math.cos(a),.025,.137*math.sin(a)) for a in [-.5+i/n for i in range(n+1)]],.009,'glaze')

    for name,objects in groups.items():
        collection=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(collection)
        for obj in objects:
            for old in list(obj.users_collection):old.objects.unlink(obj)
            collection.objects.link(obj)
    bpy.context.scene.render.engine='CYCLES';bpy.context.scene.cycles.samples=32
    bpy.context.scene.render.bake.margin=12;bpy.context.scene.render.bake.use_clear=True
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'RippleGimmicks.blend'))
    gimmick_pilot.OUT=OUT
    manifest=[gimmick_pilot.export(name,objects,ao_strength=.45) for name,objects in groups.items()]
    (OUT/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')


def slab(*args,**kwargs):
    return ripple_box.slab(*args,**kwargs)


def wave(*args,**kwargs):
    return ripple_box.wave(*args,**kwargs)
