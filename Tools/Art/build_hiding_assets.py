import bpy,math
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
previous=bpy.context.window.scene;scene=bpy.data.scenes.new('Sneak House - Hiding spots');bpy.context.window.scene=scene
M={}
for name,h in {'LeafA':'647A47','LeafB':'829454','LeafC':'56693E','Canvas':'8C9B86','CanvasShade':'657B6B','Trim':'D3BE8F','Inside':'4E5644','Wood':'77553A'}.items():
 c=tuple(int(h[i:i+2],16)/255 for i in (0,2,4));m=bpy.data.materials.new('Hide_'+name);m.diffuse_color=(*c,1);m.use_nodes=True;n=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');n.inputs['Base Color'].default_value=(*c,1);n.inputs['Roughness'].default_value=.9;M[name]=m
def root(name):
 o=bpy.data.objects.new(name,None);scene.collection.objects.link(o);return o
def mesh(name,verts,faces,mat,parent):
 d=bpy.data.meshes.new(name);d.from_pydata(verts,[],faces);d.update();o=bpy.data.objects.new(name,d);scene.collection.objects.link(o);o.data.materials.append(M[mat]);o.parent=parent;return o
def beam(a,b,width,mat,parent):
 from mathutils import Vector
 delta=Vector(b)-Vector(a);bpy.ops.mesh.primitive_cylinder_add(vertices=6,radius=width,depth=delta.length,location=(Vector(a)+Vector(b))/2);o=bpy.context.object;o.rotation_euler=delta.to_track_quat('Z','Y').to_euler();o.data.materials.append(M[mat]);o.parent=parent
bush=root('hiding_bush')
for i,(p,s) in enumerate([((-.48,0,.50),(.52,.48,.53)),((.49,0,.48),(.54,.47,.50)),((0,-.10,.72),(.65,.49,.59)),((-.22,.32,.57),(.50,.43,.55)),((.32,.27,.68),(.49,.40,.54))]):
 bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2,radius=1,location=p);o=bpy.context.object;o.scale=s;o.parent=bush;o.data.materials.append(M[['LeafA','LeafB','LeafA','LeafC','LeafB'][i]])
tent=root('hiding_tent')
mesh('Canvas left',[(-.85,-.62,.03),(-.85,.62,.03),(0,.62,1.28),(0,-.62,1.28)],[(3,2,1,0),(0,1,2,3)],'Canvas',tent)
mesh('Canvas right',[(0,-.62,1.28),(0,.62,1.28),(.85,.62,.03),(.85,-.62,.03)],[(3,2,1,0),(0,1,2,3)],'CanvasShade',tent)
mesh('Back panel',[(-.85,.62,.03),(.85,.62,.03),(0,.62,1.28)],[(0,1,2),(2,1,0)],'CanvasShade',tent)
mesh('Dark interior',[(-.66,.48,.04),(.66,.48,.04),(0,.48,1.07)],[(0,1,2),(2,1,0)],'Inside',tent)
mesh('Ground sheet',[(-.84,-.60,.035),(.84,-.60,.035),(.84,.6,.035),(-.84,.6,.035)],[(0,1,2,3),(3,2,1,0)],'Inside',tent)
mesh('Door flap L',[(-.85,-.635,.03),(-.32,-.68,.06),(-.07,-.66,1.17),(0,-.635,1.28)],[(0,1,2,3),(3,2,1,0)],'Canvas',tent)
mesh('Door flap R',[(.85,-.635,.03),(0,-.635,1.28),(.07,-.66,1.17),(.32,-.68,.06)],[(0,1,2,3),(3,2,1,0)],'Canvas',tent)
for a,b in [((-.85,-.65,.04),(0,-.65,1.28)),((0,-.65,1.28),(.85,-.65,.04)),((0,-.65,1.28),(0,.65,1.28))]:beam(a,b,.018,'Trim',tent)
for x in (-.92,.92):
 for y in (-.69,.69):beam((x,y,0),(x,y,.16),.025,'Wood',tent)
for asset in (bush,tent):
 bpy.ops.object.select_all(action='DESELECT')
 for o in asset.children:o.select_set(True)
 bpy.context.view_layer.objects.active=asset.children[0];bpy.ops.object.join();bpy.context.object.name=asset.name+'_geometry';asset.select_set(True)
 bpy.ops.export_scene.fbx(filepath=str(ROOT/'Assets/SneakHouse/Models/Exterior'/(asset.name+'.fbx')),use_selection=True,object_types={'EMPTY','MESH'},axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS',bake_anim=False,add_leaf_bones=False)
tent.location.x=3
bpy.data.libraries.write(str(ROOT/'Tools/Art/hiding-spots.blend'),{scene},fake_user=True);bpy.context.window.scene=previous
print('Dense bush and canvas tent exported; original scenes retained')
