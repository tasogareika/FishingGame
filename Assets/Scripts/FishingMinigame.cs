using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FishingMinigame : MonoBehaviour
{
    [SerializeField] private SensorConnect bluetoothController;
    private FishHandler fishHandler;
    [SerializeField] private Transform topPivot, bottomPivot, fishTransform, hookTransform, progressBar;
    private float fishPos, fishDestination, fishTimer, fishSpeed, smoothMotion, timeMultiplactor, waitTimer,
                  hookPos, hookSize, hookPower, hookProgress, hookPullVelocity, hookPullPower, hookGravityPower, hookDegredationPower,
                  horizontalRate;
    [SerializeField] SpriteRenderer hookSprite, fishSprite;
    [SerializeField] private List<GameObject> backgroundLayers;
    [SerializeField] private Animator fishermanAnimator;
    private bool isPaused, isWaiting;

    [Header("UI ELEMENTS")]
    [SerializeField] private GameObject UIPanel; 
    [SerializeField] private GameObject fishingPanel;
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private Button fishBtn;
    [SerializeField] private Image promptImg;
    [SerializeField] private Sprite woodCross, woodCheck;
    [SerializeField] private TextMeshProUGUI promptText;

    private void Start()
    {
        fishHandler = GetComponent<FishHandler>();
        timeMultiplactor = 6f; //time between fish movement (more = fish will stay in one place longer)
        smoothMotion = 3f; //time for how long fish takes to move
        hookSize = 0.3f; //size of hookarea
        hookPower = 0.1f; //how fast progress bar fills
        hookPullPower = 0.01f; //speed of hookarea movement
        hookGravityPower = 0.001f; //hookarea going downwards when there is no force
        hookDegredationPower = 0.1f; //progress bar degredation
        horizontalRate = 0.005f;
        isPaused = true;
        isWaiting = true;
        waitTimer = Random.Range(0.5f, 3f);
        promptImg.sprite = woodCross;
        promptText.text = "Waiting...";
        fishBtn.gameObject.SetActive(false);
        UIPanel.SetActive(false);
        fishingPanel.SetActive(false);
        resizeHook();
    }

    private void Update()
    {
        if (isWaiting)
        {
            if (waitTimer > 0)
            {
                waitTimer -= Time.deltaTime;
            }
            else
            {
                isWaiting = false;
                promptImg.sprite = woodCheck;
                promptText.text = "Fish!!";
                fishBtn.gameObject.SetActive(true);
            }
        }

        if (!isPaused)
        {
            fishMovement();
            hookControl();
            progressCheck();
        }

        backgroundScroll();
    }

    public void startFishing()
    {
        fishBtn.gameObject.SetActive(false);
        fishingPanel.SetActive(true);
        fishermanAnimator.Play("FishHook");
        fishSprite.sprite = fishHandler.getFishSprite();
        isPaused = false;
    }

    private void resizeHook()
    {
        Bounds b = hookSprite.bounds;
        float ySize = b.size.y;
        Vector3 ls = hookTransform.localScale;
        float distance = Vector3.Distance(topPivot.position, bottomPivot.position);
        ls.y = (distance / ySize * hookSize);
        hookTransform.localScale = ls;
    }

    private void hookControl()
    {
        #if UNITY_EDITOR
        if (Input.GetKey(KeyCode.F)) { hookPullVelocity += hookPullPower * Time.deltaTime; }
        #elif UNITY_ANDROID
        if (bluetoothController._connected) {
            float sensorVal = bluetoothController.sensorArray[0] - bluetoothController.InitialData[0];
            if (sensorVal > sensitivitySlider.value || Input.touchCount > 0) { hookPullVelocity += hookPullPower * Time.deltaTime; }
        }
        else {
            if (Input.touchCount > 0) {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began || touch.phase == TouchPhase.Stationary || touch.phase == TouchPhase.Moved) { hookPullVelocity += hookPullPower * Time.deltaTime; }
            }
        }
        #endif
        hookPullVelocity -= hookGravityPower * Time.deltaTime;
        hookPos += hookPullVelocity;
        if (hookPos - hookSize/2f <= 0f && hookPullVelocity < 0f) { hookPullVelocity = 0f; }
        if (hookPos + hookSize/2f >= 1f && hookPullVelocity > 0f) {  hookPullVelocity = 0f; }
        hookPos = Mathf.Clamp(hookPos, hookSize/2f, 1f - hookSize/2f);
        hookTransform.position = Vector3.Lerp(bottomPivot.position, topPivot.position, hookPos);
    }

    private void fishMovement()
    {
        fishTimer -= Time.deltaTime;
        if (fishTimer < 0)
        {
            fishTimer = UnityEngine.Random.value * timeMultiplactor;
            fishDestination = UnityEngine.Random.value;
        }

        fishPos = Mathf.SmoothDamp(fishPos, fishDestination, ref fishSpeed, smoothMotion);
        fishTransform.position = Vector3.Lerp(bottomPivot.position, topPivot.position, fishPos);
    }

    private void progressCheck()
    {
        Vector3 ls = progressBar.localScale;
        ls.y = hookProgress;
        progressBar.localScale = ls;

        float min = hookPos - hookSize / 2;
        float max = hookPos + hookSize / 2;
        if (min < fishPos && fishPos < max) {
            hookProgress += (hookPower * Time.deltaTime) / 3;
        } else {
            hookProgress -= (hookDegredationPower * Time.deltaTime) / 3;
        }
        if (hookProgress > 0.3)
        {
            //fish caught
            isPaused = true;
            fishermanAnimator.Play("FishCatch");
            UIPanel.SetActive(true);
        }

        hookProgress = Mathf.Clamp(hookProgress, 0f, 0.3f);
    }

    public void restartFish()
    {
        hookProgress = 0f;
        fishingPanel.SetActive(false);
        waitTimer = Random.Range(0.5f, 3f);
        isWaiting = true;
        promptImg.sprite = woodCross;
        promptText.text = "Waiting...";
        fishermanAnimator.Play("Fish");
        UIPanel.SetActive(false);
    }

    public void showSliderVal(Text displayText)
    {
        displayText.text = sensitivitySlider.value.ToString();
    }

    private void backgroundScroll()
    {
        for (int i = 0; i < backgroundLayers.Count; i++)
        {
            switch (i)
            {
                case 0:
                    var bg3rate = horizontalRate * 0.1f;
                    var bg3hPos = backgroundLayers[0].transform.position.x;
                    backgroundLayers[0].transform.position = new Vector3(bg3hPos -= bg3rate, backgroundLayers[0].transform.position.y, backgroundLayers[0].transform.position.z);
                    break;

                case 1:
                    var bg2rate = horizontalRate * 0.5f;
                    var bg2hPos = backgroundLayers[1].transform.position.x;
                    backgroundLayers[1].transform.position = new Vector3(bg2hPos -= bg2rate, backgroundLayers[1].transform.position.y, backgroundLayers[1].transform.position.z);
                    break;

                case 2:
                    var bg1hPos = backgroundLayers[2].transform.position.x;
                    backgroundLayers[2].transform.position = new Vector3(bg1hPos -= horizontalRate, backgroundLayers[2].transform.position.y, backgroundLayers[2].transform.position.z);
                    break;
            }

            if (backgroundLayers[i].transform.position.x <= -16.16f)
            {
                backgroundLayers[i].transform.localPosition = new Vector3(0, -3.05f, 0);
            }
        }
    }
}