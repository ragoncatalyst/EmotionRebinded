using System;
using System.IO.Ports;
using System.Text.RegularExpressions;
using UnityEngine;

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

        [Header("Nine Grids")]
        [Tooltip("九宫格A：索引0..8 对应 1..9 号按钮")] public NineButtons[] gridA = new NineButtons[9];
        [Tooltip("九宫格B：索引0..8 对应 1..9 号按钮")] public NineButtons[] gridB = new NineButtons[9];
        [Tooltip("将信号分发到哪一组：A、B、或同时 Both")] public TargetGrid target = TargetGrid.A;

        public enum TargetGrid { A, B, Both }

        private SerialPort serial;
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
            try
            {
                if (serial != null && serial.IsOpen) serial.Close();
                serial = new SerialPort(portName, baudRate);
                serial.ReadTimeout = 50;
                serial.Open();
                Debug.Log($"[ArduinoInputBridge] 串口已打开: {portName} @ {baudRate}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ArduinoInputBridge] 打开串口失败: {e.Message}");
            }
        }

        private void OnDestroy()
        {
            try { if (serial != null && serial.IsOpen) serial.Close(); } catch { }
        }

        private void Update()
        {
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
                Debug.LogWarning($"[ArduinoInputBridge] 串口读取异常: {e.Message}");
            }
        }

        private void ParseLine(string line)
        {
            // 例："【按下】按钮3 - 左下"
            var mPress = pressRegex.Match(line);
            if (mPress.Success)
            {
                int idx = Mathf.Clamp(ParseIndex(mPress.Groups[1].Value) - 1, 0, 8);
                DispatchPress(idx);
                return;
            }

            // 例："【释放】按钮3"（暂未发送，但保留）
            var mRelease = releaseRegex.Match(line);
            if (mRelease.Success)
            {
                int idx = Mathf.Clamp(ParseIndex(mRelease.Groups[1].Value) - 1, 0, 8);
                DispatchRelease(idx);
            }
        }

        private int ParseIndex(string s)
        {
            int n; return int.TryParse(s, out n) ? n : 1;
        }

        private void DispatchPress(int idx)
        {
            if (target == TargetGrid.A || target == TargetGrid.Both)
            {
                SafePress(gridA, idx);
            }
            if (target == TargetGrid.B || target == TargetGrid.Both)
            {
                SafePress(gridB, idx);
            }
        }

        private void DispatchRelease(int idx)
        {
            if (target == TargetGrid.A || target == TargetGrid.Both)
            {
                SafeRelease(gridA, idx);
            }
            if (target == TargetGrid.B || target == TargetGrid.Both)
            {
                SafeRelease(gridB, idx);
            }
        }

        private void SafePress(NineButtons[] grid, int idx)
        {
            if (grid == null || idx < 0 || idx >= grid.Length) return;
            var b = grid[idx];
            if (b == null) return;
            b.TryPressOrQueue();
        }

        private void SafeRelease(NineButtons[] grid, int idx)
        {
            if (grid == null || idx < 0 || idx >= grid.Length) return;
            var b = grid[idx];
            if (b == null) return;
            // 若有专门的释放逻辑可在 NineButtons 中添加并调用；当前无需处理
        }
    }
}


