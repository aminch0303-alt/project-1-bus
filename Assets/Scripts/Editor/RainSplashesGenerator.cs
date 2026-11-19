#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class RainSplashesGenerator
{
    private const string PrefabSavePath = "Assets/Other Asset/Directional arrows/PolygonParticles/PolygonParticles/Prefabs/FX_Ground_Splashes.prefab";
    private const string RippleMaterialPath = "Assets/Other Asset/Directional arrows/PolygonParticles/PolygonParticles/Materials/PolygonParticleFX_Ring_01.mat";

    [InitializeOnLoadMethod]
    private static void CheckAndGeneratePrefabOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabSavePath) == null)
            {
                GenerateGroundSplashesPrefab();
            }
        };
    }

    [MenuItem("Tools/Weather/Generate Fake Ground Splashes Prefab")]
    public static void GenerateGroundSplashesPrefab()
    {
        // 1. Create temporary GameObject
        GameObject splashObj = new GameObject("FX_Ground_Splashes");
        ParticleSystem ps = splashObj.AddComponent<ParticleSystem>();

        // 2. Main Module Settings
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.28f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.85f);
        main.startColor = new Color(0.85f, 0.92f, 1f, 0.45f);
        main.maxParticles = 80;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Local;

        // 3. Emission Module (40 splashes/sec: smooth and 0% lag)
        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 40f;

        // 4. Shape Module (Wide box covering bus & camera area)
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(35f, 0.1f, 30f);

        // 5. Size over Lifetime (Ripples expand naturally like water drops)
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.35f),
            new Keyframe(1f, 1.0f)
        );
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // 6. Color over Lifetime (Fade in and quickly fade out)
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.0f, 0.0f), new GradientAlphaKey(0.5f, 0.2f), new GradientAlphaKey(0.0f, 1.0f) }
        );
        colorOverLifetime.color = grad;

        // 7. Collision Module (STRICTLY OFF for 60 FPS mobile performance)
        var collision = ps.collision;
        collision.enabled = false;

        // 8. Renderer Module (Horizontal Billboard - lies completely flat on the road)
        ParticleSystemRenderer psRenderer = splashObj.GetComponent<ParticleSystemRenderer>();
        psRenderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        psRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        psRenderer.receiveShadows = false;

        Material rippleMat = AssetDatabase.LoadAssetAtPath<Material>(RippleMaterialPath);
        if (rippleMat != null)
        {
            psRenderer.material = rippleMat;
        }

        // 9. Save as Prefab
        PrefabUtility.SaveAsPrefabAsset(splashObj, PrefabSavePath);
        Object.DestroyImmediate(splashObj);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[Weather] <color=cyan>FX_Ground_Splashes Prefab created successfully at: " + PrefabSavePath + "</color>");
    }
}
#endif
