using System;
using System.Threading.Tasks;
using PG.Content;
using PG.Powers;
using PG.Render;
using PG.Sim;
using PG.WorldGen;

namespace PG.UI
{
    // Implemented by the boot layer. UI reads the simulation and sends commands; it never writes sim arrays.
    public interface IGameHost
    {
        ContentDB Content { get; }
        SimWorld Sim { get; }
        PowerCommandQueue Commands { get; }
        MapRenderer Renderer { get; }
        int TicksLastSecond { get; }

        event Action WorldChanged;

        void StartNewWorld(WorldGenSettings settings);
        Task SaveToSlot(int slot);
        Task LoadSlot(int slot);
    }
}
