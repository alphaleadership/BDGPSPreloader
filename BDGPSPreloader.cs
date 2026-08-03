using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;
using UnityEngine;

namespace BDGPSPreloader
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class BDGPSPreloaderMod : MonoBehaviour
    {
        private bool showGUI = false;
        private Rect windowRect = new Rect(100, 100, 350, 450);
        private ApplicationLauncherButton toolbarButton = null;

        // Form fields
        private string newName = "Target Alpha";
        private string newLat = "0.0";
        private string newLon = "0.0";
        private string newAlt = "100";

        // Coordinates database
        [Serializable]
        public class GPSCoordinate
        {
            public string Name { get; set; }
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public double Altitude { get; set; }
        }

        private List<GPSCoordinate> savedCoords = new List<GPSCoordinate>();
        private string savePath;

        private void Start()
        {
            savePath = Path.Combine(KSPUtil.ApplicationRootPath, "GameData/BDGPSPreloader/PluginData/saved_coords.xml");
            LoadCoordinates();
            GameEvents.onGUIApplicationLauncherReady.Add(OnGUIAppLauncherReady);
            GameEvents.onGUIApplicationLauncherDestroyed.Add(OnGUIAppLauncherDestroyed);
        }

        private void OnDestroy()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(OnGUIAppLauncherReady);
            GameEvents.onGUIApplicationLauncherDestroyed.Remove(OnGUIAppLauncherDestroyed);
            if (toolbarButton != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(toolbarButton);
            }
        }

        private void OnGUIAppLauncherReady()
        {
            if (ApplicationLauncher.Ready && toolbarButton == null)
            {
                toolbarButton = ApplicationLauncher.Instance.AddModApplication(
                    OnToggleTrue,
                    OnToggleFalse,
                    null,
                    null,
                    null,
                    null,
                    ApplicationLauncher.AppScenes.FLIGHT,
                    GameDatabase.Instance.GetTexture("BDGPSPreloader/Textures/icon", false) ?? Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.zero).texture
                );
            }
        }

        private void OnGUIAppLauncherDestroyed()
        {
            if (toolbarButton != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(toolbarButton);
                toolbarButton = null;
            }
        }

        private void OnToggleTrue() { showGUI = true; }
        private void OnToggleFalse() { showGUI = false; }

        private void OnGUI()
        {
            if (showGUI)
            {
                windowRect = GUILayout.Window(98541, windowRect, DrawWindow, "BDArmory GPS Preloader", GUILayout.Width(350), GUILayout.Height(400));
            }
        }

        private void DrawWindow(int windowID)
        {
            GUI.DragWindow(new Rect(0, 0, 10000, 20));

            GUILayout.BeginVertical();

            // Add Coordinate Form
            GUILayout.Label("Ajouter une coordonnée GPS :", GUI.skin.box);
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("Nom:", GUILayout.Width(80));
            newName = GUILayout.TextField(newName);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Latitude:", GUILayout.Width(80));
            newLat = GUILayout.TextField(newLat);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Longitude:", GUILayout.Width(80));
            newLon = GUILayout.TextField(newLon);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Altitude:", GUILayout.Width(80));
            newAlt = GUILayout.TextField(newAlt);
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Enregistrer la coordonnée"))
            {
                if (double.TryParse(newLat, out double lat) && double.TryParse(newLon, out double lon) && double.TryParse(newAlt, out double alt))
                {
                    savedCoords.Add(new GPSCoordinate { Name = newName, Latitude = lat, Longitude = lon, Altitude = alt });
                    SaveCoordinates();
                    ScreenMessages.PostScreenMessage("Coordonnée GPS ajoutée !", 3f, ScreenMessageStyle.UPPER_CENTER);
                }
                else
                {
                    ScreenMessages.PostScreenMessage("Erreur : Valeurs numériques invalides !", 3f, ScreenMessageStyle.UPPER_CENTER);
                }
            }

            GUILayout.Space(10);

            // Saved Coordinates List
            GUILayout.Label("Coordonnées enregistrées :", GUI.skin.box);
            Vector2 scroll = Vector2.zero;
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(180));

            GPSCoordinate toDelete = null;
            foreach (var coord in savedCoords)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{coord.Name}\nLat: {coord.Latitude:F4} | Lon: {coord.Longitude:F4} | Alt: {coord.Altitude:F1}m", GUILayout.Width(220));
                
                if (GUILayout.Button("Injecter", GUILayout.Width(60)))
                {
                    InjectGPSTarget(coord);
                }
                
                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    toDelete = coord;
                }
                GUILayout.EndHorizontal();
            }

            if (toDelete != null)
            {
                savedCoords.Remove(toDelete);
                SaveCoordinates();
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void InjectGPSTarget(GPSCoordinate coord)
        {
            try
            {
                // Utilisation de la réflexion ou de la liaison directe si BDArmory.dll est référencé
                // BDArmory utilise BDArmory.Modules.BDATargetManager.GPSTargets pour stocker les coordonnées
                // On va tenter d'appeler BDArmory dynamiquement via Reflection pour éviter les dépendances strictes de compilation si besoin.
                
                var bdAssembly = AssemblyLoader.loadedAssemblies.Find(a => a.assembly.GetName().Name == "BDArmory");
                if (bdAssembly != null)
                {
                    Type targetManagerType = bdAssembly.assembly.GetType("BDArmory.Modules.BDATargetManager");
                    if (targetManagerType != null)
                    {
                        // Structure de GPSTargetInfo dans BDArmory : 
                        // public struct GPSTargetInfo { public string name; public Vector3d gpsCoords; }
                        Type gpsTargetInfoType = bdAssembly.assembly.GetType("BDArmory.Modules.GPSTargetInfo");
                        if (gpsTargetInfoType != null)
                        {
                            object coordsVector = new Vector3d(coord.Longitude, coord.Latitude, coord.Altitude); // BDArmory stocke souvent sous forme x=lon, y=lat, z=alt
                            object gpsTargetInfoInstance = Activator.CreateInstance(gpsTargetInfoType, new object[] { coord.Name, coordsVector });
                            
                            // On récupère et ajoute la cible à la liste de cibles GPS actives de BDArmory pour l'équipe (Team) active.
                            // BDATargetManager.AddGPSTarget(GPSTargetInfo target)
                            var addMethod = targetManagerType.GetMethod("AddGPSTarget", new Type[] { gpsTargetInfoType });
                            if (addMethod != null)
                            {
                                addMethod.Invoke(null, new object[] { gpsTargetInfoInstance });
                                ScreenMessages.PostScreenMessage($"GPS '{coord.Name}' injecté dans BDArmory !", 4f, ScreenMessageStyle.UPPER_CENTER);
                            }
                            else
                            {
                                ScreenMessages.PostScreenMessage("Erreur : Impossible de trouver la méthode AddGPSTarget dans BDArmory.", 4f, ScreenMessageStyle.UPPER_CENTER);
                            }
                        }
                    }
                }
                else
                {
                    ScreenMessages.PostScreenMessage("BDArmory introuvable ! Vérifiez qu'il est installé.", 5f, ScreenMessageStyle.UPPER_CENTER);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BDGPSPreloader] Exception lors de l'injection GPS : {ex.Message}");
                ScreenMessages.PostScreenMessage("Erreur d'injection GPS. Voir le journal.", 5f, ScreenMessageStyle.UPPER_CENTER);
            }
        }

        private void SaveCoordinates()
        {
            try
            {
                string dir = Path.GetDirectoryName(savePath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                XmlSerializer serializer = new XmlSerializer(typeof(List<GPSCoordinate>));
                using (TextWriter writer = new StreamWriter(savePath))
                {
                    serializer.Serialize(writer, savedCoords);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BDGPSPreloader] Erreur lors de la sauvegarde : {ex.Message}");
            }
        }

        private void LoadCoordinates()
        {
            try
            {
                if (File.Exists(savePath))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(List<GPSCoordinate>));
                    using (TextReader reader = new StreamReader(savePath))
                    {
                        savedCoords = (List<GPSCoordinate>)serializer.Deserialize(reader);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BDGPSPreloader] Erreur lors du chargement : {ex.Message}");
            }
        }
    }
}
