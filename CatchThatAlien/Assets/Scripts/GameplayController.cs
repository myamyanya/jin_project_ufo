using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class GameplayController : MonoBehaviour
{
    [Header("Core References")]
    [Tooltip("关联上一步做的 GameManager")]
    public GameManager gameManager;
    [Tooltip("关联 ArduinoBridge 读取数据")]
    public ArduinoBridge arduinoBridge;

    [Header("1. Thermometer (Button A0)")]
    public RectTransform thermometerUI;
    [Tooltip("UI 升起的目标 Y 轴坐标（相对锚点）")]
    public float thermoTargetY = 150f; 
    public TextMeshProUGUI temperatureText;
    public string targetTemperature = "102°";

    [Header("2. Megaphone (Sound A1)")]
    public RectTransform megaphoneUI;
    [Tooltip("UI 升起的目标 Y 轴坐标")]
    public float megaTargetY = 150f;
    [Tooltip("拖入大喇叭的 AudioSource (需挂载音效，关闭 Play On Awake)")]
    public AudioSource megaphoneAudio;
    [Tooltip("喇叭UI保持升起的时间（即使声音没了也保持一下，防抖动）")]
    public float megaphoneKeepAliveTime = 2f;
    private float megaTimer = 0f;

    [Header("3. Light Control (Light A3)")]
    public RectTransform lightUI;
    [Tooltip("UI 升起的目标 Y 轴坐标")]
    public float lightTargetY = 150f;
    [Tooltip("光敏阈值：低于此值时关灯")]
    public int lightThreshold = 400;
    [Tooltip("把需要关掉的 Directional Light 和 Point Lights 拖进来")]
    public List<Light> environmentLights;

    [Header("4. Fog Spray (FSR A4, A5)")]
    public RectTransform spray1UI;
    public RectTransform spray2UI;
    [Tooltip("UI 升起的目标 Y 轴坐标")]
    public float sprayTargetY = 150f;
    
    [Tooltip("压感死区阈值（低于此值不算按压）")]
    public int fsrMinThreshold = 50;
    [Tooltip("压感满载阈值（达到此值雾气最浓）")]
    public int fsrMaxThreshold = 900;
    
    [Tooltip("最大雾气浓度 (Fog Density)")]
    public float maxFogDensity = 0.08f;
    
    [Tooltip("压感1 (A4) 对应的喷雾颜色")]
    public Color colorFSR1 = Color.blue;
    [Tooltip("压感2 (A5) 对应的喷雾颜色")]
    public Color colorFSR2 = Color.red;

    [Header("5. Heartbeat Vibration (A2)")]
    [Tooltip("心跳震动间隔时间（秒）")]
    public float heartbeatInterval = 1f;
    private Coroutine heartbeatCoroutine;

    // ==========================================
    // 内部状态变量
    // ==========================================
    private Vector2 thermoStartPos;
    private Vector2 megaStartPos;
    private Vector2 lightStartPos;
    private Vector2 spray1StartPos;
    private Vector2 spray2StartPos;
    
    private bool wasFogEnabledBeforeGame;

    void Start()
    {
        // 记录三个 UI 工具的初始位置（它们默认应该放在屏幕底部的界外，被隐藏住）
        if (thermometerUI != null) thermoStartPos = thermometerUI.anchoredPosition;
        if (megaphoneUI != null) megaStartPos = megaphoneUI.anchoredPosition;
        if (lightUI != null) lightStartPos = lightUI.anchoredPosition;
        if (spray1UI != null) spray1StartPos = spray1UI.anchoredPosition;
        if (spray2UI != null) spray2StartPos = spray2UI.anchoredPosition;
        
        // 记录初始雾气状态
        wasFogEnabledBeforeGame = RenderSettings.fog;
    }

    void Update()
    {
        // 只有在游戏进行中 (Playing) 才处理数据
        if (gameManager != null && gameManager.currentState != GameManager.GameState.Playing)
        {
            // 游戏未开始或已结束，重置状态
            StopHeartbeat();
            HideAllUIs();
            ResetEnvironment();
            return;
        }

        // 确保心跳在跳动
        StartHeartbeatIfNeeded();

        if (arduinoBridge != null)
        {
            HandleThermometer();
            HandleMegaphone();
            HandleLights();
            HandleFogSpray();
        }
    }

    // ==========================================
    // 1. 体温计逻辑 (Button A0)
    // ==========================================
    private void HandleThermometer()
    {
        if (thermometerUI == null) return;

        // 如果按住按钮 (buttonState == 1)
        bool isActive = arduinoBridge.buttonState == 1;
        
        Vector2 targetPos = isActive ? new Vector2(thermoStartPos.x, thermoTargetY) : thermoStartPos;
        // 使用 Lerp 实现平滑升降
        thermometerUI.anchoredPosition = Vector2.Lerp(thermometerUI.anchoredPosition, targetPos, Time.deltaTime * 10f);

        if (isActive && temperatureText != null)
        {
            temperatureText.text = targetTemperature;
        }
    }

    // ==========================================
    // 2. 扩音器逻辑 (Sound A1)
    // ==========================================
    private void HandleMegaphone()
    {
        if (megaphoneUI == null) return;

        bool hasSound = arduinoBridge.soundLevel == 1;

        if (hasSound)
        {
            megaTimer = megaphoneKeepAliveTime;
            
            // 播放音效 (如果没在播放)
            if (megaphoneAudio != null && !megaphoneAudio.isPlaying)
            {
                megaphoneAudio.Play();
            }
        }

        if (megaTimer > 0)
        {
            megaTimer -= Time.deltaTime;
        }

        bool isActive = megaTimer > 0;
        Vector2 targetPos = isActive ? new Vector2(megaStartPos.x, megaTargetY) : megaStartPos;
        megaphoneUI.anchoredPosition = Vector2.Lerp(megaphoneUI.anchoredPosition, targetPos, Time.deltaTime * 10f);
    }

    // ==========================================
    // 3. 灯光控制逻辑 (Light A3)
    // ==========================================
    private void HandleLights()
    {
        // 开关模式：只要光照低于阈值，就关灯；否则开灯
        bool lightsOn = arduinoBridge.lightLevel > lightThreshold;

        // UI 平滑升降 (灯关了的时候 UI 升起)
        if (lightUI != null)
        {
            Vector2 targetPos = !lightsOn ? new Vector2(lightStartPos.x, lightTargetY) : lightStartPos;
            lightUI.anchoredPosition = Vector2.Lerp(lightUI.anchoredPosition, targetPos, Time.deltaTime * 10f);
        }

        foreach (var light in environmentLights)
        {
            if (light != null)
            {
                light.enabled = lightsOn;
            }
        }
    }

    // ==========================================
    // 4. 喷雾逻辑 (FSR A4, A5)
    // ==========================================
    private void HandleFogSpray()
    {
        int fsr1 = arduinoBridge.fsr1Level;
        int fsr2 = arduinoBridge.fsr2Level;

        bool spray1Active = fsr1 > fsrMinThreshold;
        bool spray2Active = fsr2 > fsrMinThreshold;
        bool isActive = spray1Active || spray2Active;

        // UI 平滑升降
        if (spray1UI != null)
        {
            Vector2 targetPos1 = spray1Active ? new Vector2(spray1StartPos.x, sprayTargetY) : spray1StartPos;
            spray1UI.anchoredPosition = Vector2.Lerp(spray1UI.anchoredPosition, targetPos1, Time.deltaTime * 10f);
        }
        
        if (spray2UI != null)
        {
            Vector2 targetPos2 = spray2Active ? new Vector2(spray2StartPos.x, sprayTargetY) : spray2StartPos;
            spray2UI.anchoredPosition = Vector2.Lerp(spray2UI.anchoredPosition, targetPos2, Time.deltaTime * 10f);
        }

        // 雾气渲染处理
        if (isActive)
        {
            RenderSettings.fog = true;

            // 取最大的力气计算浓度渐变
            int maxForce = Mathf.Max(fsr1, fsr2);
            float t = Mathf.InverseLerp(fsrMinThreshold, fsrMaxThreshold, maxForce);
            RenderSettings.fogDensity = Mathf.Lerp(0, maxFogDensity, t);

            // 颜色混合：
            // 我们通过两边按下力气的比例，使用 Color.Lerp 来混合出紫色的中间态
            if (spray1Active && !spray2Active) 
            {
                RenderSettings.fogColor = colorFSR1;
            }
            else if (!spray1Active && spray2Active)
            {
                RenderSettings.fogColor = colorFSR2;
            }
            else
            {
                float totalForce = fsr1 + fsr2;
                float ratio = fsr2 / totalForce; // fsr2 占的比例
                RenderSettings.fogColor = Color.Lerp(colorFSR1, colorFSR2, ratio);
            }
        }
        else
        {
            // 如果没按下，雾气浓度平滑消散
            RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, 0, Time.deltaTime * 5f);
            
            // 浓度过低时彻底关闭雾气节约性能
            if (RenderSettings.fogDensity < 0.001f)
            {
                RenderSettings.fog = false;
            }
        }
    }

    // ==========================================
    // 5. 心跳震动逻辑 (A2)
    // ==========================================
    private void StartHeartbeatIfNeeded()
    {
        if (heartbeatCoroutine == null)
        {
            heartbeatCoroutine = StartCoroutine(HeartbeatRoutine());
        }
    }

    private void StopHeartbeat()
    {
        if (heartbeatCoroutine != null)
        {
            StopCoroutine(heartbeatCoroutine);
            heartbeatCoroutine = null;
        }
    }

    private IEnumerator HeartbeatRoutine()
    {
        while (true)
        {
            // 给 Arduino 发送 'V' 指令
            if (arduinoBridge != null)
            {
                arduinoBridge.TriggerVibration();
            }
            // 等待间隔 (例如1秒)，产生扑通、扑通的效果
            yield return new WaitForSeconds(heartbeatInterval);
        }
    }

    // ==========================================
    // 辅助复位 (游戏结束或菜单时)
    // ==========================================
    private void HideAllUIs()
    {
        if (thermometerUI != null) thermometerUI.anchoredPosition = Vector2.Lerp(thermometerUI.anchoredPosition, thermoStartPos, Time.deltaTime * 10f);
        if (megaphoneUI != null) megaphoneUI.anchoredPosition = Vector2.Lerp(megaphoneUI.anchoredPosition, megaStartPos, Time.deltaTime * 10f);
        if (lightUI != null) lightUI.anchoredPosition = Vector2.Lerp(lightUI.anchoredPosition, lightStartPos, Time.deltaTime * 10f);
        if (spray1UI != null) spray1UI.anchoredPosition = Vector2.Lerp(spray1UI.anchoredPosition, spray1StartPos, Time.deltaTime * 10f);
        if (spray2UI != null) spray2UI.anchoredPosition = Vector2.Lerp(spray2UI.anchoredPosition, spray2StartPos, Time.deltaTime * 10f);
    }

    private void ResetEnvironment()
    {
        // 恢复灯光
        foreach (var light in environmentLights)
        {
            if (light != null) light.enabled = true;
        }
        
        // 恢复雾气
        RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, 0, Time.deltaTime * 5f);
        if (RenderSettings.fogDensity < 0.001f)
        {
            RenderSettings.fog = wasFogEnabledBeforeGame; // 恢复到游戏启动前的默认设置
        }
    }
}
