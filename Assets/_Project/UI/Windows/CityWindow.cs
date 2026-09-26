using System.Text;
using PG.Content;
using PG.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace PG.UI
{
    // Bölüm 8.5 city window, basic Faz 4 version: read-only summary of one city, refreshed at 4 Hz (Bölüm 8.3).
    // Opened by an inspect click (UiRoot). Not modal: the map stays usable while it is open.
    public sealed class CityWindow
    {
        const float RefreshSeconds = 0.25f;

        readonly IGameHost _host;
        readonly VisualElement _root;
        readonly Label _title;
        readonly Label _species, _center, _founded, _leader, _population, _happiness, _gold, _famine, _storage, _food;
        readonly Label _stock, _jobs, _buildings;
        readonly StringBuilder _sb = new StringBuilder(512);
        int[] _complete = new int[0], _construction = new int[0];
        int _city = -1;
        float _next;

        public CityWindow(VisualElement layer, IGameHost host)
        {
            _host = host;
            _root = new VisualElement();
            _root.AddToClassList("pg-window");
            _root.AddToClassList("pg-city");
            _root.style.display = DisplayStyle.None;
            layer.Add(_root);

            _title = new Label();
            _title.AddToClassList("pg-window-title");
            _root.Add(_title);

            var scroll = new ScrollView();
            scroll.AddToClassList("pg-city-body");
            _root.Add(scroll);

            _species = Field(scroll, "ui.city.species");
            _center = Field(scroll, "ui.city.center");
            _founded = Field(scroll, "ui.city.founded");
            _leader = Field(scroll, "ui.city.leader");
            _population = Field(scroll, "ui.city.population");
            _happiness = Field(scroll, "ui.city.happiness");
            _gold = Field(scroll, "ui.city.gold");
            _famine = Field(scroll, "ui.city.famine");
            _storage = Field(scroll, "ui.city.storage");
            _food = Field(scroll, "ui.city.food");
            _stock = Section(scroll, "ui.city.stock");
            _jobs = Section(scroll, "ui.city.jobs");
            _buildings = Section(scroll, "ui.city.buildings");

            var close = new Button(Close) { text = Loc.T("ui.close") };
            close.AddToClassList("pg-button");
            close.style.alignSelf = Align.FlexEnd;
            close.style.marginTop = 8;
            _root.Add(close);
        }

        public bool IsOpen => _root.style.display == DisplayStyle.Flex;
        public int City => IsOpen ? _city : -1;

        public void Open(int city)
        {
            _city = city;
            _root.style.display = DisplayStyle.Flex;
            _next = 0f;
            Update();
        }

        public void Close()
        {
            _root.style.display = DisplayStyle.None;
            _city = -1;
        }

        public void Update()
        {
            if (!IsOpen || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + RefreshSeconds;

            var sim = _host.Sim;
            var civ = sim?.Civ;
            if (civ == null || _city < 0 || _city >= civ.Cities.Count)
            {
                Close();
                return;
            }
            var db = _host.Content;
            var city = civ.Cities[_city];
            int year = sim.Clock.Year;

            _title.text = city.Dead ? Loc.F("ui.city.dead_fmt", city.Name) : city.Name;
            _species.text = Loc.Name(db.Species[city.Species]);
            _center.text = Loc.F("ui.city.center_fmt", CenterName(civ, city), city.HallTier);
            _founded.text = Loc.F("ui.city.founded_fmt", city.FoundedYear + 1, Mathf.Max(0, year - city.FoundedYear));
            _leader.text = LeaderText(sim, city);
            _population.text = Loc.F("ui.city.population_fmt", city.Population, city.HousingCapacity);
            _happiness.text = city.Happiness.ToString();
            _gold.text = city.Gold.ToString();
            _famine.text = Loc.F("ui.city.famine_fmt", city.FamineMonths);
            _storage.text = civ.StockTotal(city) + " / " + city.StorageCapacity;
            _food.text = city.FoodTotal.ToString();

            // Stock: non-zero resources.
            _sb.Clear();
            for (int r = 0; r < city.Stock.Length && r < db.Resources.Count; r++)
            {
                if (city.Stock[r] == 0) continue;
                Separator();
                _sb.Append(Loc.F("ui.city.amount_fmt", Loc.Name(db.Resources[r]), city.Stock[r]));
            }
            _stock.text = _sb.Length > 0 ? _sb.ToString() : Loc.T("ui.city.empty");

            // Jobs: workers vs quota, only jobs with either.
            _sb.Clear();
            if (city.JobCount != null && city.JobQuota != null)
                for (int j = 0; j < db.Jobs.Count && j < city.JobCount.Length && j < city.JobQuota.Length; j++)
                {
                    if (city.JobCount[j] == 0 && city.JobQuota[j] == 0) continue;
                    Separator();
                    _sb.Append(Loc.F("ui.city.job_fmt", Loc.Name(db.Jobs[j]), city.JobCount[j], city.JobQuota[j]));
                }
            _jobs.text = _sb.Length > 0 ? _sb.ToString() : Loc.T("ui.city.empty");

            // Buildings by type: complete + under construction.
            int defs = db.Buildings.Count;
            if (_complete.Length != defs)
            {
                _complete = new int[defs];
                _construction = new int[defs];
            }
            System.Array.Clear(_complete, 0, defs);
            System.Array.Clear(_construction, 0, defs);
            var buildings = civ.Buildings;
            foreach (int b in city.Buildings)
            {
                if (b < 0 || b >= buildings.Length) continue;
                var data = buildings[b];
                if (data.Def >= defs) continue;
                if (data.State == BuildingState.Complete) _complete[data.Def]++;
                else if (data.State == BuildingState.Construction) _construction[data.Def]++;
            }
            _sb.Clear();
            for (int d = 0; d < defs; d++)
            {
                if (_complete[d] == 0 && _construction[d] == 0) continue;
                _sb.Append(_sb.Length > 0 ? "\n" : "");
                string name = Loc.Name(db.Buildings[d]);
                _sb.Append(_construction[d] > 0
                    ? Loc.F("ui.city.building_construction_fmt", name, _complete[d], _construction[d])
                    : Loc.F("ui.city.building_fmt", name, _complete[d]));
            }
            _buildings.text = _sb.Length > 0 ? _sb.ToString() : Loc.T("ui.city.empty");
        }

        void Separator()
        {
            if (_sb.Length > 0) _sb.Append(" · ");
        }

        // City shown by an inspect click at a tile: the building's city first, then the zone owner. -1 = none.
        public static int CityAt(SimWorld sim, int x, int y)
        {
            var civ = sim?.Civ;
            var map = sim?.World;
            if (civ == null || !map.InBounds(x, y)) return -1;
            int b = map.Building[map.Index(x, y)];
            if (b >= 0 && b < civ.Buildings.Length)
            {
                int c = civ.Buildings[b].City;
                if (c >= 0 && c < civ.Cities.Count && !civ.Cities[c].Dead) return c;
            }
            int z = civ.ZoneOwner(x, y);
            return z >= 0 && z < civ.Cities.Count && !civ.Cities[z].Dead ? z : -1;
        }

        static string CenterName(CivState civ, City city)
        {
            if (city.CenterBuilding >= 0 && city.CenterBuilding < civ.Buildings.Length)
                return Loc.Name(civ.DefOf(city.CenterBuilding));
            int tier = city.HallTier;
            var halls = civ.Ids.HallByTier;
            if (tier < halls.Length && halls[tier] >= 0) return Loc.Name(civ.Content.Buildings[halls[tier]]);
            return Loc.T("ui.city.none");
        }

        // Units have no names yet (Bölüm 6.2 NamePool arrives with Faz 5): the leader is shown by species and age.
        static string LeaderText(SimWorld sim, City city)
        {
            if (city.Dead) return Loc.T("ui.city.none");
            int leader = CivMonthlySystem.LeaderIndex(sim.Units, city);
            if (leader < 0) return Loc.T("ui.city.none");
            var u = sim.Units.Store;
            return Loc.F("ui.city.leader_fmt", Loc.Name(u.SpeciesOf(leader)), Mathf.FloorToInt(u.AgeYears(leader, sim.Clock.Tick)));
        }

        static Label Field(VisualElement parent, string key)
        {
            var row = new VisualElement();
            row.AddToClassList("pg-city-field");
            parent.Add(row);
            var name = new Label(Loc.T(key));
            name.AddToClassList("pg-city-key");
            row.Add(name);
            var value = new Label();
            value.AddToClassList("pg-city-value");
            row.Add(value);
            return value;
        }

        static Label Section(VisualElement parent, string key)
        {
            var header = new Label(Loc.T(key));
            header.AddToClassList("pg-city-section");
            parent.Add(header);
            var value = new Label();
            value.AddToClassList("pg-city-list");
            parent.Add(value);
            return value;
        }
    }
}
