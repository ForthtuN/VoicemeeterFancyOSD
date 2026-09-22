using AtgDev.Utils;
using AtgDev.Voicemeeter;
using AtgDev.Voicemeeter.Types;
using AtgDev.Voicemeeter.Utils;
using AtgDev.Voicemeeter.Extensions;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;

namespace VoicemeeterOsdProgram.Core;

public static class VoicemeeterApiClient
{
    private const int HealthCheckIntervalMs = 250;

    public enum Rate
    {
        Slow = 0,
        Normal = 1,
        Fast = 2,
        VeryFast = 3
    }

    private static System.Timers.Timer m_loopTimer = new()
    {
        AutoReset = true
    };
    private static VoicemeeterType m_type;
    private static bool m_isIdling;
    private static bool m_isTypeChanging;
    private static bool m_isVmRunning;
    private static bool m_isVmTurningOn;
    private static Rate m_poolingRate;
    private static bool m_isInit = false;
    private static int m_isPolling;
    private static int m_isExiting;
    private static long m_nextHealthCheckTickCount;

    private static Logger m_logger = Globals.Logger;

    static VoicemeeterApiClient()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, _) => Exit();
        Application.Current.Exit += (_, _) => Exit();

        PoolingRate = Rate.Normal;
    }

    public static async Task InitAsync(int waitTime = 0)
    {
        if (m_isInit) return;

        if (waitTime > 0)
        {
            await Task.Delay(waitTime);
        }

        await LoadAsync();

        // LoadAsync logs and absorbs initialization failures. Only mark this
        // wrapper initialized after the API client actually reached its ready
        // state so a later call can retry a transient startup failure.
        m_isInit = IsInitialized;
    }

    public static RemoteApiWrapper Api { get; private set; }

    public static bool IsLoaded { get; private set; }

    public static bool IsInitialized { get; private set; }

    public static bool IsVoicemeeterRunning 
    {
        get => Api?.GetVoicemeeterType(out _) == 0;
        private set
        {
            if (m_isVmRunning == value) return;

            m_isVmRunning = value;
            if (value)
            {
                OnVmTurnedOn();
            }
            else
            {
                OnVmTurnedOff();
            }
        }
    }

    public static bool IsHandlingParams { get; set; } = true;

    public static VoicemeeterVersion VoicemeeterVersion
    {
        get
        {
            VoicemeeterVersion vers = new(0);
            Api?.GetVoicemeeterVersion(out vers);
            return vers;
        }
    }

    public static VoicemeeterType ProgramType
    {
        get
        {
            var type = VoicemeeterType.None;
            bool isCompleted = Api?.GetVoicemeeterType(out type) == 0;
            return isCompleted ? type : VoicemeeterType.None;
        }
        private set
        {
            if ((value == VoicemeeterType.None) || (m_type == value)) return;

            if (m_type != VoicemeeterType.None)
            {
                OnProgramTypeChange();
            }
            m_type = value;
        }
    }

    public static Rate PoolingRate
    {
        get => m_poolingRate;
        set
        {
            m_logger?.Log($"VmrApi Client pooling rate is set to: {value}");

            m_poolingRate = value;
            if (IsIdling) return;

            m_loopTimer.Interval = PollingRatePolicy.GetIntervalMilliseconds((int)value);
        }
    }

    private static bool IsIdling
    {
        get => m_isIdling;
        set
        {
            if (m_isIdling == value) return;

            m_logger?.Log($"VmrApi Client is idling: {value}");

            m_isIdling = value;
            if (value)
            {
                PoolingInterval = 1000;
            }
            else
            {
                PoolingRate = m_poolingRate;
            }
        }
    }

    private static double PoolingInterval
    {
        get => m_loopTimer.Interval;
        set => m_loopTimer.Interval = value;
    }

    public static async Task LoadAsync()
    {
        if (IsInitialized) return;

        try
        {
            m_logger?.Log("Initializing VmrApi Client");
            if (!IsLoaded)
            {
                Api = new(PathHelper.GetDllPath());
                var loginRes = Api.Login();

                m_logger?.Log($"VmrApi Login result: {loginRes}");
                if ((loginRes != ResultCodes.Ok) && (loginRes != ResultCodes.OkVmNotLaunched))
                {
                    throw new InvalidOperationException("VmrApi is unable to login");
                }

                var paramsRes = await Api.WaitForNewParamsAsync(250, 1000 / 30);
                m_logger?.Log($"VmrApi WaitForNewParams returned: {paramsRes}");

                m_type = ProgramType;
                m_isVmRunning = IsVoicemeeterRunning;

                IsLoaded = true;

                OnLoad();
            }

            m_loopTimer.Elapsed += OnTimerTick;
            m_loopTimer.Start();

            IsInitialized = true;
            m_logger?.Log("VmrApi Client initialized");
        }
        catch (Exception e)
        {
            m_logger?.LogCritical($"Failed to initialize VmrApi Client: {e.GetType()} {e.Message}");
            if (!IsLoaded)
            {
                Api?.Dispose();
                Api = null;
            }

            if (m_loopTimer is not null)
            {
                m_loopTimer.Stop();
                m_loopTimer.Elapsed -= OnTimerTick;
            }
        }
    }

    public static void Exit()
    {
        if (Interlocked.Exchange(ref m_isExiting, 1) != 0) return;
        System.Diagnostics.Debug.WriteLine("Exiting VMRAPI");
        m_loopTimer?.Stop();

        // Do not block the UI thread waiting for an in-flight polling callback:
        // that callback may itself be synchronously dispatching an OSD update to
        // the UI thread. Claim the polling slot only if it is idle; otherwise the
        // callback that already owns it performs Logout from its finally block.
        if (Interlocked.CompareExchange(ref m_isPolling, 1, 0) == 0)
        {
            try
            {
                Api?.Logout();
            }
            finally
            {
                Volatile.Write(ref m_isPolling, 0);
            }
        }
    }

    private static void OnTimerTick(object sender, ElapsedEventArgs e)
    {
        if (Volatile.Read(ref m_isExiting) != 0) return;
        if (Interlocked.Exchange(ref m_isPolling, 1) != 0) return;

        try
        {
            if (Volatile.Read(ref m_isExiting) != 0) return;

            HandleServerAndProgramType();

            if (!IsHandlingParams)
            {
                _ = Api.IsParametersDirty();
                IsIdling = true;
                return;
            }

            if (!m_isVmRunning && m_isVmTurningOn && m_isTypeChanging) return;

            HandleParameters();
        }
        finally
        {
            try
            {
                if (Volatile.Read(ref m_isExiting) != 0)
                {
                    Api?.Logout();
                }
            }
            finally
            {
                Volatile.Write(ref m_isPolling, 0);
            }
        }
    }

    private static void HandleServerAndProgramType()
    {
        if (m_isVmTurningOn) return;

        long now = Environment.TickCount64;
        if (now < m_nextHealthCheckTickCount) return;
        m_nextHealthCheckTickCount = now + HealthCheckIntervalMs;

        var actualType = VoicemeeterType.None;
        bool isRunningActual = Api?.GetVoicemeeterType(out actualType) == 0;
        IsIdling = !isRunningActual;
        IsVoicemeeterRunning = isRunningActual;

        if (isRunningActual && !m_isTypeChanging)
        {
            ProgramType = actualType;
        }
    }

    private static void HandleParameters()
    {
        if (Api.IsParametersDirty() == 1)
        {
            OnNewParameters();
        }
    }

    private static event EventHandler m_loaded;

    public static event EventHandler NewParameters;
    public static event EventHandler<VoicemeeterType> ProgramTypeChange;
    public static event EventHandler VoicemeeterTurnedOff;
    public static event EventHandler VoicemeeterTurnedOn;

    public static event EventHandler Loaded
    {
        add
        {
            if (IsLoaded)
            {
                value?.Invoke(null, EventArgs.Empty);
            }
            else
            {
                m_loaded += value;
            }
        }
        remove => m_loaded -= value;
    }

    private static void OnNewParameters()
    {
        NewParameters?.Invoke(null, EventArgs.Empty);
    }

    private static void OnLoad()
    {
        m_loaded?.Invoke(null, EventArgs.Empty);
    }

    private static void OnVmTurnedOff()
    {
        m_logger?.Log("Voicemeeter shutdown detected");
        VoicemeeterTurnedOff?.Invoke(null, EventArgs.Empty);
    }

    private static void OnVmTurnedOn()
    {
        m_logger?.Log("Voicemeeter is running");
        m_isVmTurningOn = true;
        VoicemeeterTurnedOn?.Invoke(null, EventArgs.Empty);
        m_isVmTurningOn = false;
    }

    private static void OnProgramTypeChange()
    {
        m_logger?.Log($"Voicemeeter type changed to: {ProgramType}");

        m_isTypeChanging = true;
       
        var type = ProgramType;
        if (ProgramType != VoicemeeterType.None)
        {
            ProgramTypeChange?.Invoke(null, ProgramType);
        }

        m_isTypeChanging = false;
    }
}
