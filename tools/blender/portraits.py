# Render the story creatures and islanders as portraits, using Fesh's own pixel palette renderer.
# blender -b --python tools/blender/portraits.py -- <preview_dir> <game_src_dir>
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from pixelrender import Renderer, args

a = args()
OUT, SRC = os.path.abspath(a[0]), os.path.abspath(a[1])
os.makedirs(OUT, exist_ok=True)

def shades(h):
    v = [int(h[i:i+2], 16) for i in (1, 3, 5)]
    return tuple('#' + ''.join(f'{max(0,min(255,int(c*k+b))):02x}' for c in v)
                 for k,b in ((.63,0),(.85,0),(1,0),(1,26)))

def ball(r, mat, x, y, z, sx, sy, sz):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=16, radius=1, location=(x,y,r.Z(z)))
    o = bpy.context.object
    o.scale = (sx,sy,r.Z(sz))
    bpy.ops.object.shade_smooth()
    return r._put(o, r.ids[mat])

def patch(r, mat, points):
    mesh = bpy.data.meshes.new(mat)
    mesh.from_pydata([(x,y,r.Z(z)) for x,y,z in points], [], [list(range(len(points)))])
    mesh.update()
    o = bpy.data.objects.new(mat, mesh)
    r.scene.collection.objects.link(o)
    return r._put(o, r.ids[mat])

def line(r, mat, points, width=1):
    for p,q in zip(points,points[1:]): r.rod(p,q,width,r.ids[mat])

def eye(r,x,y,z,size=2,glow=False):
    ball(r,'ink',x,y,z,size*1.35,.8,size*1.35)
    ball(r,'light' if glow else 'iris',x,y-.65,z,size,.5,size)
    ball(r,'ink',x+.3,y-1,z,.45,.25,size*.72)
    ball(r,'white',x-.5,y-1.15,z+.7,.45,.2,.45)

def fish(r, guardian=False):
    # A broad head, thick lips, a rounded tail and continuous spiny dorsal identify the guardian as a grouper.
    ball(r,'body',-4,0,0,35,10,19 if guardian else 15)
    ball(r,'body',22,-.3,-1,17,12,16 if guardian else 13)
    ball(r,'belly',3,-3,-9,26,8,7)
    patch(r,'fin',[(-31,0,5),(-53,1,18),(-49,-1,0),(-53,1,-17),(-30,0,-5)])
    for z in (-13,-6,0,6,13): line(r,'ridge',[(-34,-.7,0),(-49,-.7,z)],.55)
    patch(r,'fin',[(-25,1,11),(-18,1,25),(-9,1,19),(-2,1,27),(7,1,20),(16,1,25),(21,1,11)])
    for x in range(-18,20,7): line(r,'ridge',[(x,-.2,12),(x-1,-.2,22 if guardian else 20)],.55)
    patch(r,'fin',[(4,-9,0),(-11,-15,-13),(11,-10,-8),(17,-9,-2)])
    for x in (-7,-2,3): line(r,'ridge',[(14,-10,-3),(x,-14,-11)],.45)
    line(r,'ink',[(34,-7,-4),(26,-11,-5),(22,-11,-4)],.65)
    ball(r,'belly',32,-7,-6,5,3,2)
    line(r,'ridge',[(18,-10,10),(13,-12,4),(14,-12,-5),(19,-9,-10)],.65)
    eye(r,27,-10,5,2.1,guardian)
    if guardian:
        for row,z in enumerate((-7,0,8)):
            for x in range(-26,12,9):
                ball(r,'mark',x+(row%2)*3,-9.7,z,2.2,.6,1.4)
                ball(r,'light',x+(row%2)*3-.4,-10.3,z+.2,.75,.35,.55)
    else:
        for x in (9,4,-1): line(r,'light',[(x,-9,8),(x-3,-11,3),(x-2,-11,-3),(x+1,-9,-7)],.8)
        for x,z in ((-12,7),(-22,2),(-8,-3),(-17,-7)):
            ball(r,'light',x,-10,z,1,.55,1)

