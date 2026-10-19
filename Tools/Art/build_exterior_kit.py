import bpy, math, random, json
from pathlib import Path
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/SneakHouse/Models/Exterior'
OUT.mkdir(parents=True,exist_ok=True)
previous=bpy.context.window.scene
for old_scene in list(bpy.data.scenes):
    if old_scene.name.startswith('Sneak House - Exterior Kit v1') and old_scene != previous:
        for obj in list(old_scene.objects):
            if len(obj.users_scene)==1:bpy.data.objects.remove(obj,do_unlink=True)
        bpy.data.scenes.remove(old_scene)
scene=bpy.data.scenes.new('Sneak House - Exterior Kit v1')
bpy.context.window.scene=scene
scene.unit_settings.system='METRIC'
rng=random.Random(41)
M={}
for name,h in {'wood':'986343','woodlight':'B88250','wooddark':'684731','leaf':'778948','leaflight':'94A35A','leafdark':'586B3B','cream':'E7D9AF','stone':'B5AC90','stonelight':'CDC3A6','soil':'766344','grass':'7A8B51','flower':'DEA46F','pink':'CC8E89','yellow':'E5CB77','stem':'627B41','metal':'698780','brass':'BFA05F','water':'7EA9A3','cloth':'D3B393'}.items():
    c=tuple(int(h[i:i+2],16)/255 for i in (0,2,4))
    m=bpy.data.materials.new('Exterior_'+name);m.diffuse_color=(*c,1);m.use_nodes=True
    bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    bs.inputs['Base Color'].default_value=(*c,1);bs.inputs['Roughness'].default_value=.9
    M[name]=m

assets={}; current=None
def finish(o,name,mat):
    o.name=name;o.data.materials.append(M[mat]);o.parent=current
    return o
def cube(name,loc,size,mat,rot=0):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc)
    o=bpy.context.object;o.scale=size;o.rotation_euler.z=rot
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return finish(o,name,mat)
def ico(name,loc,size,mat,sub=1):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=sub,radius=1,location=loc)
    o=bpy.context.object;o.scale=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return finish(o,name,mat)
def cyl(name,loc,r,depth,mat,vertices=8,r2=None):
    bpy.ops.mesh.primitive_cone_add(vertices=vertices,radius1=r,radius2=r if r2 is None else r2,depth=depth,location=loc)
    return finish(bpy.context.object,name,mat)
def beam(name,a,b,r,mat):
    d=Vector(b)-Vector(a);o=cyl(name,(Vector(a)+Vector(b))/2,r,d.length,mat,7,r*.72)
    o.rotation_euler=d.to_track_quat('Z','Y').to_euler();return o
def mesh(name,verts,faces,mat):
    me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update()
    o=bpy.data.objects.new(name,me);scene.collection.objects.link(o);return finish(o,name,mat)
def ring(name,center,r,width,mat,n=16):
    v=[]
    for radius in (r,r-width):
        v.extend([(center[0]+math.cos(i*2*math.pi/n)*radius,center[1]+math.sin(i*2*math.pi/n)*radius,center[2]) for i in range(n)])
    return mesh(name,v,[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],mat)
def begin(name):
    global current
    current=bpy.data.objects.new(name,None);scene.collection.objects.link(current);assets[name]=current

begin('garden_tree')
cyl('Trunk',(0,0,.95),.14,1.9,'wooddark',7,.09)
beam('Branch_L',(0,0,1.1),(-.48,.05,1.85),.07,'wooddark')
beam('Branch_R',(0,0,1.4),(.48,.12,2.05),.06,'wood')
canopy=bpy.data.objects.new('Canopy',None);scene.collection.objects.link(canopy);canopy.parent=current;canopy.location=(0,0,1.65)
for i,(p,s) in enumerate([((-.45,0,2.1),(.69,.61,.66)),((.4,.12,2.3),(.74,.66,.73)),((0,-.15,2.67),(.69,.64,.65)),((0,.39,2.24),(.65,.62,.61))]):
    o=ico('Foliage_'+str(i),p,s,['leaf','leaflight','leaf','leafdark'][i]);o.parent=canopy;o.location-=canopy.location

begin('hedge_section')
for i in range(5):ico('Hedge_'+str(i),(-.8+i*.4,0,.48),(.43,.35,.46),['leafdark','leaf','leaf','leaflight','leaf'][i])

begin('fence_section')
for x in (-1.1,1.1):
    cube('Post',(x,0,.49),(.12,.14,.98),'wooddark');cyl('Post_cap',(x,0,1.0),.11,.08,'woodlight',4,0)
