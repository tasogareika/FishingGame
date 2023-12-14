using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FishHandler : MonoBehaviour
{
    public List<Sprite> fishSprites;

    public Sprite getFishSprite()
    {
        int index = Random.Range(0,fishSprites.Count);
        return fishSprites[index];
    }
}