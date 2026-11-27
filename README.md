# Cross-thread Post during DispatcherOperation completion

This console app checks whether a worker thread can enqueue work on an Avalonia dispatcher while the dispatcher is notifying a synchronous task continuation. It uses public Avalonia APIs and exits on its own.

## Run

Tested on Windows x64 with the .NET 10 SDK. The default package is Avalonia 12.2.0.

```sh
git clone --depth 1 --single-branch --branch repro/dispatcher-task-completion-lock https://github.com/comesx4/Avalonia.git dispatcher-repro
cd dispatcher-repro
dotnet run --project Repro.csproj
```

To test Avalonia 12.0.4, run `dotnet run --project Repro.csproj -p:AvaloniaVersion=12.0.4` in the same directory. The first run requires access to NuGet.

## What to look for

On 12.2.0 and 12.0.4, the demo prints the following readings.

```text
Post returned during the completion continuation: False
Post returned by the time the worker exited: True
```

The worker first makes a control call to `Dispatcher.Post`. The UI thread then completes an `InvokeAsync<int>` operation. Its synchronous continuation wakes the worker for a second call to `Post` and gives that call two seconds to return. The worker signals as soon as `Post` returns, before the dispatcher needs to execute the queued callback.

On the affected versions, the worker has to wait until the continuation exits and releases the dispatcher lock. The timeout breaks this wait so the demo can print both readings. In the Windows repaint case, the UI thread instead waits for the compositor, whose render thread needs to post back to the dispatcher.

## Compare a local fix

With a rebuilt `Avalonia.Base.dll` targeting `net10.0`, pass its path in place of the example below.

```sh
dotnet run --project Repro.csproj "-p:AvaloniaBaseDll=../Avalonia/src/Avalonia.Base/bin/Debug/net10.0/Avalonia.Base.dll"
```

When task completion happens outside the dispatcher lock, both readings are `True`.
