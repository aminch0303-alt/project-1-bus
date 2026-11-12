using System;
using System.Collections.Generic;
using UnityEngine;

// Standalone: no dependency on any other custom script.
[DisallowMultipleComponent]
public sealed class MobileTrainSingleScript : MonoBehaviour
{
    [Serializable] public class Car
    {
        public GameObject prefabOrSceneObject;
        [Min(0f)] public float distanceBehindPrevious = 3f;
        [Tooltip("X = sideways from track, Y = vertical height, Z = distance along track (not world/local straight-line displacement).")]
        public Vector3 positionOffset;
        public Vector3 rotationOffset;
        public Vector3 scaleMultiplier = Vector3.one;
        [Min(0.05f)] public float wheelbase = 3f; // Front-to-rear rotation reference distance.
    }

    [Header("Train")]
    public List<Car> cars = new List<Car>();
    [Min(0f)] public float speed = 10f;
    public bool playOnStart = true;
    public bool reverse;
    [Header("Track detection")]
    public bool includeChildTracks = true;
    public bool useStripCenter = true;
    [Header("Smoothness / Mobile Quality")]
    [Range(0f, 1f)] public float smoothness = 0.5f;
    public bool logDiagnostics = true;
    // A single quality setting controls the cached sample count, path smoothing,
    // and rotation response. None of these values allocate memory per frame.
    private int SampleCount { get { return Mathf.RoundToInt(Mathf.Lerp(64f, 512f, smoothness) / 16f) * 16; } }
    private int CurvePasses { get { return Mathf.RoundToInt(Mathf.Lerp(1f, 6f, smoothness)); } }
    private float RotationDamping { get { return Mathf.Lerp(0.05f, 0.35f, smoothness); } }
    private readonly Dictionary<Transform, Vector3> originalScales = new Dictionary<Transform, Vector3>();

    private class Track
    {
        public Vector3[] points;
        public float[] cumulative;
        public float length;
        public Transform[] instances;
        public float distance;
    }
    private readonly List<Track> tracks = new List<Track>();
    private bool running;

    private void Start()
    {
        Build();
        running = playOnStart;
    }

    public void PlayTrain() { running = true; }
    public void PauseTrain() { running = false; }

    private void OnDestroy()
    {
        // Only destroy objects that this component instantiated.
        foreach (Track t in tracks)
            if (t.instances != null)
                foreach (Transform tr in t.instances)
                    if (tr && tr.gameObject.name.EndsWith(" (Train Instance)", StringComparison.Ordinal))
                        Destroy(tr.gameObject);
    }

