using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using Demo5.NightRun;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class VerifyTitleMenu
{
    static void Require(bool test,string message){if(!test)throw new InvalidOperationException(message);}
    static TitleMenuController Menu()=>UnityEngine.Object.FindAnyObjectByType<TitleMenuController>();
    public static string Inspect()
    {
        var c=Menu();Require(c,"No title controller");
        Require(c.SettingsPanel&&c.LoadPanel&&c.ConfirmPanel&&c.Fade,"Missing modal references");
        Require(c.NewGameButton&&c.SettingsButton&&c.LoadButton&&c.ContinueButton&&c.ExitButton,"Missing main button references");
        foreach(var name in new[]{"TitleMenu","PaperButton","SettingsDialog","LoadDialog","ConfirmDialog"})Require(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/FrontEnd/"+name+".prefab"),"Missing prefab "+name);
        Require(EditorBuildSettings.scenes[0].path=="Assets/Scenes/StartMenu.unity","Start scene not first");
        Require(EditorBuildSettings.scenes.Any(s=>s.enabled&&s.path=="Assets/Scenes/NightExpedition.unity"),"New game destination missing");
        return "Scene and 5 prefab references OK; title is first; prototype destination preserved.";
    }
    static void Click(Button b)
    {
        Require(b.IsActive()&&b.IsInteractable(),"Button unavailable: "+b.name);
        Canvas.ForceUpdateCanvases();
        var rt=(RectTransform)b.transform;var canvas=b.GetComponentInParent<Canvas>();
        var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,rt.TransformPoint(rt.rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);
        Require(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Button blocked by another raycast target: "+b.name+" top="+(hits.Count>0?hits[0].gameObject.name:"none"));
        ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerClickHandler);
    }
    public static async Task<string> PlayFlow()
    {
        Require(EditorApplication.isPlaying,"Enter Play mode");var c=Menu();Require(c,"No title menu");
        Require(!c.ContinueButton.interactable,"Continue must be disabled without saves");
        Require(c.OpenModalName=="","Modal unexpectedly open");float before=AudioListener.volume;
        Click(c.SettingsButton);await Task.Delay(150);Require(c.OpenModalName=="SettingsDialog","Settings did not open");Require(!c.Menu.interactable&&!c.Menu.blocksRaycasts,"Background remains interactive");
        c.Volume.value=.23f;Require(Mathf.Abs(AudioListener.volume-.23f)<.01f,"Volume preview failed");
        Click(c.SettingsCancel);await Task.Delay(100);Require(Mathf.Abs(AudioListener.volume-before)<.01f,"Cancel did not restore volume");Require(c.OpenModalName==""&&c.Menu.interactable,"Cancel did not restore menu");
        Click(c.LoadButton);await Task.Delay(100);Require(c.OpenModalName=="LoadDialog"&&c.LoadMessage.text.Contains("없습니다"),"Empty save state missing");Click(c.LoadClose);await Task.Delay(100);
        Click(c.ExitButton);await Task.Delay(100);Require(c.OpenModalName=="ConfirmDialog","Quit confirmation missing");Require(EventSystem.current.currentSelectedGameObject==c.ConfirmCancel.gameObject,"Quit should default to safe cancel");Click(c.ConfirmCancel);await Task.Delay(100);
        Require(EditorApplication.isPlaying&&c.OpenModalName=="","Quit cancel ended game");
        return "PASS: button raycasts/clicks, disabled continue, settings preview+cancel, input blocking, empty saves, quit confirmation+cancel.";
    }
    public static async Task<string> NewGame()
    {
        var c=Menu();Require(c,"No title menu");Click(c.NewGameButton);Require(c.IsTransitioning,"Transition not locked");
        for(int i=0;i<40&&SceneManager.GetActiveScene().name!="PartySelection";i++)await Task.Delay(100);
        Require(SceneManager.GetActiveScene().name=="PartySelection","Destination scene not loaded");
        var view=UnityEngine.Object.FindAnyObjectByType<PartySelectionController>();Require(view&&view.SelectedCount==0,"New game did not arrive at fresh party selection");
        return "PASS: New game fades and opens the new two-adventurer selection. No build executed.";
    }
    public static string OpenTitle()
    {
        Require(!EditorApplication.isPlaying,"Stop first");
        EditorSceneManager.OpenScene("Assets/Scenes/StartMenu.unity");
        return "Title scene open.";
    }
    public static async Task<string> SettingsCommit()
    {
        var c=Menu();Require(c,"No title menu");
        const string volumeKey="demo5.settings.masterVolume",screenKey="demo5.settings.fullscreen";
        bool hadVolume=PlayerPrefs.HasKey(volumeKey),hadScreen=PlayerPrefs.HasKey(screenKey);
        float oldStoredVolume=PlayerPrefs.GetFloat(volumeKey),oldLiveVolume=AudioListener.volume;
        int oldStoredScreen=PlayerPrefs.GetInt(screenKey);
        try{
            Click(c.SettingsButton);await Task.Delay(100);c.Volume.value=.42f;Click(c.SettingsApply);await Task.Delay(100);
            Require(Mathf.Abs(PlayerPrefs.GetFloat(volumeKey)-.42f)<.01f,"Applied volume not persisted");
            Click(c.SettingsButton);await Task.Delay(100);Require(Mathf.Abs(c.Volume.value-.42f)<.01f,"Reopened settings differ");
            Click(c.SettingsCancel);await Task.Delay(100);
            return "PASS: settings apply/persistence/reopen. Original preferences restored after verification.";
        }finally{
            if(c.OpenModalName=="SettingsDialog")c.CancelSettings();AudioListener.volume=oldLiveVolume;
            if(hadVolume)PlayerPrefs.SetFloat(volumeKey,oldStoredVolume);else PlayerPrefs.DeleteKey(volumeKey);
            if(hadScreen)PlayerPrefs.SetInt(screenKey,oldStoredScreen);else PlayerPrefs.DeleteKey(screenKey);
            PlayerPrefs.Save();
        }
    }
    public static string ShowSettings(){Menu().OpenSettings();return "Settings open for visual check.";}
    public static string CloseSettings(){Menu().CancelSettings();return "Settings closed without changes.";}
}
