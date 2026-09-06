using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;
using UnityEngine;

public class ArduinoBridge : MonoBehaviour
{
    [Header("Serial Port Settings")]
    [Tooltip("串口名称，例如在 Windows 上是 COM3，在 Mac 上可能是 /dev/tty.usbmodem...")]
    [SerializeField] public string portName = "COM3";
    [Tooltip("波特率，必须与 Arduino 代码中一致")]
    public int baudRate = 9600;

    [Header("Arduino Data (Read Only)")]
    public int buttonState;
    public int soundLevel;
    public int lightLevel;
    public int fsr1Level;
    public int fsr2Level;

    private SerialPort serialPort;
    private Thread readThread;
    private bool isRunning = false;
    
    // 用于在子线程和主线程之间传递最新的数据
    private string latestDataString = "";
    private readonly object dataLock = new object();

    void Start()
    {
        OpenConnection();
    }

    void Update()
    {
        // 1. 在主线程中解析最新的数据
        string dataToParse = "";
        lock (dataLock)
        {
            if (!string.IsNullOrEmpty(latestDataString))
            {
                dataToParse = latestDataString;
                latestDataString = ""; // 读取后清空
            }
        }

        if (!string.IsNullOrEmpty(dataToParse))
        {
            ParseData(dataToParse);
        }

        // 2. 测试：按空格键触发震动
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TriggerVibration();
        }
    }

    private void OpenConnection()
    {
        try
        {
            serialPort = new SerialPort(portName, baudRate);
            serialPort.ReadTimeout = 50;
            serialPort.Open();
            
            isRunning = true;
            
            // 开启后台线程读取串口数据，防止卡死 Unity 主线程
            readThread = new Thread(ReadSerialData);
            readThread.IsBackground = true;
            readThread.Start();
            
            Debug.Log($"<color=green>Arduino 成功连接到 {portName}</color>");
        }
        catch (Exception e)
        {
            Debug.LogError($"<color=red>连接 {portName} 失败: {e.Message}</color>");
        }
    }

    private void ReadSerialData()
    {
        while (isRunning && serialPort != null && serialPort.IsOpen)
        {
            try
            {
                string data = serialPort.ReadLine();
                if (!string.IsNullOrEmpty(data))
                {
                    // 加锁，将数据安全地传递给主线程
                    lock (dataLock)
                    {
                        latestDataString = data;
                    }
                }
            }
            catch (TimeoutException)
            {
                // 超时是正常的，直接忽略继续循环
            }
            catch (Exception)
            {
                // 忽略其他错误以免线程崩溃
            }
        }
    }

    private void ParseData(string data)
    {
        // 我们期待的格式是: btn,sound,light,fsr1,fsr2
        string[] values = data.Trim().Split(',');
        if (values.Length == 5)
        {
            int.TryParse(values[0], out buttonState);
            int.TryParse(values[1], out soundLevel);
            int.TryParse(values[2], out lightLevel);
            int.TryParse(values[3], out fsr1Level);
            int.TryParse(values[4], out fsr2Level);
        }
    }

    /// <summary>
    /// 触发 Arduino 端的震动马达
    /// </summary>
    public void TriggerVibration()
    {
        if (serialPort != null && serialPort.IsOpen)
        {
            // 向 Arduino 发送字符 'V'
            serialPort.Write("V");
            Debug.Log("向 Arduino 发送了震动指令！");
        }
        else
        {
            Debug.LogWarning("无法触发震动，Arduino 未连接！");
        }
    }

    void OnDestroy()
    {
        // 停止后台线程
        isRunning = false;
        
        if (readThread != null && readThread.IsAlive)
        {
            readThread.Join(500); // 最多等待 500ms 让线程结束
        }

        // 关闭串口
        if (serialPort != null && serialPort.IsOpen)
        {
            serialPort.Close();
        }
    }
}
