using System;
using Demo6.Core.TestStart;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// F1 시험 패널 세 개(마을 TownDebugPanel·던전 ExplorationLog·전투 시험장 CombatHud)의 여는 문.
    /// F1 시험 패널(마을·던전·전투 시험장)은 Unity 편집기에서만 연다. 만든 게임(개발용 빌드 포함)에서는 F1이 아무 일도 하지 않는다(키 배치 1차 0장 5).
    /// 패널 손잡이를 쓴 판은 지금처럼 시험 판으로 적힌다.
    /// 검토 1차 4장 Q7의 정식 빌드 확인 창은 뺐다. 이름(OpensDirectly·RequestOpen)은 부르는 세 패널을 고치지 않아도 되게 남기고 뜻만 바꿨다. 남는 정적 값은 없다.
    /// </summary>
    public static class DevPanelGate
    {
        /// <summary>F1 시험 패널을 여는가: Unity 편집기에서만 참(TestToolRules.PanelAllowed, 키 배치 1차 0장 5).</summary>
        public static bool OpensDirectly => TestToolRules.PanelAllowed(Application.isEditor);

        /// <summary>
        /// 패널을 F1로 열려 할 때 부른다. 편집기면 true(부르는 쪽이 연다). 아니면 false — 묻지 않고, 아무것도 열지 않고, 알림도 내지 않는다.
        /// open은 부르지 않는다(옛 확인 창의 '연다' 자리. 부르는 쪽 모양을 그대로 두려고 받기만 한다).
        /// </summary>
        public static bool RequestOpen(Action open) => OpensDirectly;
    }
}
