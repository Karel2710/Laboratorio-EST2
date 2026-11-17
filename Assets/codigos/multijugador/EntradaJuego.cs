using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TowerDefenseGameplay
{
    /// <summary>
    /// Lectura de teclado y ratón compatible con el Input System nuevo (el que usa el proyecto)
    /// y con el Input Manager clásico.
    /// </summary>
    public static class EntradaJuego
    {
#if ENABLE_INPUT_SYSTEM
        private static Key AKey(KeyCode codigo)
        {
            switch (codigo)
            {
                case KeyCode.Alpha1: return Key.Digit1;
                case KeyCode.Alpha2: return Key.Digit2;
                case KeyCode.Alpha3: return Key.Digit3;
                case KeyCode.Alpha4: return Key.Digit4;
                case KeyCode.A: return Key.A;
                case KeyCode.B: return Key.B;
                case KeyCode.C: return Key.C;
                case KeyCode.E: return Key.E;
                case KeyCode.G: return Key.G;
                case KeyCode.Q: return Key.Q;
                case KeyCode.R: return Key.R;
                case KeyCode.T: return Key.T;
                case KeyCode.W: return Key.W;
                case KeyCode.Escape: return Key.Escape;
                case KeyCode.LeftShift: return Key.LeftShift;
                case KeyCode.RightShift: return Key.RightShift;
                default: return Key.None;
            }
        }

        public static bool Presionada(KeyCode codigo)
        {
            Keyboard kb = Keyboard.current;
            Key k = AKey(codigo);
            return kb != null && k != Key.None && kb[k].wasPressedThisFrame;
        }

        public static bool Mantenida(KeyCode codigo)
        {
            Keyboard kb = Keyboard.current;
            Key k = AKey(codigo);
            return kb != null && k != Key.None && kb[k].isPressed;
        }

        public static Vector2 PosicionMouse => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        public static bool ClicIzquierdo => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        public static bool ClicDerecho => Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
#else
        public static bool Presionada(KeyCode codigo) => Input.GetKeyDown(codigo);
        public static bool Mantenida(KeyCode codigo) => Input.GetKey(codigo);
        public static Vector2 PosicionMouse => Input.mousePosition;
        public static bool ClicIzquierdo => Input.GetMouseButtonDown(0);
        public static bool ClicDerecho => Input.GetMouseButtonDown(1);
#endif

        public static bool ShiftMantenido => Mantenida(KeyCode.LeftShift) || Mantenida(KeyCode.RightShift);

        /// <summary>Posición del ratón en el plano del mapa (Z = 0).</summary>
        public static Vector2 PosicionMouseMundo()
        {
            Camera cam = Camera.main;
            if (cam == null) return Vector2.zero;
            Vector2 p = PosicionMouse;
            Vector3 m = cam.ScreenToWorldPoint(new Vector3(p.x, p.y, Mathf.Abs(cam.transform.position.z)));
            return new Vector2(m.x, m.y);
        }
    }
}
