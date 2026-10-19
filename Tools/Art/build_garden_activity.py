import bpy,math,json
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/SneakHouse/Models/Exterior'
previous=bpy.context.window.scene
scene=bpy.data.scenes.new('Sneak House - Moving garden props')
bpy.context.window.scene=scene
M={}
for name,h in {'sage':'718A68','ochre':'D4A159','cream':'E8D8AA','dark':'4C5143','metal':'6F9792','orange':'CE895C'}.items():
 m=bpy.data.materials.new('Activity_'+name);m.use_nodes=True
 c=tuple(int(h[i:i+2],16)/255 for i in (0,2,4));m.diffuse_color=(*c,1)
 n=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');n.inputs['Base Color'].default_value=(*c,1);n.inputs['Roughness'].default_value=.85;M[name]=m
assets={};parent=None
def empty(name,p=(0,0,0),par=None):
 o=bpy.data.objects.new(name,None);scene.collection.objects.link(o);o.location=p;o.parent=par;return o
def mesh(name,v,f,mat,par=None):
 me=bpy.data.meshes.new(name);me.from_pydata(v,[],f);me.update();o=bpy.data.objects.new(name,me);scene.collection.objects.link(o);o.data.materials.append(M[mat]);o.parent=par if par else parent;return o
def cylinder(name,p,r,h,mat,par=None):
 bpy.ops.mesh.primitive_cylinder_add(vertices=10,radius=r,depth=h,location=p);o=bpy.context.object;o.name=name;o.data.materials.append(M[mat]);o.parent=par if par else parent;return o
parent=empty('garden_pinwheel');assets[parent.name]=parent
cylinder('Stake',(0,0,.75),.035,1.5,'dark')
rotor=empty('Rotor',(0,-.045,1.5),parent)
for i in range(6):
 a=i*math.pi/3
 points=[]
 for r,t in [(.08,-.15),(.57,-.27),(.61,.07),(.20,.30)]:points.append((r*math.cos(a+t),0,r*math.sin(a+t)))
 mesh('Folded blade',points,[(0,1,2),(0,2,3),(2,1,0),(3,2,0)],['cream','sage','ochre'][i%3],rotor)
 # A darker triangular fold gives depth without a texture.
 mesh('Blade fold',[points[0],points[2],((points[0][0]+points[2][0])*.5,-.06,(points[0][2]+points[2][2])*.5)],[(0,1,2),(2,1,0)],'orange',rotor)
bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=.10,location=(0,-.11,1.5));o=bpy.context.object;o.parent=parent;o.data.materials.append(M['ochre']);o.name='Hub'

parent=empty('garden_sprinkler');assets[parent.name]=parent
cylinder('Base',(0,0,.04),.17,.08,'sage')
cylinder('Stem',(0,0,.15),.045,.23,'dark')
head=empty('SprayHead',(0,0,.26),parent)
cylinder('Head body',(0,0,0),.09,.09,'metal',head)
mesh('Nozzle',[(-.035,-.02,0),(.035,-.02,0),(.035,-.18,.025),(-.035,-.18,.025),(-.035,-.02,.06),(.035,-.02,.06),(.035,-.18,.07),(-.035,-.18,.07)],[(0,1,2,3),(4,7,6,5),(0,4,5,1),(3,2,6,7),(0,3,7,4),(1,5,6,2)],'ochre',head)

parent=empty('garden_butterfly');assets[parent.name]=parent
body=cylinder('Body',(0,0,0),.018,.19,'dark');body.rotation_euler.x=math.pi/2
for sign,label in [(-1,'Left'),(1,'Right')]:
 wing=empty('Wing'+label,(0,0,0),parent)
 v=[(0,-.08,0),(sign*.12,-.20,.015),(sign*.27,-.14,.012),(sign*.24,.02,0),(sign*.17,.15,.01),(sign*.06,.12,0)]
 mesh('Wing panel',v,[(0,1,2),(0,2,3),(0,3,4),(0,4,5),(2,1,0),(3,2,0),(4,3,0),(5,4,0)],'orange',wing)
 mesh('Wing marking',[(sign*.10,-.10,.018),(sign*.20,-.115,.02),(sign*.18,-.01,.02),(sign*.09,.025,.018)],[(0,1,2),(0,2,3),(2,1,0),(3,2,0)],'cream',wing)

for name,root in assets.items():
 groups={}
 for o in list(root.children_recursive):
  if o.type=='MESH':groups.setdefault(o.parent,[]).append(o)
 for group,objects in groups.items():
  if len(objects)>1:
   bpy.ops.object.select_all(action='DESELECT')
   for o in objects:o.select_set(True)
   bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();bpy.context.object.name=name+'_geometry'
 bpy.ops.object.select_all(action='DESELECT')
 for o in [root]+list(root.children_recursive):o.select_set(True)
 bpy.context.view_layer.objects.active=root
 bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'EMPTY','MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_anim=False,add_leaf_bones=False)
for i,root in enumerate(assets.values()):root.location=(i*2.5,0,0)
bpy.data.libraries.write(str(ROOT/'Tools/Art/garden-activity-v2.blend'),{scene},fake_user=True)
bpy.context.window.scene=previous
print('3 moving-prop FBX assets and editable Blender source saved')
