using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    #region Constants
    private const float c_DefaultMoveSpeed = 5f;
    private const float c_DefaultTurnSpeed = 180f;
    private const string c_FileName = "player_transform.json";
    #endregion

    #region Private Fields
    [SerializeField] private float m_MoveSpeed = c_DefaultMoveSpeed;
    [SerializeField] private float m_TurnSpeed = c_DefaultTurnSpeed;
    [SerializeField] private string m_FilePath = string.Empty;
    [SerializeField] private string m_ScriptRelativeDir = string.Empty; // e.g., "MyFolder/SubFolder" relative to Assets
    [SerializeField] private string m_PlayerId = "player_1";
    [SerializeField] private GameObject m_PlayerPrefab;
    [SerializeField] private Transform m_RemotePlayersParent;
    [SerializeField] private bool m_SimulateOthers = true;
    [SerializeField] private int m_SimulatedPlayersCount = 3;
    [SerializeField] private float m_SimulatedMoveSpeed = 3f;
    [SerializeField] private float m_SimulatedTurnSpeed = 180f;
    [SerializeField] private float m_SimulatedRadius = 10f;
    [SerializeField] private float m_SimulatedTickSeconds = 0.1f;
    [SerializeField, Range(0f, 1f)] private float m_SimulatedPosLerp = 0.3f;
    [SerializeField, Range(0f, 1f)] private float m_SimulatedRotLerp = 0.3f;
    [Header("Remote Smoothing")]
    [SerializeField] private bool m_SmoothRemoteEnabled = true;
    [SerializeField, Range(0.01f, 1f)] private float m_SmoothRemoteDuration = 0.2f;
    [SerializeField] private float m_SmoothSnapDistance = 5f;

    private Vector3 m_LastWrittenPosition;
    private Quaternion m_LastWrittenRotation;
    private WaitForSeconds m_ReadIntervalWait;
    private WaitForSeconds m_IoLatencyWait;
    private readonly Dictionary<string, Transform> m_RemotePlayers = new Dictionary<string, Transform>();
    private readonly Dictionary<string, SimState> m_SimStates = new Dictionary<string, SimState>();
    private readonly Dictionary<string, SmoothState> m_RemoteSmoothStates = new Dictionary<string, SmoothState>();
    #endregion

    #region Unity Lifecycle
    void Awake()
    {
        if (string.IsNullOrWhiteSpace(m_FilePath))
        {
            try
            {
                // Build absolute path based on stored relative directory next to the script
                string absoluteDir = string.IsNullOrWhiteSpace(m_ScriptRelativeDir)
                    ? Application.dataPath
                    : Path.Combine(Application.dataPath, m_ScriptRelativeDir.Replace('/', Path.DirectorySeparatorChar));
                m_FilePath = Path.Combine(absoluteDir, c_FileName);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Failed to resolve script directory. Falling back to persistentDataPath. Error: {exception.Message}");
                m_FilePath = Path.Combine(Application.persistentDataPath, c_FileName);
            }
        }

        m_LastWrittenPosition = transform.position;
        m_LastWrittenRotation = transform.rotation;
        // m_ReadIntervalWait = new WaitForSeconds(1f);
        m_IoLatencyWait = new WaitForSeconds(0.02f);

        // Ensure directory exists
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(m_FilePath));
        }
        catch (Exception exception)
        {
            Debug.LogError($"Failed to ensure directory for path {m_FilePath}: {exception.Message}");
        }

        // Seed simulator ids
        if (m_SimulateOthers)
        {
            m_SimStates.Clear();
            for (int i = 0; i < Mathf.Max(0, m_SimulatedPlayersCount); i++)
            {
                string simId = $"sim_{i + 1}";
                if (simId == m_PlayerId) simId += "_local";
                if (!m_SimStates.ContainsKey(simId))
                {
                    m_SimStates.Add(simId, new SimState());
                }
            }
        }

        // Write initial transform (with artificial IO latency)
        WriteTransformToFile();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        try
        {
            // Compute and store the relative directory of this script under Assets
            var monoScript = UnityEditor.MonoScript.FromMonoBehaviour(this);
            string assetPath = UnityEditor.AssetDatabase.GetAssetPath(monoScript); // e.g., "Assets/Folder/PlayerMovement.cs"
            string assetDir = Path.GetDirectoryName(assetPath) ?? "Assets";
            if (assetDir.StartsWith("Assets"))
            {
                string relative = assetDir.Length > "Assets".Length
                    ? assetDir.Substring("Assets".Length).TrimStart('/', '\\')
                    : string.Empty;
                m_ScriptRelativeDir = relative.Replace('\\', '/');
            }
            else
            {
                m_ScriptRelativeDir = string.Empty;
            }
        }
        catch (Exception)
        {
            // Ignore; will fall back at runtime
            m_ScriptRelativeDir = string.Empty;
        }
    }
