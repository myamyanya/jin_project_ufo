using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 控制 UI Image 的显示与隐藏控制器：
/// 1. 按下指定 Button（或快捷键）时激活 (Activate) 目标 UI Image；
/// 2. 当目标 UI Image 处于显示状态时，鼠标在屏幕任意位置点击均会将其关闭 (Disable)；
/// 3. 具备同一帧防误触逻辑，防止点击按钮打开的瞬间立即被自身点击判定关闭。
/// </summary>
public class PopupImageController : MonoBehaviour
{
    [Header("目标 UI 元素 (Target UI)")]
    [Tooltip("需要显示/隐藏的目标 UI Image（或包含 Image 的弹窗 GameObject）")]
    public GameObject targetImage;

    [Header("触发按钮 (Trigger Button)")]
    [Tooltip("点击后激活 targetImage 的 UI 按钮（可选，若为空也可通过外部 OnClick 事件调用 ActivateImage）")]
    public Button triggerButton;

    [Header("按键与触发设置 (Settings)")]
    [Tooltip("可选键盘快捷键（例如 KeyCode.H 或 KeyCode.I，若设为 None 则仅通过 UI 按钮触发）")]
    public KeyCode toggleKey = KeyCode.None;

    [Tooltip("开启状态下，点击鼠标任意按键是否关闭（左键/右键）")]
    public bool closeOnAnyMouseClick = true;

    [Tooltip("开启状态下，按下 ESC 键是否关闭")]
    public bool closeOnEscape = true;

    // 记录打开时的帧数，防止点击按钮打开的同一帧内因检测到鼠标按下又立即关闭
    private int openedFrame = -1;

    /// <summary>
    /// 当前目标图片是否正处于显示/激活状态
    /// </summary>
    public bool IsOpen => targetImage != null && targetImage.activeSelf;

    void Awake()
    {
        // 确保启动时目标图片默认处于禁用状态
        if (targetImage != null && targetImage.activeSelf)
        {
            targetImage.SetActive(false);
        }
    }

    void Start()
    {
        // 如果在 Inspector 中指定了 triggerButton，则自动绑定点击事件
        if (triggerButton != null)
        {
            triggerButton.onClick.AddListener(ActivateImage);
        }
    }

    void OnDestroy()
    {
        if (triggerButton != null)
        {
            triggerButton.onClick.RemoveListener(ActivateImage);
        }
    }

    void Update()
    {
        // 1. 如果配置了键盘按键，按下时激活/切换
        if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
        {
            if (IsOpen)
            {
                DisableImage();
            }
            else
            {
                ActivateImage();
            }
            return;
        }

        // 2. 如果目标图片正在显示，监听鼠标在任意位置的点击并关闭
        if (IsOpen)
        {
            // 防误触保护：必须在打开帧之后才响应关闭点击
            if (Time.frameCount > openedFrame)
            {
                bool mouseClicked = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
                bool escapePressed = closeOnEscape && Input.GetKeyDown(KeyCode.Escape);

                if ((closeOnAnyMouseClick && mouseClicked) || escapePressed)
                {
                    DisableImage();
                }
            }
        }
    }

    /// <summary>
    /// 激活目标 UI Image
    /// </summary>
    public void ActivateImage()
    {
        if (targetImage != null)
        {
            targetImage.SetActive(true);
            openedFrame = Time.frameCount; // 记录打开所在帧
        }
    }

    /// <summary>
    /// 禁用目标 UI Image
    /// </summary>
    public void DisableImage()
    {
        if (targetImage != null)
        {
            targetImage.SetActive(false);
        }
    }

    /// <summary>
    /// 切换目标 UI Image 状态
    /// </summary>
    public void ToggleImage()
    {
        if (IsOpen)
        {
            DisableImage();
        }
        else
        {
            ActivateImage();
        }
    }
}
