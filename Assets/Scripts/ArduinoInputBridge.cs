using System;
using System.Text.RegularExpressions;
using UnityEngine;
using MyGame.UI;
#if USE_SYSTEM_IO_PORTS
using System.IO.Ports; // 需在 Player Settings 的 Scripting Define Symbols 添加 USE_SYSTEM_IO_PORTS 才会启用
#endif

namespace MyGame.IO
{
    /// <summary>
    /// 读取 Arduino 串口输出，将“按钮1..9 按下/释放”映射到两个九宫格按钮阵列。
    /// - 将本脚本挂在场景的 GameObject（如 GameManager）上
    /// - 在 Inspector 里设置串口名称与波特率
    /// - 在两个数组里依序分配 9 个 `NineButtons`（左/右或上/下阵列）
    /// - 当串口行包含“【按下】按钮N”时，调用对应 NineButtons 的按下逻辑；释放同理
    /// </summary>
    public class ArduinoInputBridge : MonoBehaviour
    {
        [Header("Serial Port")]
        [Tooltip("串口名：macOS 通常为 /dev/tty.usbmodemXXX 或 /dev/tty.usbserialXXX；Windows 为 COM3/COM4 等。")]
        public string portName = "/dev/tty.usbmodem";
        public int baudRate = 9600;
        public bool autoConnectOnStart = true;

        [Header("Key Mapping (Arduino→Keyboard)")]
        [Tooltip("将 Arduino 按钮(2..10) 映射为键盘按键 QWEASDZXC")] public bool enableKeyMapping = true;

        #if USE_SYSTEM_IO_PORTS
        private SerialPort serial;
        #endif
        private readonly Regex pressRegex = new Regex("【按下】按钮(\\d+)", RegexOptions.Compiled);
        // 放置为将来扩展：目前 Arduino 未发送“释放”事件内容，但接口预留
        private readonly Regex releaseRegex = new Regex("【释放】按钮(\\d+)", RegexOptions.Compiled);

        private void Start()
        {
            if (autoConnectOnStart)
            {
                TryOpen();
            }
        }

        public void TryOpen()
        {
            #if USE_SYSTEM_IO_PORTS
            try
            {
                if (serial != null && serial.IsOpen) serial.Close();
                serial = new SerialPort(portName, baudRate);
                serial.ReadTimeout = 50;
                serial.Open();
                // Debug.Log($"[ArduinoInputBridge] 串口已打开: {portName} @ {baudRate}");
            }
            catch (Exception e)
            {
                // Debug.LogWarning($"[ArduinoInputBridge] 打开串口失败: {e.Message}");
            }
            #else
            // Debug.LogWarning("[ArduinoInputBridge] 未启用 USE_SYSTEM_IO_PORTS，已跳过串口打开。");
            #endif
        }

        private void OnDestroy()
        {
            #if USE_SYSTEM_IO_PORTS
            try { if (serial != null && serial.IsOpen) serial.Close(); } catch { }
            #endif
        }

        private void Update()
        {
            #if USE_SYSTEM_IO_PORTS
            if (serial == null || !serial.IsOpen) return;
            try
            {
                while (serial.BytesToRead > 0)
                {
                    string line = serial.ReadLine();
                    if (string.IsNullOrEmpty(line)) continue;
                    ParseLine(line);
                }
            }
            catch (TimeoutException)
            {
                // ignore
            }
            catch (Exception e)
            {
                // Debug.LogWarning($"[ArduinoInputBridge] 串口读取异常: {e.Message}");
            }
            #endif
        }

        private void ParseLine(string line)
        {
            // 例："【按下】按钮3 - 左下"
            var mPress = pressRegex.Match(line);
            if (mPress.Success)
            {
                int idx = Mathf.Clamp(ParseIndex(mPress.Groups[1].Value) - 1, 0, 8);
                HandlePressFromArduinoIndex(idx + 1); // 还原为1..9/10编号
                return;
            }

            // 例："【释放】按钮3"（暂未发送，但保留）
            var mRelease = releaseRegex.Match(line);
            if (mRelease.Success)
            {
                int idx = Mathf.Clamp(ParseIndex(mRelease.Groups[1].Value) - 1, 0, 8);
                HandleReleaseFromArduinoIndex(idx + 1);
            }
        }