def crawler(r):
    # The fish tail and ribbed shell remain recognisable alongside six jointed walking legs and two pincers.
    for side in (1,-1):
        for i,x in enumerate((-16,-3,10)):
            yy=side*6
            line(r,'fin',[(x,yy,-3),(x-4,side*13,-12),(x-9,side*18,-21+i)],1.6)
            ball(r,'ridge',x-4,side*13,-12,2,2,2)
    patch(r,'fin',[(-24,0,3),(-46,0,15),(-40,-1,0),(-46,0,-13),(-24,0,-3)])
    ball(r,'body',-3,0,0,26,12,15)
    for x in (-17,-9,-1,7):
        line(r,'ridge',[(x,3,14),(x-2,-7,10),(x,-12,0),(x+2,-8,-10)],.75)
    for x,y,z,s in ((30,-9,11,1),(35,6,-2,.85)):
        line(r,'fin',[(15,y/2,-1),(23,y,4),(x,y,z)],2)
        ball(r,'body',x,y,z,7*s,4*s,5*s)
        line(r,'ridge',[(x+1,y-4,z),(x+7,y-2,z+4)],1.8)
        line(r,'body',[(x+2,y-4,z-2),(x+8,y-2,z-4)],1.7)
    for x,y in ((11,-7),(18,3)):
        line(r,'fin',[(x,y,8),(x+1,y-2,20)],1)
        eye(r,x+1,y-3,20,2)
    for x,z in ((-15,6),(-7,9),(1,7),(8,3)): ball(r,'mark',x,-10,z,1,.7,1)

def eel(r):
    # A ribbon fin and actual dark openings in the luminous ribs carry its hollow-body motif.
    pts=[]
    for i in range(41):
        t=i/40
        x=-48+t*88; z=math.sin(t*math.pi*2)*13
        pts.append((x,math.sin(t*math.pi)*2,z))
        rad=1.3+5.4*min(1,i/15)
        ball(r,'body',x,pts[-1][1],z,3.3,rad,rad)
    patch(r,'fin',[(x,y+1,z+8) for x,y,z in pts[5:36]]+[(x,y+1,z+3) for x,y,z in reversed(pts[5:36])])
    line(r,'ridge',[(x,y-3,z+4) for x,y,z in pts[3:]],.7)
    for i in (10,16,22,28,34):
        x,y,z=pts[i]
        ball(r,'ridge',x,y-5.4,z,2.7,1,3.6)
        ball(r,'ink',x+.2,y-6.2,z,1.6,.7,2.5)
    x,y,z=pts[-1]
    ball(r,'body',x+4,y,z,9,7,6)
    eye(r,x+7,y-6,z+2,1.6,True)
    line(r,'ink',[(x+12,y-4,z-1),(x+7,y-6,z-2)],.55)

def ray(r):
    # An oblique view, broad diamond wings and silver facet strips distinguish it from the Starfall manta.
    verts=[(0,-9,16),(-13,-2,9),(-48,1,-1),(-25,3,-9),(0,4,-15),(25,3,-9),(48,1,-1),(13,-2,9),(0,-7,1)]
    for i in range(8): patch(r,'body' if i%3 else 'belly',[verts[i],verts[(i+1)%8],verts[8]])
    for side in (-1,1):
        patch(r,'ridge',[(side*10,-4,7),(side*39,0,0),(side*22,-1,2)])
        line(r,'light',[(side*11,-4,8),(side*32,-1,2)],.65)
        eye(r,side*5,-8,10,1.5)
    ball(r,'body',0,-3,1,7,6,15)
    line(r,'fin',[(0,3,-13),(3,5,-23),(10,6,-31),(17,6,-35)],.8)

def write_class(name,entries):
    lines=['// Generated by tools/blender/portraits.py. Change the models, then regenerate.','namespace Fesh;','',f'static class {name}','{',
           '    internal sealed record Sprite(string[] Palette, string[] Rows);',
           '    public static readonly Dictionary<string, Sprite> All = new()', '    {']
    for key,r,grid in entries:
        pal=r.colours([grid])
        lines += [f'        ["{key}"] = new(new[] {{ '+', '.join('"'+c+'"' for c in pal)+' }, new[]', '        {']
        lines += ['            "'+row+'",' for row in r.rows(grid,pal)]
        lines += ['        }),']
    lines += ['    };','}']
    with open(os.path.join(SRC,name+'.cs'),'w',encoding='utf8',newline='\n') as f: f.write('\n'.join(lines)+'\n')

