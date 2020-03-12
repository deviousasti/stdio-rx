namespace FSharp.Control.Reactive 

module Stdio =

    open System
    open System.IO
    open System.Diagnostics

    module Options =

        type ExitMethod = 
            
            /// Close stdin stream            
            | InputClose 
            
            /// Send close message
            | Close 
            
            /// If GUI application, close the main window
            | CloseMainWindow 
            
            /// Kill the process
            | Kill 
            
            /// Send control signal 
            | SendControlSignal of timeout:int 

            /// Send explicit quit command via stdin
            | SendQuitCommand   of command:string
  
         /// Settings for creating an stdio-bound Observable
         type ShellSettings =    
             {
                   /// Input to stdin
                   Input: IObservable<string>

                   /// Should every line end with a newline
                   WriteNewLines: bool
                   
                   /// Should force redirect stdout - defaults to true
                   RedirectInput: bool
                   
                   /// Should force redirect stderr - defaults to true
                   RedirectOutput: bool

                   /// Should force redirect stderr - defaults to true
                   RedirectError: bool

                   /// Sets the exit method of the process.
                   ExitMethod: ExitMethod

                   /// Gets or sets the working directory of the called process.
                   WorkingDirectory: string option
                   
                   /// Array of successful exit codes.
                   ExitCodes: int[]
                   
                   /// Gets or sets the process exit timeout.
                   ExitDelay: int

                   /// List of environment variables to set
                   /// for the process
                   EnvironmentVariables: Map<string, string>
            }

        /// Default settings for the process
        let defaultSettings = 
            { 
                Input = Observable.empty; 
                WriteNewLines = true; 
                RedirectInput = false;
                RedirectOutput = true;
                RedirectError = true;
                ExitMethod = Kill;
                WorkingDirectory = None;
                ExitCodes = [| 0 |];
                ExitDelay = 100;
                EnvironmentVariables = Map.empty;
            }


    /// Raised when the process has terminated with
    /// an unexpected error code
    [<Serializable>]
    exception ProcessTerminatedException of int

    /// Kills a process
    let kill (proc: Process) = function
        | _ when proc.HasExited         -> ()
        | Options.InputClose            -> proc.StandardInput.Close()
        | Options.Close                 -> proc.Close()
        | Options.Kill                  -> proc.Kill()
        | Options.CloseMainWindow       -> proc.CloseMainWindow() |> ignore
        | Options.SendQuitCommand(c)    -> proc.StandardInput.Write(c)
        | _                             -> ()
    
    /// Listens to an existing process, sends notifications 
    /// through the observer
    let listen (proc: Process) (settings:Options.ShellSettings) (observer : IObserver<string>) =
    
        let exit code =
            if settings.ExitCodes |> Array.contains code then
                observer.OnCompleted()
            else
                observer.OnError(ProcessTerminatedException(code))
        
        let exithandler _ =                    
            async {
                try 
                    let code = proc.ExitCode
                    do! Async.Sleep(settings.ExitDelay)
                    exit code
                with 
                    _ -> observer.OnCompleted()
            } |> Async.Start

        let datahandler (e:DataReceivedEventArgs) = 
            if not (isNull e.Data) then
                observer.OnNext(e.Data)

        let inputhandler: string -> unit = 
            if settings.RedirectInput then
                let stdin = proc.StandardInput
                if settings.WriteNewLines then stdin.WriteLine else stdin.Write
            else
                ignore
    
        let subscriptions =
            seq {            
                if settings.RedirectOutput then 
                    proc.OutputDataReceived.Subscribe datahandler    
                else
                    Disposable.empty
    
                if settings.RedirectError then 
                    proc.ErrorDataReceived.Subscribe datahandler    
                else
                    Disposable.empty

                if settings.RedirectInput then
                    settings.Input |> Observable.subscribe inputhandler
                else
                    Disposable.empty
            
                if not proc.HasExited then
                    proc.Exited.Subscribe exithandler
                else
                    exithandler ()
                    Disposable.empty
            }
    
        proc.EnableRaisingEvents <- true

        if proc.HasExited then
            exithandler ()
            Disposable.empty
        else        
            subscriptions |> Disposable.compose
    
    /// Starts a new process and begins redirection
    let private run (args:ProcessStartInfo) (settings: Options.ShellSettings) (observer : IObserver<string>) =
        try
        let proc = Process.Start args
        let subscription = listen proc settings observer     

        if settings.RedirectOutput then
            proc.BeginOutputReadLine()

        if settings.RedirectError then
            proc.BeginErrorReadLine()

        Disposable.create (fun () -> 
            try
                subscription.Dispose()
                kill proc settings.ExitMethod
                proc.CancelOutputRead()
                proc.Dispose()
            with | _ -> ()
        )

        with e -> 
        observer.OnError(e)    
        Disposable.empty

    /// Create an observable process with the supplied process info
    let createWith (settings:Options.ShellSettings) (proc:ProcessStartInfo)  =
        proc.RedirectStandardInput  <- settings.RedirectInput
        proc.RedirectStandardOutput <- settings.RedirectOutput
        proc.RedirectStandardError  <- settings.RedirectError    
        proc.UseShellExecute        <- false
        proc.WorkingDirectory       <- match settings.WorkingDirectory with 
                                       | Some(dir) -> dir
                                       | None -> Directory.GetCurrentDirectory()

        settings.EnvironmentVariables 
        |> Map.iter(fun key value -> proc.EnvironmentVariables.[key] <- value)

        Observable.createWithDisposable (run proc settings)

    /// Create an observable process with the supplied process file name, and arguments
    let create settings filename args =
        createWith settings (new ProcessStartInfo(filename, args))

    let private combine file path =
        Path.Combine (path, file)
    
    /// The equivalent of the where/which command which finds the directory 
    /// an executable is in from the system's PATH environment variable
    let where executable =
        let paths = Environment.GetEnvironmentVariable "PATH"
        paths.Split(';') 
        |> Seq.map (combine executable) 
        |> Seq.filter (File.Exists)
        |> Seq.tryHead
    
    