using System.Collections.Generic;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>Approved A. Original font assets bundled with the project; no OS font installation or lookup.</summary>
    public static class UiFontsA
    {
        const string Root="UI/Fonts/ApprovedA/";
        static Font _body,_emphasis,_title;
        static byte[] _titleCoverage;
        static readonly Dictionary<GUIStyle,GUIStyle> Headings=new Dictionary<GUIStyle,GUIStyle>();
        static readonly Dictionary<GUIStyle,GUIStyle> Fallbacks=new Dictionary<GUIStyle,GUIStyle>();

        public static Font Body => _body?_body:(_body=Resources.Load<Font>(Root+"Pretendard-Medium"));
        public static Font Emphasis => _emphasis?_emphasis:(_emphasis=Resources.Load<Font>(Root+"Pretendard-SemiBold"));
        public static Font Title => _title?_title:(_title=Resources.Load<Font>(Root+"Hahmlet-Bold"));
        public static bool AssetsReady => Body&&Emphasis&&Title&&Coverage!=null;

        static byte[] Coverage
        {
            get
            {
                if(_titleCoverage==null)
                {
                    var asset=Resources.Load<TextAsset>(Root+"HahmletCoverage");
                    if(asset&&asset.bytes.Length==8192)_titleCoverage=asset.bytes;
                }
                return _titleCoverage;
            }
        }

        /// <summary>Exact BMP cmap of the bundled original Hahmlet file, not OS fallback results.</summary>
        public static bool TitleCovers(string text)
        {
            var bits=Coverage;if(bits==null)return false;
            if(string.IsNullOrEmpty(text))return true;
            foreach(char c in text)
            {
                if(char.IsWhiteSpace(c))continue;
                if(char.IsSurrogate(c)||(bits[c>>3]&(1<<(c&7)))==0)return false;
            }
            return true;
        }

        /// <summary>Keep a heading's baseline consistent: if a glyph is absent, use bundled SemiBold for the whole heading.</summary>
        public static GUIStyle HeadingStyle(GUIStyle basis,string text)
        {
            bool supported=Title&&TitleCovers(text);
            var cache=supported?Headings:Fallbacks;
            if(!cache.TryGetValue(basis,out var style))
            {
                style=new GUIStyle(basis){font=supported?Title:Emphasis,fontStyle=FontStyle.Normal};
                cache[basis]=style;
            }
            return style;
        }

        public static GUIStyle ResolveTitleFallback(GUIStyle style,string text)
            =>style.font==Title&&!TitleCovers(text)?HeadingStyle(style,text):style;

        // The font importer also references the project-local SemiBold asset for
        // direct legacy GUI.Label calls which do not pass through HeadingStyle.
        // Imported Font assets are never Destroy'ed by this helper.
    }
}
