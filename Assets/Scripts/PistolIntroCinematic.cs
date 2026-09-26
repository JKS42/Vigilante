using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// First pistol drop: pause, show the gun, wait for the pickup, then reveal the next wave one enemy at a time.
/// </summary>
public class PistolIntroCinematic : MonoBehaviour
{
    struct RendererState
    {
        public Renderer renderer;
        public bool enabled;
    }

    public static bool IsRunning { get; private set; }

    static PistolIntroCinematic instance;
    static bool playedDrop;
    static bool holdingFirstWave;

    WeaponPickup watchedPickup;
    bool presentingDrop;
    readonly List<RendererState> hiddenRenderers = new List<RendererState>();
    readonly List<Animator> unscaledAnimators = new List<Animator>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void HookSceneLoads()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        playedDrop = false;
        holdingFirstWave = false;
        IsRunning = false;
        instance = null;
        if (Time.timeScale <= 0f)
            Time.timeScale = 1f;
    }

    public static void HoldFirstPistolWave()
    {
        if (playedDrop || holdingFirstWave)
            return;

        holdingFirstWave = true;
        if (WaveManager.Instance != null)
            WaveManager.Instance.HoldFollowingWave();
    }

    public static void NotifyDropped(WeaponPickup pickup, int weaponIndex)
    {
        if (playedDrop || pickup == null || weaponIndex != 1)
            return;

        playedDrop = true;
        if (WaveManager.Instance != null)
            WaveManager.Instance.HoldFollowingWave();

        GameObject host = new GameObject("PistolIntroCinematic");
        instance = host.AddComponent<PistolIntroCinematic>();
        instance.watchedPickup = pickup;
        instance.StartCoroutine(instance.DropRoutine(pickup.transform));
    }

    public static void NotifyCollected(WeaponPickup pickup)
    {
        if (instance == null || pickup == null || pickup != instance.watchedPickup)
            return;

        instance.watchedPickup = null;
        if (WaveManager.Instance != null)
            WaveManager.Instance.UseDistantSpawns();
        instance.StartCoroutine(instance.WaveRoutine());
    }

    public static void NotifyLost(WeaponPickup pickup)
    {
        if (instance == null || pickup == null || pickup != instance.watchedPickup)
            return;

        instance.watchedPickup = null;
        if (WaveManager.Instance != null)
            WaveManager.Instance.ReleaseFollowingWave();
    }

    IEnumerator DropRoutine(Transform pistol)
    {
        presentingDrop = true;
        if (!TryBeginCutscene(out Transform rig, out Transform parent, out Vector3 localPos, out Quaternion localRot, out Vector3 homePos, out Quaternion homeRot))
        {
            presentingDrop = false;
            yield break;
        }

        Vector3 focus = pistol != null ? pistol.position : homePos;
        Frame(focus, homePos, 2.3f, 1.25f, 0.25f, out Vector3 shotPos, out Quaternion shotRot);
        yield return MoveCamera(rig, shotPos, shotRot, 1.15f);
        yield return new WaitForSecondsRealtime(2.8f);
        yield return MoveCamera(rig, homePos, homeRot, 0.85f);
        EndCutscene(rig, parent, localPos, localRot);
        presentingDrop = false;
    }

    IEnumerator WaveRoutine()
    {
        while (presentingDrop)
            yield return null;

        WaveManager waves = WaveManager.Instance;
        if (waves == null)
            yield break;

        for (int i = 0; i < 3; i++)
            yield return null;

        if (!TryBeginCutscene(out Transform rig, out Transform parent, out Vector3 localPos, out Quaternion localRot, out Vector3 homePos, out Quaternion homeRot))
        {
            waves.ReleaseFollowingWave();
            yield break;
        }

        waves.BeginScriptedFollowingWave();
        TutorialPrompt.ShowPinned("New wave. Hostiles are spawning in. Clear them before the next wave.");
        int failedSpawns = 0;

        while (waves.HasScriptedSpawnsRemaining && failedSpawns < 4)
        {
            if (!waves.PreviewScriptedSpawn(out Vector3 spot))
                break;

            SnapToSpawn(rig, spot, homePos);

            if (!waves.CommitScriptedSpawn(out Vector3 spawnedAt))
            {
                failedSpawns++;
                continue;
            }

            KeepEnemyVisibleDuringPause(spawnedAt);
            yield return new WaitForSecondsRealtime(5.5f);
            break;
        }

        waves.EndScriptedSpawn();
        TutorialPrompt.HidePinned();
        rig.SetPositionAndRotation(homePos, homeRot);
        EndCutscene(rig, parent, localPos, localRot);
    }

    static void SnapToSpawn(Transform rig, Vector3 spot, Vector3 homePos)
    {
        if (rig == null)
            return;

        Frame(spot, homePos, 5.2f, 2.15f, 1.15f, out Vector3 shotPos, out Quaternion shotRot);
        rig.SetPositionAndRotation(shotPos, shotRot);
    }

    static int ClosestPathIndex(List<Transform> path, int start, Vector3 spot)
    {
        int best = Mathf.Clamp(start, 0, path.Count - 1);
        float bestDistance = float.PositiveInfinity;
        for (int i = best; i < path.Count; i++)
        {
            if (path[i] == null)
                continue;

            float distance = HorizontalDistance(path[i].position, spot);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = i;
        }

        return best;
    }

    static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    static Quaternion LookAt(Vector3 from, Vector3 target)
    {
        Vector3 dir = target - from;
        if (dir.sqrMagnitude < 0.001f)
            return Quaternion.identity;
        return Quaternion.LookRotation(dir, Vector3.up);
    }

    static bool HasAuthoredAim(Transform point)
    {
        return point != null && Quaternion.Angle(point.rotation, Quaternion.identity) > 2f;
    }

    static List<Transform> CollectRevealPath()
    {
        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        Transform root = null;
        for (int i = 0; i < all.Length; i++)
        {
            Transform candidate = all[i];
            if (candidate == null || candidate.childCount == 0 || !IsPathRootName(candidate.name))
                continue;
            if (root == null || candidate.childCount > root.childCount)
                root = candidate;
        }

        var points = new List<Transform>();
        if (root != null)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (IsUsablePoint(child))
                    points.Add(child);
            }

            if (points.Count > 0)
                return points;
        }

        root = FindFolderOfEmpties(all);
        if (root != null)
        {
            for (int i = 0; i < root.childCount; i++)
                points.Add(root.GetChild(i));
            if (points.Count > 0)
                return points;
        }

        for (int i = 0; i < all.Length; i++)
        {
            Transform candidate = all[i];
            if (!IsLoosePathPoint(candidate))
                continue;
            points.Add(candidate);
        }

        points.Sort(ComparePointOrder);
        return points;
    }

    static Transform FindFolderOfEmpties(Transform[] all)
    {
        Transform best = null;
        int bestCount = 1;
        for (int i = 0; i < all.Length; i++)
        {
            Transform candidate = all[i];
            if (candidate == null || candidate.childCount < 2)
                continue;
            if (candidate.GetComponentInParent<PlayerMovement>() != null)
                continue;
            if (candidate.GetComponentInParent<Canvas>() != null)
                continue;

            bool onlyEmpties = true;
            for (int c = 0; c < candidate.childCount; c++)
            {
                if (!IsBareEmpty(candidate.GetChild(c)))
                {
                    onlyEmpties = false;
                    break;
                }
            }

            if (!onlyEmpties || candidate.childCount <= bestCount)
                continue;

            best = candidate;
            bestCount = candidate.childCount;
        }

        return best;
    }

    static bool IsBareEmpty(Transform point)
    {
        if (point == null)
            return false;
        return point.GetComponents<Component>().Length == 1;
    }

    static bool IsPathRootName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        string n = name.ToLowerInvariant().Replace("_", " ");
        return n.Contains("camera path")
            || n.Contains("cam path")
            || n.Contains("pan path")
            || n.Contains("camera pan")
            || n.Contains("camerapath")
            || n.Contains("campath")
            || n.Contains("panpath")
            || n == "path";
    }

    static bool IsLoosePathPoint(Transform point)
    {
        if (!IsUsablePoint(point))
            return false;
        if (point.GetComponentInParent<PlayerMovement>() != null)
            return false;

        string name = point.name;
        if (name.StartsWith("GameObject"))
        {
            if (point.parent != null && !point.parent.name.StartsWith("GameObject") && !IsPathRootName(point.parent.name))
                return false;
            return point.GetComponents<Component>().Length <= 2;
        }

        return NameLooksLikePathPoint(name);
    }

    static bool NameLooksLikePathPoint(string name)
    {
        string n = name.ToLowerInvariant().Replace("_", " ");
        return n.Contains("camera path")
            || n.Contains("cam path")
            || n.Contains("pan path")
            || n.Contains("path point")
            || n.StartsWith("path ")
            || n.StartsWith("pan ")
            || n.StartsWith("cam ")
            || n.StartsWith("point ")
            || n.StartsWith("empty");
    }

    static bool IsUsablePoint(Transform point)
    {
        if (point == null)
            return false;
        if (point.GetComponent<Camera>() != null || point.GetComponent<Light>() != null)
            return false;
        if (point.GetComponent<PlayerMovement>() != null)
            return false;
        return true;
    }

    static int ComparePointOrder(Transform a, Transform b)
    {
        int number = NumberInName(a.name).CompareTo(NumberInName(b.name));
        if (number != 0)
            return number;
        return string.Compare(a.name, b.name, System.StringComparison.Ordinal);
    }

    static int NumberInName(string name)
    {
        int value = 0;
        int place = 1;
        bool found = false;
        for (int i = name.Length - 1; i >= 0; i--)
        {
            if (!char.IsDigit(name[i]))
                break;
            value += (name[i] - '0') * place;
            place *= 10;
            found = true;
        }

        return found ? value : 0;
    }

    bool TryBeginCutscene(out Transform rig, out Transform parent, out Vector3 localPos, out Quaternion localRot, out Vector3 homePos, out Quaternion homeRot)
    {
        rig = null;
        parent = null;
        localPos = Vector3.zero;
        localRot = Quaternion.identity;
        homePos = Vector3.zero;
        homeRot = Quaternion.identity;

        rig = ResolveCameraRig();
        if (rig == null)
            return false;

        IsRunning = true;
        Time.timeScale = 0f;
        HidePlayerAndWeapons();

        parent = rig.parent;
        localPos = rig.localPosition;
        localRot = rig.localRotation;
        homePos = rig.position;
        homeRot = rig.rotation;
        rig.SetParent(null, true);
        return true;
    }

    void EndCutscene(Transform rig, Transform parent, Vector3 localPos, Quaternion localRot)
    {
        RestoreAnimatorModes();

        if (rig != null)
        {
            rig.SetParent(parent, false);
            rig.localPosition = localPos;
            rig.localRotation = localRot;
        }

        ShowPlayerAndWeapons();
        IsRunning = false;
        Time.timeScale = 1f;
    }

    public static Transform ResolveCameraRig()
    {
        MouseMovement look = Object.FindFirstObjectByType<MouseMovement>();
        Transform rig = look != null ? look.transform : null;
        if (rig != null && rig.GetComponent<PlayerMovement>() != null)
            rig = null;

        if (rig == null && Camera.main != null)
            rig = Camera.main.transform;

        return rig;
    }

    public static void Frame(Vector3 focus, Vector3 preferFrom, float distance, float height, float lookHeight, out Vector3 position, out Quaternion rotation)
    {
        Vector3 lookPoint = focus + Vector3.up * lookHeight;
        Vector3 flat = preferFrom - focus;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.2f)
            flat = Vector3.back;
        flat.Normalize();

        float[] distances = { distance, distance * 0.72f, distance * 1.25f };
        float[] angles = { 0f, 24f, -24f, 48f, -48f, 72f, -72f, 100f, -100f, 140f, -140f, 180f };
        for (int d = 0; d < distances.Length; d++)
        {
            for (int a = 0; a < angles.Length; a++)
            {
                Vector3 offset = Quaternion.Euler(0f, angles[a], 0f) * flat * distances[d];
                Vector3 candidate = focus + offset + Vector3.up * height;
                if (!CanSee(candidate, lookPoint) || !IsOpen(candidate))
                    continue;

                position = candidate;
                rotation = Quaternion.LookRotation(lookPoint - candidate, Vector3.up);
                return;
            }
        }

        position = focus + flat * distance + Vector3.up * height;
        if (!SegmentClear(preferFrom, position, out Vector3 stop))
            position = stop;
        rotation = Quaternion.LookRotation(lookPoint - position, Vector3.up);
    }

    static IEnumerator MoveCamera(Transform rig, Vector3 toPos, Quaternion toRot, float duration)
    {
        if (rig == null)
            yield break;

        List<Vector3> path = new List<Vector3> { rig.position };
        AppendClearPath(rig.position, toPos, path, 4);

        float total = 0f;
        for (int i = 1; i < path.Count; i++)
            total += Vector3.Distance(path[i - 1], path[i]);

        if (total < 0.05f)
        {
            rig.SetPositionAndRotation(path[path.Count - 1], toRot);
            yield break;
        }

        Quaternion fromRot = rig.rotation;
        float travelled = 0f;
        duration = Mathf.Max(0.05f, duration);
        float speed = total / duration;
        int segment = 1;
        float segmentStart = 0f;

        while (segment < path.Count)
        {
            float segmentLength = Vector3.Distance(path[segment - 1], path[segment]);
            travelled += speed * Time.unscaledDeltaTime;
            while (segment < path.Count - 1 && travelled > segmentStart + segmentLength)
            {
                segmentStart += segmentLength;
                segment++;
                segmentLength = Vector3.Distance(path[segment - 1], path[segment]);
            }

            float along = segmentLength <= 0.001f ? 1f : Mathf.Clamp01((travelled - segmentStart) / segmentLength);
            Vector3 pos = Vector3.Lerp(path[segment - 1], path[segment], along);
            float turn = Mathf.Clamp01(travelled / total);
            rig.SetPositionAndRotation(pos, Quaternion.Slerp(fromRot, toRot, Mathf.SmoothStep(0f, 1f, turn)));

            if (travelled >= total)
                break;

            yield return null;
        }

        rig.SetPositionAndRotation(path[path.Count - 1], toRot);
    }

    const float CameraRadius = 0.32f;

    static void AppendClearPath(Vector3 from, Vector3 to, List<Vector3> path, int depth)
    {
        if (SegmentClear(from, to, out Vector3 stop))
        {
            path.Add(to);
            return;
        }

        if (depth > 0 && TryDetour(from, to, out Vector3 via))
        {
            AppendClearPath(from, via, path, depth - 1);
            AppendClearPath(path[path.Count - 1], to, path, depth - 1);
            return;
        }

        path.Add(stop);
    }

    static bool TryDetour(Vector3 from, Vector3 to, out Vector3 via)
    {
        via = default;
        Vector3 delta = to - from;
        float dist = delta.magnitude;
        if (dist < 0.6f)
            return false;

        Vector3 dir = delta / dist;
        if (!TryFirstObstacle(from, dir, dist, CameraRadius, out RaycastHit hit))
            return false;

        Vector3 side = Vector3.Cross(Vector3.up, dir);
        if (side.sqrMagnitude < 0.001f)
            side = Vector3.right;
        side.Normalize();

        float along = Mathf.Clamp(hit.distance, 0.45f, dist - 0.45f);
        Vector3 basePoint = from + dir * along;
        float height = Mathf.Lerp(from.y, to.y, along / dist);
        float[] offsets = { 1.5f, 2.6f, 3.8f };

        bool found = false;
        int bestScore = int.MaxValue;
        for (int i = 0; i < offsets.Length; i++)
        {
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Vector3 candidate = basePoint + side * (sign * offsets[i]);
                candidate.y = height;
                if (!IsOpen(candidate))
                    continue;

                int score = (SegmentClear(from, candidate, out _) ? 0 : 2) + (SegmentClear(candidate, to, out _) ? 0 : 2);
                if (score >= bestScore)
                    continue;

                via = candidate;
                bestScore = score;
                found = true;
                if (score == 0)
                    return true;
            }
        }

        return found;
    }

    static bool CanSee(Vector3 from, Vector3 lookPoint)
    {
        return SegmentClear(from, lookPoint, out _);
    }

    static bool IsOpen(Vector3 point)
    {
        Collider[] hits = Physics.OverlapSphere(point, CameraRadius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            if (IsSolidObstacle(hits[i]))
                return false;
        }

        return true;
    }

    static bool SegmentClear(Vector3 from, Vector3 to, out Vector3 stop)
    {
        stop = to;
        Vector3 delta = to - from;
        float dist = delta.magnitude;
        if (dist < 0.05f)
            return true;

        Vector3 dir = delta / dist;
        if (!TryFirstObstacle(from, dir, dist, CameraRadius, out RaycastHit hit))
            return true;

        stop = from + dir * Mathf.Max(0.2f, hit.distance - CameraRadius);
        return false;
    }

    static bool TryFirstObstacle(Vector3 origin, Vector3 dir, float dist, float radius, out RaycastHit nearest)
    {
        nearest = default;
        RaycastHit[] hits = Physics.SphereCastAll(origin, radius, dir, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float best = float.PositiveInfinity;
        bool found = false;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (!IsSolidObstacle(col))
                continue;
            if (hits[i].distance <= 0.02f || hits[i].distance >= best)
                continue;

            best = hits[i].distance;
            nearest = hits[i];
            found = true;
        }

        return found;
    }

    static bool IsSolidObstacle(Collider col)
    {
        if (col == null || col.isTrigger)
            return false;
        if (col.GetComponentInParent<PlayerMovement>() != null)
            return false;
        if (col.GetComponentInParent<EnemyAI>() != null)
            return false;
        if (col.GetComponentInParent<WeaponPickup>() != null)
            return false;
        if (col.GetComponentInParent<MedKitPickup>() != null)
            return false;
        return true;
    }

    void HidePlayerAndWeapons()
    {
        hiddenRenderers.Clear();
        PlayerMovement player = Object.FindFirstObjectByType<PlayerMovement>();
        if (player == null)
            return;

        Renderer[] renderers = player.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            hiddenRenderers.Add(new RendererState { renderer = renderer, enabled = renderer.enabled });
            renderer.enabled = false;
        }
    }

    void ShowPlayerAndWeapons()
    {
        for (int i = 0; i < hiddenRenderers.Count; i++)
        {
            RendererState state = hiddenRenderers[i];
            if (state.renderer != null)
                state.renderer.enabled = state.enabled;
        }

        hiddenRenderers.Clear();
    }

    void KeepEnemyVisibleDuringPause(Vector3 near)
    {
        EnemyAI[] enemies = Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyAI enemy = enemies[i];
            if (enemy == null || enemy.IsDead)
                continue;
            if ((enemy.transform.position - near).sqrMagnitude > 6f)
                continue;

            Animator animator = enemy.GetComponentInChildren<Animator>();
            if (animator == null || animator.updateMode == AnimatorUpdateMode.UnscaledTime)
                continue;

            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            unscaledAnimators.Add(animator);
        }
    }

    void RestoreAnimatorModes()
    {
        for (int i = 0; i < unscaledAnimators.Count; i++)
        {
            if (unscaledAnimators[i] != null)
                unscaledAnimators[i].updateMode = AnimatorUpdateMode.Normal;
        }

        unscaledAnimators.Clear();
    }
}
