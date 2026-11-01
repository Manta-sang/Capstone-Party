using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BlockFall
{
    public class BlockFallGame : NetworkBehaviour
    {
        public Transform tiles;
        public Transform[] starts;
        public GameObject[] characters;
        public GameObject blockVisual;
        public float duration = 60, warningTime = 1.2f, fallTime = .45f;
        readonly NetworkVariable<double> begins = new NetworkVariable<double>(-1);
        readonly NetworkVariable<bool> ended = new NetworkVariable<bool>();
        readonly HashSet<ulong> ready = new HashSet<ulong>();
        readonly List<ulong> alive = new List<ulong>();
        readonly Dictionary<ulong, int> ranks = new Dictionary<ulong, int>();
        readonly List<Wave> waves = new List<Wave>();
        readonly Stack<GameObject> pool = new Stack<GameObject>();
        Transform[] cells;
        bool spawned, sentReady, unlocked;
        int playersAtStart;
        float deadline;
        double nextWave;
        string result = "";
        class Wave { public int[] cells; public double hit; public GameObject[] visuals; public bool judged; }
        public override void OnNetworkSpawn()
        {
            cells = new Transform[tiles.childCount];
            for (int i=0;i<cells.Length;i++) cells[i]=tiles.GetChild(i);
            deadline = Time.realtimeSinceStartup + 30;
            if(IsServer) NetworkManager.SceneManager.OnLoadEventCompleted += Loaded;
        }
        void Loaded(string name, LoadSceneMode mode, List<ulong> complete, List<ulong> timeout)
        {
            if(name!=gameObject.scene.name || spawned) return;
            if(timeout.Count>0 || starts.Length<NetworkManager.ConnectedClientsIds.Count || cells.Length==0) { result="Entry failed"; return; }
            spawned=true;
            int i=0;
            foreach(ulong id in NetworkManager.ConnectedClientsIds)
            {
                var old=NetworkManager.SpawnManager.GetPlayerNetworkObject(id);
                if(old!=null) old.Despawn(true);
                var p=Instantiate(characters[i%characters.Length],starts[i].position,starts[i].rotation);
                p.GetComponent<NetworkObject>().SpawnAsPlayerObject(id,true);
                alive.Add(id); i++;
            }
        }
        bool RoundHasWinner => alive.Count == 0 || (playersAtStart > 1 && alive.Count == 1);
        void Update()
        {
            if(!IsSpawned) return;
            var local=NetworkManager.LocalClient != null ? NetworkManager.LocalClient.PlayerObject : null;
            if(local!=null && local.gameObject.scene==gameObject.scene && !sentReady)
            {
                Controls(local.gameObject,false); sentReady=true; ReadyServerRpc();
            }
            double now=NetworkManager.ServerTime.Time;
            if(IsServer && spawned && begins.Value<0 && ready.Count>=alive.Count) { playersAtStart=alive.Count; begins.Value=now+3; nextWave=begins.Value; }
            if(begins.Value<0) { if(Time.realtimeSinceStartup>deadline) result="Waiting timed out. Return to lobby."; return; }
            if(!unlocked && now>=begins.Value && !ended.Value)
            { unlocked=true; if(local!=null) Controls(local.gameObject,true); }
            if(IsServer && !ended.Value && now>=begins.Value)
            {
                for(int i=alive.Count-1;i>=0;i--) if(!NetworkManager.ConnectedClients.ContainsKey(alive[i])) { ranks[alive[i]]=alive.Count; alive.RemoveAt(i); }
                if(now>=begins.Value+duration || RoundHasWinner) Finish();
                else if(now>=nextWave)
                {
                    int count=Mathf.Min(Mathf.Max(1,cells.Length/3),4+(int)((now-begins.Value)/15)*2);
                    var indices=new int[count]; var used=new HashSet<int>();
                    for(int i=0;i<count;i++) { int n; do { n=Random.Range(0,cells.Length); } while(!used.Add(n)); indices[i]=n; }
                    WaveClientRpc(indices,now+warningTime+fallTime);
                    nextWave=now+warningTime+fallTime+.45;
                }
            }
            for(int w=waves.Count-1;w>=0;w--)
            {
                var wave=waves[w];
                for(int j=0;j<wave.cells.Length;j++)
                {
                    var cell=cells[wave.cells[j]];
                    float remaining=(float)(wave.hit-now);
                    var v=wave.visuals[j];
                    v.transform.position=cell.position+Vector3.up*(cell.lossyScale.y*.5f+(remaining>fallTime?.035f:Mathf.Lerp(1,12,Mathf.Clamp01(remaining/fallTime))));
                    v.transform.localScale=new Vector3(cell.lossyScale.x*.96f,remaining>fallTime?.05f:2,cell.lossyScale.z*.96f);
                }
                if(IsServer && !wave.judged && now>=wave.hit && !ended.Value)
                {
                    wave.judged=true;
                    var dead=new List<ulong>();
                    foreach(ulong id in alive)
                    {
                        var p=NetworkManager.SpawnManager.GetPlayerNetworkObject(id);
                        if(p==null) continue;
                        foreach(int index in wave.cells)
                        {
                            Vector3 q=cells[index].InverseTransformPoint(p.transform.position);
                            if(Mathf.Abs(q.x)<.5f && Mathf.Abs(q.z)<.5f) { dead.Add(id); break; }
                        }
                    }
                    int rank=alive.Count-dead.Count+1;
                    foreach(ulong id in dead) { ranks[id]=rank; alive.Remove(id); DeathClientRpc(id); }
                    if(RoundHasWinner) Finish();
                }
                if(now>wave.hit+.15) { foreach(var v in wave.visuals) { v.SetActive(false); pool.Push(v); } waves.RemoveAt(w); }
            }
        }
        [ServerRpc(RequireOwnership=false)] void ReadyServerRpc(ServerRpcParams p=default) { ready.Add(p.Receive.SenderClientId); }
        [ClientRpc] void WaveClientRpc(int[] indices,double hit)
        {
            var visuals=new GameObject[indices.Length];
            for(int i=0;i<indices.Length;i++) { visuals[i]=pool.Count>0?pool.Pop():Instantiate(blockVisual); visuals[i].SetActive(true); }
            waves.Add(new Wave { cells=indices,hit=hit,visuals=visuals });
        }
        [ClientRpc] void DeathClientRpc(ulong id)
        {
            var p=NetworkManager.SpawnManager.GetPlayerNetworkObject(id);
            if(p==null) return;
            Controls(p.gameObject,false);
            foreach(var c in p.GetComponentsInChildren<Collider>()) c.enabled=false;
            foreach(var r in p.GetComponentsInChildren<Renderer>()) r.enabled=false;
            var body=p.GetComponent<Rigidbody>(); if(body!=null) body.isKinematic=true;
        }
        static void Controls(GameObject p,bool enabled)
        {
            var move=p.GetComponent<PlayerMovement>(); if(move!=null) move.enabled=enabled;
            var push=p.GetComponent<PlayerInteraction>(); if(push!=null) push.enabled=enabled;
        }
        void Finish()
        {
            if(ended.Value) return;
            ended.Value=true;
            foreach(var id in alive) ranks[id]=1;
            string text="BlockFall — Results\n";
            foreach(var item in ranks) text+=$"Player {item.Key}: #{item.Value} / +{Mathf.Max(0,4-item.Value)}\n";
            ResultClientRpc(text);
        }
        [ClientRpc] void ResultClientRpc(string text)
        {
            result=text;
            var p=NetworkManager.LocalClient.PlayerObject; if(p!=null) Controls(p.gameObject,false);
        }
        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20,20,330,240),GUI.skin.box);
            GUILayout.Label("BLOCK FALL");
            if(!IsSpawned) GUILayout.Label("Enter from the multiplayer lobby.");
            else if(result!="") GUILayout.Label(result);
            else if(begins.Value<0) GUILayout.Label("Waiting for players...");
            else { double t=NetworkManager.ServerTime.Time-begins.Value; GUILayout.Label(t<0?$"Ready: {-t:F0}":$"Time: {Mathf.Max(0,duration-(float)t):F0}"); }
            if(IsSpawned && IsServer && (ended.Value || result!="") && GUILayout.Button("Return to Character"))
                NetworkManager.SceneManager.LoadScene("Character",LoadSceneMode.Single);
            GUILayout.EndArea();
        }
        public override void OnNetworkDespawn()
        {
            if(NetworkManager!=null && IsServer) NetworkManager.SceneManager.OnLoadEventCompleted-=Loaded;
            foreach(var wave in waves) foreach(var v in wave.visuals) if(v!=null) Destroy(v);
            foreach(var v in pool) if(v!=null) Destroy(v);
            waves.Clear(); pool.Clear();
        }
    }
}