        private int ParseIndex(string s)
        {
            int n; return int.TryParse(s, out n) ? n : 1;
        }

        private void HandlePressFromArduinoIndex(int oneBasedIndex)
        {
            if (!enableKeyMapping) return;
            KeyCode key = MapArduinoToKey(oneBasedIndex);
            if (key == KeyCode.None) return;
            string dir = GetDirectionName(oneBasedIndex);
            // Debug.Log($"[ArduinoInputBridge] ▶ 按下方位: {dir} | 映射键: {key}");

            // 1) 触发战斗九宫格中绑定该键的按钮
            var buttons = GameObject.FindObjectsOfType<NineButtons>(true);
            int hitCount = 0;
            foreach (var b in buttons)
            {
                if (b != null && b.boundKey == key)
                {
                    string skillId = b.boundSkillId;
                    var info = SkillDatabase.GetSkillInfo(skillId);
                    string skillName = info != null ? info.name : skillId;
                    // Debug.Log($"[ArduinoInputBridge]   → 触发战斗按钮 {b.gameObject.name} | 技能: {skillName}({skillId}) | 冷却: {b.cooldownSeconds}s");
                    b.TryPressOrQueue();
                    hitCount++;
                }
            }
            if (hitCount == 0)
            {
                // Debug.LogWarning($"[ArduinoInputBridge]   → 未找到绑定 {key} 的战斗按钮");
            }

            // 2) 如果升级面板开启，触发对应选择按钮（其键绑定等于目标键）
            var selects = GameObject.FindObjectsOfType<NineSelectionButtons>(true);
            int selHits = 0;
            foreach (var s in selects)
            {
                if (s != null && s.GetTargetKey() == key)
                {
                    // Debug.Log($"[ArduinoInputBridge]   → 触发升级选项按钮 {s.gameObject.name} (键 {key})");
                    s.TriggerSelect();
                    selHits++;
                }
            }
            if (selHits > 0)
            {
                // Debug.Log($"[ArduinoInputBridge]   → 升级面板响应 {selHits} 个选项");
            }
        }

        private void HandleReleaseFromArduinoIndex(int oneBasedIndex)
        {
            // 当前未需要处理释放；接口预留
        }

        private KeyCode MapArduinoToKey(int arduinoIndex)
        {
            // 硬件顺序（固定，来自你提供的中文列表）：
            // 1: 右下, 2: 正下, 3: 左下, 4: 正右, 5: 中间, 6: 正左, 7: 右上, 8: 正上, 9: 左上
            // 在 QWE / ASD / ZXC 九宫格中的对应：
            // 右下(C3)=C, 正下(C2)=X, 左下(C1)=Z, 正右(B3)=D, 中间(B2)=S, 正左(B1)=A, 右上(A3)=E, 正上(A2)=W, 左上(A1)=Q
            switch (arduinoIndex)
            {
                case 1: return KeyCode.C; // 右下
                case 2: return KeyCode.X; // 正下
                case 3: return KeyCode.Z; // 左下
                case 4: return KeyCode.D; // 正右
                case 5: return KeyCode.S; // 中间
                case 6: return KeyCode.A; // 正左
                case 7: return KeyCode.E; // 右上
                case 8: return KeyCode.W; // 正上
                case 9: return KeyCode.Q; // 左上
                default: return KeyCode.None;
            }
        }

        private string GetDirectionName(int arduinoIndex)
        {
            switch (arduinoIndex)
            {
                case 1: return "右下";
                case 2: return "正下";
                case 3: return "左下";
                case 4: return "正右";
                case 5: return "中间";
                case 6: return "正左";
                case 7: return "右上";
                case 8: return "正上";
                case 9: return "左上";
                default: return $"未知({arduinoIndex})";
            }
        }
    }
}


