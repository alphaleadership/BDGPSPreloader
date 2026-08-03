# Stubs pour compiler BDGPSPreloader dans l'intégration continue de GitHub Actions.
# Ces classes fournissent des définitions minimales des types KSP pour permettre la compilation
# sans avoir à embarquer les DLLs propriétaires de Squad (KSP) sur GitHub.

namespace UnityEngine
{
    public class MonoBehaviour {}
    
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
    
    public static class GUILayout
    {
        public static void BeginVertical() {}
        public static void EndVertical() {}
        public static void BeginHorizontal() {}
        public static void EndHorizontal() {}
        public static void Label(string text, object style = null, params object[] options) {}
        public static string TextField(string text, params object[] options) => text;
        public static bool Button(string text, params object[] options) => false;
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
        public static object skin => new GUISkin();
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
