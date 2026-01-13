using System;
using System.Threading.Tasks;

namespace YureteruWPF.Services;

/// <summary>
/// Interface for serial port communication
/// </summary>
public interface ISerialService
{
    event EventHandler<string>? DataReceived;
    event EventHandler<string>? ErrorOccurred;
    
    bool IsConnected { get; }
    bool IsMocking { get; }
    
    Task<bool> ConnectAsync(string portName, int baudRate);
    Task DisconnectAsync();
    void StartMock();
    string[] GetAvailablePorts();
}
