"""Make a matted animation preview with Aseprite; keep native RGBA art intact."""
import json
from draw_swordsman import Client, ROOT, WALK_FRAMES, build_pose, unpack

with Client() as client:
    def call(name, **args):
        return unpack(client.call(name, **args))
    for name, count in [("south-idle", 1), ("south-walk", WALK_FRAMES)]:
        native=(ROOT/f"{name}.aseprite").as_posix()
        review=(ROOT/"review"/f"{name}-matte.aseprite").as_posix()
        call("save_as", sprite_path=native, output_path=review)
        for frame in range(1,count+1):
            call("draw_rectangle",sprite_path=review,layer_name="00 Contact shadow",frame_number=frame,
                 x=0,y=0,width=128,height=160,color="#34313A",filled=True)
            cel=build_pose(None if count==1 else frame-1)["00 Contact shadow"]
            def composite(p):
                value=p['color'].lstrip('#')
                alpha=int(value[6:8],16)/255
                rgb=[round(int(value[i:i+2],16)*alpha+bg*(1-alpha)) for i,bg in zip([0,2,4],[52,49,58])]
                return dict(p,color='#'+''.join(f'{v:02X}' for v in rgb))
            pixels=[composite(p) for p in cel.payload()]
            call("draw_pixels",sprite_path=review,layer_name="00 Contact shadow",frame_number=frame,pixels=pixels)
        call("scale_sprite",sprite_path=review,scale_x=4,scale_y=4,algorithm="nearest")
        call("export_sprite",sprite_path=review,output_path=(ROOT/"review"/f"{name}-preview.png").as_posix(),format="png",frame_number=1)
        if count>1:
            call("export_sprite",sprite_path=review,output_path=(ROOT/"review"/f"{name}-preview.gif").as_posix(),format="gif",frame_number=0)
        print(name,"preview exported",flush=True)
    comparison=call("create_canvas",width=1024,height=640,color_mode="rgb")["file_path"].replace('\\','/')
    for label,version,x in [("Previous v1","검사-픽셀-남향-v1",0),("Revised v2","검사-픽셀-남향-v2",512)]:
        call("import_image",sprite_path=comparison,
             image_path=(ROOT.parent/version/"review"/"south-idle-preview.png").as_posix(),
             layer_name=label,frame_number=1,position={"x":x,"y":0})
    call("export_sprite",sprite_path=comparison,
         output_path=(ROOT/"review"/"idle-before-after.png").as_posix(),format="png",frame_number=1)
    # Separate RGBA components accompany the real layered .aseprite masters.
    (ROOT/"layers-idle").mkdir(exist_ok=True)
    for index,(name,cel) in enumerate(build_pose(None).items()):
        isolated=call("create_canvas",width=128,height=160,color_mode="rgb")["file_path"].replace('\\','/')
        call("draw_pixels",sprite_path=isolated,layer_name="Layer 1",frame_number=1,pixels=cel.payload())
        call("export_sprite",sprite_path=isolated,output_path=(ROOT/"layers-idle"/f"{index:02}-{name[3:].replace(' ','-')}.png").as_posix(),format="png",frame_number=1)
    print("Comparison and 12 separate layer PNGs exported",flush=True)
