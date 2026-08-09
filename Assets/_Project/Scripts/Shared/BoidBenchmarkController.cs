using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// 모든 브랜치(naive / grid / soa / job-burst)에서 공통으로 사용하는 벤치마크 하네스.
/// 씬에 하나만 배치하면 됨. 각 브랜치의 알고리즘 구현과는 완전히 독립적이며,
/// 로직 계산 구간만 BeginLogicSample()/EndLogicSample()로 감싸주면 됨.
///
/// 사용 예 (naive 브랜치, Boid.Update() 안):
///   BoidBenchmarkController.Instance?.BeginLogicSample();
///   // ... 이웃 탐색 + 힘 계산 ...
///   BoidBenchmarkController.Instance?.EndLogicSample();
///
/// 사용 예 (grid/soa 브랜치, 매니저의 한 번의 업데이트 호출 전후):
///   BoidBenchmarkController.Instance?.BeginLogicSample();
///   BoidManager.TickAll();
///   BoidBenchmarkController.Instance?.EndLogicSample();
///
/// 주의: 여러 보이드가 각자 Update()에서 샘플을 나눠 호출하는 구조(naive)라면,
/// Begin/End를 프레임당 여러 번 호출해도 됨 - 내부적으로 프레임 단위 누적 후 리셋됨.
/// </summary>
public class BoidBenchmarkController : MonoBehaviour
{
    public static BoidBenchmarkController Instance { get; private set; }

    [Header("환경 설정")]
    [SerializeField] private int _randomSeed = 42;
    [SerializeField] private int _targetFrameRate = -1; // -1 = uncapped
    [SerializeField] private bool _disableVSync = true;

    [Header("측정 설정")]
    [SerializeField] private int _warmupFrames = 180;       // 약 3초 (60fps 기준)
    [SerializeField] private int _measurementFrames = 600;  // 약 10초
    [SerializeField] private string _presetLabel = "naive-1000";

    [Header("오버레이")]
    [SerializeField] private bool _showOverlay = true;

    private enum Phase { Warmup, Measuring, Done }
    private Phase _phase = Phase.Warmup;
    private int _frameCounter;

    private readonly List<double> _frameTimesMs = new List<double>();
    private readonly List<double> _logicTimesMs = new List<double>();

    private readonly Stopwatch _logicStopwatch = new Stopwatch();
    private double _currentFrameLogicMs;
    private bool _stopwatchRunning;

    // 러닝 합계 (매 프레임 O(n) 재계산 방지)
    private double _frameSumMs;
    private double _logicSumMs;
    private double _minFrameMs = double.MaxValue;
    private double _maxFrameMs = double.MinValue;
    private double _lastFrameMs;

