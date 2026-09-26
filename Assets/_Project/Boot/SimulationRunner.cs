using System.Diagnostics;
using PG.Core;
using PG.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace PG.Boot
{
    // Bölüm 1.3.3 accumulator loop. Drives ticks only; simulation logic lives in ISimSystem implementations.
    public sealed class SimulationRunner : MonoBehaviour
    {
        readonly Stopwatch _budget = new Stopwatch();
        SimWorld _sim;
        float _accumulator;
        int _ticksThisSecond;
        float _secondTimer;

        public float Interpolation { get; private set; } // 0..1 between the last and next tick (unit rendering)
        public int TicksLastSecond { get; private set; }

        public void Bind(SimWorld sim)
        {
            _sim = sim;
            _accumulator = 0f;
        }

        void Update()
        {
            if (_sim == null) return;
            var clock = _sim.Clock;
            int ran = 0;

            if (clock.Paused)
            {
                _sim.ApplyInputWhilePaused();
                if (clock.ConsumeStepRequest())
                {
                    _sim.Tick();
                    ran = 1;
                }
                Interpolation = 0f;
            }
            else if (clock.SuperSpeed)
            {
                _budget.Restart();
                do
                {
                    _sim.Tick();
                    ran++;
                } while (_budget.Elapsed.TotalMilliseconds < SimConst.SuperSpeedBudgetMs);
                Interpolation = 0f;
            }
            else
            {
                _accumulator += Time.unscaledDeltaTime * clock.SpeedMultiplier;
                int ticks = Mathf.Min((int)(_accumulator / SimConst.TickDt), SimConst.MaxTicksPerFrame);
                _accumulator -= ticks * SimConst.TickDt;
                if (ticks == SimConst.MaxTicksPerFrame) _accumulator = 0f; // cannot keep up: drop the backlog
                for (int i = 0; i < ticks; i++) _sim.Tick();
                ran = ticks;
                Interpolation = _accumulator / SimConst.TickDt;
            }

            // Super speed renders only every 4th frame.
            OnDemandRendering.renderFrameInterval = clock.SuperSpeed && !clock.Paused ? SimConst.SuperSpeedRenderEvery : 1;

            _ticksThisSecond += ran;
            _secondTimer += Time.unscaledDeltaTime;
            if (_secondTimer >= 1f)
            {
                TicksLastSecond = _ticksThisSecond;
                _ticksThisSecond = 0;
                _secondTimer -= 1f;
            }
        }

        void OnDisable() => OnDemandRendering.renderFrameInterval = 1;
    }
}
