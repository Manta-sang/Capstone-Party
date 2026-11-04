using System.Collections.Generic;
using System.Linq;
using System.Collections;
using CrosswalkRun;
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
        [Header("Continuous falling blocks")]
        [Min(0.1f)] public float initialDropInterval = .4f;
        [Min(0.1f)] public float middleDropInterval = .3f;
        [Min(0.1f)] public float finalDropInterval = .22f;
        // Three equal time bands. Floor keeps coverage within the requested fraction.
        public static int DifficultyStage(double elapsed, float roundDuration)
            => Mathf.Clamp(Mathf.FloorToInt((float)(elapsed / System.Math.Max(1f, roundDuration) * 3)), 0, 2);
        public static int CoverageLimit(int cellCount, int stage)
            => Mathf.Max(0, Mathf.Min(cellCount - 1, Mathf.FloorToInt(cellCount * (Mathf.Clamp(stage, 0, 2) + 1) / 4f)));
        readonly List<int> availableCells = new List<int>();
        readonly HashSet<int> occupiedCells = new HashSet<int>();
        [SerializeField] private string resultSceneName = "Ranking";
        [SerializeField] private float resultDelay = 3f;
        private bool loadingResults;
        readonly NetworkVariable<double> begins = new NetworkVariable<double>(-1);
        readonly NetworkVariable<bool> ended = new NetworkVariable<bool>();
        readonly HashSet<ulong> ready = new HashSet<ulong>();
        readonly List<ulong> alive = new List<ulong>();
        readonly Dictionary<ulong, int> ranks = new Dictionary<ulong, int>();
        readonly List<Wave> waves = new List<Wave>();
        readonly List<ulong> contactDeaths = new List<ulong>();
        readonly Dictionary<ulong, Collider[]> playerColliders = new Dictionary<ulong, Collider[]>();
        readonly Stack<GameObject> pool = new Stack<GameObject>();
        Transform[] cells;
        BlockFallCellArea[] cellAreas;
        bool spawned, sentReady, unlocked;
        public bool HasRoundStarted => unlocked;
        public float CountdownRemaining => !IsSpawned||begins.Value<0?0:Mathf.Max(0,(float)(begins.Value-NetworkManager.ServerTime.Time));
        int playersAtStart;
        float deadline;
        double nextWave;
        string result = "";
        class Wave { public int[] cells; public double hit; public GameObject[] visuals; }
        public override void OnNetworkSpawn()
        {
            cells = new Transform[tiles.childCount];
            cellAreas = new BlockFallCellArea[cells.Length];
            for (int i=0;i<cells.Length;i++){cells[i]=tiles.GetChild(i);cellAreas[i]=cells[i].GetComponent<BlockFallCellArea>();}
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
                alive.Add(id); playerColliders[id]=p.GetComponentsInChildren<Collider>(); i++;
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
            if(IsServer && spawned && begins.Value<0 && ready.Count>=alive.Count) { playersAtStart=alive.Count; begins.Value=now+GameStartPresentation.ScheduledStartDelay; nextWave=begins.Value+2; }
            if(begins.Value<0) { if(Time.realtimeSinceStartup>deadline) result="Waiting timed out. Return to lobby."; return; }
            if(!unlocked && now>=begins.Value && !ended.Value)
            { unlocked=true; if(local!=null) Controls(local.gameObject,true); }
            if(IsServer && !ended.Value && now>=begins.Value)
            {
                for(int i=alive.Count-1;i>=0;i--) if(!NetworkManager.ConnectedClients.ContainsKey(alive[i])) { ranks[alive[i]]=alive.Count; alive.RemoveAt(i); }
                if(now>=begins.Value+duration || RoundHasWinner) Finish();
                else if(now>=nextWave && now+warningTime+fallTime+.65 < begins.Value+duration)
                {
                    // Reserve warning and falling cells together, including the brief impact effect.
                    occupiedCells.Clear();
                    foreach (var pending in waves)
                        if (now <= pending.hit + .65f)
                            foreach (int cell in pending.cells) occupiedCells.Add(cell);
                    availableCells.Clear();
                    for (int i = 0; i < cells.Length; i++)
                        if (!occupiedCells.Contains(i)) availableCells.Add(i);
                    int stage = DifficultyStage(now - begins.Value, duration);
                    int cap = CoverageLimit(cells.Length, stage);
                    int count = Mathf.Min(Random.Range(1, 4), Mathf.Max(0, cap - occupiedCells.Count), availableCells.Count);
                    if (count > 0)
                    {
                        var indices = new int[count];
                        for (int i = 0; i < count; i++)
                        {
                            int pick = Random.Range(0, availableCells.Count);
                            indices[i] = availableCells[pick];
                            availableCells.RemoveAt(pick);
                        }
                        WaveClientRpc(indices, now + warningTime + fallTime);
                    }
                    float interval = stage == 0 ? initialDropInterval : stage == 1 ? middleDropInterval : finalDropInterval;
                    nextWave = now + Mathf.Max(.1f, interval);
                }
            }
            contactDeaths.Clear();
            for(int w=waves.Count-1;w>=0;w--)
            {
                var wave=waves[w];
                for(int j=0;j<wave.cells.Length;j++)
                {
                    var cell=cells[wave.cells[j]];
                    float remaining=(float)(wave.hit-now);
                    var v=wave.visuals[j];
                    var styled = v.GetComponent<BlockFallVisual>();
                    var area = cellAreas[wave.cells[j]];
                    Vector3 areaCenter = area != null ? area.center : cell.position;
                    Vector3 areaSize = area != null ? area.size : cell.lossyScale;
                    if (styled != null) {
                        v.transform.position = areaCenter + Vector3.up * (areaSize.y * .5f + .035f);
                        v.transform.localScale = new Vector3(areaSize.x, 2, areaSize.z);
                        styled.Animate(remaining, fallTime, warningTime, wave.cells[j] + (int)(wave.hit * 100));
                    } else {
                        v.transform.position=cell.position+Vector3.up*(cell.lossyScale.y*.5f+(remaining>fallTime?.035f:Mathf.Lerp(1,12,Mathf.Clamp01(remaining/fallTime))));
                        v.transform.localScale=new Vector3(cell.lossyScale.x*.96f,remaining>fallTime?.05f:2,cell.lossyScale.z*.96f);
                    }
                }
                if(IsServer && !ended.Value && now>=wave.hit && now<=wave.hit+.65)
                {
                    foreach(ulong id in alive)
                    {
                        if(contactDeaths.Contains(id) || !playerColliders.TryGetValue(id,out var colliders))continue;
                        bool hit=false;
                        foreach(var visual in wave.visuals)
                        {
                            var styled=visual.GetComponent<BlockFallVisual>();
                            if(styled==null || !styled.TryGetDamageBounds(out var bookBounds))continue;
                            foreach(var collider in colliders)
                                if(collider!=null && collider.enabled && !collider.isTrigger && collider.gameObject.activeInHierarchy && bookBounds.Intersects(collider.bounds)){hit=true;break;}
                            if(hit)break;
                        }
                        if(hit)contactDeaths.Add(id);
                    }
                }
                if(now>wave.hit+.65) { foreach(var v in wave.visuals) { v.SetActive(false); pool.Push(v); } waves.RemoveAt(w); }
            }
            if(IsServer && !ended.Value && contactDeaths.Count>0){
                int rank=alive.Count-contactDeaths.Count+1;
                foreach(var id in contactDeaths){ranks[id]=rank;alive.Remove(id);DeathClientRpc(id);}
                if(RoundHasWinner)Finish();
            }
        }
        [ServerRpc(RequireOwnership=false)] void ReadyServerRpc(ServerRpcParams p=default) { ready.Add(p.Receive.SenderClientId); }
        [ClientRpc]
        public void CorrectBoundaryClientRpc(ulong owner, Vector3 position){
            var player=NetworkManager.LocalClient != null ? NetworkManager.LocalClient.PlayerObject : null;
            if(player!=null && player.IsOwner && player.OwnerClientId==owner)
                BlockFallArenaBoundary.ApplyPosition(player,position);
        }
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
            if(!IsServer || ended.Value) return;
            ended.Value=true;
            foreach(var id in alive) ranks[id]=1;
            string text="BlockFall — Results\n";
            foreach(var item in ranks) text+=$"Player {item.Key}: #{item.Value} / +{PartyScoreRules.ForRank(item.Value)}\n";
            var ranking = RankingManager.Instance;
            if (ranking != null && ranking.IsSpawned)
            {
                foreach (var item in ranks)
                {
                    string playerName = $"Player {item.Key}";
                    foreach (var score in ranking.ScoreList)
                        if (score.ClientId == item.Key) { playerName = score.PlayerName.ToString(); break; }
                    ranking.AddScore(item.Key, playerName, PartyScoreRules.ForRank(item.Value));
                }
            }
            else Debug.LogError("[BlockFall] RankingManager is not spawned; scores could not be saved.");
            ResultClientRpc(text);
            StartCoroutine(GoToResults());
        }
        IEnumerator GoToResults()
        {
            loadingResults = true;
            yield return new WaitForSecondsRealtime(resultDelay);
            LoadResults();
        }
        public void LoadResults()
        {
            if (!IsSpawned || !IsServer || !ended.Value) return;
            if (string.IsNullOrWhiteSpace(resultSceneName) || !Application.CanStreamedLevelBeLoaded(resultSceneName))
            { loadingResults = false; Debug.LogError("[BlockFall] Invalid result scene: " + resultSceneName); return; }
            var status = NetworkManager.SceneManager.LoadScene(PartyGameRotation.ResultScene(resultSceneName), LoadSceneMode.Single);
            loadingResults = status == SceneEventProgressStatus.Started;
            if (!loadingResults) Debug.LogError("[BlockFall] Ranking load failed: " + status);
        }
        [ClientRpc] void ResultClientRpc(string text)
        {
            result=text;
            var p=NetworkManager.LocalClient.PlayerObject; if(p!=null){Controls(p.gameObject,false);if(p.GetComponentsInChildren<Renderer>().Any(r=>r.enabled))RoundEndFacing.Face(p);}
        }
        public string GameplayStatus => ended.Value ? "경기 종료 · 랭킹으로 이동 중" : DeveloperStatus;
        public string DeveloperStatus {
            get {
                if(!IsSpawned)return "멀티플레이 방에서 입장하세요.";
                if(result!="")return result;
                if(begins.Value<0)return "참가자 준비 확인 중...";
                double t=NetworkManager.ServerTime.Time-begins.Value;
                if(t<0)return "게임 시작 준비 중...";
                return $"남은 시간 {Mathf.Max(0,duration-(float)t):F0}초 / 단계 {DifficultyStage(t,duration)+1}";
            }
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
