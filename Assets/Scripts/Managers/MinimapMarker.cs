using UnityEngine;

/// <summary>
/// Shows a distinct colored icon on the minimap for each object type.
/// Icons float HIGH above objects so they're visible even inside buildings.
///
/// Shapes:
///   Player    = white forward-pointing arrow  (rotates with player)
///   NPC       = red circle  (turns green when happy)
///   Boss      = purple diamond
///   Objective = yellow pulsing circle with outline
/// </summary>
public class MinimapMarker : MonoBehaviour
{
    public enum MarkerType { Player, NPC, Boss, Objective }

    [Header("Marker Settings")]
    public MarkerType markerType = MarkerType.NPC;

    [Tooltip("Height above the object. Keep this above your tallest building (default 15).")]
    public float heightOffset = 15f;

    [Tooltip("Size of the icon in world units.")]
    public float iconSize = 2.5f;

    // Colours
    static readonly Color ColPlayer      = Color.black;
    static readonly Color ColNPCUnhappy  = new Color(1f, 0.25f, 0.25f);
    static readonly Color ColNPCHappy    = new Color(0.2f, 1f, 0.35f);
    static readonly Color ColBoss        = new Color(0.75f, 0.1f, 1f);
    static readonly Color ColObjective   = new Color(1f, 0.9f, 0f);
    static readonly Color ColOutline     = Color.black;

    private GameObject iconRoot;
    private MeshRenderer mainRenderer;
    private UnhappyPerson trackedPerson;
    private float pulseTimer;

    void Start()
    {
        CreateIcon();
        if (markerType == MarkerType.NPC)
            trackedPerson = GetComponentInParent<UnhappyPerson>();
    }

    void CreateIcon()
    {
        iconRoot = new GameObject($"MinimapIcon_{markerType}");

        int layer = LayerMask.NameToLayer("Minimap");
        if (layer < 0) { Debug.LogWarning("[MinimapMarker] Add a layer named 'Minimap' in Project Settings > Tags and Layers."); layer = 0; }

        switch (markerType)
        {
            case MarkerType.Player:    BuildArrow(layer);   break;
            case MarkerType.NPC:       BuildCircle(layer, ColNPCUnhappy, 1.0f); break;
            case MarkerType.Boss:      BuildDiamond(layer); break;
            case MarkerType.Objective: BuildCircle(layer, ColObjective, 1.2f); break;
        }
    }

    // ── Arrow (Player) ────────────────────────────────────────────────────────
    void BuildArrow(int layer)
    {
        AddMesh(iconRoot, layer, MakeDoubleSided(CreateArrowMesh(1.3f)), ColOutline, -0.01f);
        GameObject main = AddMesh(iconRoot, layer, MakeDoubleSided(CreateArrowMesh(1f)), ColPlayer, 0f);
        mainRenderer = main.GetComponent<MeshRenderer>();
    }

    static Mesh CreateArrowMesh(float scale)
    {
        // Simple forward-pointing arrow seen from above
        Vector3[] verts = {
            new Vector3( 0f,    0f,  0.6f) * scale,   // tip
            new Vector3(-0.4f,  0f, -0.2f) * scale,   // left wing
            new Vector3(-0.15f, 0f,  0f  ) * scale,   // inner left
            new Vector3( 0.15f, 0f,  0f  ) * scale,   // inner right
            new Vector3( 0.4f,  0f, -0.2f) * scale,   // right wing
            new Vector3( 0f,    0f, -0.6f) * scale,   // tail
        };
        int[] tris = { 0,1,2,  0,2,3,  0,3,4,  2,5,3 };
        Mesh m = new Mesh();
        m.vertices = verts;
        m.triangles = tris;
        m.RecalculateNormals();
        return m;
    }

    // ── Circle (NPC / Objective) ───────────────────────────────────────────────
    void BuildCircle(int layer, Color col, float sizeMultiplier)
    {
        AddMesh(iconRoot, layer, MakeDoubleSided(CreateCircleMesh(1.35f * sizeMultiplier, 16)), ColOutline, -0.01f);
        GameObject main = AddMesh(iconRoot, layer, MakeDoubleSided(CreateCircleMesh(1f * sizeMultiplier, 16)), col, 0f);
        mainRenderer = main.GetComponent<MeshRenderer>();
    }

