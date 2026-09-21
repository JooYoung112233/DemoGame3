using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Live49.Core
{
    public static class SaveSystem
    {
        [Serializable] sealed class Envelope { public int version=1;public string payload,checksum; }
        public static JourneyState Current { get; set; }
        public static JourneyState Pending { get; private set; }
        public static event Action<bool> AutoSaveCompleted;
        public static string AutoSaveError { get; private set; }
        static string _autoPath, _autoFingerprint;
#if UNITY_EDITOR
        public static string TestSlotPath;
#endif
        public static string SlotPath
        {
            get
            {
#if UNITY_EDITOR
                if(!string.IsNullOrEmpty(TestSlotPath))return TestSlotPath;
#endif
                return Path.Combine(Application.persistentDataPath,"Live49","journey-v1.json");
            }
        }
        public static bool HasSave=>TryRead(SlotPath,out _,out _);
        public static bool CanSave=>Current!=null&&UI.GameHud.Instance!=null&&UI.GameHud.Instance.IsExploring&&Current.Valid();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){Current=null;Pending=null;AutoSaveCompleted=null;AutoSaveError=null;_autoPath=null;_autoFingerprint=null;
#if UNITY_EDITOR
            TestSlotPath=null;
#endif
        }
        public static void NewGame(){Pending=null;}
        public static bool QueueLoad(out string error)
        =>QueueLoad(SlotPath,out error);
        public static bool QueueLoad(string path,out string error)
        {if(!TryRead(path,out var state,out error)){Pending=null;return false;}Pending=state;return true;}
        public static JourneyState TakePending(){var state=Pending;Pending=null;return state;}
        static string Hash(string text)
        {using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text)));}
        static string Fingerprint(JourneyState state)
        {
            var snapshot=JsonUtility.FromJson<JourneyState>(JsonUtility.ToJson(state));
            snapshot.savedUtc="";
            return Hash(JsonUtility.ToJson(snapshot));
        }
        // Call only at a resumable checkpoint, after the whole gameplay transaction is applied.
        // Frequent dialogue/cooking checkpoints remain silent; milestones show one small completion cue.
        public static bool AutoSave(JourneyState state,out string error,bool notify=true)
        {
            string fingerprint=state==null?null:Fingerprint(state);
            if(fingerprint!=null&&_autoPath==SlotPath&&_autoFingerprint==fingerprint&&File.Exists(SlotPath))
            {error=null;return true;}
            bool saved=Write(SlotPath,state,out error);
            AutoSaveError=error;
            if(notify||!saved)AutoSaveCompleted?.Invoke(saved);
            return saved;
        }
        public static bool Write(string path,JourneyState state,out string error)
        {
            error=null;
            try
            {
                if(state==null||!state.Valid())throw new InvalidDataException("저장할 진행 상태를 확인할 수 없어요.");
                var snapshot=JsonUtility.FromJson<JourneyState>(JsonUtility.ToJson(state));snapshot.savedUtc=DateTime.UtcNow.ToString("O");
                string payload=JsonUtility.ToJson(snapshot);
                string text=JsonUtility.ToJson(new Envelope{payload=payload,checksum=Hash(payload)},true);
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                string temporary=path+".tmp";File.WriteAllText(temporary,text,new UTF8Encoding(false));
                if(!TryRead(temporary,out _,out var validation))throw new InvalidDataException(validation);
                if(File.Exists(path))File.Replace(temporary,path,path+".bak");else File.Move(temporary,path);
                state.savedUtc=snapshot.savedUtc;
                if(path==SlotPath){_autoPath=path;_autoFingerprint=Fingerprint(state);AutoSaveError=null;}
                return true;
            }
            catch(Exception exception) when(exception is IOException||exception is InvalidDataException||exception is UnauthorizedAccessException||exception is ArgumentException)
            {error="진행을 저장하지 못했어요. 저장 위치를 확인한 뒤 다시 시도해주세요.";Debug.LogWarning(exception.Message);return false;}
        }
        public static bool TryRead(string path,out JourneyState state,out string error)
        {
            state=null;error=null;
            try
            {
                if(!File.Exists(path)){error="저장된 여정이 없어요.";return false;}
                if(new FileInfo(path).Length>2000000)throw new InvalidDataException();
                var envelope=JsonUtility.FromJson<Envelope>(File.ReadAllText(path));
                if(envelope==null||envelope.version!=1||envelope.payload==null||envelope.checksum!=Hash(envelope.payload))throw new InvalidDataException();
                var loaded=JsonUtility.FromJson<JourneyState>(envelope.payload);
                if(loaded==null||!loaded.Valid())throw new InvalidDataException();
                if(!string.IsNullOrEmpty(loaded.node))
                {
                    var graph=Chapter00.ContinuationGraph.Load();
                    var node=Array.Find(graph.nodes,n=>n.id==loaded.node);
                    if(node==null||loaded.line>node.lines.Length)throw new InvalidDataException();
                }
                state=loaded;return true;
            }
            catch(Exception exception) when(exception is IOException||exception is InvalidDataException||exception is UnauthorizedAccessException||exception is ArgumentException)
            {error="저장 파일을 읽을 수 없어요. 기존 파일은 그대로 보관했어요.";return false;}
        }
    }
}

