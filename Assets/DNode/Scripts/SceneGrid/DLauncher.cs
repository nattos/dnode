using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;

namespace DNode {
  public class DLauncher : DIOOutputNode, IDLaunchable {
    private static System.Random _random = new System.Random();

    private struct QuantizedLaunchHelper {
      private double _previousTime;

      private double CurrentTime => DScriptMachine.CurrentInstance.Transport.Beat;

      public void Reset() {
        _previousTime = CurrentTime;
      }

      public bool Step(double quantization, out double quantizedPercent) {
        if (quantization < UnityUtils.DefaultEpsilon) {
          quantizedPercent = 1.0;
          return true;
        }
        double currentTime = CurrentTime;
        double currentQuantizedTime = currentTime / quantization;
        double currentQuantizedIndex = Math.Floor(currentQuantizedTime);
        bool triggered = Math.Floor(_previousTime / quantization) != currentQuantizedIndex;
        _previousTime = currentTime;
        quantizedPercent = currentQuantizedTime - currentQuantizedIndex;
        return triggered;
      }
    }

    private struct PlayHelper {
      private double CurrentTime => DScriptMachine.CurrentInstance.Transport.Beat;

      private double _launchTime;
      private double _previousTime;

      public bool Step(ref DLaunchOptions options, out double nextLaunchTime, out double playQuantizationPercent) {
        double currentTime = CurrentTime - _launchTime;
        double lengthBeats = options.LengthBeats < UnityUtils.DefaultEpsilon ? 4.0 : Math.Max(UnityUtils.DefaultEpsilon, options.LengthBeats);
        double previousPatternIndex = Math.Floor(_previousTime / lengthBeats);
        double nextPatternPos = currentTime / lengthBeats;
        double nextPatternIndex = Math.Floor(nextPatternPos);
        bool triggered = previousPatternIndex != nextPatternIndex;
        _previousTime = currentTime;
        nextLaunchTime = _launchTime + nextPatternIndex * lengthBeats;
        playQuantizationPercent = nextPatternPos - nextPatternIndex;
        return triggered;
      }

      public void Reset(double quantization) {
        if (quantization < UnityUtils.DefaultEpsilon) {
          _launchTime = CurrentTime;
        } else {
          double currentTime = CurrentTime;
          _launchTime = Math.Floor(currentTime / quantization) * quantization;
        }
        _previousTime = 0.0;
      }

      public void ResetAtLaunchTime(double launchTime) {
        _launchTime = launchTime;
        _previousTime = 0.0;
      }
    }

    [DoNotSerialize] public ValueInput NameInput;
    [DoNotSerialize] public ValueInput FirstHeaderInput;
    [DoNotSerialize] public ValueInput FirstSceneInput;
    [DoNotSerialize][PortLabelHidden][PortKey("CustomTrigger")] public ValueInput CustomTriggerInput;
    [DoNotSerialize][PortLabelHidden] public ValueOutput result;

    [DoNotSerialize] public string Name;
    [DoNotSerialize] public DLaunchHeader FirstHeader;
    [DoNotSerialize] public DLaunchScene FirstScene;

    [Inspectable] public DGlobalLaunchQuantization GlobalLaunchQuantization = DGlobalLaunchQuantization.Bar;
    [DoNotSerialize] public double GlobalLaunchQuantizationValue;

    private List<ValueOutput> _cachedInputConnections = new List<ValueOutput>();
    private List<ValueOutput> _cachedOtherInputConnections = new List<ValueOutput>();

    private bool _allStopTriggered;
    bool IDLaunchable.Triggered { get { return _allStopTriggered; } set { _allStopTriggered = value; } }
    bool IDLaunchable.StatusPlaying { get { return false; } set {} }
    bool IDLaunchable.StatusQueued { get { return false; } set {} }
    double IDLaunchable.StatusPlayingQuantizationPercent { get; set; }
    double IDLaunchable.StatusQueuedQuantizationPercent { get; set; }
    int IDLaunchable.StatusLaunchedOnFrameNumber { get; set; } = -1;
    string IDLaunchable.LaunchLabelOverride => "All Stop";
    bool IDLaunchable.HasInput => false;
    DLaunchQuantization IDLaunchable.LaunchOptionQuantization => DLaunchQuantization.Global;

