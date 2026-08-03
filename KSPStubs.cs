// Stubs pour compiler BDGPSPreloader dans l'intégration continue de GitHub Actions.
// Ces classes fournissent des définitions minimales des types KSP pour permettre la compilation
// sans avoir à embarquer les DLLs propriétaires de Squad (KSP) sur GitHub.

namespace UnityEngine
{
    public class MonoBehaviour
    {
        public static void Destroy(object obj) {}
    }
    
    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
        }
    }
    
    public struct Vector2
    {
        public float x, y;
        public static Vector2 zero => new Vector2(0, 0);
        public Vector2(float x, float y) { this.x = x; this.y = y; }
    }
    
    public struct Vector3d
    {
        public double x, y, z;
        public Vector3d(double x, double y, double z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
    }
    
    public class Texture2D : Texture
    {
        public static Texture2D whiteTexture => null;
    }
    
    public class Texture {}
    public class Sprite
    {
        public Texture2D texture => null;
        public static Sprite Create(Texture2D t, Rect r, Vector2 pivot) => null;
    }
    
    public class Transform
    {
        public Vector3 position { get; set; }
        public Vector3 forward => new Vector3();
        public Vector3 up => new Vector3();
        public void LookAt(Vector3 target) {}
    }

    public class GameObject
    {
        public Transform transform => null;
        public GameObject(string name) {}
        public T AddComponent<T>() where T : Component => null;
    }

    public class Component : MonoBehaviour {}

    public class Camera : Component
    {
        public Rect rect { get; set; }
        public int depth { get; set; }
        public float fieldOfView { get; set; }
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a;
        public static float Distance(Vector3 a, Vector3 b) => 0.0f;
        public static Vector3 operator *(Vector3 a, float d) => new Vector3(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
    }

    public static class Mathf
    {
        public static float Min(float a, float b) => a;
        public static int Min(int a, int b) => a;
    }

    public static class GUILayout
    {
        public static void BeginVertical() {}
        public static void EndVertical() {}
        public static void BeginHorizontal() {}
        public static void EndHorizontal() {}
        public static void Label(string text, object style = null, params object[] options) {}
        public static string TextField(string text, params object[] options) => text;
        public static bool Button(string text, params object[] options) => false;
        public static bool Toggle(bool value, string text, params object[] options) => value;
        public static void Space(float size) {}
        public static Vector2 BeginScrollView(Vector2 scroll, params object[] options) => scroll;
        public static void EndScrollView() {}
        public static Rect Window(int id, Rect screenRect, GUI.WindowFunction func, string text, params object[] options) => screenRect;
        public static object Width(float width) => null;
        public static object Height(float height) => null;
    }
    
    public static class GUI
    {
        public delegate void WindowFunction(int id);
        public static void DragWindow(Rect r) {}
        public static GUISkin skin => new GUISkin();
    }
    
    public class GUISkin
    {
        public object box => null;
    }
    
    public static class Debug
    {
        public static void LogError(string msg) {}
    }
}

public class KSPAddon : System.Attribute
{
    public enum Startup { Flight }
    public KSPAddon(Startup startup, bool once) {}
}

public class ApplicationLauncherButton {}
public class ApplicationLauncher
{
    public static ApplicationLauncher Instance => null;
    public static bool Ready => false;
    public ApplicationLauncherButton AddModApplication(
        System.Action onTrue, System.Action onFalse,
        System.Action onHover, System.Action onHoverOut,
        System.Action glEnable, System.Action glDisable,
        AppScenes scenes, UnityEngine.Texture texture) => null;
        
    public void RemoveModApplication(ApplicationLauncherButton btn) {}
    
    public enum AppScenes { FLIGHT }
}

public class GameEvents
{
    public static EventVoid onGUIApplicationLauncherReady = new EventVoid();
    public static EventVoid onGUIApplicationLauncherDestroyed = new EventVoid();
    
    public class EventVoid
    {
        public void Add(System.Action action) {}
        public void Remove(System.Action action) {}
    }
}

public class GameDatabase
{
    public static GameDatabase Instance => null;
    public UnityEngine.Texture2D GetTexture(string name, bool b) => null;
}

public class Part
{
    public System.Collections.Generic.List<PartModule> Modules = new System.Collections.Generic.List<PartModule>();
}

public class PartModule
{
    public Part part => null;
    public Vessel vessel => null;
}

public class Vessel
{
    public string vesselName => "";
    public UnityEngine.Transform transform => null;
    public CelestialBody mainBody => null;
    public bool packed => false;
    public bool loaded => false;
    public System.Collections.Generic.List<Part> parts = new System.Collections.Generic.List<Part>();
}

public class CelestialBody
{
    public UnityEngine.Vector3d GetLatitudeAndLongitude(UnityEngine.Vector3 position) => new UnityEngine.Vector3d();
    public double GetAltitude(UnityEngine.Vector3 position) => 0.0;
}

public static class FlightGlobals
{
    public static Vessel ActiveVessel => null;
    public static System.Collections.Generic.List<Vessel> Vessels = new System.Collections.Generic.List<Vessel>();
}

public class ScreenMessageStyle
{
    public static object UPPER_CENTER => null;
}

public static class ScreenMessages
{
    public static void PostScreenMessage(string msg, float duration, object style) {}
}

public static class KSPUtil
{
    public static string ApplicationRootPath => "";
}

public class AssemblyLoader
{
    public static System.Collections.Generic.List<LoadedAssembly> loadedAssemblies = new System.Collections.Generic.List<LoadedAssembly>();
    
    public class LoadedAssembly
    {
        public System.Reflection.Assembly assembly => null;
    }
}
