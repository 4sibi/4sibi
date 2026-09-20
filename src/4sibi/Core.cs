using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;

namespace Sibi;

public record Settings
{
    public string Language { get; set; } = "de";
    public string Accent { get; set; } = "#FF1838";
    public int Aps { get; set; } = 20;
    public string Action { get; set; } = "left";
    public int ActionKey { get; set; } = 0x46;
    public int Trigger { get; set; } = 0x75;
    public int Emergency { get; set; } = 0x7B;
    public bool Hold { get; set; }
    public bool Overlay { get; set; } = true;
    public bool RobloxOnly { get; set; } = true;
    public bool PauseOnFocusLoss { get; set; } = true;
    public bool Tray { get; set; } = true;
    public bool Beep { get; set; }
    public bool Topmost { get; set; }
    public bool Randomize { get; set; }
    public double Glow { get; set; } = 0.45;
    public double OverlayOpacity { get; set; } = 0.95;
    public double OverlayX { get; set; } = -1;
    public double OverlayY { get; set; } = -1;
    public double OverlayScale { get; set; } = 1;
    public bool OverlayLocked { get; set; }
    public bool OverlayShowAps { get; set; } = true;
    public bool OverlayShowHotkey { get; set; } = true;
    public bool OverlayShowProfile { get; set; }
    public string ActiveProfileId { get; set; } = "";
    public static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "4sibi");
    public static string FilePath => Path.Combine(DirectoryPath, "settings.json");
    public void Normalize()
    {
        Language = Language == "en" ? "en" : "de";
        if (!new[] { "#FF1838", "#24D989", "#358BFF", "#AD64FF", "#FF9F32", "#EEEEF4" }.Contains(Accent)) Accent = "#FF1838";
        Aps = Math.Clamp(Aps, 1, 1000);
        if (!new[] { "left", "right", "key" }.Contains(Action)) Action = "left";
        if (!Bindings.Valid(Trigger)) Trigger = 0x75;
        if (!Bindings.Valid(Emergency) || Emergency == Trigger) Emergency = Trigger == 0x7B ? 0x7A : 0x7B;
        if (!Bindings.Valid(ActionKey) || ActionKey <= 6) ActionKey = 0x46;
        if (Bindings.Conflict(this)) { Trigger = 0x75; Emergency = 0x7B; ActionKey = 0x46; }
        Glow = double.IsFinite(Glow) ? Math.Clamp(Glow, 0, 1) : .45;
        OverlayOpacity = double.IsFinite(OverlayOpacity) ? Math.Clamp(OverlayOpacity, .3, 1) : .95;
        if (!double.IsFinite(OverlayX)) OverlayX = -1;
        if (!double.IsFinite(OverlayY)) OverlayY = -1;
        OverlayScale = double.IsFinite(OverlayScale) ? Math.Clamp(OverlayScale, .75, 1.5) : 1;
        ActiveProfileId ??= "";
    }
    public static Settings Load()
    {
        try { var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new(); s.Normalize(); return s; }
        catch { return new(); }
    }
    public void Save(string? path = null)
    {
        path ??= FilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, path, true);
    }
}

public static class Bindings
{
    public static bool Valid(int key) => key is >= 1 and <= 254 && key != 3;
    public static int Output(Settings s) => s.Action == "left" ? 1 : s.Action == "right" ? 2 : s.ActionKey;
    public static bool Conflict(Settings s) => s.Trigger == s.Emergency || Output(s) == s.Trigger || Output(s) == s.Emergency;
    public static string Name(int vk)
    {
        if (vk == 1) return "Mouse 1";
        if (vk == 2) return "Mouse 2";
        if (vk == 4) return "Mouse 3";
        if (vk == 5) return "Mouse 4";
        if (vk == 6) return "Mouse 5";
        return System.Windows.Input.KeyInterop.KeyFromVirtualKey(vk).ToString();
    }
}

