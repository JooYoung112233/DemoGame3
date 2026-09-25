using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

// Review fixture, not an unlock/progression shortcut shipped with the game.
// Run in Play Mode after the balance and role UI builders. Production save slots are never used.
// Button tests use EventSystem pointerClick and real GraphicRaycaster hits, not hardware mouse injection.
public static class VerifyBattleRolesRuntime
{
    const string Output="아트/리소스검토/", ReportPath=Output+"role-balance-runtime.json";
    const BindingFlags Instance=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static string TestDirectory=>Path.GetFullPath("Temp/BattleRolesRuntimeSlots");
    static SettlementController C=>Object.FindAnyObjectByType<SettlementController>();
    static ExpeditionBattlePanel B=>C.ArrivalPanel.Encounter.Battle;
    static Review report;
    [Serializable] sealed class Review
    {
        public string utc,result,note;
        public List<string> checks=new List<string>(),screenshots=new List<string>(),failures=new List<string>();
        public List<ProfileMeasurement> profileText=new List<ProfileMeasurement>();
        public List<BestFitMeasurement> bestFitText=new List<BestFitMeasurement>();
    }
    [Serializable] sealed class ProfileMeasurement
    {
        public string profile,field,text,resolution;
        public int font;
        public float width,height,preferredWidth,preferredHeight;
        public bool fits;
    }
    [Serializable] sealed class BestFitMeasurement
    {
        public string path,text,resolution;
        public int configuredFont,rawRenderedFont,minimumFont,maximumFont;
        public float pixelsPerUnit,renderedFont,width,height,unconstrainedPreferredHeight,renderedPreferredWidth,renderedPreferredHeight;
        public bool fits;
    }
    static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    static void Pass(string message){report.checks.Add(message);}
    static T Private<T>(object target,string name)=>(T)target.GetType().GetField(name,Instance).GetValue(target);

