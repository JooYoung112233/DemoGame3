const fs=require('fs');
let s=fs.readFileSync('AgentScripts/OgreOriginalReview.cs','utf8')
 .replaceAll('OgreOriginalReview','OgreArmReview')
 .replace('검증/오우거-인게임-v027','검증/오우거-팔교정-v029')
 .replace('if(capture&&saved<10)','if(frameIndex%8==0)Capture(cam,rt,shot,dir+"/key-"+frameIndex.ToString("D3")+".png");\n                    if(capture&&saved<10)')
 .replace('head=rig.transform.Find','elbow=rig.ElbowPixels.ToString("F3"), wrist=rig.WristPixels.ToString("F3"), grip=rig.GripPixels.ToString("F3"), upperScale=rig.transform.Find("upper_R_rounded axis").localScale.x, foreScale=rig.transform.Find("fore_R_rounded axis").localScale.x, head=rig.transform.Find')
 .replace('previewReplayMs=100','previewReplayMs=25');
fs.writeFileSync('AgentScripts/OgreArmReview.cs',s);
