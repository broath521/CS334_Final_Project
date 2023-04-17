using Assets.Tilemaps;
using System;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Tilemaps;

namespace Assets.ScriptableObjects
{
    public class PerlinGeneration : MonoBehaviour
    {
        [Header("Perlin settings")]
        // The more octaves, the longer generation will take
        public int Octaves; 
        [Range(0, 1)]
        public float Persistance;
        public float Lacunarity;
        public float NoiseScale;
        public Vector2 Offset;

        //tilemap script and value class
        [Serializable]
        class TileValues
        {
            [Range(0f, 1f)]
            public float Height;
            public GroundTileType GroundTile;
        }
        [SerializeField]
        private TileValues[] TileTypes;

        public float adjustVal = 0.1f;
        public float riverRange = 5f;

        float[] noiseMap; //noiseMap storage
        float[] distanceMap; //distanceMap storage
        
        public void Start()
        {
            //initialize array of tiles by height
            TileTypes = TileTypes.OrderBy(a => a.Height).ToArray();
        }
        public void Generate(TilemapStructure tilemap, List<Vector2> riverMap)
        {
            float height = 0;
            //generate a noise map with given parameters
            noiseMap = GenerateNoiseMap(tilemap.Width, tilemap.Height, tilemap.Seed, NoiseScale, Octaves, Persistance, Lacunarity, Offset);
            distanceMap = GenerateDistanceMap(tilemap.Width, tilemap.Height, riverMap);

            //go through entire tilemap size to apply noise map
            for (int x=0; x < tilemap.Width; x++)
            {
                for (int y = 0; y < tilemap.Height; y++)
                {
                    if (distanceMap[y * tilemap.Width + x] == 1)
                    {
                        height = changeTerrain(noiseMap[y * tilemap.Width + x]);
                    }
                    else
                    {
                        height = noiseMap[y * tilemap.Width + x];
                    }

                    //Check all of the assigned tile types
                    for (int i = 0; i < TileTypes.Length; i++)
                    {
                        //If the height value at the current location is less than or equal to the assigned limit, set that tile to be of type [i]
                        if (height <= TileTypes[i].Height)
                        {
                            tilemap.SetTile(x, y, (int)TileTypes[i].GroundTile);
                            break;
                        }
                    }
                }
            }
        }

        public static float[] GenerateNoiseMap(int mapWidth, int mapHeight, int seed, float scale, int octaves, float persistance, float lacunarity, Vector2 offset)
        {
            float[] noiseMap = new float[mapWidth * mapHeight];

            var random = new System.Random(seed);

            // We need atleast one octave
            if (octaves <= 0)
            {
                octaves = 1;
            }

            Vector2[] octaveOffsets = new Vector2[octaves];
            for (int i = 0; i < octaves; i++)
            {
                float offsetX = random.Next(-100000, 100000) + offset.x;
                float offsetY = random.Next(-100000, 100000) + offset.y;
                octaveOffsets[i] = new Vector2(offsetX, offsetY);
            }

            if (scale <= 0f)
            {
                scale = 0.0001f;
            }

            float maxNoiseHeight = float.MinValue;
            float minNoiseHeight = float.MaxValue;

            // When changing noise scale, it zooms from top-right corner
            // This will make it zoom from the center
            float halfWidth = mapWidth / 2f;
            float halfHeight = mapHeight / 2f;

            for (int x = 0, y; x < mapWidth; x++)
            {
                for (y = 0; y < mapHeight; y++)
                {
                    // Define base values for amplitude, frequency and noiseHeight
                    float amplitude = 1;
                    float frequency = 1;
                    float noiseHeight = 0;

                    // Calculate noise for each octave
                    for (int i = 0; i < octaves; i++)
                    {
                        // We sample a point (x,y)
                        float sampleX = (x - halfWidth) / scale * frequency + octaveOffsets[i].x;
                        float sampleY = (y - halfHeight) / scale * frequency + octaveOffsets[i].y;

                        // Use unity's implementation of perlin noise
                        float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2 - 1;

                        // noiseHeight is our final noise, we add all octaves together here
                        noiseHeight += perlinValue * amplitude;
                        amplitude *= persistance;
                        frequency *= lacunarity;
                    }

                    // We need to find the min and max noise height in our noisemap
                    // So that we can later interpolate the min and max values between 0 and 1 again
                    if (noiseHeight > maxNoiseHeight)
                        maxNoiseHeight = noiseHeight;
                    else if (noiseHeight < minNoiseHeight)
                        minNoiseHeight = noiseHeight;

                    // Assign our noise
                    noiseMap[y * mapWidth + x] = noiseHeight;
                }
            }

            for (int x = 0, y; x < mapWidth; x++)
            {
                for (y = 0; y < mapHeight; y++)
                {
                    // Returns a value between 0f and 1f based on noiseMap value
                    // minNoiseHeight being 0f, and maxNoiseHeight being 1f
                    noiseMap[y * mapWidth + x] = Mathf.InverseLerp(minNoiseHeight, maxNoiseHeight, noiseMap[y * mapWidth + x]);
                }
            }
            return noiseMap;
        }
        
        public float[] GenerateDistanceMap(int width, int height, List<Vector2> riverMap)
        {
            float[] distanceMap = new float[width * height];

            for (int x = 0, y; x < width; x++)
            {
                for (y = 0; y < height; y++)
                {
                    // Calculate the distance to the nearest river tile
                    float minDistance = float.MaxValue;
                    foreach (Vector2 rivTile in riverMap)
                    {
                        float distance = Mathf.Sqrt(Mathf.Pow(rivTile.x - x, 2) + Mathf.Pow(rivTile.y - y, 2));
                        if (distance < minDistance)
                            minDistance = distance;
                    }

                    // Assign the calculated distance to the corresponding tile in the distance map
                    distanceMap[y * width + x] = minDistance;
                }
            }

            //Check if the distanceMap value is within range of river, if so set tag to change later
            for (int i = 0; i < distanceMap.Length; i++)
            {
                if(distanceMap[i] < riverRange)
                {
                    distanceMap[i] = 1;
                }
                else
                {
                    distanceMap[i] = 0;
                }
            }

            return distanceMap;
        }

        private float changeTerrain(float currentHeight)
        {
            float newHeight;

            if(currentHeight <= TileTypes[1].Height)
            {
                newHeight = TileTypes[2].Height;
            }
            if (currentHeight <= TileTypes[3].Height)
            {
                newHeight = TileTypes[2].Height;
            }
            else if(currentHeight > TileTypes[3].Height && currentHeight <= TileTypes[TileTypes.Length - 1].Height)
            {
                currentHeight -= adjustVal;
                newHeight = currentHeight;
            }
            else
            {
                newHeight = currentHeight;
            }

            return newHeight;
        }
    }
}

