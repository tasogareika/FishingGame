// Floater v0.0.2
// by Donovan Keith
//
// [MIT License](https://opensource.org/licenses/MIT)

using UnityEngine;
using UnityEngine.UI;
using System.Collections;

// Makes objects float up & down while gently spinning.
public class ObjectFloat : MonoBehaviour
{
    // User Inputs
    public float degreesPerSecond = 15.0f;
    public float amplitude = 0.5f;
    public float frequency = 1f;

    // Position Storage Variables
    Vector2 posOffset = new Vector2();
    Vector2 tempPos = new Vector2();

    private RectTransform rectPos;

    // Use this for initialization
    void Start()
    {
        rectPos = GetComponent<RectTransform>();
        // Store the starting position & rotation of the object
        posOffset = rectPos.anchoredPosition;
    }

    // Update is called once per frame
    void Update()
    {
        // Spin object around Y-Axis
        //transform.Rotate(new Vector3(0f, Time.deltaTime * degreesPerSecond, 0f), Space.World);

        // Float up/down with a Sin()
        tempPos = posOffset;
        tempPos.y += Mathf.Sin(Time.fixedTime * Mathf.PI * frequency) * amplitude;

        rectPos.anchoredPosition = tempPos;
    }
}