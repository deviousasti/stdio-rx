# stdio-rx
Standard Output, Error, and Input streams of a process as Observables

**StdioObservable** lets you express an executable program as observables of its input and output. This allows you to compose programs using various operators and reactive semantics.


## A quick example

Let's build a simple keep-alive for `couchdb`.

```csharp
static void Main(string[] args)
{
    using 
    (
        StdioObservable.Create("./couchdb.cmd")
        .Retry()
        .Subscribe(Console.WriteLine)
    )
    {
        Console.ReadLine();
    }
    Console.WriteLine("Done.");    
}
```

![stdio-demo](https://user-images.githubusercontent.com/2375486/76637326-89270600-6570-11ea-90fa-b1d2fd60798e.gif)

- The process is started on subscription.
- If the process exits with an unexpected return code, it raises an `OnError` notification, and tears down the stream, but the `Retry` operator restarts the stream, creating a new process.
- `Console.WriteLine` is called with the process output 
- As soon as the subscription is disposed, so is the process.

## Another example

This stops `wget` if it doesn't respond with an update in at least ten seconds.

```csharp
StdioObservable.Create(
	"wget", "http://releases.ubuntu.com/18.04.4/ubuntu-18.04.4-desktop-amd64.iso -q --show-progress"
)
.Timeout(TimeSpan.FromSeconds(10))
.Subscribe(
	text => Console.WriteLine(text), 
	exn =>  Console.WriteLine("Download timed out")
);
```

## Usage

There are two assemblies:

- `System.Reactive.Linq.Stdio` for a general VB/C# oriented API based on `System.Reactive`

- `FSharp.Control.Reactive.Stdio` for an idiomatic F# API based on `FSharp.Control.Reactive`

**C#**

```csharp
StdioObservable.Create ((ProcessStartInfo|filename,args) info, [StdioSettings setting])` 
```

**F#**

```fsharp
StdioObservable.create: settings: StdioSettings -> filename: string -> args: string -> IObservable<string>
```

The `StdioSettings` object for both assemblies are similar.

**C#**

```csharp
new StdioSettings { ExitCodes = new[] { 0, 200 }, ExitMethod = ProcessExitMethod.Close };
```

**F#**

```fsharp
{ Options.defaults with ExitCodes = [| 0; 200 |]; ExitMethod = Options.Kill }
```

See properties in `StdioSettings` for its descriptions.

## Notes

- If you don't need input/output redirection, you can turn it off in the settings.

- Even without observing output, the abstraction of a process lifetime tied to an observable is useful in several scenarios.

- By default, the process is run with the current working directory of your application. If you want the process to start on a different directory, you can set `WorkingDirectory` in the settings.

- Rx methods like `TimeInterval` and `Timeout` are useful in figuring out if a process isn't logging periodically

- Scanning the output for words like *panic* or *lockfile* are an easy way to shunt into repair methods.

- Some applications can be exit by sending a keystroke (like 'q') or a command like 'quit'. 

  ​	C#: Set `ExitMethod = SendQuitCommand` and `ExitCommand = "q"`, and for 

  ​	F#: Set `ExitMethod = SendQuitCommand("q")`