for z in (.27,.71):cube('Rail',(0,0,z),(2.2,.10,.12),'wood')
for i in range(7):cube('Pale',(-.92+i*.306,0,.49),(.10,.065,.7),'woodlight')

begin('garden_gate')
for z in (.25,.70):cube('Crossrail',(0,0,z),(1.6,.10,.10),'wooddark')
for i in range(8):cube('Gate_pale',(-.70+i*.20,0,.46),(.15,.095,.84),'wood')
ico('Latch',(.54,-.09,.60),(.06,.04,.04),'brass')

begin('mailbox')
cube('Post',(0,0,.43),(.10,.10,.86),'wooddark')
cube('Body',(0,0,.93),(.48,.34,.30),'cream')
mesh('Pitched_cap',[(-.28,-.2,1.08),(.28,-.2,1.08),(0,-.2,1.24),(-.28,.2,1.08),(.28,.2,1.08),(0,.2,1.24)],[(0,1,2),(3,5,4),(0,2,5,3),(2,1,4,5)],'wood')
cube('Letter_slot',(0,-.176,.97),(.30,.018,.035),'wooddark')
cube('Flag',(.27,0,1.02),(.025,.17,.10),'flower')

begin('stepping_stone')
angles=[2*math.pi*i/7 for i in range(7)];radii=[rng.uniform(.86,1.10) for _ in angles]
vs=[(math.cos(a)*r*.5,math.sin(a)*r*.38,z) for z in (0,.055) for a,r in zip(angles,radii)]
o=mesh('Stone',vs,[tuple(range(6,-1,-1)),tuple(range(7,14))]+[(i,(i+1)%7,(i+1)%7+7,i+7) for i in range(7)],'stone')
o.data.materials.append(M['stonelight']);o.data.polygons[1].material_index=1

begin('flower_cluster')
for i in range(8):
    x=rng.uniform(-.42,.42);y=rng.uniform(-.3,.3);h=rng.uniform(.22,.43)
    beam('Flower_stem',(x,y,0),(x,y,h),.013,'stem')
    color=['cream','yellow','flower','pink'][i%4]
    for j in range(5):
        a=j*math.pi*2/5
        ico('Petal',(x+math.cos(a)*.058,y+math.sin(a)*.058,h),(.058,.05,.025),color)
    ico('Pollen',(x,y,h+.019),(.035,.035,.024),'yellow')
for i in range(9):
    a=rng.random()*math.pi*2;x=rng.uniform(-.45,.45);y=rng.uniform(-.3,.3)
    mesh('Leaf',[(x-.025,y,0),(x+.025,y,0),(x+math.cos(a)*.12,y+math.sin(a)*.12,.22)],[(0,1,2)],'stem')

begin('grass_clump')
for i in range(7):
    a=i*math.pi*2/7;x=math.cos(a)*.08;y=math.sin(a)*.08
    mesh('Grass_blade',[(x-.025,y,0),(x+.025,y,0),(x+math.cos(a)*.1,y+math.sin(a)*.1,rng.uniform(.15,.3))],[(0,1,2)],'grass')

begin('wooden_crate')
cube('Dark_body',(0,0,.30),(.68,.58,.60),'wooddark')
for z in (.12,.30,.48):
    for y in (-.30,.30):cube('Side_plank',(0,y,z),(.73,.05,.14),'woodlight')
    for x in (-.36,.36):cube('End_plank',(x,0,z),(.05,.6,.14),'wood')
for x in (-.26,.26):cube('Top_batten',(x,0,.62),(.09,.64,.05),'wood')

begin('watering_can')
cyl('Body',(0,0,.19),.19,.34,'metal',10,.16)
beam('Spout',(.1,0,.12),(.44,0,.34),.055,'metal')
cyl('Rose',(.44,0,.35),.075,.045,'brass',8)
for a,b in [((-.13,0,.33),(-.29,0,.42)),((-.29,0,.42),(-.36,0,.23)),((-.36,0,.23),(-.16,0,.1))]:beam('Handle',a,b,.025,'metal')

begin('hose_coil')
for i in range(3):ring('Hose',(0,0,.025+i*.02),.29-i*.055,.035,'metal',18)

begin('garden_seat')
for x in (-.27,.27):
    for y in (-.23,.23):cube('Leg',(x,y,.21),(.06,.06,.42),'wooddark')
