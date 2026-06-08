using UnityEngine;

// Shows a distinct colored icon on the minimap for each object type.
// Icons float HIGH above objects so they're visible even inside buildings.
// Automatically hides when the parent GameObject is disabled (pickup collected).
//
// Shapes:
//   Player      = black forward-pointing arrow  (rotates with player)
//   NPC         = blue circle  (turns green when happy)
//   Boss        = purple diamond  (Watcher)
//  FinalBoss   = orange-red 6-point star  (larger, pulses faster)
//  Objective   = yellow pulsing circle with outline
//  Ammo        = red square
//  LoveBomb    = pink 4-point star
//  HealthPickup= lime-green cross / plus sign

public class MinimapMarker : MonoBehaviour
{
    public enum MarkerType { Player, NPC, Boss, FinalBoss, Objective, Ammo, LoveBomb, HealthPickup }

    [Header("Marker Settings")]
    public MarkerType markerType = MarkerType.NPC;

    [Tooltip("Height above the object. Keep this above your tallest building (default 15).")]
    public float heightOffset = 15f;

    [Tooltip("Size of the icon in world units.")]
    public float iconSize = 2.5f;

    // ── Colours ───────────────────────────────────────────────────────────────
    static readonly Color ColPlayer       = Color.black;
    static readonly Color ColNPCUnhappy   = new Color(0.2f, 0.4f, 1f);    // blue
    static readonly Color ColNPCHappy     = new Color(0.2f, 1f,  0.35f);
    static readonly Color ColBoss         = new Color(0.75f, 0.1f, 1f);   // purple  (Watcher)
    static readonly Color ColFinalBoss    = new Color(1f,   0.3f, 0f);    // orange-red
    static readonly Color ColObjective    = new Color(1f,  0.9f,  0f);
    static readonly Color ColAmmo         = new Color(1f,  0.15f, 0.15f);  // red
    static readonly Color ColLoveBomb     = new Color(1f,  0.2f,  0.65f);  // hot pink
    static readonly Color ColHealthPickup = new Color(0.55f, 1f,  0f);     // lime green
    static readonly Color ColOutline      = Color.black;

    private GameObject    iconRoot;
    private MeshRenderer  mainRenderer;
    private UnhappyPerson trackedPerson;
    private float         pulseTimer;

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    void Start()
    {
        CreateIcon();
        if (markerType == MarkerType.NPC)
            trackedPerson = GetComponentInParent<UnhappyPerson>();
    }

    /// <summary>Hide the icon when the pickup GameObject is disabled (i.e. collected).</summary>
    void OnDisable()
    {
        if (iconRoot != null) iconRoot.SetActive(false);
    }

    /// <summary>Restore the icon if the pickup is ever re-enabled.</summary>
    void OnEnable()
    {
        if (iconRoot != null) iconRoot.SetActive(true);
    }

    void OnDestroy()
    {
        if (iconRoot != null) Destroy(iconRoot);
    }

    // ── Icon construction ─────────────────────────────────────────────────────
    void CreateIcon()
    {
        iconRoot = new GameObject($"MinimapIcon_{markerType}");

        int layer = LayerMask.NameToLayer("Minimap");
        if (layer < 0)
        {
            Debug.LogWarning("[MinimapMarker] Add a layer named 'Minimap' in Project Settings > Tags and Layers.");
            layer = 0;
        }

        switch (markerType)
        {
            case MarkerType.Player:       BuildArrow(layer);                              break;
            case MarkerType.NPC:          BuildCircle(layer, ColNPCUnhappy, 1.0f);        break;
            case MarkerType.Boss:         BuildDiamond(layer);                            break;
            case MarkerType.FinalBoss:    BuildSixPointStar(layer);                       break;
            case MarkerType.Objective:    BuildCircle(layer, ColObjective,  1.2f);        break;
            case MarkerType.Ammo:         BuildSquare(layer);                             break;
            case MarkerType.LoveBomb:     BuildStar(layer);                               break;
            case MarkerType.HealthPickup: BuildCross(layer);                              break;
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
        Vector3[] verts = {
            new Vector3( 0f,    0f,  0.6f) * scale,
            new Vector3(-0.4f,  0f, -0.2f) * scale,
            new Vector3(-0.15f, 0f,  0f  ) * scale,
            new Vector3( 0.15f, 0f,  0f  ) * scale,
            new Vector3( 0.4f,  0f, -0.2f) * scale,
            new Vector3( 0f,    0f, -0.6f) * scale,
        };
        int[] tris = { 0,1,2,  0,2,3,  0,3,4,  2,5,3 };
        Mesh m = new Mesh();
        m.vertices  = verts;
        m.triangles = tris;
        m.RecalculateNormals();
        return m;
    }

    // ── Circle (NPC / Objective) ──────────────────────────────────────────────
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
        m.vertices  = verts;
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
        int[] tris = { 0,1,2,  0,2,3 };
        Mesh m = new Mesh();
        m.vertices  = verts;
        m.triangles = tris;
        m.RecalculateNormals();
        return m;
    }

