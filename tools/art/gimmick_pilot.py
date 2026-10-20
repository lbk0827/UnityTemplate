"""Box-matched cabinet / furnace study. Called by build_gimmick_models.py --pilot.
Uses authored geometry and Cycles emission baking with ambient occlusion, not external art.
The editable source keeps the individual parts; only exports are joined and UV-unwrapped.
"""
import bpy, math, json
from mathutils import Vector


def build_and_export(api):
    globals().update({k: api[k] for k in ('OUT','COLORS','groups','group','box','sphere','cylinder','torus','finish')})
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    groups.clear()
    COLORS.update(
        biscuit='D49855', caramel='B66B3D', walnut='713F30', custard='F6D298',
        jade='286C75', deepjade='194C59', mint='70BAC0', porcelain='FFF3DA',
        cobalt='3288AE', slate='607A9C', pearlsteel='B5C9DC', lilacsoft='A077DC',
        violetsoft='7440BB', deepviolet='39265D', warmbrass='D9A54C')

    group('Cabinet')
    # A generous rounded case, continuous dark rebate and two genuinely recessed trays.
    box('Rounded caramel case',(1,-.16,-.5),(2.96,.56,1.96),'caramel',.15)
    box('Lower walnut reveal',(1,-.365,-.5),(2.78,.14,1.8),'walnut',.065)
    box('Inset shadow rebate',(1,.115,-.5),(2.69,.06,1.68),'walnut',.10)
    for z in [-.97,-.03]:
        box('Recessed jade lining',(1,.149,z),(2.53,.065,.76),'deepjade',.10)
        box('Soft enamel inner field',(1,.184,z),(2.43,.015,.64),'jade',.09)
    for x in [-.36,2.36]:
        box('Pillowy solid stile',(x,.255,-.5),(.26,.49,1.86),'biscuit',.105)
        box('Inset cream face',(x,.492,-.5),(.12,.038,1.57),'custard',.045)
    for z in [.39,-1.39]:
        box('Rounded crown rail',(1,.285,z),(2.91,.49,.25),'biscuit',.11)
        box('Crown light plane',(1,.522,z),(2.59,.035,.10),'custard',.045)
    box('Shelf underside',(1,.25,-.5),(2.64,.26,.19),'walnut',.065)
    box('Thick rounded shelf',(1,.375,-.5),(2.66,.16,.22),'biscuit',.065)
    box('Shelf top highlight',(1,.452,-.5),(2.45,.024,.10),'custard',.035)
    # Two broad corner caps replace noisy pin-sized screws.
    for x in [-.35,2.35]:
        box('Lower joinery cap',(x,.508,-1.28),(.14,.036,.17),'warmbrass',.035)
    for z in [-.14,-1.08]:
        for x in [-.1,.45,1,1.55,2.1]:
            box('Plate rest',(1+(x-1)*.89,.235,z),(.25,.10,.13),'biscuit',.04)

    group('Plate')
    # Large bowl, continuous thick lip and four clear petals readable at gameplay scale.
    profile=[(0,-.026),(.135,-.026),(.185,-.005),(.23,.025),(.245,.06),
             (.243,.085),(.226,.10),(.206,.083),(.182,.048),(.145,.027),(0,.027)]
    lathe('Porcelain dish',profile,'porcelain',64)
    torus('Rolled cobalt lip',(0,.089,0),.224,.012,'cobalt')
    for i in range(4):
        a=i*math.pi/2
        o=sphere('Four-petal glaze',(math.cos(a)*.067,.034,math.sin(a)*.067),
                 (.055,.008,.10),'cobalt',16,8)
        o.rotation_euler[1]=-a+math.pi/2
    cylinder('Honey flower centre',(0,.04,0),.035,.008,'warmbrass',bevel=.003,vertices=24)

    group('Furnace')
    box('Rounded cast body',(0,-.035,0),(.92,.79,.90),'violetsoft',.17)
    box('Base shadow reveal',(0,-.345,0),(.85,.105,.83),'deepviolet',.05)
    # Layered inset panel repeats the Box's broad bevel / narrow inset vocabulary.
    box('Soft shoulder',(0,.285,0),(.88,.25,.86),'lilacsoft',.13)
    box('Brushed bezel',(0,.407,0),(.73,.075,.72),'pearlsteel',.10)
    box('Deep combustion recess',(0,.444,0),(.625,.025,.615),'deepviolet',.08)
    for z in [-.183,0,.183]:
        box('Broad vent surround',(0,.46,z),(.55,.042,.135),'slate',.045)
        box('Inset warm cavity',(0,.482,z),(.455,.012,.073),'walnut',.035)
    # Rounded side panel reads from the game's camera, without a mass of tiny vents.
    box('Front service panel shadow',(0,-.045,-.448),(.61,.43,.025),'deepviolet',.085)
    box('Front service enamel',(0,-.035,-.466),(.52,.33,.028),'violetsoft',.07)
    box('Service handle',(0,.085,-.49),(.23,.055,.045),'pearlsteel',.022)
    cylinder('Intake gasket',(.446,0,0),.36,.13,'deepviolet','X',.035)
    torus('Soft intake collar',(.505,0,0),.31,.052,'pearlsteel','X')
    group('FurnaceGlow')
    for z in [-.183,0,.183]:
        box('Warm vent core',(0,.495,z),(.405,.016,.043),'fire',.02)
    torus('Intake embers',(.523,0,0),.263,.013,'fire','X')

    group('Pipe')
    # A true hollow tube. Neighbouring end caps meet at the same one-cell spacing.
    profile=[(-.5,.255),(-.5,.30),(-.475,.33),(-.425,.337),(.425,.337),
             (.475,.33),(.5,.30),(.5,.255),(.43,.245),(-.43,.245)]
    tube('Enamel hollow barrel',profile,'slate')
    for x in [-.411,.411]:
        cylinder('Dark compression gasket',(x,0,0),.369,.11,'ink','X',.027)
        # The gasket's visible outside is covered; bores use the separate annular profile.
        tube('Rounded union collar',[(x-.065,.266),(x-.065,.342),(x-.04,.383),
             (x+.025,.383),(x+.065,.342),(x+.065,.266)],'pearlsteel')
        torus('Collar shadow seam',(x,0,0),.385,.009,'slate','X')
    # Open mouth with a dark inner sleeve, plus a recessed terminal deep in the pipe.
    tube('Inner bore',[(-.495,.253),(.495,.253),(.495,.264),(-.495,.264)],'ink')
    cylinder('Deep bore shadow',(0,0,0),.246,.015,'deepjade','X',.002)
    # Remove the solid gasket centres: their outer silhouette is supplied by union collars.
    for o in list(groups['Pipe']):
        if o.name.startswith('Dark compression gasket'):
            groups['Pipe'].remove(o);bpy.data.objects.remove(o,do_unlink=True)

    for name,objects in groups.items():
        coll=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(coll)
        for obj in objects:
            for old in list(obj.users_collection):old.objects.unlink(obj)
            coll.objects.link(obj)
    # Bake the high quality shading into a UV atlas so its depth survives small on-screen sizes.
    bpy.context.scene.render.engine='CYCLES'
    bpy.context.scene.cycles.samples=32
    bpy.context.scene.cycles.use_denoising=False
    bpy.context.scene.render.bake.margin=12
    bpy.context.scene.render.bake.use_clear=True
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'GimmickPilot.blend'))
    manifest=[]
    for name,objects in groups.items():
        manifest.append(export(name,objects))
    (OUT/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')


def lathe(name,profile,color,n):
    verts=[(r*math.cos(i*math.tau/n),y,r*math.sin(i*math.tau/n)) for r,y in profile for i in range(n)]
    faces=[((j+1)*n+i,(j+1)*n+(i+1)%n,j*n+(i+1)%n,j*n+i)
           for j in range(len(profile)-1) for i in range(n)]
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
    obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj);finish(obj,color)