for y in (-.22,0,.22):cube('Seat_slat',(0,y,.46),(.69,.18,.07),'woodlight')
for x in (-.29,.29):cube('Back_upright',(x,.25,.70),(.055,.055,.60),'wooddark')
for z in (.72,.91):cube('Back_slat',(0,.27,z),(.69,.06,.13),'wood')

begin('garden_table')
cyl('Top',(0,0,.56),.42,.07,'woodlight',10)
cyl('Pedestal',(0,0,.29),.07,.53,'wooddark')
cube('Foot_x',(0,0,.05),(.57,.08,.07),'wooddark');cube('Foot_y',(0,0,.05),(.08,.57,.07),'wooddark')
cyl('Pot',(0,0,.66),.085,.14,'flower',8,.11)
for i in range(3):ico('Plant',(rng.uniform(-.05,.05),rng.uniform(-.05,.05),.81),(.07,.07,.11),'leaf')

begin('birdbath')
cyl('Foot',(0,0,.06),.28,.12,'stone',10,.22)
cyl('Stem',(0,0,.38),.11,.58,'stonelight',8,.085)
cyl('Bowl',(0,0,.69),.32,.12,'stone',12,.39)
ring('Rim',(0,0,.755),.39,.055,'stonelight',12)
cyl('Water',(0,0,.755),.33,.006,'water',20)

begin('laundry_rack')
for x in (-.66,.66):
    cube('Pole',(x,0,.65),(.05,.05,1.3),'wooddark')
    cube('Foot',(x,0,.04),(.08,.54,.06),'wood')
beam('Clothesline',(-.66,0,1.26),(.66,0,1.26),.014,'cream')
verts=[]
for z in (1.23,.48):
    for i in range(7):verts.append((-.40+i*.133,math.sin(i*1.6)*.045,z))
cloth=mesh('Cloth',verts,[(i,i+1,i+8,i+7) for i in range(6)],'cloth')
for x in (-.35,.35):cube('Peg',(x,0,1.26),(.035,.035,.08),'woodlight')

begin('porch_lantern')
cube('Bracket',(0,.06,.20),(.06,.16,.05),'wooddark')
cyl('Glass',(0,-.04,.13),.10,.22,'yellow',6)
for a in range(4):
    t=a*math.pi/2;cube('Frame',(math.cos(t)*.085,-.04+math.sin(t)*.085,.13),(.025,.025,.26),'wooddark')
cyl('Roof',(0,-.04,.29),.15,.1,'brass',6,.035)

manifest=[]
for name,root in assets.items():
    # Combine parts per rigid group; retain canopy and cloth for independent wind motion.
    groups={}
    for obj in list(root.children_recursive):
        if obj.type=='MESH' and not obj.name.startswith('Cloth'):
            groups.setdefault(obj.parent,[]).append(obj)
    for group,objects in groups.items():
        if len(objects)>1:
            bpy.ops.object.select_all(action='DESELECT')
            for obj in objects:obj.select_set(True)
            bpy.context.view_layer.objects.active=objects[0]
            bpy.ops.object.join();bpy.context.object.name=name+'_geometry'
    bpy.ops.object.select_all(action='DESELECT')
    members=[root]+list(root.children_recursive)
    for o in members:o.select_set(True)
    bpy.context.view_layer.objects.active=root
    bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'EMPTY','MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False)
    tris=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in members if o.type=='MESH')
    manifest.append({'asset':name,'triangles':tris})

source=ROOT/'Tools/Art';source.mkdir(parents=True,exist_ok=True)
for i,root in enumerate(assets.values()):root.location=((i%4)*3.5,(i//4)*3.5,0)
bpy.ops.object.camera_add(location=(17,-17,21))
camera=bpy.context.object;camera.name='Kit overview';camera.rotation_euler=(Vector((5.2,5.2,.8))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=21;scene.camera=camera
for position,energy,size in [((1,-3,12),1800,9),((10,8,10),1300,7)]:
    bpy.ops.object.light_add(type='AREA',location=position);light=bpy.context.object;light.data.energy=energy;light.data.shape='DISK';light.data.size=size
    light.rotation_euler=(Vector((5,5,0))-light.location).to_track_quat('-Z','Y').to_euler()
scene.world=bpy.data.worlds.new('Exterior kit world');scene.world.color=(.3,.3,.3)
bpy.data.libraries.write(str(source/'exterior-kit-v1.blend'),{scene},fake_user=True)
(source/'exterior-kit-manifest.json').write_text(json.dumps(manifest,indent=2))
bpy.context.window.scene=previous
print(json.dumps({'exported':len(manifest),'triangles':sum(x['triangles'] for x in manifest),'folder':str(OUT)}))