    private void Build()
    {
        tracks.Clear();
        MeshFilter[] filters = includeChildTracks ? GetComponentsInChildren<MeshFilter>(true) : GetComponents<MeshFilter>();
        int successful = 0;
        foreach (MeshFilter filter in filters)
        {
            if (!filter.sharedMesh) continue;
            try
            {
                List<Vector3> path = ExtractPath(filter);
                if (path == null || path.Count < 3)
                {
                    if (logDiagnostics) Debug.LogWarning("[MobileTrain] No usable track in " + filter.name + ". Mesh needs connected edges / triangles.", filter);
                    continue;
                }
                Track t = MakeTrack(path, SampleCount, CurvePasses);
                if (t == null) continue;
                t.instances = new Transform[cars.Count];
                for (int i = 0; i < cars.Count; i++)
                {
                    Car car = cars[i];
                    if (!car.prefabOrSceneObject) continue;
                    GameObject source = car.prefabOrSceneObject;
                    // Scene objects can be used directly for a single track. For multiple tracks, clone to avoid stealing it.
                    bool sceneObject = source.scene.IsValid();
                    GameObject obj;
                    if (sceneObject && successful == 0 && filters.Length == 1)
                    {
                        obj = source;
                    }
                    else
                    {
                        obj = Instantiate(source);
                        obj.name = source.name + " (Train Instance)";
                        obj.SetActive(true);
                    }
                    t.instances[i] = obj.transform;
                    // Zero multiplier from older inspector versions should not hide the model.
                    Vector3 multiplier = car.scaleMultiplier;
                    if (multiplier == Vector3.zero) multiplier = Vector3.one;
                    Vector3 baseScale = obj.transform.localScale;
                    if (!originalScales.TryGetValue(source.transform, out baseScale))
                    {
                        baseScale = source.transform.localScale;
                        originalScales[source.transform] = baseScale;
                    }
                    t.instances[i].localScale = Vector3.Scale(baseScale, multiplier);
                }
                tracks.Add(t);
                PlaceCars(t);
                successful++;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MobileTrain] Skipped mesh '" + filter.name + "': " + ex.Message + ". Check FBX Read/Write Enabled.", filter);
            }
        }
        if (successful == 0) Debug.LogError("[MobileTrain] No track detected. Put this script on the imported mesh or its parent, enable FBX Read/Write, and ensure the mesh has connected geometry.", this);
        else if (logDiagnostics) Debug.Log("[MobileTrain] Ready: " + successful + " track(s), " + cars.Count + " car slot(s).", this);
    }

    private static ulong EdgeKey(int a, int b)
    {
        uint lo = (uint)Mathf.Min(a, b), hi = (uint)Mathf.Max(a, b);
        return ((ulong)lo << 32) | hi;
    }
    private static void AddEdge(Dictionary<ulong, int> counts, int a, int b)
    {
        if (a == b) return;
        ulong key = EdgeKey(a, b);
        int n;
        counts.TryGetValue(key, out n);
        counts[key] = n + 1;
    }
    private List<Vector3> ExtractPath(MeshFilter filter)
    {
        Mesh mesh = filter.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        // FBX often duplicates vertex indices at UV seams. Weld by position first.
        Dictionary<Vector3Int, int> welded = new Dictionary<Vector3Int, int>(vertices.Length);
        List<Vector3> unique = new List<Vector3>();
        int[] remap = new int[vertices.Length];
        const float weldScale = 10000f;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 v = vertices[i];
            Vector3Int key = new Vector3Int(Mathf.RoundToInt(v.x * weldScale), Mathf.RoundToInt(v.y * weldScale), Mathf.RoundToInt(v.z * weldScale));
            int id;
            if (!welded.TryGetValue(key, out id)) { id = unique.Count; welded.Add(key, id); unique.Add(v); }
            remap[i] = id;
        }
        vertices = unique.ToArray();
        Dictionary<ulong, int> edgeCounts = new Dictionary<ulong, int>(triangles.Length);
        for (int i = 0; i + 2 < triangles.Length; i += 3)
        {
            AddEdge(edgeCounts, remap[triangles[i]], remap[triangles[i + 1]]);
            AddEdge(edgeCounts, remap[triangles[i + 1]], remap[triangles[i + 2]]);
            AddEdge(edgeCounts, remap[triangles[i + 2]], remap[triangles[i]]);
        }
        Dictionary<int, List<int>> adj = new Dictionary<int, List<int>>();
        foreach (KeyValuePair<ulong, int> e in edgeCounts)
        {
            if (e.Value != 1) continue;
            int v1 = (int)(e.Key >> 32), v2 = (int)(e.Key & 0xffffffffUL);
            if (!adj.ContainsKey(v1)) adj[v1] = new List<int>(2);
            if (!adj.ContainsKey(v2)) adj[v2] = new List<int>(2);
            adj[v1].Add(v2); adj[v2].Add(v1);
        }
        if (adj.Count < 3)
        {
            if (logDiagnostics) Debug.LogWarning("[MobileTrain] " + filter.name + ": no mesh boundary. Vertices=" + vertices.Length + ", triangles=" + triangles.Length / 3 + ". Export Blender strip WITH FACES, not only edges.", filter);
            return null;
        }
        List<List<int>> loops = new List<List<int>>();
        HashSet<int> visited = new HashSet<int>();
        foreach (int start in adj.Keys)
        {
            if (visited.Contains(start)) continue;
            List<int> loop = new List<int>();
            int previous = -1, current = start;
            for (int limit = 0; limit <= adj.Count; limit++)
            {
                if (visited.Contains(current)) break;
                visited.Add(current); loop.Add(current);
                List<int> neighbors = adj[current];
                int next = -1;
                for (int k = 0; k < neighbors.Count; k++)
                    if (neighbors[k] != previous && !visited.Contains(neighbors[k])) { next = neighbors[k]; break; }
                if (next < 0) break;
                previous = current; current = next;
            }
            if (loop.Count >= 3) loops.Add(loop);
        }
        loops.Sort((a, b) => b.Count.CompareTo(a.Count));
        if (loops.Count == 0) return null;
        List<Vector3> first = ToWorld(loops[0], vertices, filter.transform);
        if (!useStripCenter || loops.Count < 2) return first;
        List<Vector3> second = ToWorld(loops[1], vertices, filter.transform);
        // Only pair contours when their perimeters are comparable.
        float lenA = PathLength(first), lenB = PathLength(second);
        if (lenA < 0.01f || lenB < 0.01f || Mathf.Min(lenA, lenB) / Mathf.Max(lenA, lenB) < 0.65f) return first;
        Vector3[] aSamples = Resample(first, SampleCount);
        Vector3[] bSamples = Resample(second, SampleCount);
        int bestOffset = 0, bestDir = 1;
        float best = float.PositiveInfinity;
        // Align the two boundaries to avoid twisting the resulting centerline.
        for (int dir = -1; dir <= 1; dir += 2)
            for (int offset = 0; offset < SampleCount; offset++)
            {
                float score = 0f;
                for (int j = 0; j < 12; j++)
                {
                    int idx = j * SampleCount / 12;
                    int other = (offset + dir * idx + SampleCount * 2) % SampleCount;
                    score += (aSamples[idx] - bSamples[other]).sqrMagnitude;
                }
                if (score < best) { best = score; bestOffset = offset; bestDir = dir; }
            }
        List<Vector3> center = new List<Vector3>(SampleCount);
        for (int i = 0; i < SampleCount; i++)
            center.Add((aSamples[i] + bSamples[(bestOffset + bestDir * i + SampleCount * 2) % SampleCount]) * 0.5f);
        return center;
    }
    private static List<Vector3> ToWorld(List<int> indices, Vector3[] verts, Transform tr)
    {
        List<Vector3> result = new List<Vector3>(indices.Count);
        foreach (int index in indices) result.Add(tr.TransformPoint(verts[index]));
        return result;
    }
    private static float PathLength(List<Vector3> p)
    {
        float total = 0f;
        for (int i = 0; i < p.Count; i++) total += Vector3.Distance(p[i], p[(i + 1) % p.Count]);
        return total;
    }
    private static Vector3[] Resample(List<Vector3> p, int count)
    {
        float[] cumulative = new float[p.Count + 1];
        for (int i = 0; i < p.Count; i++) cumulative[i + 1] = cumulative[i] + Vector3.Distance(p[i], p[(i + 1) % p.Count]);
        Vector3[] result = new Vector3[count];
        int seg = 0;
        for (int i = 0; i < count; i++)
        {
            float d = cumulative[p.Count] * i / count;
            while (seg < p.Count - 1 && cumulative[seg + 1] < d) seg++;
            float size = cumulative[seg + 1] - cumulative[seg];
            result[i] = Vector3.Lerp(p[seg], p[(seg + 1) % p.Count], size > 0.000001f ? (d - cumulative[seg]) / size : 0f);
        }
        return result;
    }
    // Closed centripetal Catmull-Rom: smooth curves through the original track vertices.
    // The dense arc-length lookup is built ONCE at Start, not each frame.
    private static Vector3 CurvePoint(List<Vector3> p, int segment, float u)
    {
        int n = p.Count;
        Vector3 a = p[(segment + n - 1) % n];
        Vector3 b = p[segment % n];
        Vector3 c = p[(segment + 1) % n];
        Vector3 d = p[(segment + 2) % n];
        // Nonuniform knots reduce overshoot around unevenly spaced vertices.
        float t0 = 0f;
        float t1 = t0 + Mathf.Max(0.0001f, Mathf.Sqrt(Vector3.Distance(a, b)));
        float t2 = t1 + Mathf.Max(0.0001f, Mathf.Sqrt(Vector3.Distance(b, c)));
        float t3 = t2 + Mathf.Max(0.0001f, Mathf.Sqrt(Vector3.Distance(c, d)));
        float t = Mathf.Lerp(t1, t2, u);
        Vector3 a1 = Vector3.LerpUnclamped(a, b, (t - t0) / (t1 - t0));
        Vector3 a2 = Vector3.LerpUnclamped(b, c, (t - t1) / (t2 - t1));
        Vector3 a3 = Vector3.LerpUnclamped(c, d, (t - t2) / (t3 - t2));
        Vector3 b1 = Vector3.LerpUnclamped(a1, a2, (t - t0) / (t2 - t0));
        Vector3 b2 = Vector3.LerpUnclamped(a2, a3, (t - t1) / (t3 - t1));
        return Vector3.LerpUnclamped(b1, b2, (t - t1) / (t2 - t1));
    }

    private static Track MakeTrack(List<Vector3> input, int samples, int smoothing)
    {
        int count = Mathf.Clamp(samples, 8, 1024);
        // First create uniformly spaced points, avoiding uneven mesh vertex density.
        Vector3[] even = Resample(input, count);
        // Circular low-pass smoothing removes vertex-by-vertex zigzags. The blend is
        // intentionally moderate to preserve the actual track shape and curvature.
        if (smoothing > 0)
        {
            Vector3[] buffer = new Vector3[count];
            for (int pass = 0; pass < smoothing; pass++)
            {
                for (int i = 0; i < count; i++)
                {
                    Vector3 prev = even[(i + count - 1) % count];
                    Vector3 next = even[(i + 1) % count];
                    buffer[i] = even[i] * 0.5f + (prev + next) * 0.25f;
                }
                Vector3[] temp = even; even = buffer; buffer = temp;
            }
        }
        // Evaluate smooth cubic spline and rebuild an arc-length table, so train
        // speed stays constant regardless of mesh vertex spacing.
        List<Vector3> controls = new List<Vector3>(even);
        Vector3[] dense = new Vector3[count * 2];
        for (int i = 0; i < dense.Length; i++)
        {
            float f = (float)i * count / dense.Length;
            int segment = Mathf.FloorToInt(f);
            dense[i] = CurvePoint(controls, segment, f - segment);
        }
        Track track = new Track();
        track.points = Resample(new List<Vector3>(dense), count);
        track.cumulative = new float[count + 1];
        for (int i = 0; i < count; i++)
            track.cumulative[i + 1] = track.cumulative[i] +
                Vector3.Distance(track.points[i], track.points[(i + 1) % count]);
        track.length = track.cumulative[count];
        return track.length > 0.01f ? track : null;
    }

    private static Vector3 SamplePosition(Track t, float distance)
    {
        float d = Mathf.Repeat(distance, t.length);
        int lo = 0, hi = t.points.Length - 1;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (t.cumulative[mid + 1] <= d) lo = mid + 1;
            else hi = mid;
        }
        float size = t.cumulative[lo + 1] - t.cumulative[lo];
        float alpha = size > 0.000001f ? (d - t.cumulative[lo]) / size : 0f;
        return Vector3.LerpUnclamped(t.points[lo], t.points[(lo + 1) % t.points.Length], alpha);
    }

    private static void Evaluate(Track t, float distance, out Vector3 pos, out Vector3 tangent)
    {
        pos = SamplePosition(t, distance);
        // Look ahead/behind instead of using a single edge direction. This removes
        // sudden rotation snaps at vertices and lets each bogie rotate independently.
        float look = Mathf.Clamp(t.length / t.points.Length * 1.5f, 0.02f, 2f);
        tangent = SamplePosition(t, distance + look) - SamplePosition(t, distance - look);
        tangent = tangent.sqrMagnitude > 0.0000001f ? tangent.normalized : Vector3.forward;
    }

    private void PlaceCars(Track t)
    {
        float behind = 0f;
        for (int i = 0; i < cars.Count; i++)
        {
            Car car = cars[i];
            if (i > 0) behind += Mathf.Max(0f, car.distanceBehindPrevious);
            Transform tr = t.instances[i];
            if (!tr) continue;
            float direction = reverse ? -1f : 1f;
            // Z offset is ARC DISTANCE along the path, never a straight local-Z translation.
            // This keeps each car on its own sampled centerline even through tight bends.
            float distance = direction * (t.distance - behind + car.positionOffset.z);
            Vector3 pos = SamplePosition(t, distance);
            // Rotation from two separated positions, not from a single polygon edge.
            // Each car is evaluated independently at its own distance.
            float halfWheelbase = Mathf.Max(0.025f, car.wheelbase * 0.5f);
            Vector3 front = SamplePosition(t, distance + direction * halfWheelbase);
            Vector3 rear = SamplePosition(t, distance - direction * halfWheelbase);
            Vector3 forward = front - rear;
            if (forward.sqrMagnitude < 0.000001f)
            {
                Vector3 tangent;
                Evaluate(t, distance, out pos, out tangent);
                forward = tangent * direction;
            }
            Quaternion targetRotation = Quaternion.LookRotation(forward.normalized, Vector3.up) * Quaternion.Euler(car.rotationOffset);
            // Optional visual rotation damping; position remains locked to the path.
            Quaternion rotation = targetRotation;
            if (Application.isPlaying && running)
            {
                float responsiveness = Mathf.Lerp(1000f, 5f, RotationDamping);
                rotation = Quaternion.Slerp(tr.rotation, targetRotation, 1f - Mathf.Exp(-responsiveness * Time.deltaTime));
            }
            // X/Y are spatial offsets; Z has already been applied to the sampled arc distance.
            // Lateral X follows the horizontal track right vector, avoiding pitch-related drift.
            Vector3 right = Vector3.Cross(Vector3.up, forward.normalized);
            if (right.sqrMagnitude < 0.000001f) right = targetRotation * Vector3.right;
            else right.Normalize();
            Vector3 finalPosition = pos + right * car.positionOffset.x + Vector3.up * car.positionOffset.y;
            tr.SetPositionAndRotation(finalPosition, rotation);
        }
    }
    private void Update()
    {
        if (!running || speed <= 0f) return;
        float step = speed * Time.deltaTime;
        foreach (Track t in tracks)
        {
            t.distance = Mathf.Repeat(t.distance + step, t.length);
            PlaceCars(t);
        }
    }
}
