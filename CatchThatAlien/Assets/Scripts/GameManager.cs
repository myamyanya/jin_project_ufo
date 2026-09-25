using System.Collections;
using UnityEngine;
using TMPro; // 需要引入 TextMeshPro 命名空间

public class GameManager : MonoBehaviour
{
    public enum GameState { StartMenu, Playing, GameOver, Win }
    public enum AnswerOption { Option1, Option2, Option3, Option4, Option5 }
    
    [Header("游戏设置 (Game Settings)")]
    [Tooltip("倒计时秒数")]
    public float countdownSeconds = 120f;
    [Tooltip("幕布升降的动画时长(秒)")]
    public float slideDuration = 1.2f; 
    
    [Header("UI 引用 (UI References)")]
    [Tooltip("开始界面的幕布 (Canvas 里的 Panel)")]
    public RectTransform startCurtain; 
    [Tooltip("失败界面的幕布 (Canvas 里的 Panel)")]
    public RectTransform failCurtain;  
    [Tooltip("显示倒计时的文字")]
    public TextMeshProUGUI timerText;  
    
    [Header("Arduino 关联 (可选)")]
    [Tooltip("拖入挂载了 ArduinoBridge 的物体，可以使用 Arduino 按钮来开始/重试")]
    public ArduinoBridge arduinoBridge; 

    [Header("胜利判定 (Win Condition)")]
    [Tooltip("设置哪一个选项是正确答案")]
    public AnswerOption correctAnswer = AnswerOption.Option1;
    [Tooltip("按 Tab 键弹出的答题面板")]
    public GameObject answerMenuPanel;
    [Tooltip("胜利时显示的画布 (Canvas)")]
    public GameObject winCanvas;
    
    // 当前状态
    public GameState currentState { get; private set; } = GameState.StartMenu;
    public bool isAnswerMenuOpen { get; private set; } = false;
    private float currentTime;
    
    // 记录幕布在屏幕中央的初始位置
    private Vector2 startCurtainOriginalPos;
    private Vector2 failCurtainOriginalPos;

    void Start()
    {
        // 记录两个幕布在屏幕中心的坐标（它们在场景里应该默认摆在正中间挡住画面）
        if (startCurtain != null) startCurtainOriginalPos = startCurtain.anchoredPosition;
        if (failCurtain != null) failCurtainOriginalPos = failCurtain.anchoredPosition;
        
        ResetGame();
    }

    void Update()
    {
        // 获取是否按下了键盘空格，或者鼠标左键点击
        bool actionButtonPressed = Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);

