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
        private Rect windowRect = new Rect(100, 100, 380, 520);
        private ApplicationLauncherButton toolbarButton = null;

        // Form fields
        private string newName = "Target Alpha";
        private string newLat = "0.0";
        private string newLon = "0.0";
        private string newAlt = "100";

        // Impact Camera Fields
        private bool enableImpactCam = false;
        private Vessel targetMissile = null;
        private Camera impactCamera = null;
        private GameObject camHolder = null;
        private Rect camViewRect = new Rect(0.7f, 0.7f, 0.28f, 0.28f); // PIP Window bottom-right

        // Scroll position – membre de classe pour ne pas être réinitialisé chaque frame
        private Vector2 scrollPosition = Vector2.zero;

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
            DestroyCamera();
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

        private void Update()
        {
            if (enableImpactCam)
            {
                UpdateImpactCamera();
            }
            else
            {
                DestroyCamera();
            }
        }

        private void OnGUI()
        {
            if (showGUI)
            {
                windowRect = GUILayout.Window(98541, windowRect, DrawWindow, "BDArmory GPS Preloader", GUILayout.Width(380), GUILayout.Height(480));
            }
        }

        private void DrawWindow(int windowID)
        {
            GUI.DragWindow(new Rect(0, 0, 10000, 20));

            GUILayout.BeginVertical();

            // Link Radar targets
            GUILayout.Label("Liaison Cibles Radar :", GUI.skin.box);
            if (GUILayout.Button("Importer coordonnées cible Radar active"))
            {
                ImportRadarTarget();
            }

            GUILayout.Space(5);

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

            // Impact Cam settings
            GUILayout.Label("Cinématique & Caméra :", GUI.skin.box);
            enableImpactCam = GUILayout.Toggle(enableImpactCam, " Activer la Caméra d'Impact (PIP)");

            GUILayout.Space(5);

            // Saved Coordinates List
            GUILayout.Label("Coordonnées enregistrées :", GUI.skin.box);
            // scrollPosition est un champ membre – la position est conservée entre les frames
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(150));

            GPSCoordinate toDelete = null;
            foreach (var coord in savedCoords)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{coord.Name}\nLat: {coord.Latitude:F4} | Lon: {coord.Longitude:F4} | Alt: {coord.Altitude:F1}m", GUILayout.Width(240));
                
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

        private void ImportRadarTarget()
        {
            try
            {
                var bdAssembly = AssemblyLoader.loadedAssemblies.Find(a => a.assembly.GetName().Name == "BDArmory");
                if (bdAssembly == null)
                {
                    ScreenMessages.PostScreenMessage("BDArmory non détecté !", 4f, ScreenMessageStyle.UPPER_CENTER);
                    return;
                }

                Vessel activeVessel = FlightGlobals.ActiveVessel;
                if (activeVessel == null) return;

                foreach (var part in activeVessel.parts)
                {
                    foreach (var module in part.Modules)
                    {
                        if (module.GetType().Name != "MissileFire") continue;

                        // vesselRadarData : champ public de MissileFire (BDArmory.Modules.MissileFire)
                        var vrdField = module.GetType().GetField("vesselRadarData",
                            BindingFlags.Public | BindingFlags.Instance);
                        if (vrdField == null) continue;

                        object vrd = vrdField.GetValue(module);
                        if (vrd == null) continue;

                        // VesselRadarData.lockedTargets : List<TargetSignatureData> (champ public)
                        // Pas de méthode GetLockedTargets() – c'est directement un champ.
                        var lockedField = vrd.GetType().GetField("lockedTargets",
                            BindingFlags.Public | BindingFlags.Instance);
                        if (lockedField == null) continue;

                        var lockedList = lockedField.GetValue(vrd) as System.Collections.IEnumerable;
                        if (lockedList == null) continue;

                        foreach (var targetSig in lockedList)
                        {
                            // TargetSignatureData.vessel est un champ public
                            var targetVesselField = targetSig.GetType().GetField("vessel",
                                BindingFlags.Public | BindingFlags.Instance);
                            if (targetVesselField == null) continue;

                            Vessel lockedVessel = targetVesselField.GetValue(targetSig) as Vessel;
                            if (lockedVessel == null) continue;

                            // CelestialBody.GetLatitude / GetLongitude / GetAltitude
                            // (GetLatitudeAndLongitude n'existe pas dans KSP)
                            CelestialBody body = activeVessel.mainBody;
                            double lat = body.GetLatitude(lockedVessel.transform.position);
                            double lon = body.GetLongitude(lockedVessel.transform.position);
                            double alt = body.GetAltitude(lockedVessel.transform.position);

                            newName = lockedVessel.vesselName;
                            newLat  = lat.ToString("F6");
                            newLon  = lon.ToString("F6");
                            newAlt  = alt.ToString("F0");

                            ScreenMessages.PostScreenMessage(
                                $"Cible Radar '{newName}' importée !", 4f, ScreenMessageStyle.UPPER_CENTER);
                            return;
                        }
                    }
                }

                ScreenMessages.PostScreenMessage(
                    "Aucun verrouillage radar trouvé sur le vaisseau actif.", 4f, ScreenMessageStyle.UPPER_CENTER);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BDGPSPreloader] Erreur lors de l'import radar : {ex.Message}");
            }
        }

        private void UpdateImpactCamera()
        {
            if (targetMissile == null)
            {
                foreach (Vessel v in FlightGlobals.Vessels)
                {
                    if (v.packed || !v.loaded) continue;
                    foreach (Part p in v.parts)
                    {
                        foreach (PartModule pm in p.Modules)
                        {
                            if (pm.GetType().Name.Contains("Missile") || pm.GetType().Name.Contains("Bomb"))
                            {
                                targetMissile = v;
                                ScreenMessages.PostScreenMessage(
                                    "Suivi du missile détecté actif !", 3f, ScreenMessageStyle.UPPER_CENTER);
                                break;
                            }
                        }
                        if (targetMissile != null) break;
                    }
                    if (targetMissile != null) break;
                }
            }

            if (targetMissile != null)
            {
                if (impactCamera == null)
                {
                    camHolder = new GameObject("BDGPS_ImpactCamHolder");
                    impactCamera = camHolder.AddComponent<Camera>();
                    impactCamera.rect = camViewRect;
                    impactCamera.depth = 99;
                    impactCamera.fieldOfView = 60;
                }

                Vector3 targetPosition = targetMissile.transform.position
                    - (targetMissile.transform.forward * 12)
                    + (targetMissile.transform.up * 4);
                camHolder.transform.position = Vector3.Lerp(camHolder.transform.position, targetPosition, 0.1f);
                camHolder.transform.LookAt(targetMissile.transform.position + (targetMissile.transform.forward * 5));
            }
            else
            {
                DestroyCamera();
            }
        }

        private void DestroyCamera()
        {
            if (impactCamera != null) { Destroy(impactCamera); impactCamera = null; }
            if (camHolder != null)    { Destroy(camHolder);    camHolder = null; }
            targetMissile = null;
        }

        private void InjectGPSTarget(GPSCoordinate coord)
        {
            try
            {
                var bdAssembly = AssemblyLoader.loadedAssemblies.Find(a => a.assembly.GetName().Name == "BDArmory");
                if (bdAssembly == null)
                {
                    ScreenMessages.PostScreenMessage(
                        "BDArmory introuvable ! Vérifiez qu'il est installé.", 5f, ScreenMessageStyle.UPPER_CENTER);
                    return;
                }

                // Namespace correct : BDArmory.Targeting (pas BDArmory.Modules)
                Type targetManagerType = bdAssembly.assembly.GetType("BDArmory.Targeting.BDATargetManager");
                if (targetManagerType == null)
                {
                    ScreenMessages.PostScreenMessage(
                        "Erreur : BDATargetManager introuvable dans BDArmory.", 5f, ScreenMessageStyle.UPPER_CENTER);
                    return;
                }

                Type gpsTargetInfoType = bdAssembly.assembly.GetType("BDArmory.Targeting.GPSTargetInfo");
                if (gpsTargetInfoType == null)
                {
                    ScreenMessages.PostScreenMessage(
                        "Erreur : GPSTargetInfo introuvable dans BDArmory.", 5f, ScreenMessageStyle.UPPER_CENTER);
                    return;
                }

                // GPSTargetInfo(Vector3d coords, string name, Vessel vessel = null)
                // gpsCoordinates : x = latitude, y = longitude, z = altitude
                // (ordre vérifié dans le source BDArmory/Targeting/GPSTargetInfo.cs)
                var gpsCoords = new Vector3d(coord.Latitude, coord.Longitude, coord.Altitude);

                var constructor = gpsTargetInfoType.GetConstructor(
                    new Type[] { typeof(Vector3d), typeof(string) });
                if (constructor == null)
                {
                    // Essai avec le constructeur à 3 paramètres (Vessel optionnel)
                    constructor = gpsTargetInfoType.GetConstructor(
                        new Type[] { typeof(Vector3d), typeof(string), typeof(Vessel) });
                }

                if (constructor == null)
                {
                    ScreenMessages.PostScreenMessage(
                        "Erreur : Constructeur GPSTargetInfo introuvable.", 4f, ScreenMessageStyle.UPPER_CENTER);
                    return;
                }

                object gpsInfo = (constructor.GetParameters().Length == 3)
                    ? constructor.Invoke(new object[] { gpsCoords, coord.Name, null })
                    : constructor.Invoke(new object[] { gpsCoords, coord.Name });

                // BDArmory stocke les cibles GPS dans la liste statique BDATargetManager.GPSTargetList
                // Il n'y a pas de méthode AddGPSTarget(GPSTargetInfo) – on ajoute directement à la liste.
                var gpsListField = targetManagerType.GetField("GPSTargetList",
                    BindingFlags.Public | BindingFlags.Static);
                if (gpsListField == null)
                {
                    // Fallback : certaines versions exposent une méthode AddGPSTarget
                    var addMethod = targetManagerType.GetMethod("AddGPSTarget",
                        BindingFlags.Public | BindingFlags.Static);
                    if (addMethod != null)
                    {
                        addMethod.Invoke(null, new object[] { gpsInfo });
                        ScreenMessages.PostScreenMessage(
                            $"GPS '{coord.Name}' injecté dans BDArmory !", 4f, ScreenMessageStyle.UPPER_CENTER);
                        return;
                    }
                    ScreenMessages.PostScreenMessage(
                        "Erreur : GPSTargetList introuvable dans BDATargetManager.", 4f, ScreenMessageStyle.UPPER_CENTER);
                    return;
                }

                var gpsList = gpsListField.GetValue(null) as System.Collections.IList;
                if (gpsList == null)
                {
                    ScreenMessages.PostScreenMessage(
                        "Erreur : GPSTargetList est null.", 4f, ScreenMessageStyle.UPPER_CENTER);
                    return;
                }

                gpsList.Add(gpsInfo);
                ScreenMessages.PostScreenMessage(
                    $"GPS '{coord.Name}' injecté dans BDArmory !", 4f, ScreenMessageStyle.UPPER_CENTER);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BDGPSPreloader] Exception lors de l'injection GPS : {ex.Message}");
                ScreenMessages.PostScreenMessage(
                    "Erreur d'injection GPS. Voir le journal.", 5f, ScreenMessageStyle.UPPER_CENTER);
            }
        }

        private void SaveCoordinates()
        {
            try
            {
                string dir = Path.GetDirectoryName(savePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

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