    public bool IsDone => _phase == Phase.Done;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        ApplyEnvironmentSettings();
    }

    private void ApplyEnvironmentSettings()
    {
        QualitySettings.vSyncCount = _disableVSync ? 0 : 1;
        Application.targetFrameRate = _targetFrameRate;
        UnityEngine.Random.InitState(_randomSeed);
    }

    /// <summary>보이드 로직 계산 시작 시점에 호출.</summary>
    public void BeginLogicSample()
    {
        if (_phase != Phase.Measuring) return;
        _logicStopwatch.Restart();
        _stopwatchRunning = true;
    }

    /// <summary>보이드 로직 계산 종료 시점에 호출.</summary>
    public void EndLogicSample()
    {
        if (!_stopwatchRunning) return;
        _logicStopwatch.Stop();
        _currentFrameLogicMs += _logicStopwatch.Elapsed.TotalMilliseconds;
        _stopwatchRunning = false;
    }

    private void Update()
    {
        switch (_phase)
        {
            case Phase.Warmup:
                _frameCounter++;
                if (_frameCounter >= _warmupFrames)
                {
                    _phase = Phase.Measuring;
                    _frameCounter = 0;
                }
                break;

            case Phase.Measuring:
                // 이 시점에는 이번 프레임의 Begin/End 샘플이 이미 끝나 있어야 함
                // (naive: Update, grid/soa/job: 각 브랜치의 boid tick 이후)
                break;
        }
    }

    private void LateUpdate()
    {
        if (_phase == Phase.Measuring)
        {
            RecordFrame();
            _frameCounter++;
            if (_frameCounter >= _measurementFrames)
            {
                _phase = Phase.Done;
                WriteCsv();
            }
        }

        // 다음 프레임을 위해 로직 누적값 리셋
        _currentFrameLogicMs = 0;
    }

    private void RecordFrame()
    {
        double frameMs = Time.unscaledDeltaTime * 1000.0;
        _lastFrameMs = frameMs;

        _frameTimesMs.Add(frameMs);
        _logicTimesMs.Add(_currentFrameLogicMs);

        _frameSumMs += frameMs;
        _logicSumMs += _currentFrameLogicMs;

        if (frameMs < _minFrameMs) _minFrameMs = frameMs;
        if (frameMs > _maxFrameMs) _maxFrameMs = frameMs;
    }

    private double AvgFrameMs => _frameTimesMs.Count > 0 ? _frameSumMs / _frameTimesMs.Count : 0;
    private double AvgLogicMs => _logicTimesMs.Count > 0 ? _logicSumMs / _logicTimesMs.Count : 0;

    private void WriteCsv()
    {
        try
        {
            string dir = Path.Combine(Application.persistentDataPath, "BoidBenchmarks");
            Directory.CreateDirectory(dir);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"{_presetLabel}_{timestamp}.csv";
            string path = Path.Combine(dir, fileName);

            var sb = new StringBuilder();
            sb.AppendLine("frame_index,frame_ms,logic_ms");
            for (int i = 0; i < _frameTimesMs.Count; i++)
            {
                sb.AppendLine($"{i},{_frameTimesMs[i]:F4},{_logicTimesMs[i]:F4}");
            }
            sb.AppendLine();
            sb.AppendLine($"# preset,{_presetLabel}");
            sb.AppendLine($"# avg_frame_ms,{AvgFrameMs:F4}");
            sb.AppendLine($"# avg_logic_ms,{AvgLogicMs:F4}");
            sb.AppendLine($"# min_frame_ms,{_minFrameMs:F4}");
            sb.AppendLine($"# max_frame_ms,{_maxFrameMs:F4}");
            sb.AppendLine($"# sample_count,{_frameTimesMs.Count}");
            sb.AppendLine($"# unity_version,{Application.unityVersion}");
            sb.AppendLine($"# platform,{Application.platform}");

            File.WriteAllText(path, sb.ToString());

            Debug.Log(
                $"[Benchmark] CSV 저장 완료: {path}\n" +
                $"평균 프레임: {AvgFrameMs:F3}ms, 평균 로직: {AvgLogicMs:F3}ms, " +
                $"min={_minFrameMs:F3}ms max={_maxFrameMs:F3}ms, n={_frameTimesMs.Count}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Benchmark] CSV 저장 실패: {e}");
        }
    }

    private void OnGUI()
    {
        if (!_showOverlay) return;

        const int width = 340;
        const int height = 160;
        GUI.Box(new Rect(10, 10, width, height), "");

        var style = new GUIStyle(GUI.skin.label) { fontSize = 16 };
        int y = 15;
        int total = _phase == Phase.Warmup ? _warmupFrames : _measurementFrames;

        GUI.Label(new Rect(20, y, width - 20, 20), $"Preset: {_presetLabel}", style); y += 20;
        GUI.Label(new Rect(20, y, width - 20, 20), $"Phase: {_phase} ({_frameCounter}/{total})", style); y += 20;
        GUI.Label(new Rect(20, y, width - 20, 20), $"Frame: {_lastFrameMs:F2} ms  ({SafeFps(_lastFrameMs):F0} fps)", style); y += 20;
        GUI.Label(new Rect(20, y, width - 20, 20), $"Avg Frame: {AvgFrameMs:F2} ms", style); y += 20;
        GUI.Label(new Rect(20, y, width - 20, 20), $"Avg Logic: {AvgLogicMs:F2} ms", style); y += 20;
        GUI.Label(new Rect(20, y, width - 20, 20), $"Min/Max: {_minFrameMs:F2} / {_maxFrameMs:F2} ms", style); y += 20;

        if (_phase == Phase.Done)
        {
            GUI.Label(new Rect(20, y, width - 20, 20), "측정 완료 (CSV 저장됨)", style);
        }
    }

    private static double SafeFps(double frameMs) => frameMs > 0.0001 ? 1000.0 / frameMs : 0;
}