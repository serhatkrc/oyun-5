using System;
using UnityEngine.InputSystem;

namespace PG.UI
{
    // Bölüm 1.12.3 input actions, bound in code (keyboard/mouse; touch handled by the camera).
    public sealed class GameInput : IDisposable
    {
        public readonly InputAction Pan = new InputAction("Pan", InputActionType.Value);
        public readonly InputAction Zoom = new InputAction("Zoom", InputActionType.Value, "<Mouse>/scroll/y");
        public readonly InputAction ZoomIn = new InputAction("ZoomIn", InputActionType.Button);
        public readonly InputAction ZoomOut = new InputAction("ZoomOut", InputActionType.Button);
        public readonly InputAction UsePower = new InputAction("UsePower", InputActionType.Button, "<Mouse>/leftButton");
        public readonly InputAction CancelPower = new InputAction("CancelPower", InputActionType.Button);
        public readonly InputAction DragPan = new InputAction("DragPan", InputActionType.Button, "<Mouse>/middleButton");
        public readonly InputAction PanModifier = new InputAction("PanModifier", InputActionType.Button, "<Keyboard>/space");
        public readonly InputAction Step = new InputAction("Step", InputActionType.Button, "<Keyboard>/period");
        public readonly InputAction BrushSizeUp = new InputAction("BrushSizeUp", InputActionType.Button, "<Keyboard>/rightBracket");
        public readonly InputAction BrushSizeDown = new InputAction("BrushSizeDown", InputActionType.Button, "<Keyboard>/leftBracket");
        public readonly InputAction TabPrev = new InputAction("TabPrev", InputActionType.Button, "<Keyboard>/q");
        public readonly InputAction TabNext = new InputAction("TabNext", InputActionType.Button, "<Keyboard>/e");
        public readonly InputAction DebugOverlay = new InputAction("DebugOverlay", InputActionType.Button, "<Keyboard>/f3");
        public readonly InputAction RegionOverlay = new InputAction("RegionOverlay", InputActionType.Button, "<Keyboard>/f4");
        public readonly InputAction Point = new InputAction("Point", InputActionType.PassThrough, "<Pointer>/position");
        public readonly InputAction[] Speed = new InputAction[7];

        public GameInput()
        {
            Pan.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            Pan.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            ZoomIn.AddBinding("<Keyboard>/equals");
            ZoomIn.AddBinding("<Keyboard>/numpadPlus");
            ZoomOut.AddBinding("<Keyboard>/minus");
            ZoomOut.AddBinding("<Keyboard>/numpadMinus");
            UsePower.AddBinding("<Touchscreen>/primaryTouch/press");
            CancelPower.AddBinding("<Mouse>/rightButton");
            CancelPower.AddBinding("<Keyboard>/escape");
            for (int i = 0; i < Speed.Length; i++)
                Speed[i] = new InputAction("Speed" + (i + 1), InputActionType.Button, "<Keyboard>/" + (i + 1));

            foreach (var action in All()) action.Enable();
        }

        InputAction[] All()
        {
            var list = new System.Collections.Generic.List<InputAction>
            {
                Pan, Zoom, ZoomIn, ZoomOut, UsePower, CancelPower, DragPan, PanModifier, Step,
                BrushSizeUp, BrushSizeDown, TabPrev, TabNext, DebugOverlay, RegionOverlay, Point,
            };
            list.AddRange(Speed);
            return list.ToArray();
        }

        public void Dispose()
        {
            foreach (var action in All()) action.Dispose();
        }
    }
}