// Deterministic state machine; no OS input is emitted here.
public sealed class MacroState
{
    public bool Armed { get; private set; }
    public bool Running { get; private set; }
    bool previousTrigger, needsRelease;
    public double NextDue { get; private set; }
    public void Stop() { Armed = Running = false; needsRelease = true; NextDue = 0; }
    public void Toggle() { if (Armed) Stop(); else { Armed = true; NextDue = 0; } }
    public bool Tick(double now, bool trigger, bool emergency, bool allowed, bool suspended, Settings s, double randomUnit)
    {
        bool rising = trigger && !previousTrigger;
        previousTrigger = trigger;
        if (!trigger) needsRelease = false;
        if (emergency || suspended) { Stop(); return false; }
        if (s.Hold) {
            if (!trigger) { Armed = Running = false; NextDue = 0; }
            else if (!needsRelease && rising) { Armed = true; NextDue = 0; }
        } else if (rising && !needsRelease) Toggle();
        if (Running && !allowed) { Stop(); return false; }
        Running = Armed && allowed;
        if (!Running || now < NextDue) return false;
        double interval = 1000.0 / s.Aps;
        if (s.Randomize) interval *= 1 + (Math.Clamp(randomUnit, 0, 1) * 2 - 1) * .15;
        // Never replay a backlog after a stall.
        NextDue = now + interval;
        return true;
    }
}

public static class Native
{
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT point);
    [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr window, uint flags);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint count, INPUT[] inputs, int size);
    [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code, uint type);
    [DllImport("winmm.dll")] public static extern uint timeBeginPeriod(uint period);
    [DllImport("winmm.dll")] public static extern uint timeEndPeriod(uint period);
    [StructLayout(LayoutKind.Sequential)] public struct INPUT { public uint type; public UNION u; }
    [StructLayout(LayoutKind.Explicit)] public struct UNION { [FieldOffset(0)] public MOUSE mouse; [FieldOffset(0)] public KEY key; }
    [StructLayout(LayoutKind.Sequential)] public struct MOUSE { public int x, y; public uint data, flags, time; public UIntPtr extra; }
    [StructLayout(LayoutKind.Sequential)] public struct KEY { public ushort vk, scan; public uint flags, time; public UIntPtr extra; }
    public static bool Down(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;
    public static INPUT[] Inputs(Settings s)
    {
        if (s.Action != "key") {
            uint down = s.Action == "left" ? 2u : 8u;
            return new[] { new INPUT { u = new UNION { mouse = new MOUSE { flags = down } } }, new INPUT { u = new UNION { mouse = new MOUSE { flags = down * 2 } } } };
        }
        uint mapped = MapVirtualKey((uint)s.ActionKey, 4);
        bool extended = (mapped & 0xFF00) != 0;
        uint flags = mapped == 0 ? 0u : 8u | (extended ? 1u : 0u);
        KEY k = new() { vk = mapped == 0 ? (ushort)s.ActionKey : (ushort)0, scan = (ushort)(mapped & 0xFF), flags = flags };
        KEY up = k; up.flags |= 2;
        return new[] { new INPUT { type = 1, u = new UNION { key = k } }, new INPUT { type = 1, u = new UNION { key = up } } };
    }
    public static bool Send(Settings s)
    {
        var inputs = Inputs(s);
        uint result = SendInput(2, inputs, Marshal.SizeOf<INPUT>());
        if (result == 1) SendInput(1, new[] { inputs[1] }, Marshal.SizeOf<INPUT>()); // release after partial delivery
        return result == 2;
    }
    public static bool IsRoblox(string name) => name.Equals("RobloxPlayerBeta", StringComparison.OrdinalIgnoreCase) || name.Equals("RobloxPlayer", StringComparison.OrdinalIgnoreCase);
    public static List<(int Id, string Name)> RobloxClients()
    {
        var result = new List<(int, string)>();
        foreach (var p in Process.GetProcesses()) { using (p) { try { if (IsRoblox(p.ProcessName)) result.Add((p.Id, p.ProcessName)); } catch { } } }
        return result;
    }
}

