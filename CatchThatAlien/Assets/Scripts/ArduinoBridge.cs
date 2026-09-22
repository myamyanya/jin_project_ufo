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

    [Header("Debug Mode (No Arduino Needed)")]
    [Tooltip("勾选此项即可在没有 Arduino 的情况下用键盘测试")]
    public bool useDebugKeyboard = true;
    
    [Header("Arduino Data (Read Only)")]
    public int buttonState;
    public int soundLevel;
    public int lightLevel = 1000; // 默认环境光明亮
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
        // 如果没有开启 Debug 模式，才尝试连接串口
        if (!useDebugKeyboard)
        {
            OpenConnection();
        }
        else
        {
            Debug.Log("<color=yellow>【Debug模式开启】已跳过 Arduino 连接，现在可以使用键盘数字键 1~5 模拟传感器输入！</color>");
        }
    }

    void Update()
    {
        if (useDebugKeyboard)
        {
            // --- 键盘模拟逻辑 ---
            // 1键：体温计按钮 (A0)
            buttonState = Input.GetKey(KeyCode.Alpha1) ? 1 : 0;
            
            // 2键：喇叭声音 (A1)
            soundLevel = Input.GetKey(KeyCode.Alpha2) ? 1 : 0;
            
            // 3键：遮住光敏传感器 (A3)，模拟环境变暗
            lightLevel = Input.GetKey(KeyCode.Alpha3) ? 100 : 1000;
            
            // 4键：按压红色喷雾 (A4) - 模拟按到底
            fsr1Level = Input.GetKey(KeyCode.Alpha4) ? 1000 : 0;
            
            // 5键：按压蓝色喷雾 (A5) - 模拟按到底
            fsr2Level = Input.GetKey(KeyCode.Alpha5) ? 1000 : 0;
        }
        else
        {
            // 1. 真实 Arduino 模式：在主线程中解析最新的数据
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
        }

        // 测试：如果处于连板状态，按空格键可以强行测试一次马达指令发送
        // （现在的震动主要是受 GameplayController 里的心跳控制了）
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
    /// 用于控制马达持续震动
    /// </summary>
    public void SetVibration(bool isOn)
    {
        if (serialPort != null && serialPort.IsOpen)
        {
            serialPort.Write(isOn ? "1" : "0");
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
