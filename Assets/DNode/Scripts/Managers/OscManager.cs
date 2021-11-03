using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DNode {
  public class OscManager : IFrameComponent {
    private readonly Dictionary<string, double> _triggeredValues = new Dictionary<string, double>();

    public int InPort = 1555;
    public int OutPort = 1556;
    public string OutHost = "127.0.0.1";

    private int _lastUsedFrameNumber = 0;
    private MakingThings.OSC _connection;

    public void Dispose() {
      _connection?.Close();
      _connection = null;
    }

    private void Start() {
      if (_connection != null) {
        return;
      }
      _connection = new MakingThings.OSC(InPort, OutHost, OutPort);
      _connection.SetAllMessageHandler(OnMessageReceived);
    }

    private void OnMessageReceived(MakingThings.OscMessage message) {
      _triggeredValues[message.address] = message.GetFloat(0);
    }

    public void OnStartFrame() {
      if (_connection == null) {
        return;
      }
      _connection.Update();
    }

    public void OnEndFrame() {
      if (_connection == null) {
        return;
      }
      if (_lastUsedFrameNumber != DScriptMachine.CurrentInstance.Transport.AbsoluteFrame) {
        _connection.Close();
        _connection = null;
      }
    }

    public bool TryGetChangedValue(string address, out double value) {
      Start();
      _lastUsedFrameNumber = DScriptMachine.CurrentInstance.Transport.AbsoluteFrame;
      return _triggeredValues.TryGetValue(address, out value);
    }

    public void SendValueChange(string address, double value) {
      Start();
      _lastUsedFrameNumber = DScriptMachine.CurrentInstance.Transport.AbsoluteFrame;
      MakingThings.OscMessage message = new MakingThings.OscMessage { address = address };
      message.values.Add((float)value);
      _connection.Send(message);
    }
  }
}
