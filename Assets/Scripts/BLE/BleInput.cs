#if !UNITY_ANDROID
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using System.IO;

//NOTE: Upon building for windows standalone, make sure architecture is x86_64 (64 bits) for BLE functionality to work
public class BleInput : MonoBehaviour
{
    public Dropdown myDropdown;
    public Button myButton1, myButton2;

    public float[] sensorArray = new float[16];
    public float[] InitialData = new float[16];
    public string DeviceName;
    public string serviceUuid;
    public List<string> characteristicUuids;

    BLE ble;
    BLE.BLEScan scan;
    public bool isScanning = false, _connected = false, isTimerRunning = false, isCalibration = false;
    string deviceId = null;
    IDictionary<string, string> discoveredDevices = new Dictionary<string, string>();
    int devicesCount = 0;

    // BLE Threads 
    Thread scanningThread, connectionThread, readingThread, serialthread, calibrationThread;

    // GUI elements
    public Text TextThread, CalibrationText;  
    string screentext;
    float m_Timer = 0.1f;
    public float x, y, z;
    public int[] fingers;


    void Start()
    {
        fingers = new int[5];
        ble = new BLE();
        readingThread = new Thread(ReadBleData);
        sensorArray = new float[16];
        InitialData = new float[16];

        myDropdown.onValueChanged.AddListener(delegate
        {
            MyDropdownValueChangedHandler(myDropdown);
        });

        myButton1.onClick.AddListener(delegate
        {
            Debug.Log("Calibrate");
            Baseline();
        });

        myButton2.onClick.AddListener(delegate
        {
            Debug.Log("Disconnect");
        });

    }

    void Update()
    {
        if (isCalibration)
        {
            if (m_Timer < 0)
            {
                m_Timer = 0.1f;
                Baseline();
            }

            m_Timer -= Time.deltaTime;

        }

        //Scan BLE devices 
        if (isScanning)
        {

            if (discoveredDevices.Count > devicesCount)
            {
                foreach (KeyValuePair<string, string> entry in discoveredDevices)
                {
                    Debug.Log("Added device: " + entry.Key);
                }
                devicesCount = discoveredDevices.Count;
            }
        }


        // The target device was found.
        if (deviceId != null && deviceId != "-1")
        {
            // Target device is connected and GUI knows.
            if (ble.isConnected && _connected)
            {
                if (!readingThread.IsAlive)
                {
                    readingThread = new Thread(ReadBleData);
                    readingThread.Start();
                }
            }
            // Target device is connected, but GUI hasn't updated yet.
            else if (ble.isConnected && !_connected)
            {
                _connected = true;
                TextThread.text = "Connected to target device:\n" + DeviceName;
                Debug.Log("Connected to target device:\n" + DeviceName);
            }
            else if (!_connected)
            {
                Debug.Log("Found target device:\n" + DeviceName);
            }
        }

        // Display unto UI
        TextThread.text = screentext;

    }


    /* Functions to handle BLE */

    //Start BLE Scan
    public void StartScanHandler()
    {
        devicesCount = 0;
        isScanning = true;
        discoveredDevices.Clear();
        deviceId = null;
        scanningThread = new Thread(ScanBleDevices);
        scanningThread.Start();
        Debug.Log("Scanning for..." + DeviceName);
        screentext = "Scanning for.. " + DeviceName;
    }

    // Start establish BLE connection with
    // target device in dedicated thread.
    public void StartConHandler()
    {
        connectionThread = new Thread(ConnectBleDevice);
        connectionThread.Start();
    }

