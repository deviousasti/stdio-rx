using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace System.Reactive.Linq.Stdio
{
    /// <summary>
    /// Thrown when the process has terminated unexpectedly
    /// </summary>
    /// <seealso cref="System.ApplicationException" />
    [Serializable]
    public class ProcessTerminatedException : ApplicationException
    {
        /// <summary>
        /// The process exit code.
        /// </summary>
        public int ExitCode { get; private set; }

        /// <summary>
        /// The time the process exited
        /// </summary>
        public DateTime ExitTime { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProcessTerminatedException"/> class.
        /// </summary>
        /// <param name="code">The code.</param>
        /// <param name="exitTime">The exit time.</param>
        public ProcessTerminatedException(int code, DateTime exitTime)
        {
            this.ExitCode = code;
            this.ExitTime = exitTime;
        }
    }

    /// <summary>
    /// Methods of ending the process 
    /// </summary>
    public enum ProcessExitMethod
    {
        /// <summary>
        /// Close stdin stream
        /// </summary>
        InputClose,

        /// <summary>
        /// Send close message
        /// </summary>
        Close,

        /// <summary>
        /// If GUI application, close the main window
        /// </summary>
        CloseMainWindow,

        /// <summary>
        /// Kill the process
        /// </summary>
        Kill,

        /// <summary>
        /// Send control signal 
        /// </summary>
        SendControlSignal,


        /// <summary>
        /// Send explicit quit command via stdin
        /// </summary>
        SendQuitCommand
    }

    /// <summary>
    /// Type of control signal to send to the process
    /// </summary>
    public enum ConsoleControlSignal
    {
        /// <summary>
        /// Send ^C
        /// </summary>
        CTRL_C = 0,

        /// <summary>
        /// Send ^BRK
        /// </summary>
        CTRL_BREAK = 1,

        /// <summary>
        /// Application close
        /// </summary>
        CTRL_CLOSE = 2,

        /// <summary>
        /// User log-off
        /// </summary>
        CTRL_LOGOFF = 5,

        /// <summary>
        /// Shutdown initiated
        /// </summary>
        CTRL_SHUTDOWN = 6
    }

    /// <summary>
    /// Settings for creating an stdio-bound Observable
    /// </summary>
    public class StdioSettings
    {
        /// <summary>
        /// Input to stdin
        /// </summary>
        public IObservable<string> Input { get; set; }

        /// <summary>
        /// Should every line end with a newline
        /// </summary>
        /// <value>
        ///   <c>true</c> if [write new lines]; otherwise, <c>false</c>.
        /// </value>
        public bool WriteNewLines { get; set; }

        /// <summary>
        /// Should force redirect stdout - defaults to true
        /// </summary>
        /// <value>
        ///   <c>true</c> if [redirect output]; otherwise, <c>false</c>.
        /// </value>
        public bool RedirectOutput { get; set; }


        /// <summary>
        /// Should force redirect stderr - defaults to true
        /// </summary>
        /// <value>
        ///   <c>true</c> if [redirect error]; otherwise, <c>false</c>.
        /// </value>
        public bool RedirectError { get; set; }

        /// <summary>
        /// Sets the exit method of the process - default is Kill.
        /// </summary>
        public ProcessExitMethod ExitMethod { get; set; }


        /// <summary>
        /// Gets or sets the process exit timeout.
        /// </summary>
        /// <value>
        /// The exit timeout.
        /// </value>
        public int ExitTimeout { get; set; }


        /// <summary>
        /// Gets or sets the control signal to the process.
        /// </summary>
        public ConsoleControlSignal ControlSignal { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to kill other processes of the same name.
        /// every time the observable is started
        /// </summary>
        /// <value>
        ///   <c>true</c> if [kill other processes]; otherwise, <c>false</c>.
        /// </value>
        public bool KillOtherProcesses { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to use the executable path as the working directory
        /// </summary>
        /// <value>
        ///   <c>true</c> if [root path]; otherwise, <c>false</c>.
        /// </value>
        public bool UseExecutablePath { get; set; }

        /// <summary>
        /// Gets or sets the working directory of the called process.
        /// </summary>
        /// <value>
        /// The working directory.
        /// </value>
        public string WorkingDirectory { get; set; }

        /// <summary>
        /// Array of successful exit codes.
        /// </summary>
        public int[] ExitCodes { get; set; }

        /// <summary>
        /// Convenience property for setting a single exit code.
        /// </summary>
        public int ExitCode { set { ExitCodes = new int[] { value }; } }

        /// <summary>
        /// Gets or sets the quit command to be sent to stdin.
        /// </summary>
        public string QuitCommand { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="StdioSettings"/> class.
        /// </summary>
        public StdioSettings()
        {
            RedirectOutput = true;
            RedirectError = true;
            ExitTimeout = 3000;
            ControlSignal = ConsoleControlSignal.CTRL_C;
            ExitMethod = ProcessExitMethod.Kill;
            ExitCodes = new int[] { 0 };
        }

        /// <summary>
        /// The default value of stdio settings
        /// </summary>
        public static StdioSettings Defaults = new StdioSettings();

    }

    /// <summary>
    /// StdioObservable contains methods to create an observable to a process' stdio
    /// </summary>
    public static class StdioObservable
    {
        /// <summary>
        /// Creates an observable with the specified process file and arguments - use default settings if none provided.
        /// </summary>
        public static IObservable<string> Create(string filename, string arguments = default, StdioSettings settings = default)
        {
            var args = new ProcessStartInfo(filename, arguments) { };

            return Create(args, settings);
        }

        /// <summary>
        /// Creates an observable with the specified process start info - use default settings if none provided.
        /// </summary>
        /// <returns></returns>
        public static IObservable<string> Create(ProcessStartInfo proc, StdioSettings settings = default)
        {
            settings = settings ?? StdioSettings.Defaults;

            proc.RedirectStandardInput = true; 
            proc.RedirectStandardOutput = settings.RedirectOutput;
            proc.RedirectStandardError = settings.RedirectError;
            proc.UseShellExecute = false; //this is neccessary

            if (!string.IsNullOrEmpty(settings.WorkingDirectory))
                proc.WorkingDirectory = settings.WorkingDirectory;
            else if (settings.UseExecutablePath)
                proc.WorkingDirectory = Path.GetDirectoryName(proc.FileName);


            return Observable.Create<string>(observer =>
            {
                if (settings.KillOtherProcesses)
                {
                    KillOthers(proc.FileName);
                }

                var process = Process.Start(proc);

                var subscription = Create(process, settings).Subscribe(observer);

                if (settings.RedirectOutput)
                    process.BeginOutputReadLine();

                if (settings.RedirectError)
                    process.BeginErrorReadLine();

                return () =>
                {
                    try
                    {
                        process.CancelOutputRead();
                        subscription.Dispose();

                        if (!process.HasExited)
                        {
                            switch (settings.ExitMethod)
                            {
                                case ProcessExitMethod.SendControlSignal:
                                    KillGroup(settings.ExitTimeout, process);
                                    break;

                                case ProcessExitMethod.InputClose:
                                    process.StandardInput.Close();
                                    break;

                                case ProcessExitMethod.Close:
                                    process.Close();
                                    break;

                                case ProcessExitMethod.CloseMainWindow:
                                    process.CloseMainWindow();
                                    break;
                                case ProcessExitMethod.Kill:
                                    process.Kill(true);
                                    break;
                                case ProcessExitMethod.SendQuitCommand:
                                    process.StandardInput.Write(settings.QuitCommand);
                                    break;

                                default:
                                    break;
                            }

                            process.Dispose();
                        }
                    }
                    catch { }
                };
            });
        }

        /// <summary>
        /// Kills other processes of the same filename.
        /// </summary>
        public static void KillOthers(string fileName)
        {
            Process
                .GetProcessesByName(Path.GetFileNameWithoutExtension(fileName))
                .Where(p => { try { return p.MainModule.FileName == fileName; } catch { return false; } })
                .ToList()
                .ForEach(p => { try { p.Kill(); p.WaitForExit(); } catch { } });
        }

        /// <summary>
        /// Creates an observable that listens to an existing process.
        /// </summary>
        [DebuggerStepThrough]
        public static IObservable<string> Create(Process process, StdioSettings settings = default(StdioSettings))
        {
            settings = settings ?? StdioSettings.Defaults;

            return Observable.Create<string>(observer =>
            {

                EventHandler exithandler = (s, e) =>
                {
                    if (settings.ExitCodes.Contains(process.ExitCode))
                        observer.OnCompleted();
                    else
                        observer.OnError(new ProcessTerminatedException(process.ExitCode, process.ExitTime));
                };

                DataReceivedEventHandler datahandler = (s, e) =>
                {
                    observer.OnNext(e.Data);
                };

                process.EnableRaisingEvents = true;
                process.Exited += exithandler;

                if (process.HasExited)
                {
                    exithandler(process, EventArgs.Empty);
                    return Disposable.Empty;
                }

                if (settings.RedirectOutput)
                    process.OutputDataReceived += datahandler;

                if (settings.RedirectError)
                    process.ErrorDataReceived += datahandler;


                IDisposable inputSubscription = Disposable.Empty;
                if (settings.Input != null)
                {
                    var stdin = process.StandardInput;

                    settings.Input.Subscribe(line =>
                    {
                        if (settings.WriteNewLines)
                            stdin.WriteLine(line);
                        else
                            stdin.Write(line);
                    });
                }


                return Disposable.Create(() =>
                {
                    inputSubscription.Dispose();
                    process.OutputDataReceived -= datahandler;
                    process.ErrorDataReceived -= datahandler;

                    process.Exited -= exithandler;
                });
            });
        }

        /// <summary>
        /// The equivalent of the where/which command which finds the directory 
        /// an executable is in from the system's PATH environment variable
        /// </summary>
        public static string Where(string file)
        {
            return Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine)
                              .Split(';')
                              .Reverse()
                              .Select(path => Path.Combine(path, file))
                              .FirstOrDefault(File.Exists);
        }

        /// <summary>
        /// Throws if the condition matches, with the specified delay for throwing the exception
        /// </summary>
        public static IObservable<T> ThrowIf<T>(this IObservable<T> obs, Predicate<T> condition, TimeSpan delay = default(TimeSpan))
        {
            return obs.ThrowIfCount(condition, 1, delay);
        }

        /// <summary>
        /// Throws if the condition matches a number of times, with the specified delay for throwing the exception
        /// </summary>
        public static IObservable<T> ThrowIfCount<T>(this IObservable<T> obs, Predicate<T> condition, int count, TimeSpan delay = default(TimeSpan))
        {
            return ThrowIfCount(obs, condition, count, delay, () => new Exception());
        }

        /// <summary>
        /// Throws the generated exception if the condition matches, with the specified delay for throwing the exception
        /// </summary>
        public static IObservable<T> ThrowIfCount<T>(this IObservable<T> obs, Predicate<T> condition, int count, TimeSpan delay, Func<Exception> exceptionfac)
        {
            return Observable.Create<T>(o =>
            {
                var current = 0;
                return obs.Subscribe(v =>
                {
                    if (condition(v))
                        current++;

                    if (current < count)
                    {
                        o.OnNext(v);
                    }
                    else
                    {
                        o.OnNext(v);
                        Task.Delay(delay).ContinueWith(_ => o.OnError(exceptionfac()));
                    }

                },
                    o.OnError,
                    o.OnCompleted);
            }
            );
        }


        //import in the declaration for GenerateConsoleCtrlEvent
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GenerateConsoleCtrlEvent(ConsoleControlSignal sigevent, int dwProcessGroupId);       

        //set up the parents CtrlC event handler, so we can ignore the event while sending to the child
        static volatile bool SENDING_CTRL_C_TO_CHILD = false;
        static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = SENDING_CTRL_C_TO_CHILD;
        }

        static StdioObservable()
        {
            Console.CancelKeyPress += new ConsoleCancelEventHandler(Console_CancelKeyPress);
        }

        static void KillGroup(int exitTimeout, Process process)
        {
            //hook up the event handler in the parent
            //send the ctrl-c to the process group (the parent will get it too!)
            SENDING_CTRL_C_TO_CHILD = true;
            GenerateConsoleCtrlEvent(ConsoleControlSignal.CTRL_C, process.SessionId);
            process.WaitForExit(exitTimeout);

            if (!process.HasExited)
                process.Close();

            SENDING_CTRL_C_TO_CHILD = false;
        }
    }
}