    // ── 6-Point Star (Final Boss) ─────────────────────────────────────────────
    void BuildSixPointStar(int layer)
    {
        AddMesh(iconRoot, layer, MakeDoubleSided(CreateSixPointStarMesh(1.35f)), ColOutline, -0.01f);
        GameObject main = AddMesh(iconRoot, layer, MakeDoubleSided(CreateSixPointStarMesh(1f)), ColFinalBoss, 0f);
        mainRenderer = main.GetComponent<MeshRenderer>();
    }

    static Mesh CreateSixPointStarMesh(float scale)
    {
        // 6 outer points + 6 inner points + centre (index 12)
        float outer = 0.6f * scale;
        float inner = 0.28f * scale;
        Vector3[] verts = new Vector3[13];
        for (int i = 0; i < 6; i++)
        {
            float outerAngle = i * 60f * Mathf.Deg2Rad - Mathf.PI / 2f;
            float innerAngle = outerAngle + 30f * Mathf.Deg2Rad;
            verts[i * 2]     = new Vector3(Mathf.Cos(outerAngle) * outer, 0f, Mathf.Sin(outerAngle) * outer);
            verts[i * 2 + 1] = new Vector3(Mathf.Cos(innerAngle) * inner, 0f, Mathf.Sin(innerAngle) * inner);
        }
        verts[12] = Vector3.zero; // centre

        // Fan triangles from centre to each consecutive outer/inner pair
        int[] tris = new int[12 * 3];
        for (int i = 0; i < 12; i++)
        {
            tris[i * 3]     = 12;
            tris[i * 3 + 1] = i;
            tris[i * 3 + 2] = (i + 1) % 12;
        }
        Mesh m = new Mesh();
        m.vertices  = verts;
        m.triangles = tris;
        m.RecalculateNormals();
        return m;
    }

    // ── Square (Ammo pickup) ──────────────────────────────────────────────────
    void BuildSquare(int layer)
    {
        AddMesh(iconRoot, layer, MakeDoubleSided(CreateSquareMesh(1.35f)), ColOutline, -0.01f);
        GameObject main = AddMesh(iconRoot, layer, MakeDoubleSided(CreateSquareMesh(1f)), ColAmmo, 0f);
        mainRenderer = main.GetComponent<MeshRenderer>();
    }

    static Mesh CreateSquareMesh(float scale)
    {
        float h = 0.5f * scale;
        Vector3[] verts = {
            new Vector3(-h, 0f,  h),
            new Vector3( h, 0f,  h),
            new Vector3( h, 0f, -h),
            new Vector3(-h, 0f, -h),
        };
        int[] tris = { 0,1,2,  0,2,3 };
        Mesh m = new Mesh();
        m.vertices  = verts;
        m.triangles = tris;
        m.RecalculateNormals();
        return m;
    }

    // ── 4-Point Star (Love Bomb pickup) ───────────────────────────────────────
    void BuildStar(int layer)
    {
        AddMesh(iconRoot, layer, MakeDoubleSided(CreateStarMesh(1.35f)), ColOutline, -0.01f);
        GameObject main = AddMesh(iconRoot, layer, MakeDoubleSided(CreateStarMesh(1f)), ColLoveBomb, 0f);
        mainRenderer = main.GetComponent<MeshRenderer>();
    }

    static Mesh CreateStarMesh(float scale)
    {
        // 4 outer points (N E S W) + 4 inner corners + centre
        float outer = 0.6f * scale;
        float inner = 0.22f * scale;
        Vector3[] verts = {
            new Vector3( 0f,    0f,  outer),  // 0 N outer
            new Vector3( inner, 0f,  inner),  // 1 NE inner
            new Vector3( outer, 0f,  0f   ),  // 2 E outer
            new Vector3( inner, 0f, -inner),  // 3 SE inner
            new Vector3( 0f,    0f, -outer),  // 4 S outer
            new Vector3(-inner, 0f, -inner),  // 5 SW inner
            new Vector3(-outer, 0f,  0f   ),  // 6 W outer
            new Vector3(-inner, 0f,  inner),  // 7 NW inner
            new Vector3( 0f,    0f,  0f   ),  // 8 centre
        };
        // Fan from centre to each consecutive pair around the star
        int[] tris = {
            8,0,1,  8,1,2,  8,2,3,  8,3,4,
            8,4,5,  8,5,6,  8,6,7,  8,7,0,
        };
        Mesh m = new Mesh();
        m.vertices  = verts;
        m.triangles = tris;
        m.RecalculateNormals();
        return m;
    }

