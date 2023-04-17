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
            public GroundTileType GroundTile;
            public Color Color;
        }

        [SerializeField]
        private TileType[] TileTypes;

        public Dictionary<int, Tile> tileDict;

        GameObject terrainMap; //Terrain map storage object
        PerlinGeneration perlinGen; //Perlin noise generation script
        RiverGenerator riverGen; //L-system river generation script

        List<Vector2> riverMap; //River mask storage

        private void Awake()
        {
            //Retrieve components and scripts
            terrainMap = GameObject.Find("TerrainMap");
            perlinGen = terrainMap.GetComponent<PerlinGeneration>();
            riverGen = terrainMap.GetComponent <RiverGenerator>();
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
            riverMap = riverGen.Apply(this);
            riverGen.UpdateRiver(this);

            //Perlin noise to tile map
            perlinGen.Generate(this, riverMap);

            // Render updated terrain tiles
            RenderTerrainTiles();
        }

        private void Update()
        {
            //Wait until the user presses the return key, then generate river block by block
            if (Input.GetKeyDown(KeyCode.Return)) {
                riverGen.DrawConnections(this, TMap);
            }
        }

        public void RenderTerrainTiles()
        {
            // Create a positions array and tile array required by _graphicMap.SetTiles
            var positionsArray = new Vector3Int[Width * Height];
            var tilesArray = new Tile[Width * Height];

            // Loop over all our tiles in our data structure
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    positionsArray[x * Width + y] = new Vector3Int(x, y, 0);
                    // Get what tile is at this position
                    var typeOfTile = GetTile(x, y);
                    // Get the ScriptableObject that matches this type and insert it
                    tilesArray[x * Width + y] = tileDict[typeOfTile];
                }
            }

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