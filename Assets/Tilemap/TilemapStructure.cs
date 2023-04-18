using Assets.ScriptableObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Assets.Tilemaps
{
    public class TilemapStructure : MonoBehaviour
    {
        [Header("Tile Map Parameters")]
        public int Width, Height, TileSize, Seed;
        private int[] tiles;
        private Tilemap TMap;

        [Serializable]
        class TileType
        {
            public TerrainType GroundTile;
            public Color Color;
        }

        [SerializeField]
        private TileType[] TileTypes;

        public Dictionary<int, Tile> tileDict;

        GameObject terrainMap; //Terrain map storage object
        PerlinGeneration perlinGen; //Perlin noise generation script
        RiverGenerator riverGen; //L-system river generation script

        //noise maps
        float[] perlinMap;
        float[] distanceMap;

        List<Vector2> riverMap, riverMapUpdated; //River mask storage

        private void Awake()
        {
            //Retrieve components and scripts
            terrainMap = GameObject.Find("TerrainMap");
            perlinGen = terrainMap.GetComponent<PerlinGeneration>();
            riverGen = terrainMap.GetComponent<RiverGenerator>();
            TMap = GetComponent<Tilemap>();

            // Initialize the one-dimensional array with our map size
            tiles = new int[Width * Height];

            //Create dictionary to hold index values for each tile
            tileDict = new Dictionary<int, Tile>();

            //Create a square sprite to draw each tile on
            var tileSprite = Sprite.Create(new Texture2D(TileSize, TileSize), new Rect(0, 0, TileSize, TileSize), new Vector2(0.5f, 0.5f), TileSize);

            // Create a Tile for each GroundTileType
            foreach (var tiletype in TileTypes)
            {
                // Create an object instance of type Tile
                Tile tile = ScriptableObject.CreateInstance<Tile>();
                //Set tile color and assign square sprite
                tiletype.Color.a = 1;
                tile.color = tiletype.Color;
                tile.sprite = tileSprite;

                //Add tile type to dictionary
                tileDict.Add((int)tiletype.GroundTile, tile);
            }
            //Generate river from L-system
            riverGen.Apply(this, perlinGen);
            riverMap = riverGen.UpdateRiver(this);
            //Perlin noise to tile map
            perlinMap = perlinGen.Generate(this, riverMap, riverGen);
            distanceMap = perlinGen.GenerateDistanceMap(Width, Height, riverMap, riverGen);
            // Render updated terrain tiles
            RenderTerrainTiles();
        }

        private void Update()
        {
            //Wait until the user presses the return key, then generate river block by block
            if (Input.GetKeyDown(KeyCode.Return)) {
                StartCoroutine(riverGen.DrawConnections(this, TMap, perlinMap, distanceMap));
            }
        }

        public void RenderTerrainTiles()
        {
            // Create a positions and Tile array for SetTiles method
            var positionsArray = new Vector3Int[Width * Height];
            var tilesArray = new Tile[Width * Height];

            // Loop over tiles within bounds
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    //set position in array
                    positionsArray[x * Width + y] = new Vector3Int(x, y, 0);
                    // Get the tile index at this position
                    var typeOfTile = GetTile(x, y);
                    // Get the Tile object corresponding to this index
                    tilesArray[x * Width + y] = tileDict[typeOfTile];
                }
            }

            //set all tiles via arrays and render them
            TMap.SetTiles(positionsArray, tilesArray);
            TMap.RefreshAllTiles();
        }

        //Return tile if within bounds, otherwise return 0
        public int GetTile(int x, int y)
        {
            return (x >= 0 && x < Width && y >= 0 && y < Height) ? tiles[y * Width + x] : 0;
        }

        //If tile is within bounds, set it to value
        public void SetTile(int x, int y, int value)
        {
            if (x >= 0 && x < Width && y >= 0 && y < Height)
            {
                tiles[y * Width + x] = value;
            }
        }
    }
}