    private Func<Flow, DLauncher> _computeLayoutFromFlow;
    private bool _isInitialized = false;
    private DLaunchHeader[] _headers = new DLaunchHeader[0];
    private DLaunchCell[,] _cells = new DLaunchCell[0, 0];
    private DLaunchScene[] _scenes = new DLaunchScene[0];
    private int?[] _playingScene = new int?[0];
    private int?[] _queuedScene = new int?[0];
    private QuantizedLaunchHelper[] _quantizedLaunchers = new QuantizedLaunchHelper[0];
    private PlayHelper[] _players = new PlayHelper[0];
    private List<DLaunchCell> _triggerOnLoopScratch = new List<DLaunchCell>();

    protected override void Definition() {
      NameInput = ValueInput<DLaunchHeader>("Name");
      FirstHeaderInput = ValueInput<DLaunchHeader>("Header");
      FirstSceneInput = ValueInput<DLaunchScene>("Scene");
      CustomTriggerInput = ValueInput("CustomTrigger", DLaunchableTriggerValue.FromUnit(this));

      _computeLayoutFromFlow = DNodeUtils.CachePerFrame(flow => {
        _cachedOtherInputConnections.Clear();
        EnumerateAllLayoutConnections(_cachedOtherInputConnections);
        if (Enumerable.SequenceEqual(_cachedInputConnections, _cachedOtherInputConnections)) {
          return this;
        }
        _cachedOtherInputConnections.Clear();

        flow.GetValue<DLaunchableTriggerValue>(CustomTriggerInput).Target = this;
        Name = DNodeUtils.GetOptional<string>(flow, NameInput, "Grid");
        FirstHeader = DNodeUtils.GetOptional<DLaunchHeader>(flow, FirstHeaderInput);
        FirstScene = DNodeUtils.GetOptional<DLaunchScene>(flow, FirstSceneInput);
        UpdateLayout();
        _cachedInputConnections.Clear();
        EnumerateAllLayoutConnections(_cachedInputConnections);

        if (!_isInitialized) {
          _isInitialized = true;
          for (int row = 0; row < _scenes.Length; ++row) {
            var scene = _scenes[row];
            if (scene == null) {
              continue;
            }
            if (scene.IsDefaultScene) {
              scene.Triggered = true;
            }
          }
          for (int row = 0; row < _cells.GetLength(0); ++row) {
            for (int col = 0; col < _cells.GetLength(1); ++col) {
              var cell = _cells[row, col];
              if (cell == null) {
                continue;
              }
              if (cell.IsDefaultCell) {
                cell.Triggered = true;
              }
            }
          }
        }

        return this;
      });

      result = ValueOutput<DFrameNodes>("result", flow => {
        GlobalLaunchQuantizationValue = GetGlobalLaunchQuantization(GlobalLaunchQuantization);

        _computeLayoutFromFlow(flow);
        UpdateTriggerInputs(flow);
        UpdateTriggering();

        List<DFrameNode> outputs = new List<DFrameNode>();
        for (int col = 0; col < _playingScene.Length; ++col) {
          if (_playingScene[col] == null) {
            continue;
          }
          DFrameNodes input = DNodeUtils.GetOptional<DFrameNodes>(flow, _cells[_playingScene[col].Value, col].Input);
          if (input.Nodes != null) {
            outputs.AddRange(input.Nodes);
          }
        }
        DMutableFrameArray<DFrameNode> result = new DMutableFrameArray<DFrameNode>(outputs);
        return result.ToValue();
      });
    }