story=[]
for key,base,fin,light,fn in (
    ('glowgill','#347caa','#23547f','#a5fff2',fish),
    ('tidecrawler','#d87940','#97432d','#eeb675',crawler),
    ('hollow_eel','#7563a1','#463768','#d6c9ff',eel),
    ('mirror_ray','#b8ccdd','#658ba3','#f0fff4',ray),
    ('abyssal','#344f5c','#243c45','#ffe6a4',lambda r:fish(r,True)),
):
    tones={'body':shades(base),'fin':shades(fin),'belly':shades('#8dada8' if key=='abyssal' else base),
           'ridge':shades('#9bbab6' if key=='abyssal' else light),'light':shades(light),'mark':shades('#768e7c'),
           'ink':('#101d2c',)*3,'iris':shades('#edc776'),'white':('#fff2d9',)*3}
    r=Renderer(128,88,pitch=13,ss=4,tones=tones,lit_at=(.68,.06))
    fn(r); r.aim((-3,0,0))
    grid=r.render(OUT)
    r.preview(os.path.join(OUT,key+'.png'),[grid],scale=5)
    story.append((key,r,grid))
write_class('StoryPortraitArt',story)

# Speaker name, skin, hair, clothing, hair shape, headwear, individual feature.
people=[
    ('Tomas','#e0b07d','#e8e8e8','#2f5d8a','short','cap','beard'),
    ('Pip','#a9714b','#b5523b','#8a5fb5','long','cap',''),
    ('Lira','#d9a066','#2b1d14','#e6a843','long','',''),
    ('Niko','#d9a066','#2b1d14','#367caa','short','',''),
    ('Maya','#d9a066','#2b1d14','#b45b66','long','','flower'),
    ('Tala','#d9a066','#2b1d14','#72a752','long','',''),
    ("Ma'am Isay",'#b07a52','#2b1d14','#f2ecf8','bun','','glasses'),
    ('Manay Mila','#a9714b','#2b1d14','#c0392b','bun','straw',''),
    ('Ben','#d9a066','#2b1d14','#2a8a8a','short','cap','glasses'),
    ('Manang Rosa','#a9714b','#2b1d14','#d9734f','bun','',''),
    ('Lola Pacing','#a9714b','#e8e8e8','#8a5fb5','bun','','glasses'),
    ('Dado','#6b4428','#2b1d14','#2f7fa3','short','cap',''),
    ('Tatay Celso','#d9a066','#c8c8c8','#5e6468','short','straw','beard'),
    ('Aling Sela','#c68a5a','#e8e8e8','#3f6f9a','long','straw','glasses'),
    ('Jun','#b97a4e','#2b1d14','#e6a843','short','cap','child'),
    ('Mang Ambo','#a86f45','#5a5048','#6f8f5a','short','straw','beard'),
    ('Lorna','#c68a5a','#2b1d14','#f2efe6','long','',''),
    ('Mang Iyo','#b97a4e','#cfcac0','#8a5fb5','short','','beard'),
    ('Aling Nita','#c68a5a','#3a2a1e','#e88ab0','long','straw','flower'),
    ('Bantay Joy','#a9714b','#2b1d14','#2f9a8a','long','cap',''),
    ('Mia','#d9a066','#2b1d14','#d97a84','long','','child'),
    ('Bea','#a9714b','#2b1d14','#72a752','bun','','child'),
]