    static Mesh CreateCircleMesh(float radius, int segments)
    {
        Vector3[] verts = new Vector3[segments + 1];
        int[]     tris  = new int[segments * 3];
        verts[0] = Vector3.zero;
        for (int i = 0; i < segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            verts[i + 1] = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            tris[i * 3]     = 0;
            tris[i * 3 + 1] = i + 1;
            tris[i * 3 + 2] = (i + 1) % segments + 1;
        }
        Mesh m = new Mesh();
        m.vertices = verts;
        m.triangles = tris;
        m.RecalculateNormals();
        return m;
    }

    // ── Diamond (Boss) ────────────────────────────────────────────────────────
    void BuildDiamond(int layer)
    {
        AddMesh(iconRoot, layer, MakeDoubleSided(CreateDiamondMesh(1.35f)), ColOutline, -0.01f);
        GameObject main = AddMesh(iconRoot, layer, MakeDoubleSided(CreateDiamondMesh(1f)), ColBoss, 0f);
        mainRenderer = main.GetComponent<MeshRenderer>();
    }

    static Mesh CreateDiamondMesh(float scale)
    {
        Vector3[] verts = {
            new Vector3( 0f,   0f,  0.6f) * scale,
            new Vector3(-0.5f, 0f,  0f  ) * scale,
            new Vector3( 0f,   0f, -0.6f) * scale,
            new Vector3( 0.5f, 0f,  0f  ) * scale,
        };
        int[] tris = { 0,1,2, 0,2,3 };
        Mesh m = new Mesh();
        m.vertices = verts;
        m.triangles = tris;
        m.RecalculateNormals();
        return m;
    }

    // ── Makes a mesh double-sided so it's visible from both above and below ──
    static Mesh MakeDoubleSided(Mesh m)
    {
        int[] tris = m.triangles;
        int origLen = tris.Length;
        int[] both = new int[origLen * 2];
        for (int i = 0; i < origLen; i++) both[i] = tris[i];
        // Reverse winding for back faces
        for (int i = 0; i < origLen; i += 3)
        {
            both[origLen + i]     = tris[i];
            both[origLen + i + 1] = tris[i + 2];
            both[origLen + i + 2] = tris[i + 1];
        }
        m.triangles = both;
        m.RecalculateNormals();
        return m;
    }

    // ── Shared mesh helper ────────────────────────────────────────────────────
    static GameObject AddMesh(GameObject parent, int layer, Mesh mesh, Color col, float yOffset)
    {
        GameObject go = new GameObject("mesh");
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = new Vector3(0f, yOffset, 0f);
        go.layer = layer;

        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.mesh = mesh;

        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        Material mat = new Material(Shader.Find("Unlit/Color"));
        // Render AFTER opaque geometry so icons show through roofs
        mat.renderQueue = 3500;
        mat.color = col;
        mr.material = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        return go;
    }

    // ── LateUpdate ────────────────────────────────────────────────────────────
    void LateUpdate()
    {
        if (iconRoot == null) return;

        // Float above object at heightOffset
        iconRoot.transform.position = transform.position + Vector3.up * heightOffset;

        // Scale by iconSize
        iconRoot.transform.localScale = Vector3.one * iconSize;

        // Mesh vertices are already flat in XZ — no X rotation needed.
        // Player arrow rotates around Y to match facing direction.
        if (markerType == MarkerType.Player)
            iconRoot.transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        else
            iconRoot.transform.rotation = Quaternion.identity;

        // NPC colour tracks mood
        if (markerType == MarkerType.NPC && mainRenderer != null && trackedPerson != null)
        {
            mainRenderer.material.color =
                trackedPerson.currentMood == UnhappyPerson.MoodState.Happy
                ? ColNPCHappy : ColNPCUnhappy;
        }

        // Objective pulses in size
        if (markerType == MarkerType.Objective)
        {
            pulseTimer += Time.deltaTime * 2.5f;
            float pulse = 1f + Mathf.Sin(pulseTimer) * 0.25f;
            iconRoot.transform.localScale = Vector3.one * iconSize * pulse;
        }
    }

    /// <summary>
    /// Hides this marker. Wire this to SectionTracker's onSectionComplete event
    /// in the Inspector to make the objective disappear when the section is done.
    /// </summary>
    public void Hide()
    {
        if (iconRoot != null) iconRoot.SetActive(false);
        enabled = false;
    }

    /// <summary>
    /// Shows this marker again (e.g. if you need to re-activate an objective).
    /// </summary>
    public void Show()
    {
        if (iconRoot != null) iconRoot.SetActive(true);
        enabled = true;
    }

    void OnDestroy()
    {
        if (iconRoot != null) Destroy(iconRoot);
    }
}
