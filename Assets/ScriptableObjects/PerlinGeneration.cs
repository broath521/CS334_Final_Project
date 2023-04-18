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
        public int octaves;
        public float persistance, lacunarity, scale;

        //tilemap script and value class
        [Serializable]
        class TileData
        {
            [Range(0f, 1f)]
            public float Height;
            public TerrainType GroundTile;
        }

        [SerializeField]
        private TileData[] TileTypes;

        public float adjustVal = 0.1f;
        public float riverRange = 5f;

        float[] noiseMap; //noiseMap storage
        float[] distanceMap; //distanceMap storage
        
        public void Start()
        {
            //initialize array of tiles by height
            TileTypes = TileTypes.OrderBy(a => a.Height).ToArray();
        }
        public float[] Generate(TilemapStructure tilemap, List<Vector2> riverMap, RiverGenerator riverGen)
        {
            float height = 0;
            //generate a noise map with given parameters
            noiseMap = GenerateNoiseMap(tilemap.Width, tilemap.Height, tilemap.Seed, scale, octaves, persistance, lacunarity);
            //Generate a distance map corresponding to generated river
            //distanceMap = GenerateDistanceMap(tilemap.Width, tilemap.Height, riverMap, riverGen);

            //go through bounds to check noise and distance maps and change terrain
            for (int x=0; x < tilemap.Width; x++)
            {
                for (int y = 0; y < tilemap.Height; y++)
                {
                    //if the distanceMap flag at this position is set, change the tile height
                    height = noiseMap[y * tilemap.Width + x];

                    //Check all of the assigned tile types
                    for (int i = 0; i < TileTypes.Length; i++)
                    {
                        //If the height value at position is <= user defined limit, set that tile to be of that indexed type
                        if (height <= TileTypes[i].Height)
                        {
                            tilemap.SetTile(x, y, (int)TileTypes[i].GroundTile);
                            break;
                        }
                    }
                }
            }

            return noiseMap;
        }

        public float[] GenerateNoiseMap(int width, int height, int seed, float scale, int octaves, float persistance, float lacunarity)
        {
            //coordinate iterators
            int x, y;

            float[] perlinMap = new float[width * height];
            var random = new System.Random(seed);

            //cant have no octaves or scale
            octaves = octaves <= 0 ? 1 : octaves;
            scale = scale <= 0f ? 0.01f : scale;

            //needed because of how Unity's tile map renderer is orientated at a corner
            float halfWidth = width / 2f;
            float halfHeight = height / 2f;

            Vector2[] octaveArr = new Vector2[octaves];
            for (int i = 0; i < octaves; i++)
            {
                float randX = random.Next(-100000, 100000);
                float randY = random.Next(-100000, 100000);
                octaveArr[i] = new Vector2(randX, randY);
            }

            //container variables for the upper and lower limits of the perlin map heights
            float maxHeight = float.MinValue;
            float minHeight = float.MaxValue;

            float perlinValue = 0f;
            Vector2 sample = new Vector2();

            for (x = 0; x < width; x++)
            {
                for (y = 0; y < height; y++)
                {
                    // Define base values for amplitude, frequency, and cumalative height
                    float amp = 1;
                    float freq = 1;
                    float cumalative = 0;

                    // Calculate noise for each octave
                    for (int i = 0; i < octaves; i++)
                    {
                        sample.x = (x - halfWidth) / scale * freq + octaveArr[i].x;
                        sample.y = (y - halfHeight) / scale * freq + octaveArr[i].y;

                        //built-in Unity method for perlin generation
                        perlinValue = Mathf.PerlinNoise(sample.x, sample.y) * 2 - 1;

                        //update based on generated value and user inputs
                        cumalative += perlinValue * amp;
                        amp *= persistance;
                        freq *= lacunarity;
                    }

                    //update min and max heights
                    maxHeight = (cumalative > maxHeight) ? cumalative : maxHeight;
                    minHeight = (cumalative < minHeight) ? cumalative : minHeight;

                    // Assign our noise
                    perlinMap[y * width + x] = cumalative;
                }
            }

            for (x = 0; x < width; x++)
            {
                for (y = 0; y < height; y++)
                {
                    //Normalize to a range of 0 and 1 within min and max height
                    perlinMap[y * width + x] = Mathf.InverseLerp(minHeight, maxHeight, perlinMap[y * width + x]);
                }
            }

            return perlinMap;
        }
        

        public float[] GenerateDistanceMap(int width, int height, List<Vector2> riverMap, RiverGenerator riverGen)
        {
            distanceMap = new float[width * height];

            for(int i = 0; i < width; i++)
            {
                for(int j = 0; j < height; j++)
                {
                    distanceMap[i * width + j] = 100; //set to a large number so it does not get flagged in last step
                }
            }
            
            for (int x = riverGen.leftBound - (int)riverRange-2; x < riverGen.rightBound + (int)riverRange+2; x++)
            {
                for (int y = riverGen.bottomBound - (int)riverRange-2; y < riverGen.topBound + (int)riverRange+2; y++)
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
                    if (x >= 0 && x < width && y >= 0 && y < height)
                    {
                        distanceMap[y * width + x] = minDistance;
                    }
                }
            }

            //Check if the distanceMap value is within range of river, if so set tag
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

        public float changeTerrain(float currentHeight)
        {
            float newHeight;

            //any tiles within range that are of water or sand, change to beach1
            if (currentHeight <= TileTypes[3].Height)
            {
                newHeight = TileTypes[2].Height;
            }
            //Anything above beach2 has special rules
            else if(currentHeight > TileTypes[3].Height && currentHeight <= TileTypes[4].Height)
            {
                newHeight = TileTypes[6].Height;
            }
            else if (currentHeight > TileTypes[4].Height && currentHeight <= TileTypes[5].Height)
            {
                newHeight = TileTypes[7].Height;
            }
            else if (currentHeight > TileTypes[6].Height && currentHeight <= TileTypes[7].Height)
            {
                newHeight = TileTypes[3].Height;
            }
            else if (currentHeight > TileTypes[7].Height && currentHeight <= TileTypes[8].Height)
            {
                newHeight = TileTypes[7].Height;
            }
            else if (currentHeight > TileTypes[8].Height && currentHeight <= TileTypes[TileTypes.Length - 1].Height)
            {
                newHeight = TileTypes[8].Height;
            }
            else
            {
                newHeight = currentHeight;
            }

            return newHeight;
        }

        public float getTypeHeight(int ind)
        {
            return TileTypes[ind].Height;
        }

        public TerrainType getTypeGround(int ind)
        {
            return TileTypes[ind].GroundTile;
        }
    }
}