def tube(name,profile,color,n=48):
    verts=[(x,r*math.cos(i*math.tau/n),r*math.sin(i*math.tau/n)) for x,r in profile for i in range(n)]
    faces=[(j*n+i,j*n+(i+1)%n,((j+1)%len(profile))*n+(i+1)%n,((j+1)%len(profile))*n+i)
           for j in range(len(profile)) for i in range(n)]
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
    obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj);finish(obj,color)
    # Consistent outward normals on the closed annular surface.
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')


def linear(c):
    return c/12.92 if c<=.04045 else ((c+.055)/1.055)**2.4


def export(name,objects,ao_strength=.65):
    # Duplicate authored parts: saved .blend retains their names and material assignments.
    bpy.ops.object.select_all(action='DESELECT')
    copies=[]
    for obj in objects:
        copy=obj.copy();copy.data=obj.data.copy();bpy.context.collection.objects.link(copy)
        copy.select_set(True);copies.append(copy)
    bpy.context.view_layer.objects.active=copies[0]
    bpy.ops.object.join();obj=bpy.context.object;obj.name=name+'_Export'
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(70),island_margin=.018,area_weight=.5)
    bpy.ops.object.mode_set(mode='OBJECT')
    size=1024 if name in ('Cabinet','Furnace') else 512
    image=bpy.data.images.new(name+'_AlbedoAO',width=size,height=size,alpha=False)
    image.colorspace_settings.name='sRGB'
    for index,source in enumerate(list(obj.data.materials)):
        material=source.copy();obj.data.materials[index]=material
        material.use_nodes=True;nodes=material.node_tree.nodes;nodes.clear();links=material.node_tree.links
        output=nodes.new('ShaderNodeOutputMaterial');emit=nodes.new('ShaderNodeEmission')
        emit.inputs['Strength'].default_value=1
        links.new(emit.outputs[0],output.inputs['Surface'])
        base=tuple(linear(c) for c in source.diffuse_color[:3])
        ao=nodes.new('ShaderNodeAmbientOcclusion');ao.inputs['Color'].default_value=(*base,1)
        ao.inputs['Distance'].default_value=.24;ao.samples=32;ao.only_local=True
        mix=nodes.new('ShaderNodeMixRGB');mix.blend_type='MIX';mix.inputs[0].default_value=ao_strength
        mix.inputs[1].default_value=(*base,1);links.new(ao.outputs['Color'],mix.inputs[2])
        links.new(mix.outputs[0],emit.inputs['Color'])
        tex=nodes.new('ShaderNodeTexImage');tex.image=image;nodes.active=tex
    # Only this joined mesh participates; unrelated catalogue items overlap at their local origins.
    for other in bpy.data.objects:
        if other.type=='MESH':other.hide_render=other!=obj
    bpy.ops.object.bake(type='EMIT')
    image.filepath_raw=str(OUT/(name+'_AlbedoAO.png'));image.file_format='PNG';image.save()
    me=obj.data;me.calc_loop_triangles()
    vertices=[];normals=[];uv=[];colors=[];triangles=[];cache={}
    for tri in me.loop_triangles:
        for li in tri.loops:
            vi=me.loops[li].vertex_index;p=me.vertices[vi].co;n=me.corner_normals[li].vector
            texcoord=me.uv_layers.active.data[li].uv
            key=(vi,tuple(round(v,5) for v in n),tuple(round(v,6) for v in texcoord))
            if key not in cache:
                cache[key]=len(vertices)//3
                vertices.extend(round(v,6) for v in p);normals.extend(round(v,6) for v in n)
                uv.extend(round(v,6) for v in texcoord);colors.extend([1,1,1,1])
            triangles.append(cache[key])
    data=dict(name=name,vertices=vertices,normals=normals,uv=uv,colors=colors,triangles=triangles)
    (OUT/(name+'.json')).write_text(json.dumps(data,separators=(',',':')),encoding='utf-8')
    result=dict(name=name,vertices=len(vertices)//3,triangles=len(triangles)//3,atlas=size)
    print('PILOT_EXPORTED',result,flush=True)
    bpy.data.objects.remove(obj,do_unlink=True)
    return result
