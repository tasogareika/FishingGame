using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class SensorConnect : MonoBehaviour
{
    public static SensorConnect _instance;
    public string DeviceName = "BlueFPS";
    //public string DeviceName = "MySensor";
    public float[] sensorArray = new float[16];
    public float[] InitialData = new float[16];
    public float x, y, z;

    public Text AccelerometerText;
    public string screentext;
    public Text SensorBugStatusText;
    public Button myButton1, myButton2;

    public Dropdown myDropdown;
    private int debugPanelState = 0;

    public bool isCalibration = false, shortcut = false;
    int number = 0;
    int firstcount = 0;
    float m_Timer = 0.1f;

    public float heading, roll, pitch;
    public float[] q = new float[] { 1.0f, 0.0f, 0.0f, 0.0f };    // vector to hold quaternion
    private float b_x = 1, b_z = 0;                                // reference direction of flux in earth frame
    private float gx_ema, gy_ema, gz_ema, gx_bias, gy_bias, gz_bias;
    private float w_bx = 0, w_by = 0, w_bz = 0;                    // estimate gyroscope biases error
    public float timet, scanTime, connectTime;

    private bool _pairing = false, startscan = false, startconnect = false;
    // Dictionary to store the UUIDs of discovered services and characteristics
    private List<String> discoveredUUIDs = new List<string>();
    bool servicefound, charfound;

    public class Characteristic
    {
        public string ServiceUUID;
        public string CharacteristicUUID;
        public bool Found;
    }

    public static List<Characteristic> Characteristics;
    public Characteristic SubscribeSensorOne;
    public Characteristic SubscribeSensorTwo;
    public Characteristic SubscribeAllSensors;
    public Characteristic WriteSensors;
    public Characteristic ReadDeviceName;

    public bool AllCharacteristicsFound { get { return !(Characteristics.Where(c => c.Found == false).Any()); } }
    public Characteristic GetCharacteristic(string serviceUUID, string characteristicsUUID)
    {
        return Characteristics.Where(c => IsEqual(serviceUUID, c.ServiceUUID) && IsEqual(characteristicsUUID, c.CharacteristicUUID)).FirstOrDefault();
    }

    public enum States
    {
        None,
        Scan,
        Connect,
        ChangeMTU,
        SendResponse,
        SendResponseOnly,
        SubscribeToAllSensors,
        SubscribingToSensor,
        Unsubscribe,
        Disconnect,
        Disconnecting,
    }

    public bool _connected = false;
    private float _timeout = 0f;
    private States _state = States.None;
    private string _deviceAddress;
    private float timer;

    private byte[] _accelerometerConfigureBytes = new byte[] { 0x01, 0x01 };

    string SensorBugStatusMessage
    {
        set
        {
            if (!string.IsNullOrEmpty(value))
                BluetoothLEHardwareInterface.Log(value);
            if (SensorBugStatusText != null)
                SensorBugStatusText.text = value;
        }
    }

    void Reset()
    {
        _connected = false;
        _timeout = 0f;
        _state = States.None;
        _deviceAddress = null;

        if (!_pairing)
        {
            SensorBugStatusMessage = "Looking for Device!";
        }

        _pairing = false;
    }

    public void SetState(States newState, float timeout)
    {
        _state = newState;
        _timeout = timeout;
    }

    void StartProcess()
    {
        Reset();
        BluetoothLEHardwareInterface.Initialize(true, false, () => {

            SetState(States.Scan, 0.1f);

        }, (error) => {

            if (_state == States.SubscribingToSensor)
            {
                _pairing = true;
                SensorBugStatusMessage = "Pairing to Device";

                // if we get an error when trying to subscribe to the SensorBug it is
                // most likely because we just paired with it. Right after pairing you
                // have to disconnect and reconnect before being able to subschribe.
                SetState(States.Disconnect, 0.1f);
            }

            BluetoothLEHardwareInterface.Log("Error: " + error);
        });
    }

    // Use this for initialization
    void Start()
    {
        scanTime = 5.0f;
        connectTime = 5.0f;
        List<string> permsList = new List<string>();
        permsList.Add("android.permission.BLUETOOTH");
        permsList.Add("android.permission.BLUETOOTH_ADMIN");
        permsList.Add("android.permission.BLUETOOTH_CONNECT");
        permsList.Add("android.permission.BLUETOOTH_SCAN");
        permsList.Add("android.permission.ACCESS_FINE_LOCATION");
        string[] permsArray = permsList.ToArray();
        AndroidRuntimePermissions.Permission[] results = AndroidRuntimePermissions.RequestPermissions(permsArray);
        myDropdown.onValueChanged.AddListener(delegate
        {
            MyDropdownValueChangedHandler(myDropdown);
        });

        myButton1.onClick.AddListener(delegate
        {
            Debug.Log("Pressed");
            Baseline();
            //SetState(States.Unsubscribe, 1f);
        });

        myButton2.onClick.AddListener(delegate
        {
            Debug.Log("Disconnect");
            SetState(States.SendResponseOnly, 1f);
        });

    }

    private void Awake()
    {
        _instance = this;
    }

    // Update is called once per frame
    void Update()
    {

        timet = Time.deltaTime / 100f;

        if (startscan)
        {
            scanTime -= Time.deltaTime;
        }

        if (_timeout > 0f)
        {
            _timeout -= Time.deltaTime;
            if (_timeout <= 0f)
            {
                _timeout = 0f;

                switch (_state)
                {
                    case States.None:
                        break;

                    case States.Scan:
                        SensorBugStatusMessage = "Scanning ";
                        //sensorprompttext.text = "Scanning...";
                        startscan = true;
                        BluetoothLEHardwareInterface.Initialize(true, false, () => {
                            BluetoothLEHardwareInterface.ScanForPeripheralsWithServices(null, null, (address, name, rssi, bytes) => {


                                if (startscan && scanTime > 0)
                                {

                                    SensorBugStatusMessage = "Scanning...\nPlease wait for " + Mathf.RoundToInt(scanTime) + " seconds...\n";

                                    if (rssi < 0 && rssi > -50)
                                    {
                                        if (name.Contains("Tv450u") || name.Contains("AR3C") || address.Contains("24:9F:89") || address.Contains("74:D2:85") || address.Contains("0C:EC:80") || address.Contains("F0:5E:CD") || address.Contains("00:3C:84"))
                                        {
                                            SensorBugStatusMessage = "Found " + address;

                                            //rightbutton.gameObject.SetActive(false);
                                            shortcut = true;
                                            _deviceAddress = address;
                                            scanTime = 5.0f;
                                            startscan = false;
                                            BluetoothLEHardwareInterface.StopScan();
                                            connectTime = 5.0f;
                                            SetState(States.Connect, 0.1f);
                                        }
                                    }

                                    if (rssi < 0 && rssi > -55)
                                    {

                                        bool match = false;
                                        foreach (var device in FoundDeviceListScript.DeviceAddressList)
                                        {
                                            if (address == device.Address)
                                            {
                                                device.Rssi = rssi.ToString();
                                                //sensorprompttext.text = "\nDevice signal: " + rssi.ToString();
                                                match = true;
                                                //UpdateSensorPrompt();
                                            }

                                        }

                                        if (!match)
                                        {
                                            SensorBugStatusMessage = "Found " + address;
                                            _deviceAddress = address;
                                            //SetState(States.Connect, 0.5f);
                                            SensorBugStatusMessage = "Added " + address;
                                            string subaddress = address.Substring(address.Length - 4);
                                            string model = subaddress[0].ToString() + subaddress[2].ToString() + subaddress[3].ToString();
                                            FoundDeviceListScript.DeviceAddressList.Add(new DeviceObject(address, name, model, rssi.ToString()));
                                            //UpdateSensorPrompt();

                                        }

                                    }

                                }
                                else
                                {
                                    BluetoothLEHardwareInterface.StopScan();

                                    if (FoundDeviceListScript.DeviceAddressList.Count > 0)
                                    {
                                        FoundDeviceListScript.ReOrderedDeviceList = FoundDeviceListScript.DeviceAddressList.OrderByDescending(o => o.Rssi).ToList();
                                        _deviceAddress = FoundDeviceListScript.ReOrderedDeviceList[0].Address;
                                        SensorBugStatusMessage = "Attempting connection on " + FoundDeviceListScript.ReOrderedDeviceList[0].Address;
                                        connectTime = 5.0f;
                                        SetState(States.Connect, 0.5f);
                                    }

                                    else
                                    {
                                        SensorBugStatusMessage = "No Device Found!";
                                    }

                                    scanTime = 5.0f;
                                    startscan = false;
                                }


                            }, true);

                        }, (error) => {

                            BluetoothLEHardwareInterface.Log("BLE Error: " + error);

                        });
                        break;

                    case States.Connect:
                        BluetoothLEHardwareInterface.StopScan();
                        SensorBugStatusMessage = "Connecting....";

                        discoveredUUIDs = new List<string>();
                        servicefound = false;
                        charfound = false;

                        if (connectTime > 0)
                        {
                            SensorBugStatusMessage = "Connecting..\nPlease wait for a few seconds...\n";
                            StartCoroutine(ConnectDeviceCoroutine(_deviceAddress));
                            startconnect = true;
                            if (connectTime < 2.5)
                                SensorBugStatusMessage = "Connecting..\nJust a while more...\n";
                            if (_connected) break;
                        }
                        if (connectTime < 0)
                        {
                            BluetoothLEHardwareInterface.DisconnectAll();
                            StopCoroutine("ConnectDeviceCoroutine");
                            _connected = false;
                            startconnect = false;
                            connectTime = 5.0f;
                            FoundDeviceListScript.ReOrderedDeviceList.RemoveAt(0);
                            if (FoundDeviceListScript.ReOrderedDeviceList.Count > 0)
                            {

                                _deviceAddress = FoundDeviceListScript.ReOrderedDeviceList[0].Address;
                                SensorBugStatusMessage = "Attempting next device " + FoundDeviceListScript.ReOrderedDeviceList[0].Address;
                                SetState(States.Connect, 0.5f);
                            }

                            else
                            {
                                SensorBugStatusMessage = "No Device Found!";
                            }

                        }

                        break;

                    case States.ChangeMTU:
                        SensorBugStatusMessage = "Changing MTU..";
                        BluetoothLEHardwareInterface.RequestMtu(_deviceAddress, 248, (name, mtu) =>
                        {
                            SetState(States.SendResponse, 0.5f);
                        });
                        break;

                    case States.SendResponse:
                        SensorBugStatusMessage = "Sending haptic response..";
                        String s = "111\n";
                        byte[] _ConfigureBytes = Encoding.UTF8.GetBytes(s);

                        BluetoothLEHardwareInterface.WriteCharacteristic(_deviceAddress, WriteSensors.ServiceUUID, WriteSensors.CharacteristicUUID, _ConfigureBytes, _ConfigureBytes.Length, true, (address) => {
                            SensorBugStatusMessage = "Sent vibration!";
                            SetState(States.SubscribeToAllSensors, 0.5f);

                        });
                        break;

                    case States.SendResponseOnly:
                        SensorBugStatusMessage = "Sending haptic response..";
                        String input = "111\n";
                        byte[] _Bytes = Encoding.UTF8.GetBytes(input);

                        BluetoothLEHardwareInterface.WriteCharacteristic(_deviceAddress, WriteSensors.ServiceUUID, WriteSensors.CharacteristicUUID, _Bytes, _Bytes.Length, true, (address) => {
                            SensorBugStatusMessage = "Sent vibration!";

                        });
                        break;

                    case States.SubscribeToAllSensors:
                        //SetState(States.SubscribingToSensor, 2f);
                        SensorBugStatusMessage = "Subscribing to Characteristics...";

                        BluetoothLEHardwareInterface.SubscribeCharacteristic(_deviceAddress, SubscribeAllSensors.ServiceUUID, SubscribeAllSensors.CharacteristicUUID, null, (characteristric, bytes) =>
                        {

                            //_state = States.None;

                            //var sBytes = BitConverter.ToString(bytes);
                            //AccelerometerText.text = "Data: " + sBytes + "," + bytes.Length;
                            _connected = true;
                            ProcessByteData(bytes);

                        });
                        break;

                    case States.Unsubscribe:
                        SensorBugStatusMessage = "Unsubscribe";

                        BluetoothLEHardwareInterface.UnSubscribeCharacteristic(_deviceAddress, SubscribeAllSensors.ServiceUUID, SubscribeAllSensors.CharacteristicUUID, null);

                        break;

                    case States.SubscribingToSensor:
                        // if we got here it means we timed out subscribing to the accelerometer
                        SetState(States.Disconnect, 1f);
                        break;

                    case States.Disconnect:
                        SetState(States.Disconnecting, 5f);
                        if (_connected)
                        {
                            BluetoothLEHardwareInterface.DisconnectPeripheral(_deviceAddress, (address) =>
                            {
                                // since we have a callback for disconnect in the connect method above, we don't
                                // need to process the callback here.
                            });
                        }
                        else
                        {
                            Reset();
                            SetState(States.Scan, 1f);
                        }
                        break;

                    case States.Disconnecting:
                        // if we got here we timed out disconnecting, so just go to disconnected state
                        Reset();
                        SetState(States.Scan, 1f);
                        break;
                }

            }
        }

        AccelerometerText.text = screentext;
        if (debugPanelState == 1)
        {

            float num = UnityEngine.Random.Range(0, 1000);
            float num2 = UnityEngine.Random.Range(50, 150);
        }

        if (isCalibration)
        {
            if (m_Timer < 0)
            {
                m_Timer = 0.1f;
                Baseline();
            }

            m_Timer -= Time.deltaTime;

        }
    }

    bool IsEqual(string uuid1, string uuid2)
    {
        return (uuid1.ToUpper().CompareTo(uuid2.ToUpper()) == 0);
    }

    void ProcessByteData(byte[] bytes)
    {

        if (myDropdown.value == 1)
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
    }

    // Convert IMU to roll, pitch, yaw
    private void ConvertIMU(float[] sensorArray)
    {
        QuaternionUpdate(sensorArray[5], sensorArray[6], sensorArray[7], (float)(sensorArray[8] * Math.PI / 180f), (float)(sensorArray[9] * Math.PI / 180.0f), (float)(sensorArray[10] * Math.PI / 180.0f), sensorArray[11], sensorArray[12], sensorArray[13]);
        heading = Mathf.Atan2(2.0f * (q[1] * q[2] + q[0] * q[3]), q[0] * q[0] + q[1] * q[1] - q[2] * q[2] - q[3] * q[3]);
        pitch = -Mathf.Asin(2.0f * (q[1] * q[3] - q[0] * q[2]));
        roll = Mathf.Atan2(2.0f * (q[0] * q[1] + q[2] * q[3]), q[0] * q[0] - q[1] * q[1] - q[2] * q[2] + q[3] * q[3]);
    }

    private void QuaternionUpdate(float ax, float ay, float az, float gx, float gy, float gz, float mx, float my, float mz)
    {
        float beta = (float)(Math.Sqrt(3.0f / 4.0f) * Math.PI * (40.0f / 180.0f));
        float zeta = (float)(Mathf.Sqrt(3.0f / 4.0f) * Math.PI * (0.0f / 180.0f));
        float q1 = q[0], q2 = q[1], q3 = q[2], q4 = q[3];   // short name local variable for readability

        // local system variables
        float norm;                                                             // vector norm
        float SEqDot_omega_1, SEqDot_omega_2, SEqDot_omega_3, SEqDot_omega_4;   // quaternion rate from gyroscopes elements
        float f_1, f_2, f_3, f_4, f_5, f_6;                                     // objective function elements
        float J_11or24, J_12or23, J_13or22, J_14or21, J_32, J_33,               // objective function Jacobian elements
        J_41, J_42, J_43, J_44, J_51, J_52, J_53, J_54, J_61, J_62, J_63, J_64; //
        float SEqHatDot_1, SEqHatDot_2, SEqHatDot_3, SEqHatDot_4;               // estimated direction of the gyroscope error
        float w_err_x, w_err_y, w_err_z;                                        // estimated direction of the gyroscope error (angular)
        float h_x, h_y, h_z;                                                    // computed flux in the earth frame
                                                                                // axulirary variables to avoid reapeated calcualtions
        float halfSEq_1 = 0.5f * q1;
        float halfSEq_2 = 0.5f * q2;
        float halfSEq_3 = 0.5f * q3;
        float halfSEq_4 = 0.5f * q4;
        float twoSEq_1 = 2.0f * q1;
        float twoSEq_2 = 2.0f * q2;
        float twoSEq_3 = 2.0f * q3;
        float twoSEq_4 = 2.0f * q4;
        float twob_x = 2.0f * b_x;
        float twob_z = 2.0f * b_z;
        float twob_xSEq_1 = 2.0f * b_x * q1;
        float twob_xSEq_2 = 2.0f * b_x * q2;
        float twob_xSEq_3 = 2.0f * b_x * q3;
        float twob_xSEq_4 = 2.0f * b_x * q4;
        float twob_zSEq_1 = 2.0f * b_z * q1;
        float twob_zSEq_2 = 2.0f * b_z * q2;
        float twob_zSEq_3 = 2.0f * b_z * q3;
        float twob_zSEq_4 = 2.0f * b_z * q4;
        float SEq_1SEq_2;
        float SEq_1SEq_3 = q1 * q3;
        float SEq_1SEq_4;
        float SEq_2SEq_3;
        float SEq_2SEq_4 = q2 * q4;
        float SEq_3SEq_4;
        float twom_x = 2.0f * mx;
        float twom_y = 2.0f * my;
        float twom_z = 2.0f * mz;

        gx_ema = zeta * gx + (1 - zeta) * gx_ema;
        float tempx = Math.Abs(gx - gx_ema);
        float weightx;
        weightx = tempx > 100 ? zeta : 1;
        gx_bias = weightx * gx + (1 - weightx) * gx_bias;
        float gx_a = gx - gx_bias;
        gx = gx_a;

        gy_ema = zeta * gy + (1 - zeta) * gy_ema;
        float tempy = Math.Abs(gy - gy_ema);
        float weighty;
        weighty = tempy > 100 ? zeta : 1;
        gy_bias = weighty * gy + (1 - weighty) * gy_bias;
        float gy_a = gy - gy_bias;
        gy = gy_a;

        gz_ema = zeta * gz + (1 - zeta) * gz_ema;
        float tempz = Math.Abs(gz - gz_ema);
        float weightz;
        weightz = tempz > 100 ? zeta : 1;
        gz_bias = weightz * gz + (1 - weightz) * gz_bias;
        float gz_a = gz - gz_bias;
        gz = gz_a;

        // normalise the accelerometer measurement
        norm = Mathf.Sqrt(ax * ax + ay * ay + az * az);
        ax /= norm;
        ay /= norm;
        az /= norm;
        // normalise the magnetometer measurement
        norm = Mathf.Sqrt(mx * mx + my * my + mz * mz);
        mx /= norm;
        my /= norm;
        mz /= norm;
        // compute the objective function and Jacobian
        f_1 = twoSEq_2 * q4 - twoSEq_1 * q3 - ax;
        f_2 = twoSEq_1 * q2 + twoSEq_3 * q4 - ay;
        f_3 = 1.0f - twoSEq_2 * q2 - twoSEq_3 * q3 - az;
        f_4 = twob_x * (0.5f - q3 * q3 - q4 * q4) + twob_z * (SEq_2SEq_4 - SEq_1SEq_3) - mx;
        f_5 = twob_x * (q2 * q3 - q1 * q4) + twob_z * (q1 * q2 + q3 * q4) - my;
        f_6 = twob_x * (SEq_1SEq_3 + SEq_2SEq_4) + twob_z * (0.5f - q2 * q2 - q3 * q3) - mz;
        J_11or24 = twoSEq_3;                                                    // J_11 negated in matrix multiplication
        J_12or23 = 2.0f * q4;
        J_13or22 = twoSEq_1;                                                    // J_12 negated in matrix multiplication
        J_14or21 = twoSEq_2;
        J_32 = 2.0f * J_14or21;                                                 // negated in matrix multiplication
        J_33 = 2.0f * J_11or24;                                                 // negated in matrix multiplication
        J_41 = twob_zSEq_3;                                                     // negated in matrix multiplication
        J_42 = twob_zSEq_4;
        J_43 = 2.0f * twob_xSEq_3 + twob_zSEq_1;                                // negated in matrix multiplication
        J_44 = 2.0f * twob_xSEq_4 - twob_zSEq_2;                                // negated in matrix multiplication
        J_51 = twob_xSEq_4 - twob_zSEq_2;                                       // negated in matrix multiplication
        J_52 = twob_xSEq_3 + twob_zSEq_1;
        J_53 = twob_xSEq_2 + twob_zSEq_4;
        J_54 = twob_xSEq_1 - twob_zSEq_3;                                       // negated in matrix multiplication
        J_61 = twob_xSEq_3;
        J_62 = twob_xSEq_4 - 2.0f * twob_zSEq_2;
        J_63 = twob_xSEq_1 - 2.0f * twob_zSEq_3;
        J_64 = twob_xSEq_2;
        // compute the gradient (matrix multiplication)
        SEqHatDot_1 = J_14or21 * f_2 - J_11or24 * f_1 - J_41 * f_4 - J_51 * f_5 + J_61 * f_6;
        SEqHatDot_2 = J_12or23 * f_1 + J_13or22 * f_2 - J_32 * f_3 + J_42 * f_4 + J_52 * f_5 + J_62 * f_6;
        SEqHatDot_3 = J_12or23 * f_2 - J_33 * f_3 - J_13or22 * f_1 - J_43 * f_4 + J_53 * f_5 + J_63 * f_6;
        SEqHatDot_4 = J_14or21 * f_1 + J_11or24 * f_2 - J_44 * f_4 - J_54 * f_5 + J_64 * f_6;
        // normalise the gradient to estimate direction of the gyroscope error
        norm = Mathf.Sqrt(SEqHatDot_1 * SEqHatDot_1 + SEqHatDot_2 * SEqHatDot_2 + SEqHatDot_3 * SEqHatDot_3 + SEqHatDot_4 * SEqHatDot_4);
        SEqHatDot_1 = SEqHatDot_1 / norm;
        SEqHatDot_2 = SEqHatDot_2 / norm;

        SEqHatDot_3 = SEqHatDot_3 / norm;
        SEqHatDot_4 = SEqHatDot_4 / norm;
        // compute angular estimated direction of the gyroscope error
        w_err_x = twoSEq_1 * SEqHatDot_2 - twoSEq_2 * SEqHatDot_1 - twoSEq_3 * SEqHatDot_4 + twoSEq_4 * SEqHatDot_3;
        w_err_y = twoSEq_1 * SEqHatDot_3 + twoSEq_2 * SEqHatDot_4 - twoSEq_3 * SEqHatDot_1 - twoSEq_4 * SEqHatDot_2;
        w_err_z = twoSEq_1 * SEqHatDot_4 - twoSEq_2 * SEqHatDot_3 + twoSEq_3 * SEqHatDot_2 - twoSEq_4 * SEqHatDot_1;
        // compute and remove the gyroscope baises
        w_bx += w_err_x * timet * zeta;
        w_by += w_err_y * timet * zeta;
        w_bz += w_err_z * timet * zeta;
        gx -= w_bx;
        gy -= w_by;
        gz -= w_bz;
        // compute the quaternion rate measured by gyroscopes
        SEqDot_omega_1 = -halfSEq_2 * gy - halfSEq_3 * gy - halfSEq_4 * gz;
        SEqDot_omega_2 = halfSEq_1 * gx + halfSEq_3 * gz - halfSEq_4 * gy;
        SEqDot_omega_3 = halfSEq_1 * gy - halfSEq_2 * gz + halfSEq_4 * gx;
        SEqDot_omega_4 = halfSEq_1 * gz + halfSEq_2 * gy - halfSEq_3 * gx;
        // compute then integrate the estimated quaternion rate
        q1 += (SEqDot_omega_1 - (beta * SEqHatDot_1)) * timet;
        q2 += (SEqDot_omega_2 - (beta * SEqHatDot_2)) * timet;
        q3 += (SEqDot_omega_3 - (beta * SEqHatDot_3)) * timet;
        q4 += (SEqDot_omega_4 - (beta * SEqHatDot_4)) * timet;
        // normalise quaternion
        norm = Mathf.Sqrt(q1 * q1 + q2 * q2 + q3 * q3 + q4 * q4);
        q[0] = q1 / norm;
        q[1] = q2 / norm;
        q[2] = q3 / norm;
        q[3] = q4 / norm;
        // compute flux in the earth frame
        SEq_1SEq_2 = q1 * q2;                                             // recompute axulirary variables
        SEq_1SEq_3 = q1 * q3;
        SEq_1SEq_4 = q1 * q4;
        SEq_3SEq_4 = q3 * q4;
        SEq_2SEq_3 = q2 * q3;
        SEq_2SEq_4 = q2 * q4;
        h_x = twom_x * (0.5f - q3 * q3 - q4 * q4) + twom_y * (SEq_2SEq_3 - SEq_1SEq_4) + twom_z * (SEq_2SEq_4 + SEq_1SEq_3);
        h_y = twom_x * (SEq_2SEq_3 + SEq_1SEq_4) + twom_y * (0.5f - q2 * q2 - q4 * q4) + twom_z * (SEq_3SEq_4 - SEq_1SEq_2);
        h_z = twom_x * (SEq_2SEq_4 - SEq_1SEq_3) + twom_y * (SEq_3SEq_4 + SEq_1SEq_2) + twom_z * (0.5f - q2 * q2 - q3 * q3);
        // normalise the flux vector to have only components in the x and z
        b_x = Mathf.Sqrt((h_x * h_x) + (h_y * h_y));
        b_z = h_z;

    }


    private IEnumerator ConnectDeviceCoroutine(string deviceAddress)
    {

        BluetoothLEHardwareInterface.ConnectToPeripheral(_deviceAddress, null, null, (address, serviceUUID, characteristicUUID) =>
        {
            // If a service and characteristic are discovered, add their UUIDs to the dictionary

            discoveredUUIDs.Add(serviceUUID);
            discoveredUUIDs.Add(characteristicUUID);
            if (serviceUUID.ToUpper().Contains("FFE0") || serviceUUID.ToUpper().Contains("FF90"))
                SensorBugStatusMessage = "Found ARIS!";

        }, (disconnectedAddress) => {
            BluetoothLEHardwareInterface.Log("Device disconnected: " + disconnectedAddress);
            _connected = false;
            FoundDeviceListScript.ReOrderedDeviceList.RemoveAt(0);
            if (FoundDeviceListScript.ReOrderedDeviceList.Count > 0)
            {
                _deviceAddress = FoundDeviceListScript.ReOrderedDeviceList[0].Address;
                SensorBugStatusMessage = "Attempting next device " + FoundDeviceListScript.ReOrderedDeviceList[0].Address;
                connectTime = 5.0f;
                SetState(States.Connect, 0.5f);
            }

            else
            {
                SensorBugStatusMessage = "No ARIS Found! Please restart ARIS and try again.";
            }

        });

        yield return new WaitForSeconds(2);

        // Wait for the coroutine to finish before proceeding to the next step
        // After all services and characteristics are discovered, check if the desired service and characteristic are available


        foreach (string uuid in discoveredUUIDs)
        {

            if (uuid.ToUpper().Contains("FFE0"))
            {
                SubscribeAllSensors.ServiceUUID = uuid;
                servicefound = true;
            }

            if (uuid.ToUpper().Contains("FFE4"))
            {
                SubscribeAllSensors.CharacteristicUUID = uuid;
                charfound = true;
            }


        }

        if (servicefound && charfound)
        {
            SensorBugStatusMessage = "Pairing device.. ";
            SetState(States.ChangeMTU, 0.5f);
        }


        else
        {
            _connected = false;
            FoundDeviceListScript.ReOrderedDeviceList.RemoveAt(0);
            if (FoundDeviceListScript.ReOrderedDeviceList.Count > 0)
            {
                _deviceAddress = FoundDeviceListScript.ReOrderedDeviceList[0].Address;
                SensorBugStatusMessage = "Attempting next device ";
                connectTime = 5.0f;
                SetState(States.Connect, 0.5f);
            }
            else
            {
                SensorBugStatusMessage = "No ARIS Found! Please restart ARIS and try again.";
            }
        }

    }

    // Read the desired characteristic of the connected BLE device
    private void PairDevice(string serviceuuid, string charuuid)
    {
        BluetoothLEHardwareInterface.ReadCharacteristic(_deviceAddress, serviceuuid, charuuid, (characteristic, bytes) => {
            //sensorprompttext.text += "Reading device name from " + ReadDeviceName.ServiceUUID;
            FoundDeviceListScript.SensorDeviceAddress = new List<DeviceObject>();
            string utf8String = Encoding.UTF8.GetString(bytes);
            //sensorprompttext.text += "Connected to: " + utf8String;

            if (utf8String.Contains("Tv450u") || utf8String.Contains("AR3C"))
            {
                FoundDeviceListScript.SensorDeviceAddress.Add(new DeviceObject(_deviceAddress, utf8String, utf8String, "0"));
                SensorBugStatusMessage = "Connected to: " + utf8String;
                _connected = true;
                myDropdown.gameObject.SetActive(false);
                StopCoroutine("ConnectDeviceCoroutine");
                connectTime = 5.0f;
                SetState(States.ChangeMTU, 0.2f);
            }

            else
            {
                SensorBugStatusMessage = "Error! Please try again. ";
                BluetoothLEHardwareInterface.DisconnectAll();
            }

        }
        );
    }



    public void MyDropdownValueChangedHandler(Dropdown target)
    {
        if (target.value == 0)
        {

            BluetoothLEHardwareInterface.DisconnectAll();
        }

        else if (target.value == 1)
        { 
            DeviceName = "Tv450u";
            Characteristics = new List<Characteristic>
            {
                    new Characteristic { ServiceUUID =  "0000ffe0-0000-1000-8000-00805f9b34fb", CharacteristicUUID = "0000ffe4-0000-1000-8000-00805f9b34fb", Found = false },
                    new Characteristic { ServiceUUID =  "0000ffe5-0000-1000-8000-00805f9b34fb", CharacteristicUUID = "0000ffe9-0000-1000-8000-00805f9b34fb", Found = false },

            };
            SubscribeAllSensors = Characteristics[0];
            WriteSensors = Characteristics[1];
            StartProcess();

        }
    }

    // Set initial data as threshold
    public void Baseline()
    {
        InitialData[0] = sensorArray[0];
        isCalibration = true;

    }


    public void ExitProgram()
    {
        BluetoothLEHardwareInterface.DisconnectAll();
        Application.Quit();
    }
}
