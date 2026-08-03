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
            Vector2 scroll = Vector2.zero;
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(150));

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
                if (bdAssembly != null)
                {
                    // Recherche du Weapons Manager (MissileFire) actif sur le vaisseau actif
                    Vessel activeVessel = FlightGlobals.ActiveVessel;
                    if (activeVessel != null)
                    {
                        foreach (var part in activeVessel.parts)
                        {
                            foreach (var module in part.Modules)
                            {
                                if (module.GetType().Name == "MissileFire")
                                {
                                    // Récupération de vesselRadarData
                                    var vrdField = module.GetType().GetField("vesselRadarData", BindingFlags.Public | BindingFlags.Instance);
                                    if (vrdField != null)
                                    {
                                        object vrdInstance = vrdField.GetValue(module);
                                        if (vrdInstance != null)
                                        {
                                            // Méthode GetLockedTargets() renvoie List<TargetSignatureData>
                                            var getLockedTargetsMethod = vrdInstance.GetType().GetMethod("GetLockedTargets", BindingFlags.Public | BindingFlags.Instance);
                                            if (getLockedTargetsMethod != null)
                                            {
                                                var targetList = getLockedTargetsMethod.Invoke(vrdInstance, null) as System.Collections.IEnumerable;
                                                if (targetList != null)
                                                {
                                                    foreach (var targetSig in targetList)
                                                    {
                                                        // TargetSignatureData contient 'predictedPosition' (Vector3) et 'vessel' (Vessel) ou 'exists'
                                                        var targetVesselField = targetSig.GetType().GetField("vessel", BindingFlags.Public | BindingFlags.Instance);
                                                        if (targetVesselField != null)
                                                        {
                                                            Vessel radarLockedVessel = targetVesselField.GetValue(targetSig) as Vessel;
                                                            if (radarLockedVessel != null)
                                                            {
                                                                // Convertir la position monde de la cible en coordonnées géographiques KSP
                                                                Vector3d coords = activeVessel.mainBody.GetLatitudeAndLongitude(radarLockedVessel.transform.position);
                                                                double lat = coords.x;
                                                                double lon = coords.y;
                                                                double alt = activeVessel.mainBody.GetAltitude(radarLockedVessel.transform.position);

                                                                newName = radarLockedVessel.vesselName;
                                                                newLat = lat.ToString("F6");
                                                                newLon = lon.ToString("F6");
                                                                newAlt = alt.ToString("F0");

                                                                ScreenMessages.PostScreenMessage($"Cible Radar '{newName}' importée !", 4f, ScreenMessageStyle.UPPER_CENTER);
                                                                return;
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        ScreenMessages.PostScreenMessage("Aucun verrouillage radar trouvé sur le vaisseau actif.", 4f, ScreenMessageStyle.UPPER_CENTER);
                    }
                }
                else
                {
                    ScreenMessages.PostScreenMessage("BDArmory non détecté !", 4f, ScreenMessageStyle.UPPER_CENTER);
                }
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
                // Trouver le premier missile (ou bombe) BDArmory en vol tiré depuis ou actif dans la zone
                foreach (Vessel v in FlightGlobals.Vessels)
                {
                    if (v.packed || !v.loaded) continue;
                    // BDArmory met les missiles en vol dans une catégorie spécifique ou via des modules comme MissileBase/MissileGuidance
                    foreach (Part p in v.parts)
                    {
                        foreach (PartModule pm in p.Modules)
                        {
                            if (pm.GetType().Name.Contains("Missile") || pm.GetType().Name.Contains("Bomb"))
                            {
                                targetMissile = v;
                                ScreenMessages.PostScreenMessage("Suivi du missile détecté actif !", 3f, ScreenMessageStyle.UPPER_CENTER);
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
                // Si la caméra n'existe pas, la créer
                if (impactCamera == null)
                {
                    camHolder = new GameObject("BDGPS_ImpactCamHolder");
                    impactCamera = camHolder.AddComponent<Camera>();
                    impactCamera.rect = camViewRect;
                    impactCamera.depth = 99; // Au-dessus de la caméra principale
                    impactCamera.fieldOfView = 60;
                }

                // Positionner la caméra légèrement derrière et au-dessus du missile pour voir l'impact à venir
                Vector3 targetPosition = targetMissile.transform.position - (targetMissile.transform.forward * 12) + (targetMissile.transform.up * 4);
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
            if (impactCamera != null)
            {
                Destroy(impactCamera);
                impactCamera = null;
            }
            if (camHolder != null)
            {
                Destroy(camHolder);
                camHolder = null;
            }
            targetMissile = null;
        }

        private void InjectGPSTarget(GPSCoordinate coord)
        {
            try
            {
                var bdAssembly = AssemblyLoader.loadedAssemblies.Find(a => a.assembly.GetName().Name == "BDArmory");
                if (bdAssembly != null)
                {
                    Type targetManagerType = bdAssembly.assembly.GetType("BDArmory.Modules.BDATargetManager");
                    if (targetManagerType != null)
                    {
                        Type gpsTargetInfoType = bdAssembly.assembly.GetType("BDArmory.Modules.GPSTargetInfo");
                        if (gpsTargetInfoType != null)
                        {
                            object coordsVector = new Vector3d(coord.Longitude, coord.Latitude, coord.Altitude); // x=lon, y=lat, z=alt
                            
                            var constructor = gpsTargetInfoType.GetConstructor(new Type[] { typeof(Vector3d), typeof(string) });
                            if (constructor != null)
                            {
                                object gpsTargetInfoInstance = constructor.Invoke(new object[] { coordsVector, coord.Name });
                                
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
                            else
                            {
                                ScreenMessages.PostScreenMessage("Erreur : Impossible de trouver le constructeur de GPSTargetInfo.", 4f, ScreenMessageStyle.UPPER_CENTER);
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