    // ── Cross / Plus (Health pickup) ──────────────────────────────────────────
    void BuildCross(int layer)
    {
        AddMesh(iconRoot, layer, MakeDoubleSided(CreateCrossMesh(1.35f)), ColOutline, -0.01f);
        GameObject main = AddMesh(iconRoot, layer, MakeDoubleSided(CreateCrossMesh(1f)), ColHealthPickup, 0f);
        mainRenderer = main.GetComponent<MeshRenderer>();
    }

    static Mesh CreateCrossMesh(float scale)
    {
        float arm = 0.55f * scale;   // half-length of each arm
        float bar = 0.18f * scale;   // half-width of each arm

        // 12 vertices forming a plus sign
        Vector3[] verts = {
            // vertical bar
            new Vector3(-bar, 0f,  arm),   // 0
            new Vector3( bar, 0f,  arm),   // 1
            new Vector3( bar, 0f, -arm),   // 2
            new Vector3(-bar, 0f, -arm),   // 3
            // horizontal bar
            new Vector3(-arm, 0f,  bar),   // 4
            new Vector3( arm, 0f,  bar),   // 5
            new Vector3( arm, 0f, -bar),   // 6
            new Vector3(-arm, 0f, -bar),   // 7
        };
        // Two overlapping quads
        int[] tris = {
            0,1,2,  0,2,3,   // vertical
            4,5,6,  4,6,7,   // horizontal
        };
        Mesh m = new Mesh();
        m.vertices  = verts;
        m.triangles = tris;
        m.RecalculateNormals();
        return m;
    }

    // ── Shared mesh helper ────────────────────────────────────────────────────
    static Mesh MakeDoubleSided(Mesh m)
    {
        int[] tris   = m.triangles;
        int   origLen = tris.Length;
        int[] both   = new int[origLen * 2];
        for (int i = 0; i < origLen; i++) both[i] = tris[i];
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

    // Cached shader — found once and reused for every marker icon.
    // Falls back through several always-included shaders so the markers
    // show their correct colours in builds even if Unlit/Color is missing
    // from the Graphics Settings "Always Included Shaders" list.
    static Shader _iconShader;
    static Shader IconShader
    {
        get
        {
            if (_iconShader != null) return _iconShader;
            _iconShader = Shader.Find("Unlit/Color");           // ideal — flat, no lighting
            if (_iconShader == null)
                _iconShader = Shader.Find("Sprites/Default");   // always included in builds
            if (_iconShader == null)
                _iconShader = Shader.Find("UI/Default");        // last resort
            if (_iconShader == null)
                Debug.LogError("[MinimapMarker] Could not find any suitable shader. " +
                               "Go to Project Settings → Graphics → Always Included Shaders " +
                               "and add 'Unlit/Color'.");
            return _iconShader;
        }
    }

    static GameObject AddMesh(GameObject parent, int layer, Mesh mesh, Color col, float yOffset)
    {
        GameObject go = new GameObject("mesh");
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = new Vector3(0f, yOffset, 0f);
        go.layer = layer;

        MeshFilter   mf  = go.AddComponent<MeshFilter>();
        mf.mesh = mesh;

        MeshRenderer mr  = go.AddComponent<MeshRenderer>();
        Material     mat = new Material(IconShader);
        mat.renderQueue  = 3500;   // render after opaque so icons show through roofs
        mat.color        = col;
        mr.material      = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows    = false;

        return go;
    }

    // ── LateUpdate ────────────────────────────────────────────────────────────
    void LateUpdate()
    {
        if (iconRoot == null) return;

        // Float above the object
        iconRoot.transform.position   = transform.position + Vector3.up * heightOffset;
        iconRoot.transform.localScale = Vector3.one * iconSize;

        // Player arrow rotates with facing direction; everything else stays flat
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

        // Final Boss pulses faster and more aggressively than a regular objective
        if (markerType == MarkerType.FinalBoss)
        {
            pulseTimer += Time.deltaTime * 5f;
            float pulse = 1f + Mathf.Sin(pulseTimer) * 0.4f;
            iconRoot.transform.localScale = Vector3.one * iconSize * pulse;
        }

        // Pickups pulse gently so they stand out from static markers
        if (markerType == MarkerType.Ammo ||
            markerType == MarkerType.LoveBomb ||
            markerType == MarkerType.HealthPickup)
        {
            pulseTimer += Time.deltaTime * 3f;
            float pulse = 1f + Mathf.Sin(pulseTimer) * 0.15f;
            iconRoot.transform.localScale = Vector3.one * iconSize * pulse;
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    //
    // Hides this marker. Wire to onSectionComplete or call when the objective is done.
    //
    public void Hide()
    {
        if (iconRoot != null) iconRoot.SetActive(false);
        enabled = false;
    }

   
    // Shows this marker (to re-activate an objective).
   
    public void Show()
    {
        if (iconRoot != null) iconRoot.SetActive(true);
        enabled = true;
    }
}