    // Scan BLE devices
    private void ScanBleDevices()
    {
        scan = BLE.ScanDevices();
        Debug.Log("BLE.ScanDevices() started.");
        screentext = "BLE.ScanDevices() started.";
        scan.Found = (_deviceId, deviceName) =>
        {
            discoveredDevices.Add(_deviceId, deviceName);

            //if found the target device, immediately stop scan and attempt to connect
            if (deviceId == null && (deviceName.Contains("Tv450u") || deviceName.Contains("AR3C")))
            {
                Debug.Log("Found device!");
                screentext = "Found device!";
                deviceId = _deviceId;
                StartConHandler();
            }
        };

        scan.Finished = () =>
        {
            isScanning = false;
            screentext = "scan finished";
            Debug.Log("scan finished");
            if (deviceId == null)
                deviceId = "-1";
        };
        while (deviceId == null)
            Thread.Sleep(500);
        scan.Cancel();
        scanningThread = null;
        isScanning = false;

        if (deviceId == "-1")
        {
            screentext = "no device found!";
            Debug.Log("no device found!");
            return;
        }
    }

    // Connect BLE device
    private void ConnectBleDevice()
    {
        if (deviceId != null)
        {
            try
            {
                ble.Connect(deviceId,
                serviceUuid,
                characteristicUuids.ToArray());
            }
            catch (Exception e)
            {
                Debug.Log("Could not establish connection to device with ID " + deviceId + "\n" + e);
            }
        }
        if (ble.isConnected)
        {
            Debug.Log("Connected to: " + DeviceName);
            screentext = "Connected to: " + DeviceName;
            _connected = true;

        }

    }

    // Read BLE Data
    private void ReadBleData(object obj)
    {
        byte[] bytes = BLE.ReadBytes(248); //data input via bytes
        ProcessByteData(bytes);
    }

    // Process BLE Port Data
    void ProcessByteData(byte[] bytes)
    {
        screentext = " ";

        byte[] temp = new byte[4];
        for (int i = 0; i < 16; i++)
        {
            temp[0] = bytes[i * 4 + 2 + 3];
            temp[1] = bytes[i * 4 + 2 + 2];
            temp[2] = bytes[i * 4 + 2 + 1];
            temp[3] = bytes[i * 4 + 2 + 0];
            sensorArray[i] = (float)BitConverter.ToInt32(temp, 0);
            if (i < 5) sensorArray[i] *= 10;
        }

        //Baseline(sensorArray);
        float sensorVal = sensorArray[0] - InitialData[0];
        isCalibration = true;
        screentext += "\nStreaming via BLE, " + "\nSensorArray: " + sensorVal + "," + sensorArray[14];
    }

    // Reset BLE handler
    public void ResetHandler()
    {
        // Reset previous discovered devices
        discoveredDevices.Clear();
        deviceId = null;
        CleanUp();

    }

    public void MyDropdownValueChangedHandler(Dropdown target)
    {
        if (target.value == 0)
        {


        }

        else if (target.value == 1)
        {
            DeviceName = "Tv450u";
            serviceUuid = "{0000ffe0-0000-1000-8000-00805f9b34fb}";
            characteristicUuids = new List<string>() { "{0000ffe4-0000-1000-8000-00805f9b34fb}" };
            StartScanHandler();

        }
    }

    /* Functions to initiate calibration */



    // Set initial data as threshold
    public void Baseline()
    {
        InitialData[0] = sensorArray[0];
        isCalibration = true;

    }



    // Handle GameObject destroy
    private void OnDestroy()
    {
        ResetHandler();
    }

    // Handle Quit Game
    private void OnApplicationQuit()
    {
        ResetHandler();

    }

    // Prevent threading issues and free BLE stack.
    // Can cause Unity to freeze and lead
    // to errors when omitted.
    private void CleanUp()
    {
        try
        {
            scan.Cancel();
        }
        catch (NullReferenceException e)
        {
            Debug.Log("Scan never initialized.\n" + e);
        }


        try
        {
            ble.Close();
        }
        catch (NullReferenceException e)
        {
            Debug.Log("ble never initialized.\n" + e);
        }

        try
        {
            scanningThread.Abort();
        }
        catch (NullReferenceException e)
        {
            Debug.Log("Scan thread never initialized.\n" + e);
        }

        try
        {
            connectionThread.Abort();
        }
        catch (NullReferenceException e)
        {
            Debug.Log("Connection thread never initialized.\n" + e);
        }
    }


}
#endif