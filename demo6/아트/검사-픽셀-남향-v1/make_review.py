"""Make a matted animation preview with Aseprite; keep native RGBA art intact."""
import json
from draw_swordsman import Client, ROOT, build_pose, unpack

with Client() as client:
    def call(name, **args):
        return unpack(client.call(name, **args))
    for name, count in [("south-idle", 1), ("south-walk", 8)]:
        native=(ROOT/f"{name}.aseprite").as_posix()
        review=(ROOT/"review"/f"{name}-matte.aseprite").as_posix()
        call("save_as", sprite_path=native, output_path=review)
        for frame in range(1,count+1):
            call("draw_rectangle",sprite_path=review,layer_name="00 Contact shadow",frame_number=frame,
                 x=0,y=0,width=128,height=160,color="#34313A",filled=True)
            cel=build_pose(None)["00 Contact shadow"]
            pixels=[dict(p,color="#2F2B35") for p in cel.payload()]
            call("draw_pixels",sprite_path=review,layer_name="00 Contact shadow",frame_number=frame,pixels=pixels)
        call("scale_sprite",sprite_path=review,scale_x=4,scale_y=4,algorithm="nearest")
        call("export_sprite",sprite_path=review,output_path=(ROOT/"review"/f"{name}-preview.png").as_posix(),format="png",frame_number=1)
        if count>1:
            call("export_sprite",sprite_path=review,output_path=(ROOT/"review"/f"{name}-preview.gif").as_posix(),format="gif",frame_number=0)
        print(name,"preview exported",flush=True)
