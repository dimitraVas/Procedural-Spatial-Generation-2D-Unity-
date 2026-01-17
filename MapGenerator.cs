using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    [Header("Grid")]
    public int width = 60;
    public int height = 35;
    public float cellSize = 1f;
    public GameObject tilePrefab;

    [Header("Seed")]
    public bool randomizeSeedOnPlay = true;
    public int seed = 12345;

    [Header("Height (terrain)")]
    [Range(0.01f, 0.3f)] public float heightNoiseScale = 0.08f;
    [Range(0f, 1f)] public float riverThreshold = 0.72f;//(below this = water)
    [Range(0f, 1f)] public float mountainThreshold = 0.72f;//(below this = water)

    private SpriteRenderer[,] renderers;
    private float[,] heightMap;// 2d array

    [Header("Population")]
    public GameObject[] forestPrefabs;
    public GameObject[] waterPrefabs;
    public GameObject[] mountainPrefabs;

    [Range(0f, 1f)] public float forestSpawnChance = 0.45f;
    [Range(0f, 1f)] public float waterSpawnChance = 0.25f;
    [Range(0f, 1f)] public float mountainSpawnChance = 0.35f;

    public float jitter = 0.2f;

    Transform forestParent, waterParent, mountainParent;
    

    public Transform ForestParent => forestParent;// get and return
    public Transform WaterParent => waterParent;// get and return
    public Transform MountainParent => mountainParent;// get and return

    public MacroCell[,] macroGrid;// 2d array

    void Start()
    {


    }

    public void StartMap()
    {

        if (tilePrefab == null)
        {
            Debug.LogError("Assign Tile Prefab in the inspector.");
            return;
        }

        if (randomizeSeedOnPlay)
        {
            seed = Random.Range(-999999, 999999);
        }



        renderers = new SpriteRenderer[width, height];

        macroGrid = new MacroCell[width, height];

        BuildGrid();
        GenerateHeightMap();
        PopulateMacroCell();
        SetupParents();
        Populate();
    }

    void BuildGrid()
    {
        
        // Clean old tiles when re-entering play mode
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                Vector3 pos = new Vector3(x * cellSize, y * cellSize, 0f);
                GameObject go = Instantiate(tilePrefab, pos, Quaternion.identity, transform);
                go.name = $"Tile_{x}_{y}";
                renderers[x, y] = go.GetComponent<SpriteRenderer>();
            }
    }

    

    void GenerateHeightMap()
    {
        heightMap = new float[width, height];

        float ox = seed * 0.0137f + 10.5f;
        float oy = seed * 0.0219f + 77.7f;

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                float nx = (x + ox) * heightNoiseScale;
                float ny = (y + oy) * heightNoiseScale;
                heightMap[x, y] = Mathf.PerlinNoise(nx, ny); // 0..1
            }
    }

    void PopulateMacroCell()
    {
        for (int x=0; x<width; x++)
        {
            for (int y=0; y<height; y++)
            {
                float h= heightMap[x, y];
                if (h >= mountainThreshold)
                {
                    macroGrid[x, y] = new MacroCell(x, y, heightMap[x, y], BiomeType.Mountain,false,false);
                    

                }
                else if (h >= riverThreshold)
                {
                    macroGrid[x, y] = new MacroCell(x, y, heightMap[x, y], BiomeType.Forest,false,false);
                    
                }
                else
                {
                    macroGrid[x, y] = new MacroCell(x, y, heightMap[x, y], BiomeType.Water,false,false);
                    
                }
            }
            
        }
        PaintLandFromHeight();
    }
    void PaintLandFromHeight()
    {
        Color river = new Color(0.05f, 0.18f, 0.45f, 1f);
        Color forest = new Color(0.20f, 0.55f, 0.25f, 1f);
        Color mountain = new Color(0.30f, 0.35f, 0.35f, 1f);
        for (int x=0; x<width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (macroGrid[x, y].biome == BiomeType.Forest)
                {
                    renderers[x, y].color = forest;
                }
                else if (macroGrid[x, y].biome == BiomeType.Mountain)
                {
                    renderers[x, y].color = mountain;
                }
                else
                {
                    renderers[x, y].color = river;
                }
            }
        }
    }



    void FitCameraToMap()
    {
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return;

        // Center camera on the map
        float mapWidth = (width - 1) * cellSize;
        float mapHeight = (height - 1) * cellSize;
        cam.transform.position = new Vector3(mapWidth / 2f, mapHeight / 2f, -10f);

        // Fit orthographic size to show whole map (with small padding)
        float padding = 1f;
        float sizeBasedOnHeight = (mapHeight / 2f) + padding;
        float sizeBasedOnWidth = (mapWidth / 2f) / cam.aspect + padding;
        cam.orthographicSize = Mathf.Max(sizeBasedOnHeight, sizeBasedOnWidth);
    }// For start only

    void SetupParents()
    {
        forestParent = new GameObject("Forest_Instances").transform;
        waterParent = new GameObject("Water_Instances").transform;
        mountainParent = new GameObject("Mountain_Instances").transform;

        forestParent.SetParent(transform);
        waterParent.SetParent(transform);
        mountainParent.SetParent(transform);
    }

    void Populate()
    {
        Random.InitState(seed); // deterministic

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                Vector3 cellPos = new Vector3(x * cellSize, y * cellSize, 0f);
                if (macroGrid[x, y].biome == BiomeType.Forest)
                {
                    TrySpawn(forestPrefabs, forestSpawnChance, cellPos, forestParent);
                }
                else if (macroGrid[x, y].biome == BiomeType.Mountain)
                {
                    TrySpawn(mountainPrefabs, mountainSpawnChance, cellPos, mountainParent);
                }
                else
                {
                    TrySpawn(waterPrefabs, waterSpawnChance, cellPos, waterParent);
                }

            }
    }

    void TrySpawn(GameObject[] prefabs, float chance, Vector3 cellPos, Transform parent)
    {
        if (prefabs == null || prefabs.Length == 0) return;
        if (Random.value > chance) return;

        Vector3 pos = cellPos + new Vector3(Random.Range(-jitter, jitter), Random.Range(-jitter, jitter), 0f);
        var prefab = prefabs[Random.Range(0, prefabs.Length)];

        GameObject go = Instantiate(prefab, pos, Quaternion.Euler(0, 0, Random.Range(0f, 360f)), parent);

        float s = Random.Range(0.9f, 1.1f);
        go.transform.localScale = new Vector3(s, s, 1f);
    }

}