def person(r,hair,hat,feature,index):
    ball(r,'shirt',0,2,4,21,10,17)
    ball(r,'skin',0,0,23,4.5,4,7)
    if hair in ('long','bun'): ball(r,'hair',0,3,34,12,7,17)
    ball(r,'skin',0,0,35,10.5 if feature!='child' else 11.5,7.5,13.5)
    for side in (-1,1):
        ball(r,'skin',side*10,0,34,2.1,3,4)
        ball(r,'hair',side*9,1,41,2,6,7)
    ball(r,'hair',0,1,46,10.5,7.5,4)
    ball(r,'hair',-4,-4,44,6,4,3.5)
    if hair=='bun': ball(r,'hair',9,4,47,5,5,5)
    if hair=='long':
        for side in (-1,1): ball(r,'hair',side*10,1,28,3,5,11)
    for side in (-1,1):
        ex=side*4
        ball(r,'white',ex,-6.8,36,2.0,.8,1.25)
        ball(r,'ink',ex+.25,-7.5,36,1,.5,1.2)
        ball(r,'white',ex,-8,36.5,.35,.15,.35)
        line(r,'hair',[(ex-1.7,-7,39),(ex+1.4,-7,39.3)],.5)
    ball(r,'skin',.5,-7.5,33.5,1.8,2.5,2.6)
    line(r,'lip',[(-3,-6.5,29.2),(0,-7.2,28.5),(3,-6.5,29.2)],.5)
    patch(r,'trim',[(-8,-8,20),(-1,-9,15),(-4,-9,24)])
    patch(r,'trim',[(8,-8,20),(1,-9,15),(4,-9,24)])
    for z in (6,11,16): ball(r,'gold',1,-8,z,.65,.65,.65)
    line(r,'trim',[(-15,-5,12),(-13,-8,3)],.5)
    if feature=='beard':
        ball(r,'hair',0,-5,25.5,6,3.5,3)
        for side in (-1,1): ball(r,'hair',side*2.5,-7.7,30,2.6,.7,.9)
    if feature=='glasses':
        for ex in (-4,4):
            bpy.ops.mesh.primitive_torus_add(major_radius=2.6,minor_radius=.4,major_segments=20,minor_segments=6,
                location=(ex,-8.3,r.Z(36)),rotation=(math.pi/2,0,0))
            r._put(bpy.context.object,r.ids['ink'])
        line(r,'ink',[(-1.4,-8.5,36),(1.4,-8.5,36)],.4)
    if feature=='flower':
        for i in range(5):
            t=i*math.tau/5
            ball(r,'trim',10+math.cos(t)*2,-4,43+math.sin(t)*2,1.6,.8,1.6)
        ball(r,'gold',10,-5,43,1,.7,1)
    if hat=='straw':
        ball(r,'hat',0,0,47,17,10,2)
        ball(r,'hat',0,1,50,10,7,4)
        line(r,'trim',[(-9,-6,49),(9,-6,49)],.6)
    if hat=='cap':
        ball(r,'hat',0,1,48,11,8,5)
        ball(r,'hat',2,-8,46.5,10,5,1.2)
        ball(r,'gold',1,-7.5,50,1.5,.7,1.5)

folk=[]
for index,(name,skin,hair,shirt,style,hat,feature) in enumerate(people):
    cap = '#1d2f45' if name=='Tomas' else '#2f7fa3' if name=='Bantay Joy' else '#c0392b' if name=='Jun' else '#e04b3a'
    tones={'skin':shades(skin),'hair':shades(hair),'shirt':shades(shirt),'hat':shades(cap if hat=='cap' else '#dfbd78'),
           'trim':shades('#ead7ae'),'gold':shades('#e6b85c'),'lip':shades('#854c3e'),
           'ink':('#172635',)*3,'white':('#f9eddb',)*3}
    r=Renderer(48,56,pitch=6,ss=4,tones=tones,lit_at=(.62,.04))
    person(r,style,hat,feature,index); r.aim((0,0,27))
    grid=r.render(OUT)
    folk.append((name,r,grid))
    if name in ('Tomas','Pip',"Ma'am Isay",'Maya','Lola Pacing'):
        r.preview(os.path.join(OUT,name.replace(' ','-').replace("'",'')+'.png'),[grid],scale=6)
write_class('FolkPortraitArt',folk)
print('DONE',len(story),'creatures,',len(folk),'islanders')
