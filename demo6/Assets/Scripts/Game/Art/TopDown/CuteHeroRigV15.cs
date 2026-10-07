using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// Native-pixel, single-longsword appearance with a small original-pixel collar
    /// overlap at the shoulder joint. Existing pose timing drives articulated parts;
    /// this is not a new directional animation set or an equipment appearance set.
    /// </summary>
    public sealed class CuteHeroRigV15
    {
        readonly SpriteRenderer[] _hidden;
        readonly bool[] _enabled;
        readonly Transform[] _glows;
        readonly Vector3[] _positions, _scales;
        readonly Quaternion[] _rotations;
        bool _applied;
        CuteArtProfileV15 _art;
        readonly SpriteRenderer _shoulder, _footL, _footR;
        readonly bool _leftFlipY, _rightFlipY;
        Quaternion _leftToeRotation, _rightToeRotation;
        Vector3 _leftToeScale, _rightToeScale;
        bool _toesApplied;
        float _shoulderAngle;
        bool _whirlActive;
        CuteWhirlMotionV18.Pose _whirl;
        readonly CuteWholeBodyV19 _wholeBody;
        public float CapeImpulse => _whirlActive ? _whirl.Cape : 0f;
        public void SetWhirl(bool active, CuteWhirlMotionV18.Pose pose = default)
        { _whirlActive=active;_whirl=pose; }
        public float ShoulderAngle => _shoulderAngle;
        public bool Active { get; private set; }
        public bool WholeBodyReady => _art && _art.wholeBodyEnabled && _art.torso && _art.head && _art.leftShoulder && _art.leftHand;
        public Sprite Body => WholeBodyReady ? _art.torso : _art.body;
        public Sprite Cape => _art.cape;

        public CuteHeroRigV15(SpriteRenderer[] hidden, SpriteRenderer shoulder, SpriteRenderer footL, SpriteRenderer footR, CuteWholeBodyV19 wholeBody, params Transform[] glows)
        {
            _hidden = hidden; _enabled = new bool[hidden.Length];
            _shoulder=shoulder;_footL=footL;_footR=footR;
            _wholeBody=wholeBody;
            _leftFlipY=footL.flipY;_rightFlipY=footR.flipY;
            _glows = glows; _positions = new Vector3[glows.Length];
            _scales = new Vector3[glows.Length]; _rotations = new Quaternion[glows.Length];
        }

        // Called before the normal rig updates each frame, including weapon switches.
        public void Restore()
        {
            if (!_applied) return;
            _shoulder.enabled=false;
            _wholeBody.Hide();
            _footL.flipY=_leftFlipY;_footR.flipY=_rightFlipY;
            if(_toesApplied){
                _footL.transform.localRotation=_leftToeRotation;_footR.transform.localRotation=_rightToeRotation;
                _footL.transform.localScale=_leftToeScale;_footR.transform.localScale=_rightToeScale;
                _toesApplied=false;
            }
            for (int i = 0; i < _hidden.Length; i++) if (_hidden[i]) _hidden[i].enabled = _enabled[i];
            for (int i = 0; i < _glows.Length; i++) if (_glows[i])
            {
                _glows[i].localPosition = _positions[i];
                _glows[i].localScale = _scales[i];
                _glows[i].localRotation = _rotations[i];
            }
            _applied = false;
        }

        public bool Select(string weaponId, bool hasArt)
        {
            _art = CuteArtProfileV15.Current;
            Active = hasArt && weaponId == "wpn_longsword" && _art && _art.characterEnabled && _art.body && _art.cape && _art.weaponArm;
            if(!Active||!WholeBodyReady)_wholeBody.Reset();
            return Active;
        }

        public void Place(SpriteRenderer weapon, TopDownHand hand, TopDownHand rest,
            Vector2 offset, Vector2 bodyScale, float twist, float weaponScale, SpriteRenderer body, float phase, float stepAmp, bool walking, float dt, PlayerPose pose, CuteWalkV22 gait)
        {
            if (!Active) return;
            bool walkingPose=pose==PlayerPose.Idle||pose==PlayerPose.Move;
            for (int i = 0; i < _hidden.Length; i++)
            {
                _enabled[i] = _hidden[i].enabled;
                _hidden[i].enabled = false;
            }
            weapon.sprite = _art.weaponArm;
            // Pivot at the visible shoulder/forearm overlap, not at a fabricated hand.
            // Keep the anchor attached while following the existing swing angle/size.
            Vector2 anchor = new Vector2(-0.075f * bodyScale.x, -0.5125f * bodyScale.y);
            if(_art.shoulder){
                // A bounded shoulder joint follows the lower arm. The torso never
                // receives this attack angle. Keep the source clasp on the torso.
                float relative=Mathf.DeltaAngle(rest.Angle+twist,hand.Angle);
                float target=_whirlActive?_whirl.Shoulder:Mathf.Sin(relative*Mathf.Deg2Rad)*20f;
                float follow=_whirlActive&&WholeBodyReady?60f:22f;
                _shoulderAngle=Mathf.LerpAngle(_shoulderAngle,target,1f-Mathf.Exp(-follow*dt));
                float visibleShoulder=_shoulderAngle+(walkingPose?gait.ShoulderTurn:0f);
                Vector2 hinge=new Vector2(.0125f*bodyScale.x,-.34375f*bodyScale.y);
                anchor=hinge+TopDownCanvas.Rotate(anchor-hinge,visibleShoulder);
                _shoulder.sprite=_art.shoulder;_shoulder.enabled=true;
                _shoulder.transform.localPosition=TopDownCanvas.Rotate(hinge,twist)+offset;
                _shoulder.transform.localRotation=Quaternion.Euler(0,0,twist+visibleShoulder);
                _shoulder.transform.localScale=new Vector3(bodyScale.x,bodyScale.y,1);
                _shoulder.color=body.color;_shoulder.sortingLayerID=body.sortingLayerID;
                _shoulder.sortingOrder=body.sortingOrder-1;_shoulder.forceRenderingOff=body.forceRenderingOff;
            }
            if(WholeBodyReady)_wholeBody.Place(_art,body,bodyScale,offset,twist,_whirlActive,_whirl,pose,dt,walkingPose?gait:null);
            if(_art.toe&&(pose==PlayerPose.Move||pose==PlayerPose.Idle)&&gait.Amount>.001f){
                _leftToeRotation=_footL.transform.localRotation;_rightToeRotation=_footR.transform.localRotation;
                _leftToeScale=_footL.transform.localScale;_rightToeScale=_footR.transform.localScale;_toesApplied=true;
                PlaceToe(_footL,body,gait.Left);
                PlaceToe(_footR,body,gait.Right);
            }
            var t = weapon.transform;
            t.localPosition = TopDownCanvas.Rotate(anchor, twist) + offset;
            // Hand.Angle already contains the body's roll/whirl angle. Adding twist
            // again would make the weapon spin twice during the existing dodge.
            t.localRotation = Quaternion.Euler(0, 0, Mathf.DeltaAngle(rest.Angle, hand.Angle)+(walkingPose?gait.ShoulderTurn*.6f:0f));
            float size = hand.Size * weaponScale;
            t.localScale = new Vector3(bodyScale.x * hand.Length * size, bodyScale.y * size, 1f);
            // The existing skill glow now follows the actual short blade in this crop.
            for (int i = 0; i < _glows.Length; i++)
            {
                var glow = _glows[i]; _positions[i] = glow.localPosition;
                _scales[i] = glow.localScale; _rotations[i] = glow.localRotation;
                glow.localPosition = new Vector3(.51f, -.25f, 0);
                glow.localRotation = Quaternion.Euler(0, 0, -19.5f);
                glow.localScale = new Vector3(i == 0 ? .72f : .76f, i == 0 ? .026f : .05f, 1f);
            }
            _applied = true;
        }

        void PlaceToe(SpriteRenderer toe,SpriteRenderer body,Vector2 position)
        {
            toe.sprite=_art.toe;toe.enabled=true;toe.flipY=false;
            // Ground contacts do not inherit the torso's sway, lean or breathing scale.
            toe.transform.localPosition=position;
            toe.transform.localRotation=Quaternion.identity;
            toe.transform.localScale=new Vector3(1.38f,1.60f,1f);
            toe.color=body.color;toe.sortingLayerID=body.sortingLayerID;toe.sortingOrder=body.sortingOrder-3;
            toe.forceRenderingOff=body.forceRenderingOff;
        }
    }
}