public sealed class Engine : IDisposable
{
    readonly object gate = new();
    readonly MacroState state = new();
    readonly Thread worker;
    Settings config;
    TestTarget? testTarget;
    double testStarted = -1;
    volatile bool disposed;
    public volatile bool Suspended;
    public string Status { get { lock (gate) return error ? "error" : state.Running ? "running" : state.Armed ? "waiting" : "paused"; } }
    bool error;
    public long Count;
    public double ActualAps;
    public bool RobloxForeground;
    public Engine(Settings settings, bool suspended = false) { config = settings with { }; Suspended = suspended; worker = new Thread(Loop) { IsBackground = true, Name = "4sibi input scheduler" }; worker.Start(); }
    public void Configure(Settings s) { lock (gate) { state.Stop(); config = s with { }; error = false; } }
    public void Toggle() { lock (gate) { error = false; state.Toggle(); } }
    public void Stop() { lock (gate) state.Stop(); }
    public void SetTestTarget(TestTarget? target) { lock (gate) { state.Stop(); testTarget = target; testStarted = -1; } }
    void Loop()
    {
        var clock = Stopwatch.StartNew();
        uint cachedPid = uint.MaxValue;
        bool isRoblox = false;
        long lastCheck = -1000, rateAt = 0, rateCount = 0;
        IntPtr startFocus = IntPtr.Zero;
        bool wasRunning = false, highResolution = false;
        try {
            while (!disposed) {
                IntPtr hwnd = Native.GetForegroundWindow();
                Native.GetWindowThreadProcessId(hwnd, out uint pid);
                long ms = clock.ElapsedMilliseconds;
                if (pid != cachedPid || ms - lastCheck > 500) {
                    cachedPid = pid; lastCheck = ms;
                    try { using var p = Process.GetProcessById((int)pid); isRoblox = Native.IsRoblox(p.ProcessName); } catch { isRoblox = false; }
                }
                RobloxForeground = isRoblox;
                lock (gate) {
                    bool allowed;
                    if (testTarget != null) {
                        bool cursorKnown = Native.GetCursorPos(out var cursor);
                        cursorKnown = cursorKnown && Native.GetAncestor(Native.WindowFromPoint(cursor), 2) == testTarget.Handle;
                        allowed = testTarget.Allows(hwnd, cursor.X, cursor.Y, cursorKnown);
                        if (testStarted >= 0 && clock.Elapsed.TotalMilliseconds - testStarted >= 10000) allowed = false;
                    } else allowed = hwnd != IntPtr.Zero && pid != Environment.ProcessId && (!config.RobloxOnly || isRoblox);
                    if (wasRunning && config.PauseOnFocusLoss && hwnd != startFocus) { state.Stop(); wasRunning = false; }
                    bool emit = state.Tick(clock.Elapsed.TotalMilliseconds, Native.Down(config.Trigger), Native.Down(config.Emergency), allowed, Suspended, config, Random.Shared.NextDouble());
                    if (state.Running && !wasRunning) startFocus = hwnd;
                    wasRunning = state.Running;
                    if (state.Running != highResolution) { if (state.Running) Native.timeBeginPeriod(1); else Native.timeEndPeriod(1); highResolution = state.Running; }
                    if (emit) { if (testTarget != null && testStarted < 0) testStarted = clock.Elapsed.TotalMilliseconds; if (Native.Send(config)) Interlocked.Increment(ref Count); else { state.Stop(); error = true; } }
                }
                if (ms - rateAt >= 1000) { long n = Interlocked.Read(ref Count); ActualAps = (n - rateCount) * 1000.0 / (ms - rateAt); rateCount = n; rateAt = ms; }
                Thread.Sleep(1);
            }
        } catch (Exception ex) {
            lock (gate) { state.Stop(); error = true; }
            try { Directory.CreateDirectory(Settings.DirectoryPath); File.AppendAllText(Path.Combine(Settings.DirectoryPath, "error.log"), DateTime.Now + " scheduler: " + ex + "\n"); } catch { }
        } finally { if (highResolution) Native.timeEndPeriod(1); }
    }
    public void Dispose() { disposed = true; worker.Join(2000); }
}
