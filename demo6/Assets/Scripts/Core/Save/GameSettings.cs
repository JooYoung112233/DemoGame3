using System;
using System.Globalization;
using System.Text;

namespace Demo6.Core.Save
{
    /// <summary>
    /// 설정 값 한 벌(기획/저장-처음화면-멈춤창-1차.md 6장 '설정 창', 서로 부를 이름 11-7 'D'). 코드에 이미 있는 값만 담는다(6-1):
    /// 소리 켬·끔(Tuning.Sound), 소리 크기(Tuning.SoundVolume, 0~1, 5% 단위), 화면 흔들림(Tuning.ShakeScale, 0~1, 10% 단위),
    /// 피해 숫자(Tuning.DamageNumbers), 타격 멈춤(Tuning.HitStopEnabled), 피 발자국(GoreFootprints.Enabled), 대사 글 바로 보이기(TalkWindow.InstantText).
    /// 기본값은 지금 코드 기본과 같다(Tuning.cs 11-20, GoreFootprints.cs 15). 기척 소리·손맛 손잡이·백어택 글자는 넣지 않는다(6-2).
    /// 저장 파일과 따로 기기 설정(PlayerPrefs 'demo6.settings')에 ToText 글로 둔다(6-3). 읽고 쓰는 곳은 Game의 SettingsStore 하나다.
    /// Core라 UnityEngine을 쓰지 않는다.
    /// </summary>
    public sealed class GameSettings
    {
        /// <summary>설정 글 첫 줄(판본 1). 첫 줄이 이것과 다르면 모두 기본값으로 읽는다(6-3).</summary>
        public const string Header = "settings v1";
        /// <summary>소리 크기 기본(Tuning.SoundVolume 기본 0.6과 같음).</summary>
        public const float DefaultVolume = 0.6f;
        /// <summary>화면 흔들림 기본(Tuning.ShakeScale 기본 1과 같음).</summary>
        public const float DefaultShake = 1f;
        /// <summary>소리 크기 칸 수: 0~1을 20칸(5% 단위)으로 맞춘다(6-1).</summary>
        public const int VolumeSteps = 20;
        /// <summary>화면 흔들림 칸 수: 0~1을 10칸(10% 단위)으로 맞춘다(6-1).</summary>
        public const int ShakeSteps = 10;

        /// <summary>소리 켬·끔(Tuning.Sound).</summary>
        public bool Sound = true;
        /// <summary>소리 크기 0~1(Tuning.SoundVolume).</summary>
        public float Volume = DefaultVolume;
        /// <summary>화면 흔들림 0~1(Tuning.ShakeScale). 시험 패널 손잡이는 3까지 가지만 설정은 1까지만.</summary>
        public float Shake = DefaultShake;
        /// <summary>피해 숫자(Tuning.DamageNumbers).</summary>
        public bool DamageNumbers = true;
        /// <summary>타격 멈춤(Tuning.HitStopEnabled).</summary>
        public bool HitStop = true;
        /// <summary>피 발자국(GoreFootprints.Enabled).</summary>
        public bool Footprints = true;
        /// <summary>대사 글 바로 보이기(TalkWindow.InstantText, 기본 끔).</summary>
        public bool InstantText;

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>
        /// 설정 글(6-3 모양): 첫 줄 Header, 그 아래 키=값 한 줄씩. 소수는 "0.00", 켬·끔은 1/0. 줄바꿈은 LF만, 끝에 줄바꿈 하나.
        /// </summary>
        public string ToText()
        {
            var sb = new StringBuilder();
            sb.Append(Header).Append('\n');
            Line(sb, "sound", Bool(Sound));
            Line(sb, "volume", Volume.ToString("0.00", Inv));
            Line(sb, "shake", Shake.ToString("0.00", Inv));
            Line(sb, "damagenumbers", Bool(DamageNumbers));
            Line(sb, "hitstop", Bool(HitStop));
            Line(sb, "footprints", Bool(Footprints));
            Line(sb, "instanttext", Bool(InstantText));
            return sb.ToString();
        }

