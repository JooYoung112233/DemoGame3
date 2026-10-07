using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace Demo6.Game
{
    /// <summary>Read-only render of the equipped sprite rig and its live cape mesh. No changes to world layers or equipment.</summary>
    internal sealed class EquipmentPortraitV5 : IDisposable
    {
        readonly List<Renderer> _renderers=new List<Renderer>();
        RenderTexture _target;
        CommandBuffer _commands;
        static readonly Comparison<Renderer> Order=(a,b)=>a.sortingOrder.CompareTo(b.sortingOrder);
        public void Draw(Rect area,PlayerController player)
        {
            if(!player||Event.current.type!=EventType.Repaint)return;
            _renderers.Clear();player.GetComponentsInChildren(false,_renderers);
            _renderers.RemoveAll(r=>!r||!r.enabled||r.forceRenderingOff||!r.sharedMaterial||r.name.IndexOf("shadow",StringComparison.OrdinalIgnoreCase)>=0||(r is SpriteRenderer s&&(!s.sprite||s.color.a<=.01f)));
            if(_renderers.Count==0)return;_renderers.Sort(Order);
            var bounds=_renderers[0].bounds;foreach(var r in _renderers)bounds.Encapsulate(r.bounds);
            float aspect=area.width/area.height;
            float halfHeight=Mathf.Max(bounds.extents.y,bounds.extents.x/aspect)*1.04f;
            if(!_target){_target=new RenderTexture(392,460,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB){name="Live equipment portrait",hideFlags=HideFlags.DontSave,filterMode=FilterMode.Bilinear};_target.Create();}
            if(_commands==null)_commands=new CommandBuffer{name="Live equipment portrait (unlit)"};
            _commands.Clear();_commands.SetRenderTarget(_target);_commands.ClearRenderTarget(true,true,Color.clear);
            var center=bounds.center;var projection=Matrix4x4.Ortho(center.x-halfHeight*aspect,center.x+halfHeight*aspect,center.y-halfHeight,center.y+halfHeight,-100,100);
            _commands.SetViewProjectionMatrices(Matrix4x4.identity,GL.GetGPUProjectionMatrix(projection,true));
            foreach(var r in _renderers){var mat=r.sharedMaterial;int pass=mat.shader.name=="Demo6/SpriteFlash"?2:0;_commands.DrawRenderer(r,mat,0,pass);}
            var previous=RenderTexture.active;bool previousSrgb=GL.sRGBWrite;GL.sRGBWrite=QualitySettings.activeColorSpace==ColorSpace.Linear;Graphics.ExecuteCommandBuffer(_commands);GL.sRGBWrite=previousSrgb;RenderTexture.active=previous;
            var tint=GUI.color;GUI.color=Color.white;GUI.DrawTextureWithTexCoords(area,_target,SystemInfo.graphicsUVStartsAtTop?new Rect(0,1,1,-1):new Rect(0,0,1,1),true);GUI.color=tint;
        }
        public void Dispose(){if(_commands!=null){_commands.Release();_commands=null;}if(_target){_target.Release();UnityEngine.Object.Destroy(_target);_target=null;}}
    }
}