    public static async Task<string> Run()
    {
        Check(Application.isPlaying,"Enter Play Mode first.");
        report=new Review{utc=DateTime.UtcNow.ToString("O"),note="Disposable new-game fixture. Four extra people are created from current roster profiles; creature lineups are explicit UI fixtures, not newly unlocked encounters. Native screenshots retain the reviewed resolution. Save override remains isolated until Play is stopped and Finish is called."};
        try
        {
            await Start();
            var battle=B;string originalRules=JsonUtility.ToJson(battle.Rules);var roster=battle.Creatures;
            string[][] groups={
                new[]{"01-door-bearer","06-meter-keeper","11-root-receiver"},
                new[]{"04-seam-hound","05-laundry","07-moth-nest"},
                new[]{"02-listener","08-puddle","09-stairback"},
                new[]{"03-under-table","10-twin-coat","12-bellied-cart"}
            };
            var first=groups[0].Select(roster.Find).ToList();
            Check(first.All(c=>c!=null),"Missing mixed role fixture creatures.");
            Check(C.ArrivalPanel.Encounter.OpenThreat("전투 역할 검수용 조우","방어 · 원거리 · 지원",first),"Could not open fixture encounter.");
            await ClickWhenReady(C.ArrivalPanel.Encounter.Fight);await Ready();
            Check(battle.State.Units.Count(u=>!u.Enemy)==6&&battle.State.Units.Count(u=>u.Enemy)==3,"Mixed encounter did not preserve the six-person squad.");
            await Resolutions(async wh=>
            {
                await CheckTargets();await Shot("role-balance-mixed-"+wh.x+"x"+wh.y);
                await AimPreview();await Shot("role-balance-range-preview-"+wh.x+"x"+wh.y);
                battle.Escape();battle.ScriptedPointerPosition=new Vector2(-3000,-3000);await Task.Delay(80);
            });
            for(int i=1;i<groups.Length;i++)
            {
                battle.Restage(groups[i].Select(roster.Find).ToList());await Ready();
                Check(ReferenceEquals(battle.State.Rules,battle.Rules),"A mixed lineup unexpectedly received single-resident rules.");
                await CheckTargets();
            }
            Pass("All 12 connected creatures: matching overhead/turn/detail role glyphs, names, exact tactical text and segmented maximum health; all nine turn cards stay inside the viewport at both resolutions, with centred portraits and unclipped labels; player input selecting targets costs no action.");

            battle.Restage(new List<BattleCreature>());await Ready();
            await CheckTargets();
            Check(battle.State.Units.Count(u=>u.Enemy)==1&&battle.State.Units.First(u=>u.Enemy).Creature==null,"Original infected fallback was replaced by creature content.");
            Check(battle.TargetRoleLabel.text.StartsWith(CreatureRoles.Name(CreatureRole.Melee)),"Fallback infected is not labelled as melee.");
            Pass("Fallback infected uses the melee glyph and the real rules' stats.");

            battle.Restage(first);await Ready();
            await HumanTreatment();
            // Stop before the final ally acts: this fixture audits role information, not a random combat result.
            while(!battle.State.Current.Enemy)
            {
                HumanDetails();
                int next=battle.State.Actor+1;
                if(next>=battle.State.Units.Count||battle.State.Units[next].Enemy)break;
                await Click(battle.Guard);await Ready();
            }
            var guard=battle.State.Current;
            if(guard.Person.Traits.GuardDamageReduction>0)
            {
                string expected=string.Format(battle.GuardDescription,battle.Rules.GuardHitPenalty,battle.State.GuardReductionFor(battle.State.Actor,true));
                Check(battle.Guard.transform.Find("Description").GetComponent<Text>().text==expected,"Guard card ignored the actor's passive damage reduction.");
            }
            await Resolutions(async wh=>{Layout();await Shot("role-balance-human-trait-"+wh.x+"x"+wh.y);});
            Pass("All six human role/passive summaries match their roster data; guard card uses the acting person's rule-engine reduction.");

            var resident=roster.Find("02-listener");Check(resident!=null&&resident.SuppressReinforcements,"Resident suppression flag is missing.");
            battle.Restage(new List<BattleCreature>{resident});await Ready();
            Check(battle.State.Units.Count(u=>u.Enemy)==1,"Resident encounter contains extra enemies.");
            Check(!ReferenceEquals(battle.State.Rules,battle.Rules)&&battle.State.Rules.MaxReinforcements==0&&!battle.State.ReinforcementsLeft,"Resident did not receive isolated no-reinforcement rules.");
            Check(JsonUtility.ToJson(battle.Rules)==originalRules,"Resident encounter mutated the original panel rules.");
            await CheckTargets();
            await Resolutions(async wh=>{Layout();await Shot("role-balance-resident-"+wh.x+"x"+wh.y);});
            battle.Restage(first);await Ready();
            Check(ReferenceEquals(battle.State.Rules,battle.Rules)&&JsonUtility.ToJson(battle.Rules)==originalRules,"No-reinforcement override leaked into the following mixed battle.");
            Pass("Single management-room resident disables reinforcements on a rules copy; original rules and subsequent mixed encounters remain unchanged.");
        }
        catch(Exception error){report.failures.Add(error.ToString());}
        finally
        {
            if(C&&C.ArrivalPanel&&C.ArrivalPanel.Encounter&&C.ArrivalPanel.Encounter.Battle)C.ArrivalPanel.Encounter.Battle.ScriptedPointer=false;
            report.result=report.failures.Count==0?"PASS":"FAIL";
            File.WriteAllText(ReportPath,Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
        }
        Check(report.result=="PASS","Battle roles runtime review failed. "+Path.GetFullPath(ReportPath)+"\n"+string.Join("\n",report.failures));
        return "PASS\n"+string.Join("\n",report.checks)+"\n"+Path.GetFullPath(ReportPath)+"\n"+string.Join("\n",report.screenshots);
    }

    static async Task Start()
    {
        CampaignSaveStore.TestDirectory=TestDirectory;PartySelectionSession.Clear();
        SceneManager.LoadScene("PartySelection");await Task.Delay(700);
        var selection=Object.FindAnyObjectByType<PartySelectionController>();Check(selection,"Party selection did not load.");
        foreach(var card in selection.Cards.Where(c=>c.gameObject.activeInHierarchy&&c.Button.IsInteractable()))
        {if(PartySelectionSession.Selected.Count>=2)break;await Click(card.Button);}
        Check(PartySelectionSession.Selected.Count==2,"Opening pair not selected.");
        await Resolutions(async wh=>
        {
            foreach(var card in selection.Cards.Where(c=>c.gameObject.activeInHierarchy&&c.Button.IsInteractable()).ToArray())
            {
                await Click(card.Button);Canvas.ForceUpdateCanvases();
                foreach(var text in selection.Details.GetComponentsInChildren<Text>(true).Where(t=>t.enabled&&t.gameObject.activeInHierarchy))
                    RecordProfileText("opening-"+selection.FocusedCandidateId,text);
            }
        });
        await Click(selection.Continue);await Task.Delay(650);
        var home=Object.FindAnyObjectByType<HomeSelectionController>();Check(home,"Home selection did not load.");
        await Click(home.Cards[0].Button);await Click(home.Continue);await Task.Delay(950);
        Check(C&&C.Campaign!=null,"Settlement did not load.");C.Introduction.Restore(10);
        foreach(var candidate in C.Roster.Candidates.Where(c=>!PartySelectionSession.Selected.Contains(c.Id)).ToArray())
        {
            Check(C.Campaign.AddResident(candidate.CreateAdventurer(),6),"Could not add roster-backed fixture member "+candidate.Id);
            PartySelectionSession.Selected.Add(candidate.Id);
        }
        Check(C.Campaign.Party.Count()==6,"Expected six fixture members.");
        foreach(var person in C.Campaign.Party)
        {
            var profile=C.Roster.Candidates.Single(c=>c.DisplayName==person.Name);
            Check(person.MaxHealth==profile.Health&&person.BagCapacity==profile.BagCapacity,"Fixture ignored profile values: "+person.Name);
            person.Health=Mathf.Max(1,person.MaxHealth-2);
        }
        C.Campaign.Party.First().Health=1;C.RefreshMembers();C.Opening.Evaluate();
        Check(C.ArrivalPanel.Begin(C.Campaign.Party.ToArray(),C.ExpeditionPanel.Destinations.First(d=>d.Id=="mall")),"Fixture departure failed.");
        await Task.Delay(1100);if(C.ArrivalPanel.Popup.activeSelf)C.ArrivalPanel.ClosePopup();
        foreach(var person in C.ArrivalPanel.Participants)
        {
            Check(C.InventoryPanel.TransferField(person,"ammo",2,true),"Could not stock fixture ammo: "+person.Name);
            Check(C.InventoryPanel.TransferField(person,"bandage",2,true),"Could not stock fixture bandage: "+person.Name);
        }
        await Profiles();
        Pass("New-game selection/home UI loaded; six current roster profiles entered the real expedition with their distinct health, bags and traits.");
    }

    static async Task Profiles()
    {
        var bags=C.ArrivalPanel.FieldBags;Check(bags,"Field bag profile view is missing.");
        await Resolutions(async wh=>
        {
            for(int i=0;i<C.ArrivalPanel.Participants.Count;i++)
            {
                bags.Open(i);await Task.Delay(100);Check(bags.IsOpen&&bags.Source==C.ArrivalPanel.Participants[i],"Profile did not select member "+i);
                Canvas.ForceUpdateCanvases();
                var profile=C.Roster.Candidates.Single(c=>c.DisplayName==bags.Source.Name);
                RecordProfileText(profile.Id,bags.ProfileTrait);RecordProfileText(profile.Id,bags.ProfileCharacteristics);
                Check(bags.ProfileTrait.text==profile.TraitTitle+"\n"+profile.TraitDescription&&bags.ProfileCharacteristics.text==profile.Characteristics,"Profile description not connected to current roster: "+profile.Id);
                if(profile.Id=="mechanic"||profile.Id=="researcher")await Shot("role-balance-profile-"+profile.Id+"-"+wh.x+"x"+wh.y);
                await Click(bags.Back);
            }
        });
        Pass("Opening pair detail text and all six field-bag trait/characteristic blocks measured at 1920x1080 and 1280x800; any overflow is recorded without hiding the remaining profile checks.");
    }

    static void RecordProfileText(string profile,Text text)
    {
        var m=new ProfileMeasurement{profile=profile,field=text.name,text=text.text,resolution=Screen.width+"x"+Screen.height,
            font=text.fontSize,width=text.rectTransform.rect.width,height=text.rectTransform.rect.height,
            preferredHeight=text.preferredHeight,preferredWidth=text.preferredWidth};
        m.fits=m.preferredHeight<=m.height+2&&(text.horizontalOverflow!=HorizontalWrapMode.Overflow||m.preferredWidth<=m.width+2);
        report.profileText.Add(m);
        if(!m.fits)report.failures.Add("Profile text clips: "+profile+" / "+m.field+" / "+m.resolution+" preferred "+m.preferredHeight+" > height "+m.height+" font "+m.font+" / "+m.text);
    }

    static async Task Ready()
    {
        for(int i=0;i<100;i++)
        {
            if(B&&B.IsOpen&&!B.Busy&&!B.Result.activeSelf&&B.State.PlayerTurn){await Task.Delay(550);B.ScriptedPointer=true;B.ScriptedPointerPosition=new Vector2(-3000,-3000);return;}
            await Task.Delay(100);
        }
        throw new InvalidOperationException("Battle did not return to an idle player turn.");
    }

    static async Task CheckTargets()
    {
        var battle=B;Canvas.ForceUpdateCanvases();
        var huds=Private<List<BattlePawnHud>>(battle,"huds");var cards=Private<List<BattleTurnCard>>(battle,"cards");
        for(int i=0;i<battle.State.Units.Count;i++)
        {
            var unit=battle.State.Units[i];Check(huds[i]&&cards[i],"Missing unit UI: "+unit.Name);
            Check(huds[i].RoleBadge&&cards[i].RoleBadge,"Role component missing: "+unit.Name);
            Check(huds[i].RoleBadge.gameObject.activeSelf==unit.Enemy&&cards[i].RoleBadge.gameObject.activeSelf==unit.Enemy,"Role glyph visibility wrong: "+unit.Name);
            Check(huds[i].Segments.childCount==unit.Maximum+1,"Overhead HP cells do not match maximum: "+unit.Name);
            if(unit.Enemy)
            {
                var role=CreatureRoles.For(unit.Creature);
                Check(huds[i].RoleBadge.Role==role&&cards[i].RoleBadge.Role==role,"Role glyph disagrees with data: "+unit.Name);
                Check(!Overlap(huds[i].RoleBadge.rectTransform,huds[i].Name.rectTransform),"Head role icon overlaps name: "+unit.Name);
                Check(!Overlap(cards[i].RoleBadge.rectTransform,cards[i].Portrait.rectTransform),"Turn role icon covers portrait: "+unit.Name);
                int before=battle.State.Actions;
                // A repeated tap attacks in this game; toggle the action card off first to clear its armed target.
                await Click(battle.Melee);await Click(battle.Melee);
                int cell=9+unit.Lane*3+unit.Depth;var geometry=battle.Cells[cell];
                Vector2 center=(geometry.TopLeft+geometry.TopRight+geometry.BottomRight+geometry.BottomLeft)*.25f;
                await ClickAt(battle.CellButtons[cell],geometry.rectTransform.TransformPoint(center));
                Check(battle.SelectedTarget==i&&battle.State.Actions==before&&!battle.Busy,"Selecting a creature unexpectedly spent an action.");
                Check(battle.TargetRoleBadge.gameObject.activeInHierarchy&&battle.TargetRoleBadge.Role==role,"Target role glyph not selected: "+unit.Name);
                Check(battle.TargetRoleLabel.text==CreatureRoles.Name(role)+" · 방어 "+battle.State.ArmorOf(i,battle.Ranged),"Target role/armor disagrees with current attack rules: "+unit.Name);
                Check(battle.TargetTactic.text==ExpeditionBattlePanel.CreatureTactic(unit.Creature),"Target tactic differs from creature data: "+unit.Name);
                Check(battle.TargetInfo.text.Contains("/ "+unit.Maximum),"Target maximum HP differs from unit: "+unit.Name);
                Layout();
            }
        }
        HumanDetails();
    }

    static void HumanDetails()
    {
        var battle=B;var unit=battle.State.Current;Check(!unit.Enemy,"Expected a human actor.");
        var profile=C.Roster.Candidates.Single(c=>c.DisplayName==unit.Name);
        Check(battle.ActorRoleLabel.text==profile.CombatRole&&battle.ActorTactic.text==profile.CombatTraitSummary,"Human role/passive summary stale: "+unit.Name);
        Check(battle.ActorInfo.text.Contains("/ "+unit.Maximum)&&unit.Maximum==profile.Health,"Human HP summary stale: "+unit.Name);
        Layout();
    }

    static async Task AimPreview()
    {
        var battle=B;await Click(battle.Shoot);
        int target=battle.State.Units.FindIndex(u=>u.Enemy&&u.Creature!=null&&u.Creature.Role==CreatureRole.Defender);
        if(target<0)target=battle.State.Units.FindIndex(u=>u.Enemy);
        battle.ScriptedPointer=true;battle.ScriptedPointerPosition=battle.AimPoint(target);await Task.Delay(300);
        Check(battle.Aiming&&battle.AimTarget==target&&battle.SelectedTarget==target,"Scripted aim preview did not reach chosen creature.");
        Check(battle.AimTooltip.gameObject.activeInHierarchy,"Aim tooltip missing.");
        Check(battle.AimTooltip.Armor.text=="방어력 "+battle.State.ArmorOf(target,true),"Aim armor differs from rule engine.");
        Check(battle.AimTooltip.Damage.text.Contains("피해 "+battle.State.ExpectedDamage(target,true)),"Aim damage differs from rule engine.");
        Check(battle.TargetRoleLabel.text.EndsWith("방어 "+battle.State.ArmorOf(target,true)),"Role card armor did not refresh for shooting.");
        Layout();
    }

    static async Task HumanTreatment()
    {
        var battle=B;HumanDetails();await Click(battle.Guard);await Ready();
        var actor=battle.State.Current;Check(actor.Name==C.Roster.Candidates.Single(c=>c.Id=="medic").DisplayName,"Medic is not second in the opening pair.");
        HumanDetails();
        await Click(battle.Items);await Task.Delay(600);
        var item=C.InventoryPanel.Items.Single(i=>i.Id=="bandage");
        var slot=battle.Drawer.Slots.GetComponentsInChildren<BattleItemSlot>().First(s=>s.Icon.sprite==item.Icon&&s.Button.IsInteractable());
        await Click(slot.Button);
        int target=battle.State.Units.FindIndex(u=>!u.Enemy&&u.Name==C.Campaign.Party.First().Name);
        var unit=battle.State.Units[target];
        var chip=battle.Drawer.Chips.GetComponentsInChildren<BattleTargetChip>().Single(c=>c.Name.text==unit.Name);
        await Click(chip.Button);
        int recovery=battle.State.ItemRecovery(item.Recovery,item.Id),before=unit.Health,after=Mathf.Min(unit.Maximum,before+recovery);
        int stock=C.InventoryPanel.CountFor(actor.Person,item.Id);
        Check(recovery>item.Recovery,"Medic's bandage trait is not active in this fixture.");
        Check(battle.Drawer.DetailDescription.text.Contains("체력 +"+recovery),"Item description ignored medic recovery.");
        Check(battle.PreviewBefore.Health.text==before+" / "+unit.Maximum&&battle.PreviewAfter.Health.text==after+" / "+unit.Maximum,"Item before/after preview is wrong.");
        Check(battle.Drawer.Message.text.Contains("→ "+after),"Item message disagrees with preview.");
        foreach(var card in battle.OrderContent.GetComponentsInChildren<BattleTurnCard>())
        {
            var data=battle.State.Units.FirstOrDefault(u=>u.Name==card.Label.text);
            if(data==null)continue;
            Check(card.RoleBadge&&card.RoleBadge.gameObject.activeSelf==data.Enemy,"Item-order role glyph missing.");
            if(data.Enemy)Check(card.RoleBadge.Role==CreatureRoles.For(data.Creature),"Item-order role glyph wrong.");
        }
        await Resolutions(async wh=>{Layout();Check(Hit(battle.Drawer.Use)!=null,"Use hit missing.");await Shot("role-balance-treatment-"+wh.x+"x"+wh.y);});
        await Click(battle.Drawer.Use);await Ready();
        Check(unit.Health==after&&unit.Person.Health==after,"Actual treatment differs from preview.");
        Check(C.InventoryPanel.CountFor(actor.Person,item.Id)==stock-item.UseCost,"Treatment consumed the wrong item count.");
        Pass("Medic's actual item/target/use button path: role shown, +1 bandage trait in description/before-after/message, exact recovery and one item consumed; six-member item drawer reviewed at both resolutions.");
    }

    static void Layout()
    {
        Canvas.ForceUpdateCanvases();var battle=B;
        var specific=new[]{battle.ActorName,battle.ActorInfo,battle.ActorRoleLabel,battle.ActorTactic,battle.TargetName,battle.TargetInfo,battle.TargetRoleLabel,battle.TargetTactic,battle.Chance};
        foreach(var text in specific.Where(t=>t&&t.enabled&&t.gameObject.activeInHierarchy))Fits(text);
        foreach(var hud in battle.HudLayer.GetComponentsInChildren<BattlePawnHud>())if(hud.gameObject.activeInHierarchy)Fits(hud.Name);
        var turnCards=battle.TurnContent.GetComponentsInChildren<BattleTurnCard>();
        var turnViewport=(RectTransform)battle.TurnContent.parent;
        if(turnCards.Length<=9)ContainsRect(turnViewport,battle.TurnContent,"Turn content exceeds the nine-card viewport.");
        foreach(var card in turnCards)
        {
            var rect=(RectTransform)card.transform;Fits(card.Label);
            ContainsRect(battle.TurnContent,rect,"Turn card exceeds content: "+card.Label.text);
            if(turnCards.Length<=9)ContainsRect(turnViewport,rect,"Turn card is clipped by viewport: "+card.Label.text);
            ContainsRect(rect,card.Paper.rectTransform,"Turn paper exceeds card: "+card.Label.text);
            ContainsRect(rect,card.Label.rectTransform,"Turn name exceeds card: "+card.Label.text);
            ContainsRect(rect,card.Portrait.rectTransform,"Turn portrait exceeds card: "+card.Label.text);
            Vector3 portraitCenter=rect.InverseTransformPoint(card.Portrait.rectTransform.TransformPoint(card.Portrait.rectTransform.rect.center));
            Check(Mathf.Abs(portraitCenter.x-rect.rect.center.x)<=.5f,"Turn portrait is not centred: "+card.Label.text);
        }
        if(battle.ItemMode)
            foreach(var text in battle.Drawer.GetComponentsInChildren<Text>(true).Where(t=>t.enabled&&t.gameObject.activeInHierarchy))Fits(text);
        if(battle.AimTooltip&&battle.AimTooltip.gameObject.activeInHierarchy)
            foreach(var text in battle.AimTooltip.GetComponentsInChildren<Text>(true).Where(t=>t.enabled&&t.gameObject.activeInHierarchy))Fits(text);
    }
    static void Fits(Text text)
    {
        if(string.IsNullOrEmpty(text.text))return;
        float height=text.preferredHeight,width=text.preferredWidth,font=text.fontSize;
        if(text.resizeTextForBestFit)
        {
            // preferredHeight ignores the available height. Native best-fit font metadata
            // has undocumented units in this Editor: verify the rendered glyph geometry
            // instead, using the same pixelsPerUnit conversion as Text.OnPopulateMesh.
            var generator=text.cachedTextGenerator;int rawFont=generator.fontSizeUsedForBestFit;
            float pixelsPerUnit=text.pixelsPerUnit;Check(pixelsPerUnit>0,"Invalid text pixelsPerUnit: "+text.name);
            Check(text.resizeTextMinSize>=12&&text.resizeTextMaxSize>=text.resizeTextMinSize,"Invalid best-fit limits: "+text.name);
            // GetMesh is the final local-space render result. During a GameView resize,
            // pixelsPerUnit may already be new while the generator cache is still old.
            var mesh=text.canvasRenderer.GetMesh();
            var points=mesh?mesh.vertices.Select(v=>(Vector2)v).ToList():new List<Vector2>();
            bool complete=points.Count>0&&generator.characterCountVisible>=text.text.Count(c=>!char.IsWhiteSpace(c));
            Check(complete,"Rendered best-fit text omits characters: "+text.name+" / "+text.text);
            height=points.Max(p=>p.y)-points.Min(p=>p.y);width=points.Max(p=>p.x)-points.Min(p=>p.x);font=-1;
            var bounds=text.rectTransform.rect;
            bool contained=points.All(p=>p.x>=bounds.xMin-2&&p.x<=bounds.xMax+2&&p.y>=bounds.yMin-2&&p.y<=bounds.yMax+2);
            var measurement=new BestFitMeasurement{
                path=UnityEditor.AnimationUtility.CalculateTransformPath(text.transform,null),text=text.text,resolution=Screen.width+"x"+Screen.height,
                configuredFont=text.fontSize,rawRenderedFont=rawFont,pixelsPerUnit=pixelsPerUnit,renderedFont=font,minimumFont=text.resizeTextMinSize,maximumFont=text.resizeTextMaxSize,
                width=text.rectTransform.rect.width,height=text.rectTransform.rect.height,unconstrainedPreferredHeight=text.preferredHeight,
                renderedPreferredHeight=height,renderedPreferredWidth=width};
            measurement.fits=contained;
            if(!report.bestFitText.Any(m=>m.path==measurement.path&&m.text==measurement.text&&m.resolution==measurement.resolution&&m.renderedFont==font))report.bestFitText.Add(measurement);
            Check(contained,"Rendered best-fit glyphs exceed their rect: "+measurement.path+" / "+text.text);
        }
        Check(height<=text.rectTransform.rect.height+2,"Text exceeds height: "+text.name+" "+height+" > "+text.rectTransform.rect.height+" / rendered font "+font+" / "+text.text);
        if(text.horizontalOverflow==HorizontalWrapMode.Overflow)
            Check(width<=text.rectTransform.rect.width+2,"Text exceeds width: "+text.name+" / rendered font "+font+" / "+text.text);
    }
    static bool Overlap(RectTransform a,RectTransform b)
    {
        var ca=new Vector3[4];var cb=new Vector3[4];a.GetWorldCorners(ca);b.GetWorldCorners(cb);
        return Mathf.Min(ca[2].x,cb[2].x)-Mathf.Max(ca[0].x,cb[0].x)>.1f&&Mathf.Min(ca[2].y,cb[2].y)-Mathf.Max(ca[0].y,cb[0].y)>.1f;
    }
    static void ContainsRect(RectTransform parent,RectTransform child,string message)
    {
        var corners=new Vector3[4];child.GetWorldCorners(corners);var rect=parent.rect;
        foreach(var world in corners)
        {
            Vector3 local=parent.InverseTransformPoint(world);
            Check(local.x>=rect.xMin-.5f&&local.x<=rect.xMax+.5f&&local.y>=rect.yMin-.5f&&local.y<=rect.yMax+.5f,message+" / "+local+" outside "+rect);
        }
    }
    static bool TryHit(Button button,Vector3? world,out PointerEventData data,out string reason)
    {
        data=null;
        if(!button||!button.IsActive()||!button.IsInteractable()){reason="Button is not clickable: "+(button?button.name:"null");return false;}
        var canvas=button.GetComponentInParent<Canvas>();
        if(!canvas||!EventSystem.current){reason="Button has no canvas/EventSystem: "+button.name;return false;}
        var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
        var rect=(RectTransform)button.transform;Vector3 point=world??rect.TransformPoint(rect.rect.center);
        data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(camera,point),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
        reason="Button raycast blocked: "+button.name+" / "+(hits.Count>0?hits[0].gameObject.name:"no hit");
        return hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button;
    }
    static PointerEventData Hit(Button button,Vector3? world=null)
    {
        Check(TryHit(button,world,out var data,out var reason),reason);
        return data;
    }
    static async Task ClickWhenReady(Button button)
    {
        // OpenThreat enables a previously inactive canvas branch. Its Graphic depth/layout is
        // not ready until a rendered frame; only poll hits here, never retry a dispatched click.
        int openedFrame=Time.frameCount;string reason="No frame after opening encounter.";
        for(int i=0;i<30;i++)
        {
            await Task.Delay(50);
            if(Time.frameCount==openedFrame)continue;
            Canvas.ForceUpdateCanvases();
            if(!TryHit(button,null,out var data,out reason))continue;
            ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);
            await Task.Delay(100);return;
        }
        string groups=button?string.Join("; ",button.GetComponentsInParent<CanvasGroup>(true).Select(g=>g.name+": active="+g.gameObject.activeInHierarchy+", interactable="+g.interactable+", blocks="+g.blocksRaycasts+", ignoreParents="+g.ignoreParentGroups)):"none";
        var graphic=button?button.targetGraphic:null;
        Check(false,reason+" after waiting for encounter layout. Frame "+openedFrame+" -> "+Time.frameCount+"; graphic="+(graphic?graphic.name+", depth="+graphic.depth+", raycast="+graphic.raycastTarget+", culled="+graphic.canvasRenderer.cull:"none")+"; groups="+groups);
    }
    static async Task Click(Button button){ExecuteEvents.Execute(button.gameObject,Hit(button),ExecuteEvents.pointerClickHandler);await Task.Delay(100);}
    static async Task ClickAt(Button button,Vector3 world){ExecuteEvents.Execute(button.gameObject,Hit(button,world),ExecuteEvents.pointerClickHandler);await Task.Delay(100);}