    public override void ComputeFromFlow(Flow flow) {
      _computeLayoutFromFlow.Invoke(flow);
    }

    private void EnumerateAllLayoutConnections(List<ValueOutput> inputConnections) {
      inputConnections.Add(FirstHeaderInput.connection?.source);
      inputConnections.Add(FirstSceneInput.connection?.source);
      foreach (var header in _headers) {
        inputConnections.Add(header?.PreviousHeaderInput?.connection?.source);
      }
      foreach (var scene in _scenes) {
        inputConnections.Add(scene?.NextSceneInput?.connection?.source);
      }
      foreach (var cell in _cells) {
        inputConnections.Add(cell?.PreviousSiblingInput?.connection?.source);
      }
    }

    private void UpdateLayout() {
      int columnCount = 0;
      {
        DLaunchHeader header = FirstHeader;
        while (header != null) {
          columnCount++;
          header = header.PreviousHeader;
        }
      }

      int sceneCount = 0;
      {
        DLaunchScene scene = FirstScene;
        while (scene != null) {
          scene.LayoutSceneNumber = sceneCount++;
          scene.LayoutGrid = this;
          scene = scene.NextScene;
        }
      }
      if (_cells.GetLength(0) != sceneCount || _cells.GetLength(1) != columnCount) {
        _cells = new DLaunchCell[sceneCount, columnCount];
        Array.Resize(ref _scenes, sceneCount);
        Array.Resize(ref _headers, columnCount);
        Array.Resize(ref _playingScene, columnCount);
        Array.Resize(ref _queuedScene, columnCount);
        Array.Resize(ref _quantizedLaunchers, columnCount);
        Array.Resize(ref _players, columnCount);
      }
      {
        int col = columnCount - 1;
        DLaunchHeader header = FirstHeader;
        while (header != null) {
          header.LayoutColumn = col;
          _headers[col] = header;
          header = header.PreviousHeader;
          col--;
        }
      }
      {
        int sceneNumber = 0;
        DLaunchScene scene = FirstScene;
        while (scene != null && sceneNumber < sceneCount) {
          int col = columnCount - 1;
          DLaunchCell cell = scene.FirstCell;
          while (cell != null && col >= 0) {
            _cells[sceneNumber, col] = cell;
            cell.LayoutGrid = this;
            cell.LayoutColumn = col;
            cell.LayoutScene = sceneNumber;
            cell = cell.PreviousSibling;
            col--;
          }
          _scenes[sceneNumber] = scene;
          scene = scene.NextScene;
          sceneNumber++;
        }
      }
      for (int col = 0; col < columnCount; ++col) {
        int? columnPlayingIndex = null;
        int? columnQueuedIndex = _queuedScene[col] < 0 ? (int?)-1 : null;
        for (int row = 0; row < sceneCount; ++row) {
          if (_cells[row, col] == null) {
            continue;
          }
          if (_cells[row, col].StatusPlaying) {
            if (columnPlayingIndex == null) {
              columnPlayingIndex = row;
            } else {
              _cells[row, col].StatusPlaying = false;
            }
          }
          if (_cells[row, col].StatusQueued) {
            if (columnQueuedIndex == null) {
              columnQueuedIndex = row;
            } else {
              _cells[row, col].StatusQueued = false;
            }
          }
        }
        _playingScene[col] = columnPlayingIndex;
        _queuedScene[col] = columnQueuedIndex;
      }
    }

    private void UpdateTriggerInputs(Flow flow) {
      if (flow.GetValue<DLaunchableTriggerValue>(CustomTriggerInput)?.Trigger == true) {
        _allStopTriggered = true;
      }
      foreach (DLaunchHeader header in _headers) {
        if (flow.GetValue<DLaunchableTriggerValue>(header.CustomTriggerInput)?.Trigger == true) {
          header.Triggered = true;
        }
      }
      foreach (DLaunchScene scene in _scenes) {
        if (scene != null && scene.CustomTriggerInput != null &&
            flow.GetValue<DLaunchableTriggerValue>(scene.CustomTriggerInput)?.Trigger == true) {
          scene.Triggered = true;
        }
      }
      foreach (DLaunchCell cell in _cells) {
        if (cell != null && cell.CustomTriggerInput != null &&
            flow.GetValue<DLaunchableTriggerValue>(cell.CustomTriggerInput)?.Trigger == true) {
          cell.Triggered = true;
        }
      }
    }

