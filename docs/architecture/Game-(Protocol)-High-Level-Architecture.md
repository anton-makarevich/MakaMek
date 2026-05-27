### **Design Principles**
1. **Decouple Client and Server:**
   - Use interfaces to abstract the communication mechanism, so neither the Client nor the Server directly knows how messages are transmitted.
   - Both the Server and Client should interact through interfaces like `ICommandPublisher`.

2. **Pluggable Transport Layer:**
   - Use SigmalR or pure WebSockets for remote communication.
   - Use lightweight mechanisms like RX for local communication.

3. **Shared Protocol:**
   - Define commands and updates as serializable DTOs to ensure compatibility across different communication layers.

---

### **High-Level Architecture**
```plaintext
+-------------------+    Commands      +-------------------+
|   Game Client     | <--------------> |   Game Server     |
|                   |   State Updates  |                   |
+-------------------+                   +-------------------+
       ^                                       ^
       |                                       |
       |                                       |
   Interfaces                            Interfaces
       ^                                       ^
       |                                       |
+-------------------+                 +-------------------+
| Transport Adapter |                 | Transport Adapter |
| (e.g., SignalR)   |                 | (e.g., SignalR  ) |
+-------------------+                 +-------------------+
```

---

### **Implementation Details**

#### **1. Define Interfaces**
Create generic interfaces for communication between the client and server.

```csharp
public interface ICommandPublisher
{
    void PublishCommand(GameCommand command); // Sends a command
    void Subscribe(Action<GameCommand> onCommandReceived); // Registers a callback for received commands
}
```

---

#### **2. Create Shared DTOs**
Define the shared protocol for commands and state updates.

```csharp
public class GameCommand
{
    public string CommandType { get; set; }
    public string Payload { get; set; } // JSON or other serialization
}
```

---

#### **3. Local Transport Layer**
For local play, implement `ICommandPublisher` and `IStateSubscriber` using Reactive Extensions.

**Using RX (System.Reactive):**
```csharp
public class RxLocalTransport : ICommandPublisher
{
    private readonly Subject<GameCommand> _commandStream = new();

    public void PublishCommand(GameCommand command)
    {
        _commandStream.OnNext(command);
    }

    public void Subscribe(Action<GameCommand> onCommandReceived)
    {
        _commandStream.Subscribe(onCommandReceived);
    }
}

```

---

#### **4. Network Transport Layer**
For remote play, implement `ICommandPublisher` using SignalR.

**Server Transport:**
```csharp
public class SignalRServerTransport : ICommandPublisher
{
    private readonly IHubContext<GameHub> _hubContext;
    private Action<GameCommand> _onCommandReceived;

    public SignalRServerTransport(IHubContext<GameHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public void PublishCommand(GameCommand command)
    {
        // Broadcast the command to all clients
        _hubContext.Clients.All.SendAsync("ReceiveCommand", command);
    }

    public void Subscribe(Action<GameCommand> onCommandReceived)
    {
        _onCommandReceived = onCommandReceived;
    }

    // Method for receiving commands (hooked into the SignalR pipeline)
    public void OnCommandReceived(GameCommand command)
    {
        _onCommandReceived?.Invoke(command);
    }
}
```

**Client Transport:**
```csharp
public class SignalRClientTransport : ICommandPublisher
{
    private readonly HubConnection _connection;
    private Action<GameCommand> _onCommandReceived;

    public SignalRClientTransport(string serverUrl)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl($"{serverUrl}/gamehub")
            .Build();

        // Set up listener for commands from the server
        _connection.On<GameCommand>("ReceiveCommand", command =>
        {
            _onCommandReceived?.Invoke(command);
        });
    }

    public async Task StartAsync()
    {
        await _connection.StartAsync();
        Console.WriteLine("Client connected to server.");
    }

    public async Task StopAsync()
    {
        await _connection.StopAsync();
        Console.WriteLine("Client disconnected from server.");
    }

    public void PublishCommand(GameCommand command)
    {
        // Send a command to the server
        _connection.InvokeAsync("SendCommand", command);
    }

    public void Subscribe(Action<GameCommand> onCommandReceived)
    {
        _onCommandReceived = onCommandReceived;
    }
}
```