    static async Task Resolutions(Func<Vector2Int,Task> review)
    {
        var assembly=typeof(UnityEditor.Editor).Assembly;var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
        var sizes=typeof(UnityEditor.ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance",BindingFlags.Public|BindingFlags.Static).GetValue(null);
        var group=sizesType.GetMethod("GetGroup",Instance).Invoke(sizes,new[]{sizesType.GetProperty("currentGroupType",Instance).GetValue(sizes)});
        var view=UnityEditor.EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));var selected=view.GetType().GetProperty("selectedSizeIndex",Instance);int original=(int)selected.GetValue(view);
        var added=new List<int>();var sizeType=assembly.GetType("UnityEditor.GameViewSize");
        try
        {
            foreach(var wh in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,800)})
            {
                int count=(int)group.GetType().GetMethod("GetTotalCount",Instance).Invoke(group,null),index=-1;
                for(int i=0;i<count;i++)
                {
                    var size=group.GetType().GetMethod("GetGameViewSize",Instance).Invoke(group,new object[]{i});
                    if((int)sizeType.GetProperty("width",Instance).GetValue(size)==wh.x&&(int)sizeType.GetProperty("height",Instance).GetValue(size)==wh.y){index=i;break;}
                }
                if(index<0)
                {
                    var type=assembly.GetType("UnityEditor.GameViewSizeType");
                    var constructor=sizeType.GetConstructor(Instance,null,new[]{type,typeof(int),typeof(int),typeof(string)},null);
                    Check(constructor!=null,"Cannot create temporary GameView review size.");
                    var size=constructor.Invoke(new[]{Enum.Parse(type,"FixedResolution"),(object)wh.x,wh.y,"Role balance temporary review"});
                    group.GetType().GetMethod("AddCustomSize",Instance).Invoke(group,new[]{size});index=count;
                    int builtins=(int)group.GetType().GetMethod("GetBuiltinCount",Instance).Invoke(group,null);added.Add(index-builtins);
                }
                selected.SetValue(view,index);view.Repaint();await Task.Delay(650);
                Check(Screen.width==wh.x&&Screen.height==wh.y,"GameView resolution did not apply: "+Screen.width+"x"+Screen.height);
                await review(wh);
            }
        }
        finally
        {
            selected.SetValue(view,original);view.Repaint();
            foreach(int index in added.OrderByDescending(i=>i))group.GetType().GetMethod("RemoveCustomSize",Instance).Invoke(group,new object[]{index});
            await Task.Delay(150);
        }
    }
    static async Task Shot(string name)
    {
        Canvas.ForceUpdateCanvases();string native=Path.GetFullPath("Temp/"+name+"-native.png");if(File.Exists(native))File.Delete(native);
        ScreenCapture.CaptureScreenshot(native);for(int i=0;i<40&&!File.Exists(native);i++)await Task.Delay(100);
        Check(File.Exists(native),"Native capture missing: "+name);await Task.Delay(160);
        string output=Path.GetFullPath(Output+name+".png");File.Copy(native,output,true);report.screenshots.Add(output);
    }
    // A failed full review leaves its item drawer open. Inspect that exact state without
    // rebuilding the squad, using an item, or overwriting the full walkthrough report.
    public static async Task<string> CurrentItemLayout()
    {
        Check(Application.isPlaying&&B&&B.IsOpen&&B.ItemMode,"Open battle item mode first.");
        report=new Review{utc=DateTime.UtcNow.ToString("O"),note="Read-only inspection. Best-fit checks rendered glyph bounds and visible characters, not unconstrained preferred size. renderedFont=-1 means native font-size metadata units are unverified."};
        try{await Resolutions(async wh=>{await Shot("role-balance-current-items-"+wh.x+"x"+wh.y);Layout();});}
        catch(Exception error){report.failures.Add(error.ToString());}
        report.result=report.failures.Count==0?"PASS":"FAIL";
        string path=Path.GetFullPath(Output+"role-balance-item-layout.json");
        File.WriteAllText(path,Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
        Check(report.result=="PASS",path+"\n"+string.Join("\n",report.failures));
        return "PASS\n"+path+"\n"+string.Join("\n",report.screenshots);
    }
    public static string Finish()
    {
        Check(!Application.isPlaying,"Stop Play Mode first.");if(CampaignSaveStore.TestDirectory==TestDirectory)CampaignSaveStore.TestDirectory=null;
        PartySelectionSession.Clear();
        return "Stopped; battle-role review save override and test party cleared.";
    }
}
