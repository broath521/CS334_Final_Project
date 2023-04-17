using Assets.Tilemaps;
using Mono.Cecil.Cil;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Assets.ScriptableObjects
{
    public class RiverGenerator : MonoBehaviour
   {
        System.Random random;
        public string riverFile;
        string lSystemString;
        List<Vector2> riverMap; //river mask storage for each turning point
        List<Vector2> entireRiver; //every tile to draw for the river

        float lowerAng = 0;
        float upperAng = 0;
        int iterations = 0;
        string axiom;

        public float distance = 5f;
        public float startingAngle = 90;
        int seed;

        public int startX = 0;
        public int startY = 0;

        int leftBound, rightBound, bottomBound, topBound;

        //class and dictionary to hold the rules of the L-system
        class Rule
        {
            public float probability;
            public string replacement;

            public Rule(float _probability, string _replacement)
            {
                probability = _probability;
                replacement = _replacement;
            }
        }
        private Dictionary<char, List<Rule>> rules = new Dictionary<char, List<Rule>>();

        public List<Vector2> Apply(TilemapStructure tilemap)
        {
            seed = GameObject.Find("TerrainMap").GetComponent<TilemapStructure>().Seed;
            random = new System.Random(seed);
            riverMap = new List<Vector2>();
            
            //Open and read file data for L-system
            StreamReader stream = new StreamReader(riverFile);
            ReadFileData(stream);
            // Generate L-system string
            lSystemString = GenerateLSystem();
            //Debug.Log(lSystemString);

            // Draw L-system onto tilemap
            DrawRiver(lSystemString, tilemap);
            return riverMap;
        }

        public void ReadFileData(StreamReader stream)
        {
            //read in angles from file. If 2 angles, set accordingly, otherwise set upper and lower to eachother
            string inAngleStr = "";
            if ((inAngleStr = stream.ReadLine()) != null)
            {
                int spaceInd;
                if ((spaceInd = inAngleStr.IndexOf(' ')) != -1)
                {
                    lowerAng = float.Parse(inAngleStr.Substring(0, spaceInd));
                    upperAng = float.Parse(inAngleStr.Substring(spaceInd + 1));
                }
                else
                {
                    lowerAng = float.Parse(inAngleStr);
                    upperAng = lowerAng;
                }
            }

            //read iterations from file, cast to int
            string inIterStr = "";
            if ((inIterStr = stream.ReadLine()) != null)
            {
                iterations = int.Parse(inIterStr);
            }

            //read axiom from file
            axiom = stream.ReadLine();

            string rulesStr;
            while (stream.Peek() >= 0)
            {
                rulesStr = stream.ReadLine();

                if (rulesStr.Length == 0)
                {
                    continue;
                }

                if (!rules.ContainsKey(rulesStr[0]))
                {
                    rules[rulesStr[0]] = new List<Rule>();
                }

                if (rulesStr.Length != 0)
                {
                    if (rulesStr[2] == '0')
                    {
                        rules[rulesStr[0]].Add(new Rule(float.Parse(rulesStr.Substring(2, 4)), rulesStr.Substring(9, rulesStr.Length-9)));
                    }
                    else
                    {
                        rules[rulesStr[0]].Add(new Rule(1.0f, rulesStr.Substring(4, rulesStr.Length-4)));
                    }
                }
            }
        }

        private string GenerateLSystem()
        {
            string returnStr = axiom;
            for(int i = 0; i < iterations; i++)
            {
                foreach (char c in returnStr)
                {
                    if (rules.ContainsKey(c))
                    {
                        List<Rule> ruleSet = rules[c];
                        float randVal = (random.Next(0, 1000) / 1000f);
                        float probabilitySum = 0.0f;
                        foreach (Rule rule in ruleSet)
                        {
                            probabilitySum += rule.probability;
                            if (randVal <= probabilitySum)
                            {
                                returnStr += rule.replacement;
                                break;
                            }
                        }
                    }
                    else
                    {
                        returnStr += c;
                    }
                }
            }
            return returnStr;
        }

        public List<Vector2> DrawRiver(string str, TilemapStructure tilemap)
        {
            entireRiver = new List<Vector2>();
            var random = new System.Random(seed);
            float[] angles = { lowerAng, upperAng };
            float rightChance = 0.5f;
            float turnRand, randAngle;
            float currAngle = startingAngle;

            //stacks for saving turtle data
            Stack<Vector3> locationStack = new Stack<Vector3>();
            Stack<Vector3> previousStack = new Stack<Vector3>();
            Stack<float> rightChanceStack = new Stack<float>();
            Stack<float> angleStack = new Stack<float>();

            Vector3 prevPos;
            Vector3 currentPos = Vector3.zero;
            List<Vector2> tilesBetween;

            //set starting position
            currentPos.x = startX;
            currentPos.y = startY;

            //set initial max bounds
            leftBound = (int)currentPos.x;
            rightBound = (int)currentPos.x;
            bottomBound = (int)currentPos.y;
            topBound = (int)currentPos.y;

            //add the first point to the river map
            riverMap.Add(new Vector2(currentPos.x, currentPos.y));

            prevPos = currentPos;

            for (int i = 0; i < str.Length; i++)
            {
                //pick a random angle to turn with in the range of lower to upper
                if (angles[0] != angles[1])
                {
                    randAngle = angles[0] + (random.Next(0,1000)/1000f) * (angles[1] - angles[0]);
                }
                else
                {
                    randAngle = angles[0];
                }

                //Debug.Log(randAngle);

                if (str[i] == 'F' || str[i] == 'B' || str[i] == 'A' || str[i] == 'a' || str[i] == 'b')
                { //main river components
                    prevPos = currentPos;

                    currentPos.x += (int)(Mathf.Cos(currAngle * Mathf.PI / 180) * distance);
                    currentPos.y += (int)(Mathf.Sin(currAngle * Mathf.PI / 180) * distance);
                    UpdateBounds(currentPos);
                    riverMap.Add(new Vector2(currentPos.x, currentPos.y)); //add the updated point to the rivermap

                    tilesBetween = GetTilesInLine(prevPos, currentPos);

                    foreach (Vector2 tile in tilesBetween)
                    {
                        List<Vector2> neighbors = GetTileNeighbors((int)tile.x, (int)tile.y);
                        foreach (Vector2 newtile in neighbors)
                        {
                            entireRiver.Add(newtile);
                        }
                        entireRiver.Add(tile);
                    }
                }
                else if (str[i] == 'g' || str[i] == 'G')
                { //main river components
                    prevPos = currentPos;

                    //update location
                    currentPos.x += (int)(Mathf.Cos(currAngle * Mathf.PI / 180) * distance);
                    currentPos.y += (int)(Mathf.Sin(currAngle * Mathf.PI / 180) * distance);

                    //add the updated point to the rivermap
                    riverMap.Add(new Vector2(currentPos.x, currentPos.y));

                    tilesBetween = GetTilesInLine(prevPos, currentPos);

                    foreach (Vector2 tile in tilesBetween)
                    {
                        entireRiver.Add(tile);
                    }
                }
                else if (str[i] == '+')
                {
                    turnRand = (random.Next(0, 1000) / 1000f);
                    if (turnRand < rightChance)
                    {
                        currAngle -= randAngle;
                        rightChance -= 0.05f;
                    }
                    else
                    {
                        currAngle += randAngle;
                        rightChance += 0.05f;
                    }
                }
                else if (str[i] == '[')
                {
                    rightChanceStack.Push(rightChance);
                    locationStack.Push(currentPos);
                    previousStack.Push(prevPos);
                    angleStack.Push(currAngle);
                }
                else if (str[i] == ']')
                {
                    currentPos = locationStack.Peek();
                    prevPos = previousStack.Peek();
                    currAngle = angleStack.Peek();
                    rightChance = rightChanceStack.Peek();

                    rightChanceStack.Pop();
                    locationStack.Pop();
                    previousStack.Pop();
                    angleStack.Pop();
                }
            }
            
            Debug.Log(leftBound);
            Debug.Log(rightBound);
            Debug.Log(topBound);
            Debug.Log(bottomBound);
            
            return riverMap;
        }

        //Bresenham's line algorithm to get every instance of a tile between start and end points
        public List<Vector2> GetTilesInLine(Vector2 startTile, Vector2 endTile)
        {
            List<Vector2> tiles = new List<Vector2>();

            int x0 = (int)startTile.x;
            int y0 = (int)startTile.y;
            int x1 = (int)endTile.x;
            int y1 = (int)endTile.y;

            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = (x0 < x1) ? 1 : -1;
            int sy = (y0 < y1) ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                tiles.Add(new Vector2Int(x0, y0));

                if (x0 == x1 && y0 == y1)
                    break;

                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }

            return tiles;
        }

        public List<Vector2> GetTileNeighbors(int x, int y)
        {
            List<Vector2> neighbors = new List<Vector2>();

            for (int i = x - 1; i <= x + 1; i++)
            {
                for (int j = y - 1; j <= y + 1; j++)
                {
                    if (i == x && j == y) continue; // Skip the tile itself
                    neighbors.Add(new Vector2(i, j));
                }
            }

            return neighbors;
        }

        public void DrawConnections(TilemapStructure tilemap, Tilemap TMap)
        {
            float deepChance = 0.7f;

            foreach(Vector2 tile in entireRiver)
            {
                if (tile.x >= 0 && tile.x < tilemap.Width && tile.y >= 0 && tile.y < tilemap.Height)
                {
                    float randVal = (random.Next(0, 1000) / 1000f);
                    if (randVal < deepChance)
                        tilemap.SetTile((int)tile.x, (int)tile.y, 0);
                    else
                        tilemap.SetTile((int)tile.x, (int)tile.y, 1);

                    StartCoroutine(WaitRender());
                    Debug.Log("Yes");
                    Tile typeTile = tilemap.tileDict[tilemap.GetTile((int)tile.x, (int)tile.y)];
                    TMap.SetTile(new Vector3Int((int)tile.x, (int)tile.y, 0), typeTile);
                    TMap.RefreshTile(new Vector3Int((int)tile.x, (int)tile.y, 0));
                }
            }
        }

        private void UpdateBounds(Vector2 newPos)
        {
            if(newPos.x < leftBound)
            {
                leftBound = (int)newPos.x;
            }
            else if(newPos.x > rightBound)
            {
                rightBound = (int)newPos.x;
            }

            if (newPos.y < bottomBound)
            {
                bottomBound = (int)newPos.y;
            }
            else if (newPos.y > topBound)
            {
                topBound = (int)newPos.y;
            }
        }

        public void UpdateRiver(TilemapStructure tilemap)
        {
            if(leftBound < 0) { 
                for(int i = 0; i < entireRiver.Count; i++)
                {
                    Vector2 tempVec = new Vector2(entireRiver[i].x, entireRiver[i].y);
                    tempVec.x += Mathf.Abs(leftBound);
                    if (bottomBound < 0)
                    {
                        tempVec.y += Mathf.Abs(bottomBound) + tilemap.Height/5;
                    }
                    entireRiver[i] = tempVec;
                }
            }
            if (rightBound > tilemap.Width)
            {
                for (int i = 0; i < entireRiver.Count; i++)
                {
                    Vector2 tempVec = new Vector2(entireRiver[i].x, entireRiver[i].y);
                    tempVec.x -= rightBound - tilemap.Width;
                    if (bottomBound < 0)
                    {
                        tempVec.y += Mathf.Abs(bottomBound) + tilemap.Height/5;
                    }
                    entireRiver[i] = tempVec;
                }
            }
        }

        IEnumerator WaitRender()
        {
            yield return new WaitForSeconds(1);
        }
    }
}
