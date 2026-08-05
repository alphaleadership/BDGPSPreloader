using System;
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

        // ── ABM Auto-Shoot Fields ─────────────────────────────────────────────
        // Active/désactive le tir automatique sur tout lock détecté par le radar ABM
        private bool enableABMAutoShoot = false;

        // Délai minimal entre deux tirs pour la même cible (évite la rafale)
        private const float ABMShootCooldown = 4f; // secondes

        // Cibles déjà engagées : vesselId → dernière heure de tir (Time.time)
        private Dictionary<Guid, float> abmEngagedTargets = new Dictionary<Guid, float>();

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

        // ── Pièces custom du mod ──────────────────────────────────────────────

        public class PartInfo
        {
            public string InternalName { get; set; }   // name = dans le .cfg
            public string Title        { get; set; }   // Nom affiché
            public string Category     { get; set; }   // "Missile" ou "Radar"
            public string Description  { get; set; }   // Résumé court
            public string Stats        { get; set; }   // Stats principales en une ligne
        }

        private static readonly List<PartInfo> modParts = new List<PartInfo>
        {
            // ── Missiles ─────────────────────────────────────────────────────
            new PartInfo
            {
                InternalName = "bdGpsPreloaderABM500",
                Title        = "RIM-500 'Archangel' (ABM - 500 km)",
                Category     = "Missile",
                Description  = "Intercepteur anti-balistique sol-air hypersonique stratégique.",
                Stats        = "Portée: 500 km | Vitesse: Mach 6.5 | Ogive: 180 kg TNT | Alt. lobée: 45 km"
            },
            new PartInfo
            {
                InternalName = "bdGpsPreloaderCruise500",
                Title        = "AS-500 'Zephyr' Cruise Missile (500 km)",
                Category     = "Missile",
                Description  = "Missile de croisière stratégique subsonique GPS à très longue portée.",
                Stats        = "Portée: 500 km | Vitesse: ~Mach 2.5 | Ogive: 450 kg TNT | Alt. croisière: 1200 m"
            },
            new PartInfo
            {
                InternalName = "bdGpsPreloaderRIM66ER",
                Title        = "RIM-66ER Standard Missile 2 (Extended Range)",
                Category     = "Missile",
                Description  = "Variante longue portée étendue du RIM-66D SM-2.",
                Stats        = "Portée: 120 km | Radar actif: 60 km | Ogive: 115 kg TNT | Alt. lobée: 28 km"
            },

            // ── Radars ───────────────────────────────────────────────────────
            new PartInfo
            {
                InternalName = "bdGpsPreloaderOrbitalRadar",
                Title        = "Radar de Surveillance Orbitale (OSR)",
                Category     = "Radar",
                Description  = "Radar SAR orbital – détecte cibles terrestres/maritimes depuis l'espace.",
                Stats        = "Détection: 500 km (descendant) | Verrous: 10 | Conso: 4.0 ec/s | Masse: 2 t"
            },
            new PartInfo
            {
                InternalName = "bdGpsPreloaderABMRadar",
                Title        = "Radar Anti-Balistique (ABM)",
                Category     = "Radar",
                Description  = "Radar fixe à haute altitude, spécialisé détection balistique/rentrée.",
                Stats        = "Détection: 350 km (360° hémisphérique) | Verrous: 5 | Conso: 5.0 ec/s | Masse: 6 t"
            },
            new PartInfo
            {
                InternalName = "bdGpsPreloaderBTHRadar",
                Title        = "Radar BTH (Beyond The Horizon)",
                Category     = "Radar",
                Description  = "Radar trans-horizon géant pour détection à très longue distance.",
                Stats        = "Détection: 250 km (omnidirectionnel) | Verrous: 3 | Conso: 3.5 ec/s | Masse: 4.5 t"
            },
        };

        // Onglet actif dans la section Catalogue
        private bool showPartsCatalog = false;
        private Vector2 partsScrollPos  = Vector2.zero;
        private string  catalogFilter   = "Tous"; // "Tous", "Missile", "Radar"

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

            ABMAutoShoot();
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

            // ── ABM Auto-Shoot ──────────────────────────────────────────────
            GUILayout.Label("Défense Anti-Balistique :", GUI.skin.box);

            enableABMAutoShoot = GUILayout.Toggle(enableABMAutoShoot, " Auto-Shoot ABM (tir auto sur lock radar ABM)");

            if (enableABMAutoShoot)
            {
                int activeEngagements = 0;
                float now = Time.time;
                foreach (var kv in abmEngagedTargets)
                    if (now - kv.Value < ABMShootCooldown) activeEngagements++;
                GUILayout.Label($"  Cibles en cours : {activeEngagements} | Cooldown : {ABMShootCooldown}s");

                if (GUILayout.Button("Réinitialiser engagements ABM"))
                {
                    abmEngagedTargets.Clear();
                    ScreenMessages.PostScreenMessage("Engagements ABM réinitialisés.", 3f, ScreenMessageStyle.UPPER_CENTER);
                }
            }

            GUILayout.Space(5);

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
                GUILayout.Label($"{coord.Name}\nLat: {coord.Latitude:F4} | Lon: {coord.Longitude:F4} | Alt: {coord.Altitude:F1}m", GUILayout.Width(220));
                
                if (GUILayout.Button("Injecter", GUILayout.Width(60)))
                {
                    InjectGPSTarget(coord);
                }

                if (GUILayout.Button("Lock LP", GUILayout.Width(65)))
                {
                    LongRangeLock(coord);
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

            GUILayout.Space(8);

            // ── Catalogue des pièces du mod ───────────────────────────────
            showPartsCatalog = GUILayout.Toggle(showPartsCatalog, " Afficher le catalogue des pièces du mod");

            if (showPartsCatalog)
            {
                GUILayout.Label("Pièces BDGPSPreloader :", GUI.skin.box);

                // Filtres rapides
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Tous",    catalogFilter == "Tous"    ? GUI.skin.box : GUI.skin.button, GUILayout.Width(80)))  catalogFilter = "Tous";
                if (GUILayout.Button("Missiles",catalogFilter == "Missile" ? GUI.skin.box : GUI.skin.button, GUILayout.Width(100))) catalogFilter = "Missile";
                if (GUILayout.Button("Radars",  catalogFilter == "Radar"   ? GUI.skin.box : GUI.skin.button, GUILayout.Width(80)))  catalogFilter = "Radar";
                GUILayout.EndHorizontal();

                partsScrollPos = GUILayout.BeginScrollView(partsScrollPos, GUILayout.Height(200));

                foreach (var part in modParts)
                {
                    if (catalogFilter != "Tous" && part.Category != catalogFilter) continue;

                    GUILayout.BeginVertical(GUI.skin.box);

                    // Ligne titre + catégorie
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"[{part.Category}]", GUILayout.Width(60));
                    GUILayout.Label($"<b>{part.Title}</b>");
                    GUILayout.EndHorizontal();

                    // Description courte
                    GUILayout.Label(part.Description);

                    // Stats
                    GUILayout.Label(part.Stats, GUI.skin.box);

                    GUILayout.EndVertical();
                    GUILayout.Space(4);
                }

                GUILayout.EndScrollView();
            }

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

        /// <summary>
        /// Verrouillage Longue Portée : injecte la coordonnée GPS dans BDArmory PUIS
        /// force le MissileFire du vaisseau actif à cibler ces coordonnées GPS,
        /// en convertissant lat/lon/alt en position monde (WorldPos) pour un
        /// ciblage à portée extrême indépendamment du radar.
        /// </summary>
        private void LongRangeLock(GPSCoordinate coord)
        {
            try
            {
                var bdAssembly = AssemblyLoader.loadedAssemblies.Find(a => a.assembly.GetName().Name == "BDArmory");
                if (bdAssembly == null)
                {
                    ScreenMessages.PostScreenMessage(
                        "BDArmory introuvable !", 4f, ScreenMessageStyle.UPPER_CENTER);
                    return;
                }

                Vessel activeVessel = FlightGlobals.ActiveVessel;
                if (activeVessel == null) return;

                // 1. Injecter d'abord la cible GPS dans la liste BDArmory
                InjectGPSTarget(coord);

                // 2. Convertir lat/lon/alt en position monde KSP
                CelestialBody body = activeVessel.mainBody;
                Vector3d worldPos = body.GetWorldSurfacePosition(coord.Latitude, coord.Longitude, coord.Altitude);

                // 3. Trouver le module MissileFire sur le vaisseau actif
                foreach (Part part in activeVessel.parts)
                {
                    foreach (PartModule module in part.Modules)
                    {
                        if (module.GetType().Name != "MissileFire") continue;

                        // Tenter de définir la cible GPS via le champ currentTarget (TargetInfo)
                        // ou via les méthodes publiques exposées par BDArmory

                        // a) Chercher SetGPSTarget(GPSTargetInfo) ou OverrideTarget(Vector3)
                        var setGpsMethod = module.GetType().GetMethod("SetGPSTarget",
                            BindingFlags.Public | BindingFlags.Instance);
                        if (setGpsMethod != null)
                        {
                            Type gpsInfoType = bdAssembly.assembly.GetType("BDArmory.Targeting.GPSTargetInfo");
                            if (gpsInfoType != null)
                            {
                                var gpsCoords   = new Vector3d(coord.Latitude, coord.Longitude, coord.Altitude);
                                var ctor        = gpsInfoType.GetConstructor(new Type[] { typeof(Vector3d), typeof(string) });
                                if (ctor != null)
                                {
                                    object gpsInfo = ctor.Invoke(new object[] { gpsCoords, coord.Name });
                                    setGpsMethod.Invoke(module, new object[] { gpsInfo });
                                    ScreenMessages.PostScreenMessage(
                                        $"Lock LP '{coord.Name}' activé via SetGPSTarget !", 4f, ScreenMessageStyle.UPPER_CENTER);
                                    return;
                                }
                            }
                        }

                        // b) Fallback : positionner le champ guardTarget ou mousePosGPS (Vector3)
                        //    BDArmory utilise mousePosGPS pour le ciblage GPS manuel
                        var mousePosField = module.GetType().GetField("mousePosGPS",
                            BindingFlags.Public | BindingFlags.Instance);
                        if (mousePosField != null)
                        {
                            mousePosField.SetValue(module, (Vector3)worldPos);

                            // Activer le mode ciblage GPS si disponible
                            var gpsTargetingField = module.GetType().GetField("GPSTarget",
                                BindingFlags.Public | BindingFlags.Instance);
                            if (gpsTargetingField != null && gpsTargetingField.FieldType == typeof(bool))
                                gpsTargetingField.SetValue(module, true);

                            ScreenMessages.PostScreenMessage(
                                $"Lock LP '{coord.Name}' activé (mousePosGPS) !", 4f, ScreenMessageStyle.UPPER_CENTER);
                            return;
                        }

                        // c) Fallback 2 : forcer guardTarget via le champ Vector3 guardTarget
                        var guardTargetField = module.GetType().GetField("guardTarget",
                            BindingFlags.Public | BindingFlags.Instance);
                        if (guardTargetField != null)
                        {
                            // guardTarget est un Transform ou Vessel selon la version — on tente Vector3 via méthode
                            var overrideMethod = module.GetType().GetMethod("TargetOverride",
                                BindingFlags.Public | BindingFlags.Instance);
                            if (overrideMethod != null)
                            {
                                overrideMethod.Invoke(module, new object[] { (Vector3)worldPos });
                                ScreenMessages.PostScreenMessage(
                                    $"Lock LP '{coord.Name}' activé via TargetOverride !", 4f, ScreenMessageStyle.UPPER_CENTER);
                                return;
                            }
                        }

                        // d) Dernier recours : injecter dans VesselRadarData.lockedTargets un TargetSignatureData fictif
                        //    pointant vers la position monde de la coordonnée GPS
                        var vrdField = module.GetType().GetField("vesselRadarData",
                            BindingFlags.Public | BindingFlags.Instance);
                        if (vrdField != null)
                        {
                            object vrd = vrdField.GetValue(module);
                            if (vrd != null)
                            {
                                // Chercher méthode TryLockTarget(Vector3) ou LockTarget(Vector3)
                                var lockMethod = vrd.GetType().GetMethod("TryLockTarget",
                                    BindingFlags.Public | BindingFlags.Instance,
                                    null, new Type[] { typeof(Vector3) }, null);
                                if (lockMethod == null)
                                    lockMethod = vrd.GetType().GetMethod("LockTarget",
                                        BindingFlags.Public | BindingFlags.Instance,
                                        null, new Type[] { typeof(Vector3) }, null);

                                if (lockMethod != null)
                                {
                                    lockMethod.Invoke(vrd, new object[] { (Vector3)worldPos });
                                    ScreenMessages.PostScreenMessage(
                                        $"Lock LP '{coord.Name}' activé via VesselRadarData !", 4f, ScreenMessageStyle.UPPER_CENTER);
                                    return;
                                }
                            }
                        }

                        ScreenMessages.PostScreenMessage(
                            "GPS injecté. Lock LP : aucune méthode directe trouvée dans cette version de BDArmory.",
                            5f, ScreenMessageStyle.UPPER_CENTER);
                        return;
                    }
                }

                ScreenMessages.PostScreenMessage(
                    "Aucun MissileFire trouvé sur le vaisseau actif.", 4f, ScreenMessageStyle.UPPER_CENTER);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BDGPSPreloader] Erreur LongRangeLock : {ex.Message}");
                ScreenMessages.PostScreenMessage(
                    "Erreur Lock LP. Voir le journal.", 5f, ScreenMessageStyle.UPPER_CENTER);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // ABM Auto-Shoot
        // Surveille les cibles verrouillées par le radar ABM (bdGpsPreloaderABMRadar)
        // et ordonne au MissileFire de tirer un intercepteur sur chaque nouvelle cible.
        // Un cooldown par cible évite la sur-munition.
        // ─────────────────────────────────────────────────────────────────────
        private void ABMAutoShoot()
        {
            if (!enableABMAutoShoot) return;

            try
            {
                // Trouver BDArmory
                var bdAssembly = AssemblyLoader.loadedAssemblies
                    .Find(a => a.assembly.GetName().Name == "BDArmory");
                if (bdAssembly == null) return;

                Vessel activeVessel = FlightGlobals.ActiveVessel;
                if (activeVessel == null) return;

                // ── 1. Collecter les cibles actuellement verrouillées par un radar ABM ──
                // On cherche les ModuleRadar dont le radarName contient "ABM" ou
                // la pièce s'appelle bdGpsPreloaderABMRadar.
                var lockedByABM = new List<object>(); // TargetSignatureData boxés

                foreach (Vessel v in FlightGlobals.Vessels)
                {
                    if (v.packed || !v.loaded) continue;
                    foreach (Part p in v.parts)
                    {
                        bool isABMRadar = p.partInfo != null &&
                            (p.partInfo.name == "bdGpsPreloaderABMRadar" ||
                             (p.partInfo.title != null && p.partInfo.title.ToLower().Contains("abm")));

                        foreach (PartModule pm in p.Modules)
                        {
                            if (pm.GetType().Name != "ModuleRadar") continue;

                            // Identifier le radar ABM soit par la pièce, soit par le radarName
                            bool nameMatchesABM = false;
                            if (!isABMRadar)
                            {
                                var radarNameField = pm.GetType().GetField("radarName",
                                    BindingFlags.Public | BindingFlags.Instance);
                                if (radarNameField != null)
                                {
                                    string rn = radarNameField.GetValue(pm) as string ?? "";
                                    nameMatchesABM = rn.ToUpper().Contains("ABM");
                                }
                            }

                            if (!isABMRadar && !nameMatchesABM) continue;

                            // Lire les cibles verrouillées : champ lockedTargets sur ModuleRadar
                            var lockedField = pm.GetType().GetField("lockedTargets",
                                BindingFlags.Public | BindingFlags.Instance);
                            if (lockedField == null)
                            {
                                // Certaines versions utilisent lockedTarget (singulier, TargetSignatureData)
                                var singularField = pm.GetType().GetField("lockedTarget",
                                    BindingFlags.Public | BindingFlags.Instance);
                                if (singularField != null)
                                {
                                    object tsd = singularField.GetValue(pm);
                                    if (tsd != null)
                                    {
                                        // Vérifier que la cible est valide (champ "exists" ou "vessel" non null)
                                        var existsProp = tsd.GetType().GetProperty("exists",
                                            BindingFlags.Public | BindingFlags.Instance);
                                        bool exists = existsProp != null && (bool)existsProp.GetValue(tsd, null);
                                        if (exists) lockedByABM.Add(tsd);
                                    }
                                }
                            }
                            else
                            {
                                var list = lockedField.GetValue(pm) as System.Collections.IEnumerable;
                                if (list != null)
                                    foreach (var tsd in list)
                                        if (tsd != null) lockedByABM.Add(tsd);
                            }
                        }
                    }
                }

                if (lockedByABM.Count == 0) return;

                // ── 2. Trouver le MissileFire (Weapon Manager) du vaisseau actif ──
                PartModule missileFire = null;
                foreach (Part part in activeVessel.parts)
                {
                    foreach (PartModule pm in part.Modules)
                    {
                        if (pm.GetType().Name == "MissileFire") { missileFire = pm; break; }
                    }
                    if (missileFire != null) break;
                }

                if (missileFire == null) return;

                float now = Time.time;

                // Nettoyer les entrées expirées du dictionnaire
                var expiredKeys = new List<Guid>();
                foreach (var kv in abmEngagedTargets)
                    if (now - kv.Value > ABMShootCooldown * 3f) expiredKeys.Add(kv.Key);
                foreach (var k in expiredKeys) abmEngagedTargets.Remove(k);

                // ── 3. Pour chaque cible verrouillée non encore engagée → tirer ──
                foreach (object tsd in lockedByABM)
                {
                    // Récupérer le vaisseau cible pour son ID unique
                    var vesselField = tsd.GetType().GetField("vessel",
                        BindingFlags.Public | BindingFlags.Instance);
                    Vessel targetVessel = vesselField != null
                        ? vesselField.GetValue(tsd) as Vessel
                        : null;

                    // Construire un ID : si on a le vaisseau, utiliser son Guid ; sinon hash sur position
                    Guid targetId;
                    if (targetVessel != null)
                    {
                        targetId = targetVessel.id;
                    }
                    else
                    {
                        // Fallback : construire un Guid 16 octets depuis le hash de position
                        var posField = tsd.GetType().GetField("position",
                            BindingFlags.Public | BindingFlags.Instance);
                        if (posField == null) continue;
                        Vector3 pos = (Vector3)posField.GetValue(tsd);
                        byte[] guidBytes = new byte[16];
                        byte[] hx = BitConverter.GetBytes(pos.x.GetHashCode());
                        byte[] hy = BitConverter.GetBytes(pos.y.GetHashCode());
                        byte[] hz = BitConverter.GetBytes(pos.z.GetHashCode());
                        byte[] hc = BitConverter.GetBytes((pos.x + pos.y + pos.z).GetHashCode());
                        Array.Copy(hx, 0, guidBytes, 0,  4);
                        Array.Copy(hy, 0, guidBytes, 4,  4);
                        Array.Copy(hz, 0, guidBytes, 8,  4);
                        Array.Copy(hc, 0, guidBytes, 12, 4);
                        targetId = new Guid(guidBytes);
                    }

                    // Vérifier le cooldown
                    if (abmEngagedTargets.TryGetValue(targetId, out float lastShot))
                        if (now - lastShot < ABMShootCooldown) continue;

                    // ── Tenter le tir via FireCurrentMissile ou FireMissileAt ──
                    bool fired = false;

                    // a) Méthode FireMissileAt(TargetSignatureData) — certaines versions BDA
                    var fireMissileAt = missileFire.GetType().GetMethod("FireMissileAt",
                        BindingFlags.Public | BindingFlags.Instance,
                        null, new Type[] { tsd.GetType() }, null);
                    if (fireMissileAt != null)
                    {
                        fireMissileAt.Invoke(missileFire, new object[] { tsd });
                        fired = true;
                    }

                    // b) Définir guardTarget + FireCurrentMissile
                    if (!fired)
                    {
                        // Pointer guardTarget vers la cible
                        if (targetVessel != null)
                        {
                            var guardTargetField = missileFire.GetType().GetField("guardTarget",
                                BindingFlags.Public | BindingFlags.Instance);
                            if (guardTargetField != null && guardTargetField.FieldType == typeof(Vessel))
                                guardTargetField.SetValue(missileFire, targetVessel);
                        }

                        // FireCurrentMissile() — méthode standard BDArmory
                        var fireMethod = missileFire.GetType().GetMethod("FireCurrentMissile",
                            BindingFlags.Public | BindingFlags.Instance);
                        if (fireMethod != null)
                        {
                            fireMethod.Invoke(missileFire, null);
                            fired = true;
                        }
                    }

                    // c) Fallback : OrderFire()
                    if (!fired)
                    {
                        var orderFire = missileFire.GetType().GetMethod("OrderFire",
                            BindingFlags.Public | BindingFlags.Instance);
                        if (orderFire != null)
                        {
                            orderFire.Invoke(missileFire, null);
                            fired = true;
                        }
                    }

                    if (fired)
                    {
                        abmEngagedTargets[targetId] = now;
                        string targetName = targetVessel != null ? targetVessel.vesselName : "cible inconnue";
                        ScreenMessages.PostScreenMessage(
                            $"[ABM Auto-Shoot] Missile tiré sur : {targetName}",
                            4f, ScreenMessageStyle.UPPER_CENTER);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BDGPSPreloader] ABMAutoShoot exception : {ex.Message}");
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
