using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class FishHandler : MonoBehaviour
{
    //fishStats.txt file format-
    //name, value, timeMultiplactor, smoothMotion, hookPower debuffer (1 for normal, less than 1 to increase 'weight' on progress bar)
    public TextAsset fishStatsRef;

    public List<Sprite> fishSprites;
    private List<string> fishStatList;

    private void Start()
    {
        //get stats for fish and put to list
        fishStatList = new List<string>();
        string allStats = fishStatsRef.text;
        string[] fishList = allStats.Split('\n');
        foreach(string fish in fishList)
        {
            fishStatList.Add(fish);
        }
    }

    public Sprite getFishSprite()
    {
        int index = UnityEngine.Random.Range(0,fishSprites.Count);
        setFishStats(index);
        return fishSprites[index];
    }

    private void setFishStats(int fishIndex)
    {
        string currFish = fishStatList[fishIndex];
        string[] fishArray = currFish.Split(',');
        FishingMinigame.singleton.setHookStats(fishArray[0], int.Parse(fishArray[1]), float.Parse(fishArray[2]), float.Parse(fishArray[3]),float.Parse(fishArray[4]));
    }
}