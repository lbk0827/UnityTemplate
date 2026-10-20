"""Original Ripple Box: rounded enamel case with a droplet and two ripple marks.
Run through build_gimmick_models.py -- <output> --box.
The grayscale baked atlas is shared by four coloured materials and three matching fragments.
"""
import bpy, bmesh, math, json
from mathutils import Vector
import gimmick_pilot


def build_and_export(api):
    globals().update({k:api[k] for k in ('OUT','COLORS','groups','group','box','finish')})
    for obj in list(bpy.data.objects):bpy.data.objects.remove(obj,do_unlink=True)
    groups.clear()
    COLORS.update(boxbody='EFEFEF',boxpanel='DEDEDE',boxgroove='999999',boxseal='FFFFFF',boxshadow='BCBCBC')
    group('RippleBox')
    box('Soft enamel shell',(0,-.025,0),(.96,.90,.96),'boxbody',.145)
    slab('Recessed lid rebate',(0,.416,0),(.79,.045,.79),.14,.016,'boxgroove')
    slab('Floating inset lid',(0,.445,0),(.715,.065,.715),.12,.025,'boxpanel')
    # A shallow raised seal, with its own darker seat. The two rings form a bevel.
    droplet('Seal seat',.85,.476,.041,'boxshadow')
    droplet('Pearl droplet',.76,.491,.04,'boxseal')
    for z in [-.185,-.265]:
        wave('Lid ripple',[(x,.482,z+.025*math.sin(x*math.pi/.21)) for x in [(-.21+i*.42/24) for i in range(25)]],.012,'boxseal')
    # Side panels are quiet: one large wave repeated on the exposed front face.
    slab('Front panel rebate',(0,-.045,-.452),(.75,.035,.50),.095,.012,'boxshadow',front=True)
    slab('Front enamel insert',(0,-.035,-.474),(.67,.025,.415),.085,.01,'boxpanel',front=True)
    wave('Front ripple',[(x,-.025+.035*math.sin(x*math.pi/.21),-.491) for x in [(-.21+i*.42/24) for i in range(25)]],.015,'boxseal')
    # Mirror the front so rotated art reviews retain a deliberate finish.
    slab('Rear enamel insert',(0,-.035,.465),(.67,.025,.415),.085,.01,'boxpanel',front=True)
    for name,objects in groups.items():
        collection=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(collection)
        for obj in objects:
            for old in list(obj.users_collection):old.objects.unlink(obj)
            collection.objects.link(obj)
    bpy.context.scene.render.engine='CYCLES';bpy.context.scene.cycles.samples=32
    bpy.context.scene.render.bake.margin=12;bpy.context.scene.render.bake.use_clear=True
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'RippleBox.blend'))
    gimmick_pilot.OUT=OUT
    manifest=[gimmick_pilot.export('RippleBox',groups['RippleBox'],ao_strength=.45)]
    data=json.loads((OUT/'RippleBox.json').read_text(encoding='utf-8'))
    # Cut the authored visible surface into real matching fragments, preserving its UVs.
    cuts=[
        [((-.06,0,0),(1,0,0))],
        [((-.06,0,0),(-1,0,0)),((0,0,.035),(0,0,1))],
        [((-.06,0,0),(-1,0,0)),((0,0,.035),(0,0,-1))]
    ]
    for i,planes in enumerate(cuts):
        manifest.append(fragment(data,'RippleShard'+str(i),planes))
    (OUT/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')


def slab(name,p,size,radius,bevel,color,front=False):
    # Independent horizontal corner radius and vertical bevel: a thin lid should not
    # become a sharp rectangle simply because its thickness is small.
    w,h,d=size
    profile=[(-h/2,bevel),(-h/2+bevel*.293,bevel*.293),(-h/2+bevel,0),
             (h/2-bevel,0),(h/2-bevel*.293,bevel*.293),(h/2,bevel)]
    verts=[]
    for y,inset in profile:
        for corner in range(4):
            a0=corner*math.pi/2
            cx=(w/2-radius)*(1 if corner in (0,3) else -1)
            cz=(d/2-radius)*(1 if corner in (0,1) else -1)
            for step in range(9):
                a=a0+step*math.pi/16
                x=cx+(radius-inset)*math.cos(a);z=cz+(radius-inset)*math.sin(a)
                v=(x,z,-y) if front else (x,y,z)
                verts.append(tuple(v[i]+p[i] for i in range(3)))
    n=36;faces=[]
    for j in range(len(profile)-1):
        for i in range(n):faces.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
    faces.extend([tuple(range(n-1,-1,-1)),tuple((len(profile)-1)*n+i for i in range(n))])
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
    obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj);finish(obj,color)
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
    modifier=obj.modifiers.new('Weighted lid normals','WEIGHTED_NORMAL');modifier.keep_sharp=True;modifier.weight=50
    bpy.ops.object.modifier_apply(modifier=modifier.name)