        /// <summary>
        /// 설정 글을 읽는다. 비었거나 첫 줄(앞뒤 빈칸을 뺀 것)이 Header가 아니면 모두 기본값.
        /// 모르는 키는 건너뛰고, 빠진 키·읽지 못한 값은 기본값을 둔다. 끝에 Normalize. 예외를 던지지 않는다.
        /// </summary>
        public static GameSettings Parse(string text)
        {
            try
            {
                if (string.IsNullOrEmpty(text)) return new GameSettings();
                var lines = text.Replace("\r", "").Split('\n');
                if (lines[0].Trim() != Header) return new GameSettings();
                var s = new GameSettings();
                for (int i = 1; i < lines.Length; i++)
                {
                    string line = lines[i];
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    s.Read(line.Substring(0, eq).Trim(), line.Substring(eq + 1));
                }
                return s.Normalize();
            }
            catch (Exception)
            {
                return new GameSettings();
            }
        }

        /// <summary>
        /// 값을 맞춘다: 소리 크기는 0~1로 자르고 5% 단위로, 화면 흔들림은 0~1로 자르고 10% 단위로(가까운 쪽, 한가운데는 위로).
        /// 숫자가 아닌 값(NaN)은 기본값. 자신을 돌려준다.
        /// </summary>
        public GameSettings Normalize()
        {
            Volume = Snap(Volume, VolumeSteps, DefaultVolume);
            Shake = Snap(Shake, ShakeSteps, DefaultShake);
            return this;
        }

        public GameSettings Clone() => (GameSettings)MemberwiseClone();

        /// <summary>
        /// 일곱 값이 모두 같은가(소수는 0.0001 안). 설정 창이 '값이 실제로 바뀐 때만 쓰기'(6-4)에 쓴다.
        /// 맞춘(Normalize) 값끼리 견주는 것이 뜻에 맞다.
        /// </summary>
        public bool SameAs(GameSettings other)
        {
            if (other == null) return false;
            return Sound == other.Sound
                && Math.Abs(Volume - other.Volume) < 0.0001f
                && Math.Abs(Shake - other.Shake) < 0.0001f
                && DamageNumbers == other.DamageNumbers
                && HitStop == other.HitStop
                && Footprints == other.Footprints
                && InstantText == other.InstantText;
        }

        static float Snap(float value, int steps, float fallback)
        {
            if (float.IsNaN(value)) value = fallback;
            double v = Math.Max(0.0, Math.Min(1.0, (double)value));
            return (float)(Math.Round(v * steps, MidpointRounding.AwayFromZero) / steps);
        }

        static string Bool(bool on) => on ? "1" : "0";

        static void Line(StringBuilder sb, string key, string value) => sb.Append(key).Append('=').Append(value).Append('\n');

        /// <summary>키 하나를 읽는다. 모르는 키나 읽지 못한 값은 건너뛴다(기본값 유지).</summary>
        void Read(string key, string value)
        {
            switch (key)
            {
                case "sound": ReadBool(value, ref Sound); break;
                case "volume": ReadFloat(value, ref Volume); break;
                case "shake": ReadFloat(value, ref Shake); break;
                case "damagenumbers": ReadBool(value, ref DamageNumbers); break;
                case "hitstop": ReadBool(value, ref HitStop); break;
                case "footprints": ReadBool(value, ref Footprints); break;
                case "instanttext": ReadBool(value, ref InstantText); break;
            }
        }

        static void ReadFloat(string value, ref float field)
        {
            if (float.TryParse(value.Trim(), NumberStyles.Float, Inv, out float f) && !float.IsNaN(f)) field = f;
        }

        static void ReadBool(string value, ref bool field)
        {
            if (TryBool(value, out bool on)) field = on;
        }

        /// <summary>1/0(또는 true/false). 그 밖은 읽지 못함.</summary>
        static bool TryBool(string value, out bool on)
        {
            string v = value.Trim();
            on = v == "1" || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
            return on || v == "0" || string.Equals(v, "false", StringComparison.OrdinalIgnoreCase);
        }
    }
}