    private void UpdateTriggering() {
      int sceneCount = _cells.GetLength(0);
      int columnCount = _cells.GetLength(1);
      int currentFrame = DScriptMachine.CurrentInstance.Transport.AbsoluteFrame;
      _triggerOnLoopScratch.Clear();

      if (_allStopTriggered) {
        _allStopTriggered = false;
        foreach (DLaunchHeader header in _headers) {
          header.Triggered = true;
        }
      }

      for (int col = 0; col < columnCount; ++col) {
        if (_playingScene[col] == null) {
          continue;
        }

        int row = _playingScene[col].Value;
        DLaunchCell cell = _cells[row, col];
        if (cell == null) {
          continue;
        }

        DLaunchOptions launchOptions = cell.LaunchOptions;
        ref PlayHelper player = ref _players[col];
        bool followActionTriggered = player.Step(ref launchOptions, out double nextLaunchTime, out double playQuantizationPercent);
        cell.StatusPlayingQuantizationPercent = playQuantizationPercent;
        if (followActionTriggered) {
          double p = _random.NextDouble();
          DLaunchFollowActionType followAction;
          if (p < launchOptions.FollowABChance) {
            followAction = launchOptions.FollowActionB;
            //UnityEngine.Debug.Log($"FollowAction: B => {followAction}");
          } else {
            followAction = launchOptions.FollowActionA;
            //UnityEngine.Debug.Log($"FollowAction: A => {followAction}");
          }

          bool triggerStop = false;
          DLaunchCell toTrigger = null;
          switch (followAction) {
            case DLaunchFollowActionType.None:
              break;
            case DLaunchFollowActionType.Stop:
              triggerStop = true;
              break;
            case DLaunchFollowActionType.PlayAgain:
              toTrigger = cell;
              break;
            case DLaunchFollowActionType.Previous:
              toTrigger = GetCellOrNull(row - 1, col);
              triggerStop = toTrigger == null;
              break;
            case DLaunchFollowActionType.Next:
              toTrigger = GetCellOrNull(row + 1, col);
              triggerStop = toTrigger == null;
              break;
            case DLaunchFollowActionType.First: {
              (int minRow, int maxRow) = GetCellBlockRange(row, col);
              toTrigger = GetCellOrNull(minRow, col);
              break;
            }
            case DLaunchFollowActionType.Last: {
              (int minRow, int maxRow) = GetCellBlockRange(row, col);
              toTrigger = GetCellOrNull(maxRow, col);
              break;
            }
            case DLaunchFollowActionType.Any: {
              (int minRow, int maxRow) = GetCellBlockRange(row, col);
              int next = _random.Next(minRow, maxRow + 1);
              toTrigger = GetCellOrNull(next, col);
              break;
            }
            case DLaunchFollowActionType.Other: {
              (int minRow, int maxRow) = GetCellBlockRange(row, col);
              int range = maxRow - minRow + 1;
              if (range <= 1) {
                toTrigger = null;
              } else {
                int next = _random.Next(minRow, maxRow);
                if (next >= row) {
                  next += 1;
                }
                toTrigger = GetCellOrNull(next, col);
              }
              break;
            }
            case DLaunchFollowActionType.Jump:
              break;
          }

          if (triggerStop) {
            cell.StatusPlaying = false;
            _playingScene[col] = null;
          } else if (toTrigger != null) {
            cell.StatusPlaying = false;
            toTrigger.Triggered = false;
            toTrigger.StatusQueued = false;
            if (toTrigger.HasInput) {
              toTrigger.StatusPlaying = true;
              _triggerOnLoopScratch.Add(toTrigger);
            } else {
              toTrigger.StatusPlaying = false;
            }
            _playingScene[col] = toTrigger.LayoutScene;
            _players[col].ResetAtLaunchTime(nextLaunchTime);
          }
          // TODO: Do not trigger if playing cell changed.
          if (toTrigger == null || toTrigger == cell) {
            if (launchOptions.FireTriggerOnLoop) {
              _triggerOnLoopScratch.Add(cell);
            }
          }
        }
      }

      for (int col = 0; col < columnCount; ++col) {
        DLaunchHeader header = _headers[col];
        if (!header.Triggered) {
          continue;
        }
        bool wasQueued = header.StatusQueued;
        if (_queuedScene[col] != null) {
          int oldQueuedScene = _queuedScene[col].Value;
          IDLaunchable oldCell = oldQueuedScene < 0 ? (IDLaunchable)_headers[col] : _cells[oldQueuedScene, col];
          if (oldCell != null) {
            oldCell.StatusQueued = false;
          }
        }
        header.Triggered = false;
        header.StatusQueued = true;
        _queuedScene[col] = -1;
        if (!wasQueued) {
          _quantizedLaunchers[col].Reset();
        }
      }
      for (int row = 0; row < sceneCount; ++row) {
        if (!_scenes[row].Triggered) {
          continue;
        }
        _scenes[row].Triggered = false;
        for (int col = 0; col < columnCount; ++col) {
          DLaunchCell newCell = _cells[row, col];
          bool wasQueued = newCell?.StatusQueued ?? false;
          if (_queuedScene[col] != null) {
            int oldQueuedScene = _queuedScene[col].Value;
            IDLaunchable oldCell = oldQueuedScene < 0 ? (IDLaunchable)_headers[col] : _cells[oldQueuedScene, col];
            if (oldCell != null) {
              oldCell.StatusQueued = false;
            }
          }
          if (newCell != null) {
            newCell.StatusQueued = true;
          }
          _queuedScene[col] = row;
          if (!wasQueued) {
            _quantizedLaunchers[col].Reset();
          }
        }
      }
      for (int col = 0; col < columnCount; ++col) {
        for (int row = 0; row < sceneCount; ++row) {
          if (_cells[row, col] == null) {
            continue;
          }
          if (!_cells[row, col].Triggered) {
            continue;
          }
          if (_queuedScene[col] != null) {
            int oldQueuedScene = _queuedScene[col].Value;
            IDLaunchable oldCell = oldQueuedScene < 0 ? (IDLaunchable)_headers[col] : _cells[oldQueuedScene, col];
            if (oldCell != null) {
              oldCell.StatusQueued = false;
            }
          }
          _cells[row, col].Triggered = false;
          _cells[row, col].StatusQueued = true;
          _queuedScene[col] = row;
          _quantizedLaunchers[col].Reset();
        }
      }
      for (int col = 0; col < columnCount; ++col) {
        if (_queuedScene[col] == null) {
          continue;
        }
        int queuedScene = _queuedScene[col].Value;
        IDLaunchable cell = queuedScene < 0 ? (IDLaunchable)_headers[col] : _cells[queuedScene, col];
        if (cell == null) {
          continue;
        }

        double launchQuantization = GetLaunchQuantization(cell.LaunchOptionQuantization);
        bool launched = _quantizedLaunchers[col].Step(launchQuantization, out double launchQuantizationPercent);
        cell.StatusQueuedQuantizationPercent = launchQuantizationPercent;
        if (launched) {
          cell.StatusQueued = false;
          int? oldPlayingScene = _playingScene[col];
          if (cell.HasInput) {
            cell.StatusPlaying = true;
            cell.StatusLaunchedOnFrameNumber = currentFrame;
            _playingScene[col] = queuedScene;
          } else {
            cell.StatusPlaying = false;
            _playingScene[col] = null;
          }
          _queuedScene[col] = null;
          if (oldPlayingScene != null) {
            DLaunchCell oldCell = _cells[oldPlayingScene.Value, col];
            if (oldCell != null && oldCell != cell) {
              oldCell.StatusPlaying = false;
            }
          }
          _players[col].Reset(launchQuantization);
        }
      }

      for (int row = 0; row < sceneCount; ++row) {
        bool allQueued = false;
        bool allPlaying = false;
        bool isFirstCell = true;
        for (int col = 0; col < columnCount; ++col) {
          DLaunchCell cell = _cells[row, col];
          if (cell != null && cell.HasInput) {
            if (isFirstCell) {
              isFirstCell = false;
              allQueued = true;
              allPlaying = true;
            }
            allQueued = allQueued && cell.StatusQueued;
            allPlaying = allPlaying && cell.StatusPlaying;
          }
        }
        _scenes[row].StatusQueued = allQueued;
        _scenes[row].StatusPlaying = allPlaying;
      }

      foreach (var cell in _triggerOnLoopScratch) {
        if (cell.StatusPlaying) {
          cell.StatusLaunchedOnFrameNumber = currentFrame;
        }
      }
      _triggerOnLoopScratch.Clear();
    }

