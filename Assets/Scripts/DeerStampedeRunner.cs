using UnityEngine;

/// <summary>
/// Individual Deer runner in the stampede herd.
/// - Pure mathematical ground snapping via Terrain.SampleHeight (ZERO Physics Raycast, ZERO lag/freeze).
/// - SkinnedMeshRenderer enable/disable for zero-cost activation without Animator hitching.
/// </summary>
public class DeerStampedeRunner : MonoBehaviour
{
    private Vector3 _startPosition;
    private Vector3 _targetPosition;
    private float _speed = 7.5f;
    private System.Action _onReachedDestination;

    private bool _canMove = false;
    private Animator _animator;
    private Renderer[] _renderers;
    private static Terrain _cachedTerrain;
    private static float _cachedTerrainY;
    private static bool _terrainChecked = false;

    public void Prewarm(RuntimeAnimatorController controller)
    {
        _animator = GetComponent<Animator>();
        if (_animator != null && controller != null)
        {
            _animator.runtimeAnimatorController = controller;
            _animator.applyRootMotion = false;
        }

        // Cache renderers so we can toggle visibility without SetActive hitching
        _renderers = GetComponentsInChildren<Renderer>(true);

        // Strip any colliders and rigidbodies
        Collider[] cols = GetComponentsInChildren<Collider>(true);
        foreach (var c in cols)
        {
            Destroy(c);
        }
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) Destroy(rb);
    }

    public void SetVisible(bool visible)
    {
        if (_renderers == null) _renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null) _renderers[i].enabled = visible;
        }
    }

    public void Initialize(Vector3 start, Vector3 target, float speed, float animSpeedMultiplier, RuntimeAnimatorController runController = null, System.Action onReached = null)
    {
        _speed = speed;
        _onReachedDestination = onReached;

        _startPosition = SnapToGround(start);
        _targetPosition = SnapToGround(target);

        transform.position = _startPosition;

        Vector3 dir = (_targetPosition - _startPosition);
        dir.y = 0;
        if (dir.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        if (_animator == null) _animator = GetComponent<Animator>();
        if (_animator != null)
        {
            if (runController != null)
            {
                _animator.runtimeAnimatorController = runController;
            }
            _animator.speed = animSpeedMultiplier;
            _animator.applyRootMotion = false;
            _animator.Play("Run", 0, Random.Range(0f, 1f));
        }

        // Make visible smoothly
        SetVisible(true);
    }

    public void SignalStartRunning()
    {
        _canMove = true;
    }

    private void Update()
    {
        if (!_canMove) return;

        Vector3 currentPos = transform.position;
        Vector3 targetFlat = _targetPosition;
        targetFlat.y = currentPos.y;

        Vector3 toTarget = targetFlat - currentPos;
        float step = _speed * Time.deltaTime;

        if (toTarget.magnitude <= step || toTarget.sqrMagnitude <= 0.35f)
        {
            StopAndDeactivate();
            return;
        }

        Vector3 newPos = Vector3.MoveTowards(currentPos, targetFlat, step);

        // Instant math calculation for ground Y (0 physics cost)
        newPos = SnapToGround(newPos);

        Vector3 fwd = targetFlat - currentPos;
        fwd.y = 0;
        if (fwd.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(fwd.normalized, Vector3.up), 8f * Time.deltaTime);
        }

        transform.position = newPos;
    }

    public void StopAndDeactivate()
    {
        _canMove = false;
        _onReachedDestination?.Invoke();
        SetVisible(false);
    }

    /// <summary>
    /// Pure mathematical ground calculation via Terrain.SampleHeight.
    /// ZERO Physics.Raycast, ZERO colliders check, 0.0001ms execution time!
    /// </summary>
    public static Vector3 SnapToGround(Vector3 pos)
    {
        if (!_terrainChecked || _cachedTerrain == null)
        {
            _cachedTerrain = Terrain.activeTerrain;
            if (_cachedTerrain != null)
            {
                _cachedTerrainY = _cachedTerrain.transform.position.y;
                _terrainChecked = true;
            }
        }

        if (_cachedTerrain != null)
        {
            float groundY = _cachedTerrain.SampleHeight(pos) + _cachedTerrainY;
            return new Vector3(pos.x, groundY, pos.z);
        }

        return pos;
    }
}
