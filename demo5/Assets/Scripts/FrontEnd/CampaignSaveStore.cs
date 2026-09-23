using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using UnityEngine;

namespace Demo5.FrontEnd
{
    public sealed class SaveSlotInfo
    {
        public int Slot;
        public bool Exists;
        public CampaignSaveData Data;
        public string Error;
        public bool CanLoad => Data!=null && Error==null;
    }
    public static class CampaignSaveStore
    {
        [Serializable] sealed class Envelope { public string Magic, Payload, Hash; }
        public const int SlotCount=3;
        const string Magic="NEIGHBOR-NOT-SAVE";
#if UNITY_EDITOR
        // Verification uses an isolated folder and never overwrites a player's slots.
        public static string TestDirectory;
#endif
        public static string DirectoryPath {
            get {
#if UNITY_EDITOR
                if(!string.IsNullOrEmpty(TestDirectory))return TestDirectory;
#endif
                return Path.Combine(Application.persistentDataPath,"Saves");
            }
        }
        public static string SlotPath(int slot)
        {
            if(slot<0 || slot>=SlotCount)throw new ArgumentOutOfRangeException(nameof(slot));
            return Path.Combine(DirectoryPath,"slot-"+(slot+1)+".json");
        }
        static string Digest(string value) { using(var sha=SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(value))); }
        public static SaveSlotInfo Read(int slot,SettlementController catalog)
        {
            var info=new SaveSlotInfo { Slot=slot };
            try
            {
                string path=SlotPath(slot);info.Exists=File.Exists(path);if(!info.Exists)return info;
                if(new FileInfo(path).Length>4*1024*1024)throw new InvalidDataException("저장 파일 크기가 올바르지 않습니다.");
                var envelope=JsonUtility.FromJson<Envelope>(File.ReadAllText(path,Encoding.UTF8));
                if(envelope==null || envelope.Magic!=Magic || string.IsNullOrEmpty(envelope.Payload) || envelope.Hash!=Digest(envelope.Payload))throw new InvalidDataException("저장 파일이 손상되었습니다.");
                var data=JsonUtility.FromJson<CampaignSaveData>(envelope.Payload);CampaignPersistence.Upgrade(data,catalog);info.Data=data;
            }
            catch(Exception e) { info.Data=null;info.Error=Friendly(e); }
            return info;
        }
        static string Friendly(Exception e)
        {
            if(e is UnauthorizedAccessException)return "저장 폴더에 접근할 수 없습니다.";
            if(e is IOException && !(e is InvalidDataException))return "저장 파일을 읽거나 쓰지 못했습니다.";
            if(e is InvalidOperationException || e is InvalidDataException)return e.Message;
            return "저장 내용을 읽을 수 없습니다.";
        }
        public static bool Write(int slot,CampaignSaveData data,SettlementController catalog,out string error)
        {
            error=null;string temp=null;
            try
            {
                CampaignPersistence.Validate(data,catalog);string path=SlotPath(slot);Directory.CreateDirectory(DirectoryPath);
                string payload=JsonUtility.ToJson(data);string encoded=JsonUtility.ToJson(new Envelope { Magic=Magic, Payload=payload, Hash=Digest(payload) });
                temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
                using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)) { byte[] bytes=Encoding.UTF8.GetBytes(encoded);file.Write(bytes,0,bytes.Length);file.Flush(true); }
                // Keep the existing slot intact until the full replacement has reached disk.
                if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);
                return true;
            }
            catch(Exception e) { error=Friendly(e);return false; }
            finally { if(temp!=null && File.Exists(temp))try { File.Delete(temp); } catch(IOException) { } catch(UnauthorizedAccessException) { } }
        }
        public static SaveSlotInfo Latest(SettlementController catalog) => Enumerable.Range(0,SlotCount).Select(i=>Read(i,catalog)).Where(i=>i.CanLoad).OrderByDescending(i=>DateTime.Parse(i.Data.SavedUtc).ToUniversalTime()).FirstOrDefault();
    }
}
