using UnityEngine;

/// <summary>
/// 全局临时日志静音器：启动时关闭 Unity 的 Debug 输出，
/// 仅供当前调试期使用。需要输出的脚本可在内部短暂开启再关闭。
/// </summary>
[DefaultExecutionOrder(-10000)]
public class GlobalLogSilencer : MonoBehaviour
{
    [Tooltip("进入播放模式时是否自动关闭所有 Debug 日志输出")] public bool muteOnStart = true;
    [Tooltip("在播放模式结束时恢复日志输出")] public bool restoreOnQuit = true;

    private void Awake()
    {
        if (muteOnStart) Debug.unityLogger.logEnabled = false;
    }

    private void OnDestroy()
    {
        if (restoreOnQuit) Debug.unityLogger.logEnabled = true;
    }
}


