using System;
using System.Collections.Generic;
using PG.Persistence;
using UnityEngine;
using UnityEngine.UIElements;

namespace PG.UI
{
    // Slot list with thumbnails: manual slots save/load/delete, autosaves load/delete. Delete needs a second press.
    public sealed class SaveLoadWindow
    {
        const float ConfirmSeconds = 3f;

        readonly IGameHost _host;
        readonly Action<string> _toast;
        readonly VisualElement _root;
        readonly ScrollView _list;
        readonly List<Texture2D> _thumbs = new List<Texture2D>();
        int _confirmSlot = -1;
        float _confirmUntil;
        bool _busy;

        public SaveLoadWindow(VisualElement layer, IGameHost host, Action<string> toast)
        {
            _host = host;
            _toast = toast;
            _root = new VisualElement();
            _root.AddToClassList("pg-window");
            _root.style.display = DisplayStyle.None;
            layer.Add(_root);

            var title = new Label(Loc.T("ui.save_load"));
            title.AddToClassList("pg-window-title");
            _root.Add(title);

            _list = new ScrollView();
            _list.style.maxHeight = 520;
            _root.Add(_list);

            var close = new Button(Close) { text = Loc.T("ui.close") };
            close.AddToClassList("pg-button");
            close.style.alignSelf = Align.FlexEnd;
            close.style.marginTop = 8;
            _root.Add(close);
        }

        public bool IsOpen => _root.style.display == DisplayStyle.Flex;

        public void Open()
        {
            _root.style.display = DisplayStyle.Flex;
            Refresh();
        }

        public void Close()
        {
            _root.style.display = DisplayStyle.None;
            ClearThumbs();
        }

        void Refresh()
        {
            _list.Clear();
            ClearThumbs();
            foreach (var info in SaveSystem.ListSlots()) _list.Add(Row(info));
        }

        VisualElement Row(SaveSlotInfo info)
        {
            var row = new VisualElement();
            row.AddToClassList("pg-slot");

            var thumb = new Image { scaleMode = ScaleMode.ScaleToFit };
            thumb.AddToClassList("pg-slot-thumb");
            if (info.ThumbnailPng != null && info.ThumbnailPng.Length > 0)
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                if (tex.LoadImage(info.ThumbnailPng))
                {
                    thumb.image = tex;
                    _thumbs.Add(tex);
                }
                else UnityEngine.Object.Destroy(tex);
            }
            row.Add(thumb);

            int number = info.IsAuto ? info.Slot - SaveSystem.AutoSlotBase : info.Slot;
            string name = Loc.F(info.IsAuto ? "ui.autosave_fmt" : "ui.slot_fmt", number);
            string details = info.Exists
                ? Loc.F("ui.slot_info_fmt", info.WorldName, info.Year + 1, info.SizeX, info.SizeY) + "\n" + info.CreatedUtc.ToLocalTime().ToString("g")
                : Loc.T("ui.empty_slot");
            var label = new Label(name + "\n" + details);
            label.AddToClassList("pg-slot-info");
            row.Add(label);

            if (!info.IsAuto) row.Add(MakeButton("ui.save", () => Save(info.Slot)));
            if (info.Exists)
            {
                row.Add(MakeButton("ui.load", () => Load(info.Slot)));
                var delete = MakeButton("ui.delete", null);
                delete.clicked += () => Delete(info.Slot, delete);
                delete.AddToClassList("pg-button--danger");
                row.Add(delete);
            }
            return row;
        }

        async void Save(int slot)
        {
            if (_busy || _host.Sim == null) return;
            _busy = true;
            try
            {
                await _host.SaveToSlot(slot);
                _toast(Loc.T("ui.saved"));
                if (IsOpen) Refresh();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _toast(Loc.F("ui.save_failed", e.Message));
            }
            finally
            {
                _busy = false;
            }
        }

        async void Load(int slot)
        {
            if (_busy) return;
            _busy = true;
            try
            {
                await _host.LoadSlot(slot);
                Close();
                _toast(Loc.T("ui.loaded"));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _toast(Loc.F("ui.load_failed", e.Message));
            }
            finally
            {
                _busy = false;
            }
        }

        void Delete(int slot, Button button)
        {
            if (_confirmSlot != slot || Time.unscaledTime > _confirmUntil)
            {
                _confirmSlot = slot;
                _confirmUntil = Time.unscaledTime + ConfirmSeconds;
                button.text = Loc.T("ui.confirm_delete");
                return;
            }
            _confirmSlot = -1;
            SaveSystem.Delete(slot);
            Refresh();
        }

        static Button MakeButton(string key, Action onClick)
        {
            var b = onClick != null ? new Button(onClick) : new Button();
            b.text = Loc.T(key);
            b.AddToClassList("pg-button");
            return b;
        }

        void ClearThumbs()
        {
            foreach (var t in _thumbs) UnityEngine.Object.Destroy(t);
            _thumbs.Clear();
        }
    }
}