    private DLaunchCell GetCellOrNull(int row, int col) {
      int sceneCount = _cells.GetLength(0);
      int columnCount = _cells.GetLength(1);
      if (row < 0 || row >= sceneCount) {
        return null;
      }
      if (col < 0 || col >= columnCount) {
        return null;
      }
      return _cells[row, col];
    }
  
    private (int minRow, int maxRow) GetCellBlockRange(int row, int col) {
      DLaunchCell cell;
      int minRow = row;
      while ((cell = GetCellOrNull(minRow - 1, col)) != null && cell.HasInput) {
        minRow--;
      }
      int maxRow = row;
      while ((cell = GetCellOrNull(maxRow + 1, col)) != null && cell.HasInput) {
        maxRow++;
      }
      return (minRow, maxRow);
    }

    private double GetLaunchQuantization(DLaunchQuantization quantization) {
      switch (quantization) {
        default:
        case DLaunchQuantization.Global:
          return GlobalLaunchQuantizationValue;
        case DLaunchQuantization.None:
          return 0.0;
        case DLaunchQuantization.Note32nd:
          return 1.0 / 8;
        case DLaunchQuantization.Note16nd:
          return 1.0 / 4;
        case DLaunchQuantization.NoteQuater:
          return 1.0;
        case DLaunchQuantization.Bar:
          return 4.0;
        case DLaunchQuantization.Bar2:
          return 4.0 * 2;
        case DLaunchQuantization.Bar4:
          return 4.0 * 4;
        case DLaunchQuantization.Bar8:
          return 4.0 * 8;
        case DLaunchQuantization.Bar16:
          return 4.0 * 16;
      }
    }

    private static double GetGlobalLaunchQuantization(DGlobalLaunchQuantization quantization) {
      switch (quantization) {
        case DGlobalLaunchQuantization.None:
          return 0.0;
        case DGlobalLaunchQuantization.Note32nd:
          return 1.0 / 8;
        case DGlobalLaunchQuantization.Note16nd:
          return 1.0 / 4;
        case DGlobalLaunchQuantization.NoteQuater:
          return 1.0;
        default:
        case DGlobalLaunchQuantization.Bar:
          return 4.0;
        case DGlobalLaunchQuantization.Bar2:
          return 4.0 * 2;
        case DGlobalLaunchQuantization.Bar4:
          return 4.0 * 4;
        case DGlobalLaunchQuantization.Bar8:
          return 4.0 * 8;
        case DGlobalLaunchQuantization.Bar16:
          return 4.0 * 16;
      }
    }
  }
}