#endif

    void OnEnable()
    {
        StartCoroutine(ReadTransformLoop());
        if (m_SimulateOthers)
        {
            StartCoroutine(SimulateOthersLoop());
        }
    }

    void OnDisable()
    {
        StopAllCoroutines();
    }

    void Update()
    {
        HandleMovementInput();
        MaybeWriteTransformIfChanged();
    }
    #endregion

    #region Private Methods
    private void HandleMovementInput()
    {
        float horizontal = Input.GetAxisRaw("Horizontal"); // A/D or Left/Right
        float vertical = Input.GetAxisRaw("Vertical");   // W/S or Up/Down

        Vector3 inputDirection = new Vector3(horizontal, 0f, vertical);
        if (inputDirection.sqrMagnitude > 0.0001f)
        {
            Vector3 moveDirection = inputDirection.normalized;
            transform.position += moveDirection * (m_MoveSpeed * Time.deltaTime);

            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, m_TurnSpeed * Time.deltaTime);
        }
    }

    private void MaybeWriteTransformIfChanged()
    {
        if ((transform.position - m_LastWrittenPosition).sqrMagnitude > 0.0001f || Quaternion.Angle(transform.rotation, m_LastWrittenRotation) > 0.1f)
        {
            WriteTransformToFile();
        }
    }

    private void WriteTransformToFile()
    {
        StartCoroutine(WriteTransformToFileDelayed(transform.position, transform.rotation));
    }

    private IEnumerator WriteTransformToFileDelayed(Vector3 _position, Quaternion _rotation)
    {
        yield return m_IoLatencyWait;
        try
        {
            // Read current snapshot (if any)
            System.Collections.Generic.List<PlayerRecord> records;
            if (File.Exists(m_FilePath) && TryDeserializePlayers(File.ReadAllText(m_FilePath), out records))
            {
                bool updated = false;
                for (int i = 0; i < records.Count; i++)
                {
                    if (records[i].id == m_PlayerId)
                    {
                        records[i].position = _position;
                        records[i].rotation = _rotation;
                        updated = true;
                        break;
                    }
                }
                if (!updated)
                {
                    records.Add(new PlayerRecord { id = m_PlayerId, position = _position, rotation = _rotation });
                }
            }
            else
            {
                records = new System.Collections.Generic.List<PlayerRecord>
                {
                    new PlayerRecord { id = m_PlayerId, position = _position, rotation = _rotation }
                };
            }

            string serialized = SerializePlayers(records);
            File.WriteAllText(m_FilePath, serialized);
            m_LastWrittenPosition = _position;
            m_LastWrittenRotation = _rotation;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Failed to write transform to file {m_FilePath}: {exception.Message}");
        }
    }

    private IEnumerator ReadTransformLoop()
    {
        while (true)
        {
            yield return m_ReadIntervalWait;
            yield return m_IoLatencyWait;
            ReadAndApplyTransformFromFile();
        }
    }

    private void ReadAndApplyTransformFromFile()
    {
        try
        {
            if (!File.Exists(m_FilePath))
            {
                return;
            }

            string content = File.ReadAllText(m_FilePath);
            if (TryDeserializePlayers(content, out List<PlayerRecord> players))
            {
                ApplyPlayersSnapshot(players);
            }
        }
        catch (Exception exception)
        {
            Debug.LogError($"Failed to read transform from file {m_FilePath}: {exception.Message}");
        }
    }

    [Serializable]
    private class PlayerRecord
    {
        public string id;
        public Vector3 position;
        public Quaternion rotation;
    }

    [Serializable]
    private class PlayersData
    {
        public PlayerRecord[] players;
    }

    private void ApplyPlayersSnapshot(List<PlayerRecord> _players)
    {
        // Update/Spawn
        System.Collections.Generic.HashSet<string> seenIds = new System.Collections.Generic.HashSet<string>();
        for (int i = 0; i < _players.Count; i++)
        {
            PlayerRecord record = _players[i];
            if (string.IsNullOrEmpty(record.id))
            {
                continue;
            }
            seenIds.Add(record.id);

            if (record.id == m_PlayerId)
            {
                // Local player is authoritative; skip applying to self
                continue;
            }

            if (!m_RemotePlayers.TryGetValue(record.id, out Transform remoteTransform) || remoteTransform == null)
            {
                if (m_PlayerPrefab == null)
                {
                    Debug.LogWarning("Player prefab is not assigned; cannot spawn remote players.");
                    continue;
                }
                GameObject instance = Instantiate(m_PlayerPrefab, record.position, record.rotation, m_RemotePlayersParent);
                remoteTransform = instance.transform;
                m_RemotePlayers[record.id] = remoteTransform;
                instance.name = $"RemotePlayer_{record.id}";
                m_RemoteSmoothStates[record.id] = new SmoothState { fromPos = record.position, toPos = record.position, fromRot = record.rotation, toRot = record.rotation, t = 1f };
            }
            else
            {
                if (m_SmoothRemoteEnabled)
                {
                    QueueSmooth(record.id, remoteTransform, record.position, record.rotation);
                }
                else
                {
                    if ((remoteTransform.position - record.position).sqrMagnitude > 0.000001f || Quaternion.Angle(remoteTransform.rotation, record.rotation) > 0.01f)
                    {
                        remoteTransform.SetPositionAndRotation(record.position, record.rotation);
                    }
                }
            }
        }

        // Remove missing
        if (m_RemotePlayers.Count > 0)
        {
            System.Collections.Generic.List<string> toRemove = null;
            foreach (var kvp in m_RemotePlayers)
            {
                if (!seenIds.Contains(kvp.Key))
                {
                    if (toRemove == null) toRemove = new System.Collections.Generic.List<string>();
                    toRemove.Add(kvp.Key);
                }
            }
            if (toRemove != null)
            {
                for (int i = 0; i < toRemove.Count; i++)
                {
                    string id = toRemove[i];
                    if (m_RemotePlayers.TryGetValue(id, out Transform tr) && tr != null)
                    {
                        Destroy(tr.gameObject);
                    }
                    m_RemotePlayers.Remove(id);
                    m_RemoteSmoothStates.Remove(id);
                }
            }
        }
    }

    private struct SmoothState
    {
        public Vector3 fromPos;
        public Vector3 toPos;
        public Quaternion fromRot;
        public Quaternion toRot;
        public float t;
    }

    private void QueueSmooth(string _id, Transform _tr, Vector3 _targetPos, Quaternion _targetRot)
    {
        SmoothState s;
        if (!m_RemoteSmoothStates.TryGetValue(_id, out s))
        {
            s = new SmoothState
            {
                fromPos = _tr.position,
                toPos = _targetPos,
                fromRot = _tr.rotation,
                toRot = _targetRot,
                t = 0f
            };
        }
        else
        {
            float dist = (_tr.position - _targetPos).magnitude;
            if (dist > m_SmoothSnapDistance)
            {
                // Large jump -> snap
                _tr.SetPositionAndRotation(_targetPos, _targetRot);
                s.fromPos = _targetPos;
                s.toPos = _targetPos;
                s.fromRot = _targetRot;
                s.toRot = _targetRot;
                s.t = 1f;
                m_RemoteSmoothStates[_id] = s;
                return;
            }

            // Start from current transform and blend to new target
            s.fromPos = _tr.position;
            s.toPos = _targetPos;
            s.fromRot = _tr.rotation;
            s.toRot = _targetRot;
            s.t = 0f;
        }
        m_RemoteSmoothStates[_id] = s;
    }

    private void LateUpdate()
    {
        if (!m_SmoothRemoteEnabled || m_RemoteSmoothStates.Count == 0) return;
        float duration = Mathf.Max(0.01f, m_SmoothRemoteDuration);
        System.Collections.Generic.List<string> ids = new System.Collections.Generic.List<string>(m_RemoteSmoothStates.Keys);
        for (int i = 0; i < ids.Count; i++)
        {
            string id = ids[i];
            if (!m_RemotePlayers.TryGetValue(id, out Transform tr) || tr == null) continue;
            SmoothState s = m_RemoteSmoothStates[id];
            if (s.t >= 1f) continue;
            s.t = Mathf.Clamp01(s.t + Time.deltaTime / duration);
            Vector3 pos = Vector3.Lerp(s.fromPos, s.toPos, s.t);
            Quaternion rot = Quaternion.Slerp(s.fromRot, s.toRot, s.t);
            tr.SetPositionAndRotation(pos, rot);
            m_RemoteSmoothStates[id] = s;
        }
    }

    private static string SerializePlayers(System.Collections.Generic.List<PlayerRecord> _records)
    {
        PlayersData data = new PlayersData { players = _records.ToArray() };
        return JsonUtility.ToJson(data);
    }

    private static bool TryDeserializePlayers(string _json, out System.Collections.Generic.List<PlayerRecord> _records)
    {
        _records = null;
        if (string.IsNullOrWhiteSpace(_json))
        {
            return false;
        }
        try
        {
            PlayersData data = JsonUtility.FromJson<PlayersData>(_json);
            if (data != null && data.players != null)
            {
                _records = new System.Collections.Generic.List<PlayerRecord>(data.players);
                return true;
            }
        }
        catch
        {
            // ignore
        }
        return false;
    }

    // --- Simulator ---
    private class SimState
    {
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public Vector3 target;
    }

    private IEnumerator SimulateOthersLoop()
    {
        WaitForSeconds tick = new WaitForSeconds(Mathf.Max(0.01f, m_SimulatedTickSeconds));
        // Initialize positions around origin
        foreach (var kvp in m_SimStates)
        {
            SimState s = kvp.Value;
            s.position = UnityEngine.Random.insideUnitSphere;
            s.position.y = 0f;
            s.position *= m_SimulatedRadius * 0.25f;
            s.target = GetNewSimTarget(s.position);
        }

        while (true)
        {
            yield return tick;

            // Load existing records
            List<PlayerRecord> records;
            if (File.Exists(m_FilePath) && TryDeserializePlayers(File.ReadAllText(m_FilePath), out records))
            {
                // ok
            }
            else
            {
                records = new List<PlayerRecord>();
            }

            // Ensure local record present as-is from current transform
            UpsertRecord(records, m_PlayerId, transform.position, transform.rotation);

            // Advance sim states and upsert
            foreach (var kvp in m_SimStates)
            {
                string id = kvp.Key;
                SimState s = kvp.Value;
                StepSim(ref s);
                UpsertRecord(records, id, s.position, s.rotation);
            }

            // Save
            string json = SerializePlayers(records);
            try
            {
                File.WriteAllText(m_FilePath, json);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Failed to write simulated players to file {m_FilePath}: {exception.Message}");
            }
        }
    }

    private void UpsertRecord(List<PlayerRecord> _records, string _id, Vector3 _pos, Quaternion _rot)
    {
        bool updated = false;
        for (int i = 0; i < _records.Count; i++)
        {
            if (_records[i].id == _id)
            {
                _records[i].position = _pos;
                _records[i].rotation = _rot;
                updated = true;
                break;
            }
        }
        if (!updated)
        {
            _records.Add(new PlayerRecord { id = _id, position = _pos, rotation = _rot });
        }
    }

    private void StepSim(ref SimState _s)
    {
        Vector3 toTarget = _s.target - _s.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.1f)
        {
            _s.target = GetNewSimTarget(_s.position);
            toTarget = _s.target - _s.position;
            toTarget.y = 0f;
        }
        Vector3 dir = toTarget.normalized;
        float dt = Mathf.Max(0.01f, m_SimulatedTickSeconds);
        Vector3 desiredStep = _s.position + dir * (m_SimulatedMoveSpeed * dt);
        _s.position = Vector3.Lerp(_s.position, desiredStep, m_SimulatedPosLerp);
        if (dir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
            Quaternion desiredRot = Quaternion.RotateTowards(_s.rotation, targetRot, m_SimulatedTurnSpeed * dt);
            _s.rotation = Quaternion.Slerp(_s.rotation, desiredRot, m_SimulatedRotLerp);
        }
    }

    private Vector3 GetNewSimTarget(Vector3 _around)
    {
        Vector2 rand = UnityEngine.Random.insideUnitCircle * m_SimulatedRadius;
        return new Vector3(_around.x + rand.x, 0f, _around.z + rand.y);
    }

    #endregion

    #if UNITY_EDITOR
    [ContextMenu("Open File Location")]
    private void OpenFileLocation()
    {
        if (string.IsNullOrWhiteSpace(m_FilePath))
        {
            return;
        }
        string directory = Path.GetDirectoryName(m_FilePath);
        if (Directory.Exists(directory))
        {
            UnityEditor.EditorUtility.RevealInFinder(m_FilePath);
        }
    }
    #endif
}
