using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pre-instantiates the deer herd pool at Start().
/// Kept active in scene with renderers disabled so no GameObject.SetActive hitch occurs at runtime!
/// </summary>
public class DeerStampedePoolManager : MonoBehaviour
{
    private List<DeerStampedeRunner> _pool = new List<DeerStampedeRunner>();
    private DeerStampedeRunner _tracker;
    private GameObject _trackerGo;

    public void InitializePool(int count, GameObject stagPrefab, GameObject doePrefab, RuntimeAnimatorController stagCtrl, RuntimeAnimatorController doeCtrl)
    {
        ClearPool();

        GameObject defaultPrefab = stagPrefab != null ? stagPrefab : doePrefab;
        if (defaultPrefab == null) return;

        for (int i = 0; i < count; i++)
        {
            bool isDoe = (doePrefab != null && (i % 2 == 1));
            GameObject prefabToUse = isDoe ? doePrefab : defaultPrefab;
            RuntimeAnimatorController ctrlToUse = isDoe ? (doeCtrl != null ? doeCtrl : stagCtrl) : stagCtrl;

            // Instantiate under this pool manager as parent to keep Hierarchy 100% neat & organized
            GameObject animalGo = Instantiate(prefabToUse, new Vector3(0, -300f, 0), Quaternion.identity, transform);
            animalGo.name = $"Pooled_{prefabToUse.name}_{i + 1}";

            MonoBehaviour cageScript = animalGo.GetComponent("AnimalCageWanderer") as MonoBehaviour;
            if (cageScript != null) Destroy(cageScript);

            // Strip any colliders and rigidbodies
            Collider[] allCols = animalGo.GetComponentsInChildren<Collider>(true);
            foreach (var col in allCols)
            {
                Destroy(col);
            }
            Rigidbody rb = animalGo.GetComponent<Rigidbody>();
            if (rb != null) Destroy(rb);

            DeerStampedeRunner runner = animalGo.GetComponent<DeerStampedeRunner>();
            if (runner == null) runner = animalGo.AddComponent<DeerStampedeRunner>();

            runner.Prewarm(ctrlToUse);
            // Hide renderers so GPU doesn't draw them until triggered
            runner.SetVisible(false);
            _pool.Add(runner);
        }

        // Prewarm tracker under parent
        _trackerGo = new GameObject("HerdCameraTracker");
        _trackerGo.transform.SetParent(transform);
        _tracker = _trackerGo.AddComponent<DeerStampedeRunner>();
        _tracker.Prewarm(null);
        _tracker.SetVisible(false);
    }

    public Transform ActivateHerd(Vector3 startCenter, Vector3 endCenter, float spreadWidth, float formationDepth, float minSpeed, float maxSpeed, out DeerStampedeRunner trackerOut)
    {
        startCenter = DeerStampedeRunner.SnapToGround(startCenter);
        endCenter = DeerStampedeRunner.SnapToGround(endCenter);

        Vector3 crossDir = (endCenter - startCenter);
        crossDir.y = 0;
        crossDir.Normalize();

        Vector3 spreadDir = Vector3.Cross(crossDir, Vector3.up).normalized;

        _trackerGo.transform.position = startCenter;
        _tracker.Initialize(startCenter, endCenter, (minSpeed + maxSpeed) * 0.5f, 1.0f, null, null);
        trackerOut = _tracker;

        int total = _pool.Count;
        for (int i = 0; i < total; i++)
        {
            DeerStampedeRunner runner = _pool[i];
            if (runner == null) continue;

            float depthRatio = (float)i / Mathf.Max(1, total - 1);
            float lateralOffset = Random.Range(-spreadWidth * 0.5f, spreadWidth * 0.5f);
            float depthOffset = (depthRatio * formationDepth) + Random.Range(-1.5f, 1.5f);
            float speed = Random.Range(minSpeed, maxSpeed);
            float animSpeedMultiplier = speed / 8.5f;

            Vector3 stagStart = startCenter + (spreadDir * lateralOffset) - (crossDir * depthOffset);
            Vector3 stagEnd = endCenter + (spreadDir * (lateralOffset * 0.7f)) + (crossDir * 8f);

            stagStart = DeerStampedeRunner.SnapToGround(stagStart);
            stagEnd = DeerStampedeRunner.SnapToGround(stagEnd);

            runner.Initialize(stagStart, stagEnd, speed, animSpeedMultiplier, null, null);
        }

        return _trackerGo.transform;
    }

    public void StartHerdMovement()
    {
        if (_tracker != null) _tracker.SignalStartRunning();
        foreach (var runner in _pool)
        {
            if (runner != null) runner.SignalStartRunning();
        }
    }

    public void DeactivateAll()
    {
        if (_tracker != null) _tracker.SetVisible(false);
        foreach (var runner in _pool)
        {
            if (runner != null) runner.SetVisible(false);
        }
    }

    private void ClearPool()
    {
        foreach (var r in _pool)
        {
            if (r != null) Destroy(r.gameObject);
        }
        _pool.Clear();
        if (_trackerGo != null) Destroy(_trackerGo);
    }

    private void OnDestroy()
    {
        ClearPool();
    }
}
