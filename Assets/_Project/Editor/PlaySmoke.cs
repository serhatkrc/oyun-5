using PG.Boot;
using PG.Persistence;
using PG.Powers;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PG.EditorTools
{
    // Plays the GameBootstrap scene for a few hundred frames, drives brush/save/load through the host
    // and counts console errors. Batch: Unity -batchmode -projectPath . -executeMethod PG.EditorTools.PlaySmoke.Run
    [InitializeOnLoad]
    public static class PlaySmoke
    {
        const string ActiveKey = "PG.PlaySmoke.Active";
        const string FramesKey = "PG.PlaySmoke.Frames";
        const string ErrorsKey = "PG.PlaySmoke.Errors";
        const string ScenePath = "Assets/Scenes/GameBootstrap.unity";
        const int SmokeSlot = 901;
        const int FramesToRun = 400;

        static PlaySmoke()
        {
            if (SessionState.GetBool(ActiveKey, false)) Hook();
        }

        [MenuItem("PixelGenesis/Play Smoke Run")]
        public static void Run()
        {
            DataSync.Sync();
            EditorSceneManager.OpenScene(ScenePath);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetInt(FramesKey, 0);
            SessionState.SetInt(ErrorsKey, 0);
            Hook();
            EditorApplication.EnterPlaymode();
        }

        static void Hook()
        {
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                SessionState.SetInt(ErrorsKey, SessionState.GetInt(ErrorsKey, 0) + 1);
        }

        static void Update()
        {
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying) return;
            int frame = SessionState.GetInt(FramesKey, 0) + 1;
            SessionState.SetInt(FramesKey, frame);

            var host = Object.FindFirstObjectByType<GameBootstrap>();
            if (host != null && host.Sim != null)
            {
                if (frame == 30) host.Sim.Clock.SetSpeed(6);
                if (frame >= 40 && frame < 120 && frame % 4 == 0) Brush(host, frame);
                if (frame == 100 && host.Renderer != null) host.Renderer.ShowRegions = true;
                if (frame == 150) _ = host.SaveToSlot(SmokeSlot);
                if (frame == 250) _ = host.LoadSlot(SmokeSlot);
            }

            if (frame < FramesToRun) return;
            int errors = SessionState.GetInt(ErrorsKey, 0);
            SessionState.SetBool(ActiveKey, false);
            SaveSystem.Delete(SmokeSlot);
            Debug.Log(errors == 0
                ? $"[PlaySmoke] OK: {frame} frames, tick {host?.Sim?.Clock.Tick}, regions {host?.Sim?.Regions.RegionCount}"
                : $"[PlaySmoke] {errors} error(s) logged");
            EditorApplication.ExitPlaymode();
            if (Application.isBatchMode) EditorApplication.Exit(errors == 0 ? 0 : 1);
        }

        static void Brush(GameBootstrap host, int frame)
        {
            var db = host.Content;
            int power = -1;
            for (int i = 0; i < db.Powers.Count && power < 0; i++)
                if (PowerOps.IsSupported(db.Powers[i], db) && (frame / 4) % 3 == i % 3) power = i;
            if (power < 0) return;
            var map = host.Sim.World;
            var at = new int2(map.Width / 2 + frame % 40, map.Height / 2);
            host.Commands.Enqueue(new PowerCommand
            {
                PowerId = (ushort)power,
                From = at,
                To = at + new int2(12, 6),
                Brush = new BrushSpec(BrushShape.Circle, 5),
                Tick = host.Sim.Clock.Tick,
            });
        }
    }
}
