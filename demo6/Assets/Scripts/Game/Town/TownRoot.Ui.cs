using Demo6.Core.Town;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 마을 루트와 꾸러미 3(주민·대화·의뢰 화면)을 잇는 자리(TownRoot.cs의 partial 메서드 구현). 꾸러미 3을 부르는 곳은 이 파일 하나다.
    /// 쓰는 것: TownNpc.SpawnAll·BarkNow·ResetResidents, TalkWindow(Ensure·Talk·Play·AnyOpen·AssumeOgreDen·ResetStatics),
    /// QuestListWindow(Open·ResetStatics), QuestHud.Attach(마을 모드, 제목은 TownHud가 그림).
    /// 꾸러미 3의 이름·모양이 바뀌면 이 파일만 맞춘다. 이 파일을 빼면 TownRoot는 오프닝을 건너뛰기로 넣고 바크·물체 글·게시판을 알림으로 대신한다.
    /// </summary>
    public sealed partial class TownRoot
    {
        static partial void ResetUiStatics()
        {
            TalkWindow.ResetStatics();
            QuestListWindow.ResetStatics();
            ForgeWindow.ResetStatics();
            TownNpc.ResetResidents();
        }

        static partial void SetUiOgreDen(bool on) => TalkWindow.AssumeOgreDen = on;

        partial void DetectTownUi(ref bool present) => present = true;

        partial void AddTownUi()
        {
            if (!GetComponent<TalkWindow>()) gameObject.AddComponent<TalkWindow>();
            if (!GetComponent<QuestListWindow>()) gameObject.AddComponent<QuestListWindow>();
            var hud = QuestHud.Attach(gameObject, QuestHudMode.Town);
            if (hud) hud.DrawTownTitle = false;
            TalkWindow.AssumeOgreDen = OgreDenAssumed;
        }

        partial void SpawnResidents() => TownNpc.SpawnAll(transform);

        partial void PlayTalk(string npcId, ref bool started)
        {
            var window = TalkWindow.Ensure();
            started = window && window.Talk(npcId);
        }

        partial void ShowNarration(string text, ref bool shown)
        {
            var window = TalkWindow.Ensure();
            if (!window) return;
            // 물체 글은 이름표 없는 나레이션 한 줄 장면(끝 효과 없음).
            var scene = new TalkScene { Id = "obj.narration", Kind = TalkSceneKind.Repeat, Lines = new[] { new TalkLine { Text = text } } };
            shown = window.Play(scene, "");
        }

        partial void ShowBark(string npcId, string text, ref bool shown) => shown = TownNpc.BarkNow(npcId, text);

        partial void OpenQuestList(ref bool opened) => opened = QuestListWindow.Open();

        partial void CheckTalkOpen(ref bool open)
        {
            if (TalkWindow.AnyOpen) open = true;
        }
    }
}