def droplet(name,scale,y,z,color):
    curves=[
        ((0,.22),(.03,.16),(.18,.045),(.18,-.06)),
        ((.18,-.06),(.18,-.25),(-.18,-.25),(-.18,-.06)),
        ((-.18,-.06),(-.18,.045),(-.03,.16),(0,.22))
    ]
    outline=[]
    for p0,p1,p2,p3 in curves:
        for i in range(12):
            t=i/12;u=1-t
            outline.append(tuple((u**3*p0[a]+3*u*u*t*p1[a]+3*u*t*t*p2[a]+t**3*p3[a])*scale for a in (0,1)))
    n=len(outline);verts=[(x,y+dy,z+v) for dy in [-.012,.012] for x,v in outline]
    faces=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
    obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj)
    finish(obj,color,.009)


def wave(name,points,radius,color):
    curve=bpy.data.curves.new(name,'CURVE');curve.dimensions='3D';curve.resolution_u=1
    curve.bevel_depth=radius;curve.bevel_resolution=2;curve.use_fill_caps=True
    spline=curve.splines.new('POLY');spline.points.add(len(points)-1)
    for target,p in zip(spline.points,points):target.co=(*p,1)
    obj=bpy.data.objects.new(name,curve);bpy.context.collection.objects.link(obj)
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    bpy.ops.object.convert(target='MESH');finish(bpy.context.object,color)


def fragment(data,name,planes):
    vertices=[tuple(data['vertices'][i:i+3]) for i in range(0,len(data['vertices']),3)]
    faces=[tuple(data['triangles'][i:i+3]) for i in range(0,len(data['triangles']),3)]
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.update()
    uv=mesh.uv_layers.new()
    for loop in mesh.loops:uv.data[loop.index].uv=data['uv'][loop.vertex_index*2:loop.vertex_index*2+2]
    bm=bmesh.new();bm.from_mesh(mesh)
    # UV/normal seams are separate vertices in the interchange mesh. Weld first for closed cuts.
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001)
    uv_layer=bm.loops.layers.uv.active
    cap_uv=Vector(data['uv'][0:2])
    for point,normal in planes:
        result=bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),
            plane_co=Vector(point),plane_no=Vector(normal),clear_outer=True,clear_inner=False,dist=.000001)
        edges=[e for e in result['geom_cut'] if isinstance(e,bmesh.types.BMEdge) and e.is_boundary]
        if edges:
            caps=bmesh.ops.holes_fill(bm,edges=edges,sides=0)
            for face in caps['faces']:
                for loop in face.loops:loop[uv_layer].uv=cap_uv
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
    for face in mesh.polygons:face.use_smooth=True
    mesh.calc_loop_triangles()
    lo=Vector(tuple(min(v.co[a] for v in mesh.vertices) for a in range(3)))
    hi=Vector(tuple(max(v.co[a] for v in mesh.vertices) for a in range(3)))
    centre=(lo+hi)*.5
    verts=[];normals=[];texcoords=[];indices=[]
    for tri in mesh.loop_triangles:
        for li in tri.loops:
            v=mesh.vertices[mesh.loops[li].vertex_index].co-centre
            verts.extend(round(x,6) for x in v);normals.extend(round(x,6) for x in mesh.corner_normals[li].vector)
            texcoords.extend(round(x,6) for x in mesh.uv_layers.active.data[li].uv);indices.append(len(indices))
    exported=dict(name=name,vertices=verts,normals=normals,uv=texcoords,triangles=indices,center=list(centre))
    (OUT/(name+'.json')).write_text(json.dumps(exported,separators=(',',':')),encoding='utf-8')
    result=dict(name=name,vertices=len(verts)//3,triangles=len(indices)//3)
    print('BOX_FRAGMENT_EXPORTED',result,flush=True)
    return result
