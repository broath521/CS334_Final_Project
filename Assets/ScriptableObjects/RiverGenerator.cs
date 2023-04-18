using Assets.Tilemaps;
using Mono.Cecil.Cil;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using Unity.Burst;
using Unity.Mathematics;
using UnityEditor.Build.Content;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Assets.ScriptableObjects
{
    /*
     * Most of the code for L-system generation in this class is taken from Assignment 4 from CS334 - Spring 2023
     * This code implements a stochastic method for generate semi-realistic river pathing and branching
    */
    public class RiverGenerator : MonoBehaviour
   {
        System.Random random;
        string lSystemString;
        List<Vector2> riverMap; //river mask storage for each turning point
        List<Vector2> entireRiver; //every tile to draw for the river
        List<Vector2> updatedEntireRiver; //modifed entireRiver
        List<Vector2> updatedRiverMap;

        PerlinGeneration perlinGen; //script access for perlin generation data

        float lowerAng = 0;
        float upperAng = 0;
        int iterations = 0;
        string axiom;
        int seed;

        [Header("River settings")]
        public string riverFile;
        public float distance = 5f;
        public float startingAngle = 90;
        public int startX = 0;
        public int startY = 0;
        public int mainRadius = 1, branchRadius = 0;

        public int leftBound, rightBound, bottomBound, topBound;

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

        public List<Vector2> Apply(TilemapStructure tilemap, PerlinGeneration _perlinGen)
        {
            perlinGen = _perlinGen; //get script reference for perlin generation class

            //grab seed from main script and seed functions
            seed = GameObject.Find("TerrainMap").GetComponent<TilemapStructure>().Seed;
            random = new System.Random(seed);
            riverMap = new List<Vector2>();
            
            //Open and read file data for L-system
            StreamReader stream = new StreamReader(riverFile);
            ReadFileData(stream);
            // Generate L-system string
            lSystemString = GenerateLSystem();

            // Draw L-system onto tilemap
            riverMap = DrawRiver(lSystemString, tilemap);
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
            //start with axiom
            string returnStr = axiom;
            //loop through every iteration to update returnStr
            for(int i = 0; i < iterations; i++)
            {
                foreach (char c in returnStr)
                {
                    //if the overall ruleset contains the character c, get random number from 0 to 1 and pick from character's ruleset
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
            var random = new System.Random(seed); //seed random generator
            float[] angles = { lowerAng, upperAng };
            float rightChance = 0.5f; //chance of taking a right turn instead of left
            float randTurnchance, randAngle; //random turning chance and generated random angle
            float currAngle = startingAngle; //set the current angle to the user given start angle

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

                if (str[i] == 'F' || str[i] == 'B' || str[i] == 'A' || str[i] == 'a' || str[i] == 'b')
                { //main river components
                    prevPos = currentPos;

                    currentPos.x += (int)(Mathf.Cos(currAngle * Mathf.PI / 180) * distance);
                    currentPos.y += (int)(Mathf.Sin(currAngle * Mathf.PI / 180) * distance);
                    UpdateBounds(currentPos);
                    //add the updated point to the rivermap
                    riverMap.Add(new Vector2(currentPos.x, currentPos.y));

                    //grab every tile between prev and current inclusive
                    tilesBetween = GetTilesInLine(prevPos, currentPos);

                    foreach (Vector2 tile in tilesBetween)
                    {
                        List<Vector2> neighbors = GetTileNeighbors((int)tile.x, (int)tile.y, mainRadius);
                        foreach (Vector2 newtile in neighbors)
                        {
                            //if the neighbor is not already in the river, add it
                            if (!entireRiver.Contains(newtile)){
                                entireRiver.Add(newtile);
                            }
                        }
                        entireRiver.Add(tile);
                    }
                }
                else if (str[i] == 'g' || str[i] == 'G')
                { //branch components
                    prevPos = currentPos;

                    currentPos.x += (int)(Mathf.Cos(currAngle * Mathf.PI / 180) * distance);
                    currentPos.y += (int)(Mathf.Sin(currAngle * Mathf.PI / 180) * distance);
                    UpdateBounds(currentPos);
                    //add the updated point to the rivermap
                    riverMap.Add(new Vector2(currentPos.x, currentPos.y));

                    //grab every tile between prev and current inclusive
                    tilesBetween = GetTilesInLine(prevPos, currentPos);

                    foreach (Vector2 tile in tilesBetween)
                    {
                        List<Vector2> neighbors = GetTileNeighbors((int)tile.x, (int)tile.y, branchRadius);
                        foreach (Vector2 newtile in neighbors)
                        {
                            //if the neighbor is not already in the river, add it
                            if (!entireRiver.Contains(newtile))
                            {
                                entireRiver.Add(newtile);
                            }
                        }
                        entireRiver.Add(tile);
                    }
                }
                else if (str[i] == '+')
                {
                    //pick a random number, if less than right chance, change current angle and update right chance
                    randTurnchance = (random.Next(0, 1000) / 1000f);
                    if (randTurnchance < rightChance)
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
                    //push current items to the top of the stack
                    rightChanceStack.Push(rightChance);
                    locationStack.Push(currentPos);
                    previousStack.Push(prevPos);
                    angleStack.Push(currAngle);
                }
                else if (str[i] == ']')
                {
                    //take out top of stack and update variables
                    currentPos = locationStack.Peek();
                    prevPos = previousStack.Peek();
                    currAngle = angleStack.Peek();
                    rightChance = rightChanceStack.Peek();
                    //remove top item from stack
                    rightChanceStack.Pop();
                    locationStack.Pop();
                    previousStack.Pop();
                    angleStack.Pop();
                }
            }
            return riverMap;
        }

        //Bresenham's line algorithm to get every instance of a tile between start and end points
        public List<Vector2> GetTilesInLine(Vector2 start, Vector2 end)
        {
            List<Vector2> tiles = new List<Vector2>();
            Vector2 startTemp = start, endTemp = end;

            int dx = Mathf.Abs((int)endTemp.x - (int)startTemp.x); //change in x
            int dy = Mathf.Abs((int)endTemp.y - (int)startTemp.y); //change in y

            int sx = ((int)start.x < (int)end.x) ? 1 : -1; //x direction
            int sy = ((int)start.y < (int)end.y) ? 1 : -1; //y direction

            int err = dx - dy; //error variable for tracking cumulative error
            int errL = 0; //logic term to track when to change y-coordinate

            while (!((int)startTemp.x == (int)endTemp.x && (int)startTemp.y == (int)endTemp.y))
            {
                //add the new tile
                tiles.Add(new Vector2((int)startTemp.x, (int)startTemp.y));

                errL = 2 * err;
                if (errL > -dy)
                {
                    err -= dy;
                    startTemp.x += sx;
                }
                if (errL < dx)
                {
                    err += dx;
                    startTemp.y += sy;
                }
            }

            return tiles;
        }

        //gets the neighbors of a given position within a radius
        public List<Vector2> GetTileNeighbors(int x, int y, int radius)
        {
            List<Vector2> neighbors = new List<Vector2>();

            for (int i = x - radius; i <= x + radius; i++)
            {
                for (int j = y - radius; j <= y + radius; j++)
                {
                    if (i == x && j == y)
                    {
                        continue; //skip itself
                    }
                    neighbors.Add(new Vector2(i, j));
                }
            }
            return neighbors;
        }

        public IEnumerator DrawConnections(TilemapStructure tilemap, Tilemap TMap, float[] perlinMap, float[] distanceMap)
        {
            List<Vector2> distanceNeighbors; //list of neighbors to access within distance map
            float newHeight = 0; //height to update with distance map
            foreach (Vector2 tile in updatedEntireRiver)
            {
                if (tile.x >= 0 && tile.x < tilemap.Width && tile.y >= 0 && tile.y < tilemap.Height)
                {
                    tilemap.SetTile((int)tile.x, (int)tile.y, 0);

                    Tile typeTile = tilemap.tileDict[tilemap.GetTile((int)tile.x, (int)tile.y)];
                    TMap.SetTile(new Vector3Int((int)tile.x, (int)tile.y, 0), typeTile);
                    TMap.RefreshTile(new Vector3Int((int)tile.x, (int)tile.y, 0));

                    if(updatedRiverMap.Contains(new Vector2(tile.x, tile.y)))
                    {
                        distanceNeighbors = GetTileNeighbors((int)tile.x, (int)tile.y, (int)perlinGen.riverRange);
                        for(int i = 0; i < distanceNeighbors.Count; i++)
                        {
                            int x = (int)distanceNeighbors[i].x;
                            int y = (int)distanceNeighbors[i].y;

                            Debug.Log(distanceMap.Length);
                            if (x >= 0 && x < tilemap.Width && y >= 0 && y < tilemap.Height)
                            {
                                if (distanceMap[y * tilemap.Width + x] == 1 && ((perlinMap[y * tilemap.Width + x] <= perlinGen.getTypeHeight(1)) || !updatedEntireRiver.Contains(new Vector2(x, y))))
                                {
                                    distanceMap[y * tilemap.Width + x] = 0;
                                    newHeight = perlinGen.changeTerrain(perlinMap[y * tilemap.Width + x]);
                                    for (int j = 0; j < 12; j++)
                                    {
                                        //If the height value at position is <= user defined limit, set that tile to be of that indexed type
                                        if (newHeight <= perlinGen.getTypeHeight(j))
                                        {
                                            tilemap.SetTile(x, y, (int)perlinGen.getTypeGround(j));
                                            typeTile = tilemap.tileDict[tilemap.GetTile(x, y)];
                                            TMap.SetTile(new Vector3Int(x, y, 0), typeTile);
                                            TMap.RefreshTile(new Vector3Int(x, y, 0));
                                            break;
                                        }
                                    }
                                }
                            }    
                        }
                    }

                    yield return new WaitForSeconds(0.002f);
                }
            }
        }

        //update the furthest positions of the river if necessasary
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

        //check the updated furthest positions of the river, if they are past the tilemap bounds, update the current river maps
        public List<Vector2> UpdateRiver(TilemapStructure tilemap)
        {
            updatedRiverMap = new List<Vector2>();
            updatedEntireRiver = new List<Vector2>();
            List<Vector2> currRiverMap = entireRiver;

            Vector2 tempVec, tempVec2 = new();

            if (leftBound < 0) { 
                for(int i = 0; i < currRiverMap.Count; i++)
                {
                    tempVec.x = currRiverMap[i].x;
                    tempVec.y = currRiverMap[i].y;
                    tempVec2 = tempVec;

                    tempVec.x += Mathf.Abs(leftBound);
                    if (bottomBound < 0)
                    {
                        tempVec.y += Mathf.Abs(bottomBound) + tilemap.Height/5;
                    }

                    updatedEntireRiver.Add(tempVec);
                    if (riverMap.Contains(tempVec2))
                    {
                        updatedRiverMap.Add(tempVec);
                    }
                }
                rightBound += Mathf.Abs(leftBound);
                leftBound = 0;
            }
            else if (rightBound > tilemap.Width)
            {
                for (int i = 0; i < currRiverMap.Count; i++)
                {
                    tempVec.x = currRiverMap[i].x;
                    tempVec.y = currRiverMap[i].y;
                    tempVec2 = tempVec;

                    tempVec.x -= rightBound - tilemap.Width;
                    if (bottomBound < 0)
                    {
                        tempVec.y += Mathf.Abs(bottomBound) + tilemap.Height/5;
                    }

                    updatedEntireRiver.Add(tempVec);
                    if (riverMap.Contains(tempVec2))
                    {
                        updatedRiverMap.Add(tempVec);
                    }
                }
                leftBound -= rightBound - tilemap.Width;
                rightBound = tilemap.Width;
            }
            else
            {
                updatedEntireRiver = currRiverMap;
                updatedRiverMap = riverMap;
            }

            return updatedRiverMap;
        }
    }
}