        switch (currentState)
        {
            case GameState.StartMenu:
                // 在开始菜单，按键拉开幕布开始游戏
                if (actionButtonPressed)
                {
                    StartCoroutine(StartGameRoutine());
                }
                break;
                
            case GameState.Playing:
                // Tab 打开/关闭 答题面板
                if (Input.GetKeyDown(KeyCode.Tab))
                {
                    isAnswerMenuOpen = !isAnswerMenuOpen;
                    if (answerMenuPanel != null) answerMenuPanel.SetActive(isAnswerMenuOpen);
                }

                // 如果答题面板打开了，拦截 1-5 键作为答题输入
                if (isAnswerMenuOpen)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) SelectOption(AnswerOption.Option1);
                    else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) SelectOption(AnswerOption.Option2);
                    else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) SelectOption(AnswerOption.Option3);
                    else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) SelectOption(AnswerOption.Option4);
                    else if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5)) SelectOption(AnswerOption.Option5);
                    // 倒计时依然继续
                }

                // 游戏中，执行倒计时
                currentTime -= Time.deltaTime;
                UpdateTimerUI();
                
                if (currentTime <= 0)
                {
                    currentTime = 0;
                    UpdateTimerUI();
                    GameOver(); // 时间到，失败！
                }
                break;
                
            case GameState.GameOver:
                // 失败菜单，按键拉起幕布重试
                if (actionButtonPressed)
                {
                    StartCoroutine(RetryGameRoutine());
                }
                break;
        }
    }

    private void UpdateTimerUI()
    {
        if (timerText != null)
        {
            // 只有在游玩状态下才显示倒计时
            timerText.gameObject.SetActive(currentState == GameState.Playing);
            
            // 将秒数转换为 分:秒 的格式 (例如 02:00)
            int minutes = Mathf.FloorToInt(currentTime / 60F);
            int seconds = Mathf.FloorToInt(currentTime - minutes * 60);
            timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }
    }

    // --- 游戏流程与动画 ---

    private IEnumerator StartGameRoutine()
    {
        currentState = (GameState)999; // 临时状态，防止在动画播放时重复触发按键
        
        // 开始幕布升起
        yield return StartCoroutine(SlideCurtain(startCurtain, moveUp: true));
        
        // 动画结束，进入游玩状态，开始倒计时
        currentTime = countdownSeconds;
        currentState = GameState.Playing;
        UpdateTimerUI(); // 开启倒计时显示
    }

    private IEnumerator GameOverRoutine()
    {
        currentState = (GameState)999;
        
        // 游戏结束，隐藏倒计时
        if (timerText != null) timerText.gameObject.SetActive(false);
        
        // 失败幕布降下 (从屏幕上方降回屏幕中央)
        yield return StartCoroutine(SlideCurtain(failCurtain, moveUp: false, startFromTop: true));
        
        currentState = GameState.GameOver;
    }

    private IEnumerator RetryGameRoutine()
    {
        currentState = (GameState)999;
        
        // 在失败幕布升起之前，提前把开始幕布“瞬间”放回原位（挡在下面）
        // 这样失败幕布升起时，露出来的就是开始界面，而不是游戏画面了
        if (startCurtain != null) 
        {
            startCurtain.anchoredPosition = startCurtainOriginalPos;
        }
        
        // 失败幕布重新升起
        yield return StartCoroutine(SlideCurtain(failCurtain, moveUp: true));
        
        // 动画结束后，执行彻底的内部重置
        ResetGame();
    }
    
    public void SelectOptionByIndex(int index)
    {
        SelectOption((AnswerOption)index);
    }

    private void SelectOption(AnswerOption chosen)
    {
        if (currentState != GameState.Playing) return;

        if (chosen == correctAnswer)
        {
            WinGame();
        }
        else
        {
            GameOver(); // 失败
        }
    }

    public void GameOver()
    {
        if (currentState != GameState.Playing) return;
        StartCoroutine(GameOverRoutine());
    }

    private void WinGame()
    {
        currentState = GameState.Win;
        isAnswerMenuOpen = false;
        
        if (answerMenuPanel != null) answerMenuPanel.SetActive(false);
        if (timerText != null) timerText.gameObject.SetActive(false);
        if (winCanvas != null) winCanvas.SetActive(true);
        // 胜利时画面直接冻结，不播放幕布动画
    }

    private void ResetGame()
    {
        // 恢复满时间
        currentTime = countdownSeconds;
        currentState = GameState.StartMenu; // 提前设置状态，以便 UpdateTimerUI 正确隐藏
        isAnswerMenuOpen = false;

        UpdateTimerUI();
        
        if (answerMenuPanel != null) answerMenuPanel.SetActive(false);
        if (winCanvas != null) winCanvas.SetActive(false);
        
        // 开始幕布回到屏幕中央（防备首次运行）
        if (startCurtain != null) 
        {
            startCurtain.anchoredPosition = startCurtainOriginalPos;
        }
        
        // 失败幕布放到屏幕正上方（等待失败时掉下来）
        if (failCurtain != null)
        {
            float screenHeight = failCurtain.rect.height;
            failCurtain.anchoredPosition = failCurtainOriginalPos + new Vector2(0, screenHeight);
        }
    }
    

    // --- 核心滑动协程 ---
    private IEnumerator SlideCurtain(RectTransform curtain, bool moveUp, bool startFromTop = false)
    {
        if (curtain == null) yield break;

        float elapsedTime = 0f;
        float height = curtain.rect.height; // 获取幕布的高度（通常等同于屏幕高度）
        
        // 确定是哪个幕布，以拿到它的基准坐标
        Vector2 originalPos = (curtain == startCurtain) ? startCurtainOriginalPos : failCurtainOriginalPos;
        
        // 起点和终点计算
        Vector2 startPos = startFromTop ? originalPos + new Vector2(0, height) : originalPos;
        Vector2 endPos = moveUp ? originalPos + new Vector2(0, height) : originalPos;

        // 如果要从天而降，先把它的坐标瞬移到天花板
        if (startFromTop && !moveUp) 
        {
            curtain.anchoredPosition = startPos;
        }

        // 逐帧平滑移动
        while (elapsedTime < slideDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / slideDuration);
            
            // 简单的平滑缓动曲线 (Ease In Out)，让滑动不死板
            t = t * t * (3f - 2f * t);
            
            curtain.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            yield return null; // 等待下一帧
        }
        
        // 确保最终位置严丝合缝
        curtain.anchoredPosition = endPos;
    }
}
