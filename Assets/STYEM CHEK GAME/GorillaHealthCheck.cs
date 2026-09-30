using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// ULTIMATIVES VOLLAUTOMATISCHES DIAGNOSE- UND HEALTH-CHECK-SYSTEM
/// Entwickelt für Gorilla Tag Fan-Games (TRR-STUDIOS-MonkeyMadness).
/// Sucht ALLES komplett selbstständig, speichert geladene Skripte und erkennt neue Skripte automatisch.
/// </summary>
public class GorillaHealthCheck : MonoBehaviour
{
    [Header("================ MAIN SETTINGS ================")]
    [Tooltip("Aktivieren, damit die Diagnose automatisch beim Start (Play Mode) ausgeführt wird.")]
    public bool pcTestAktivieren = true;

    [Tooltip("Gibt detaillierte Warnungen für kleinere Schönheitsfehler aus.")]
    public bool zeigeDetailWarnungen = true;

    [Header("================ UI SETTINGS ================")]
    [Tooltip("Soll der Kasten mit den Fehlern direkt im Spiel (GUI) angezeigt werden?")]
    public bool zeigeInGameKasten = true;

    [Header("================ INSPECTOR SCHNELL-AKTIONEN ================")]
    [Tooltip("Haken setzen, um die Diagnose direkt aus dem Inspector auszuführen!")]
    public bool jetztDiagnoseAusfuehren = false;

    [Tooltip("Haken setzen, um alle Logs und Zähler aus dem Inspector zu löschen!")]
    public bool jetztLogsLoeschen = false;

    // Dynamische interne Referenzen
    private GameObject computerObject;
    private StreetTriggerNetwork streetTrigger;
    private GameObject leftHandObject;
    private GameObject rightHandObject;

    // Diagnose Statistiken
    private int totalTests = 0;
    private int passedTests = 0;
    private int warningCount = 0;
    private int failedTests = 0;
    private int noticeCount = 0;

    // Struktur für gespeicherte Fehler & Warnungen
    public struct HealthLogEntry
    {
        public string scriptOrSystem;
        public string message;
        public LogType type;
    }

    // Speicher für gespeicherte Berichte
    private List<HealthLogEntry> savedLogs = new List<HealthLogEntry>();

    // SPEICHER FÜR ERFASSTE SKRIPTE (Vergleich von alten vs. neuen Skripten)
    private HashSet<string> knownScriptKeys = new HashSet<string>();

    // Cache-Klassifikatoren für Map-Objekte
    private List<Collider> foundGroundColliders = new List<Collider>();
    private List<Collider> foundWallColliders = new List<Collider>();
    private List<Collider> foundCeilingColliders = new List<Collider>();

    // Scroll-Position für den OnGUI-Kasten
    private Vector2 guiScrollPosition = Vector2.zero;

    private void OnValidate()
    {
        if (jetztDiagnoseAusfuehren)
        {
            jetztDiagnoseAusfuehren = false;
            RunFullSystemDiagnostics();
        }

        if (jetztLogsLoeschen)
        {
            jetztLogsLoeschen = false;
            ClearAllLogs();
        }
    }

    private void Start()
    {
        if (pcTestAktivieren)
        {
            StartCoroutine(RunDiagnosticsDelayed());
        }
    }

    private IEnumerator RunDiagnosticsDelayed()
    {
        yield return null;
        RunFullSystemDiagnostics();
    }

    /// <summary>
    /// Führt die vollständige automatische System-Diagnose durch.
    /// </summary>
    [ContextMenu("=== JETZT VOLLAUTOMATISCHE DIAGNOSE STARTEN ===")]
    public void RunFullSystemDiagnostics()
    {
        // Wir löschen die Text-Logs für den neuen Durchlauf, behalten aber das Skript-Gedächtnis (knownScriptKeys)
        savedLogs.Clear();
        foundGroundColliders.Clear();
        foundWallColliders.Clear();
        foundCeilingColliders.Clear();

        totalTests = 0;
        passedTests = 0;
        warningCount = 0;
        failedTests = 0;
        noticeCount = 0;

        AutoFindAllSceneReferences();

        Debug.Log("<color=cyan><b>================================================================================================</b></color>");
        Debug.Log("<color=cyan><b>   STARTING AUTOMATED MONKEY MADNESS FULL-SYSTEM DIAGNOSTICS ENGINE                             </b></color>");
        Debug.Log("<color=cyan><b>================================================================================================</b></color>");

        // 0. SCRIPT TRACKING & SCAN FOR NEW SCRIPTS
        SectionHeader("0. SCRIPT MONITORING & NEUE SKRIPTE");
        ScanAndTrackNewScripts();

        // 1. LAYER & TAG SETUP CHECKS
        SectionHeader("1. PROJECT SETUP: LAYERS & TAGS");
        CheckLayerExists("Player");
        CheckLayerExists("Default");
        CheckTagExists("Player");
        CheckTagExists("HandTag");
        CheckTagExists("MainCamera");

        // 2. MAP GEOMETRY & ENVIRONMENT
        SectionHeader("2. MAP GEOMETRY: GROUND, WALLS, CEILING & OBJECTS");
        ScanEnvironmentAndColliders();

        // 3. GORILLA PLAYER & LOCOMOTION
        SectionHeader("3. GORILLA PLAYER: RIG, LOCOMOTION & RIGIDBODY SETUP");
        CheckGorillaPlayerBase();

        // 4. VR HANDS & ANIMATIONS
        SectionHeader("4. HAND ANIMATIONS & VR POSES: ANIMATORS, CONTROLLERS & FINGERS");
        CheckGorillaHandsAndAnimationsFull();

        // 5. COMPUTER SYSTEM
        SectionHeader("5. COMPUTER SYSTEM: TERMINAL, SCREEN & INTERACTION");
        CheckComputerSystemFull();

        // 6. CARS, CAR HITBOX & ANIMATIONS
        SectionHeader("6. VEHICLES & HAZARDS: CARS, CARHITBOX & ANIMATIONS");
        CheckCarsAndHazardsFull();

        // 7. PHOTON PUN2 MULTIPLAYER
        SectionHeader("7. MULTIPLAYER: PHOTON PUN2 NETWORK INTEGRATION");
        CheckPhotonMultiplayerFull();

        // 8. AUDIO SYSTEM
        SectionHeader("8. AUDIO SYSTEM: AUDIO SOURCES & SOUND EFFECTS");
        CheckAudioSystem();

        // 9. FINAL DIAGNOSTIC SUMMARY
        PrintFinalReport();
    }

    /// <summary>
    /// Scannt alle MonoBehaviours in der Szene, merkt sich diese und erkennt neu hinzugefügte Skripte.
    /// </summary>
    private void ScanAndTrackNewScripts()
    {
        totalTests++;
        MonoBehaviour[] allScripts = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        int newScriptCount = 0;
        bool firstRun = (knownScriptKeys.Count == 0);

        foreach (MonoBehaviour mb in allScripts)
        {
            if (mb == null) continue;

            string scriptName = mb.GetType().Name;
            string objectName = mb.gameObject.name;
            // Eindeutiger Schlüssel aus Objekt-Name, Instanz-ID und Skript-Klasse
            string uniqueKey = $"{objectName}_{scriptName}_{mb.GetInstanceID()}";

            if (!knownScriptKeys.Contains(uniqueKey))
            {
                knownScriptKeys.Add(uniqueKey);

                // Wenn es nicht der erste Scan ist, ist dieses Skript NEU!
                if (!firstRun)
                {
                    newScriptCount++;
                    bool isRunning = mb.enabled && mb.gameObject.activeInHierarchy;
                    string statusText = isRunning ? "Aktiv & Funktionsfähig [LÄUFT]" : "Inaktiv / Deaktiviert";

                    LogSuccess("Script Monitor", $"<b>[NEUES SKRIPT GEFUNDEN]</b> Skript '<b>{scriptName}</b>' an Objekt '<b>{objectName}</b>' erkannt. Status: {statusText}");
                }
            }
        }

        if (firstRun)
        {
            LogSuccess("Script Monitor", $"Initiales Skript-Gedächtnis geladen: {knownScriptKeys.Count} Skripte in der Szene gespeichert.");
        }
        else if (newScriptCount == 0)
        {
            LogSuccess("Script Monitor", "Keine neuen Skripte seit der letzten Diagnose hinzugefügt.");
        }
    }

    /// <summary>
    /// Löscht alle gespeicherten Logs, Statistiken UND das Skript-Gedächtnis.
    /// </summary>
    [ContextMenu("=== ALLE LOGS & SPEICHER LÖSCHEN ===")]
    public void ClearAllLogs()
    {
        totalTests = 0;
        passedTests = 0;
        warningCount = 0;
        failedTests = 0;
        noticeCount = 0;

        savedLogs.Clear();
        knownScriptKeys.Clear();
        foundGroundColliders.Clear();
        foundWallColliders.Clear();
        foundCeilingColliders.Clear();

        Debug.Log("<color=yellow><b>[HealthCheck] Alle gespeicherten Fehler-Logs, Skript-Speicher und Statistiken wurden gelöscht!</b></color>");
    }

    #region AUTOMATIC SCENE REFERENCE FINDER

    private void AutoFindAllSceneReferences()
    {
        computerObject = GameObject.Find("Computer") ??
                         GameObject.Find("GorillaComputer") ??
                         GameObject.Find("ComputerTerminal") ??
                         GameObject.Find("Terminal") ??
                         GameObject.FindWithTag("Finish");

        streetTrigger = FindAnyObjectByType<StreetTriggerNetwork>();

        leftHandObject = GameObject.Find("LeftHand") ??
                         GameObject.Find("LeftHandController") ??
                         GameObject.Find("Left_Hand") ??
                         GameObject.Find("Left Hand") ??
                         GameObject.Find("HandLeft") ??
                         GameObject.Find("LeftHandFollower");

        rightHandObject = GameObject.Find("RightHand") ??
                          GameObject.Find("RightHandController") ??
                          GameObject.Find("Right_Hand") ??
                          GameObject.Find("Right Hand") ??
                          GameObject.Find("HandRight") ??
                          GameObject.Find("RightHandFollower");

        if (GorillaLocomotion.Player.Instance != null)
        {
            if (leftHandObject == null)
            {
                Transform t = GorillaLocomotion.Player.Instance.transform.Find("LeftHand") ??
                              GorillaLocomotion.Player.Instance.transform.Find("LeftHandController") ??
                              GorillaLocomotion.Player.Instance.transform.Find("LeftHandFollower");
                if (t != null) leftHandObject = t.gameObject;
            }

            if (rightHandObject == null)
            {
                Transform t = GorillaLocomotion.Player.Instance.transform.Find("RightHand") ??
                              GorillaLocomotion.Player.Instance.transform.Find("RightHandController") ??
                              GorillaLocomotion.Player.Instance.transform.Find("RightHandFollower");
                if (t != null) rightHandObject = t.gameObject;
            }
        }
    }

    #endregion

    #region 1. PROJECT SETUP CHECKS

    private void CheckLayerExists(string layerName)
    {
        totalTests++;
        int layer = LayerMask.NameToLayer(layerName);
        if (layer != -1)
        {
            LogSuccess("Tags & Layers", $"Layer '{layerName}' ist im Projekt eingerichtet (ID: {layer}).");
        }
        else
        {
            LogNotice("Tags & Layers", $"Layer '{layerName}' fehlt. Spiel läuft, aber Kollisionen/Multiplayer können auf Quest fehlschlagen.");
        }
    }

    private void CheckTagExists(string tagName)
    {
        totalTests++;
        try
        {
            GameObject.FindWithTag(tagName);
            LogSuccess("Tags & Layers", $"Tag '{tagName}' ist im Projekt registriert.");
        }
        catch
        {
            LogNotice("Tags & Layers", $"Tag '{tagName}' ist nicht definiert. Spiel läuft, aber Skripte mit FindWithTag('{tagName}') schlagen fehl.");
        }
    }

    #endregion

    #region 2. MAP GEOMETRY & ENVIRONMENT CHECKS

    private void ScanEnvironmentAndColliders()
    {
        totalTests++;
        Collider[] allColliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);

        if (allColliders.Length == 0)
        {
            LogError("Map Geometry", "KEINE Collider in der Szene gefunden! Spieler fällt durch die Map.");
            return;
        }

        int triggerCount = 0;
        int solidColliderCount = 0;
        int terrainCount = 0;

        foreach (Collider col in allColliders)
        {
            if (col.isTrigger)
            {
                triggerCount++;
            }
            else
            {
                solidColliderCount++;

                string nameLower = col.gameObject.name.ToLower();
                if (nameLower.Contains("ground") || nameLower.Contains("floor") || nameLower.Contains("boden") || nameLower.Contains("terrain"))
                {
                    foundGroundColliders.Add(col);
                }
                else if (nameLower.Contains("wall") || nameLower.Contains("wand") || nameLower.Contains("border") || nameLower.Contains("boundary"))
                {
                    foundWallColliders.Add(col);
                }
                else if (nameLower.Contains("roof") || nameLower.Contains("ceiling") || nameLower.Contains("dach"))
                {
                    foundCeilingColliders.Add(col);
                }
            }

            if (col is TerrainCollider)
            {
                terrainCount++;
            }
        }

        LogSuccess("Map Geometry", $"Szene gescannt: {allColliders.Length} Collider gefunden ({solidColliderCount} feste, {triggerCount} Trigger).");

        totalTests++;
        if (foundGroundColliders.Count > 0 || terrainCount > 0)
        {
            LogSuccess("Boden-System", $"{foundGroundColliders.Count} explizite Böden/Terrains identifiziert.");
        }
        else
        {
            LogNotice("Boden-System", "Keine expliziten 'Ground/Boden'-Namen gefunden. Feste Collider existieren, aber prüfe die Boden-Kollision.");
        }

        totalTests++;
        if (foundWallColliders.Count > 0)
        {
            LogSuccess("Wand-System", $"{foundWallColliders.Count} Wand-Collider identifiziert.");
        }
        else
        {
            LogNotice("Wand-System", "Keine Objekte namens 'Wall/Wand' gefunden. Feste Wände existieren eventuell ohne Standardnamen.");
        }

        totalTests++;
        if (foundCeilingColliders.Count > 0)
        {
            LogSuccess("Decken-System", $"{foundCeilingColliders.Count} Decken-Collider gefunden.");
        }
        else if (zeigeDetailWarnungen)
        {
            LogNotice("Decken-System", "Keine expliziten Decken-Collider ('Roof'/'Dach') gefunden.");
        }
    }

    #endregion

    #region 3. GORILLA PLAYER BASE CHECKS

    private void CheckGorillaPlayerBase()
    {
        totalTests++;

        Rigidbody[] rbs = FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);
        Rigidbody playerRb = null;

        foreach (Rigidbody rb in rbs)
        {
            if (rb.gameObject.layer == LayerMask.NameToLayer("Player") || rb.CompareTag("Player") || rb.name.ToLower().Contains("gorilla") || rb.name.ToLower().Contains("player"))
            {
                playerRb = rb;
                break;
            }
        }

        if (playerRb == null && GorillaLocomotion.Player.Instance != null)
        {
            playerRb = GorillaLocomotion.Player.Instance.GetComponent<Rigidbody>();
        }

        if (playerRb != null)
        {
            LogSuccess("GorillaLocomotion", $"Player Rigidbody an Objekt '{playerRb.name}' gefunden.");

            totalTests++;
            if (!playerRb.isKinematic)
            {
                LogSuccess("GorillaLocomotion", "Player Rigidbody 'isKinematic' ist DEAKTIVIERT (Korrekt für Physik/Knockback).");
            }
            else
            {
                LogNotice("GorillaLocomotion", "Player Rigidbody ist 'isKinematic = true'. Spieler bewegt sich, aber Auto-Knockbacks funktionieren nicht.");
            }
        }
        else
        {
            LogError("GorillaLocomotion", "KEIN Gorilla-Player oder Rigidbody in der Szene gefunden!");
        }
    }

    #endregion

    #region 4. ADVANCED VR HANDS & ANIMATIONS SCAN

    private void CheckGorillaHandsAndAnimationsFull()
    {
        totalTests++;
        Collider[] allColliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
        int handColliderCount = 0;

        foreach (Collider c in allColliders)
        {
            string cName = c.gameObject.name.ToLower();
            if (c.CompareTag("HandTag") || cName.Contains("hand") || cName.Contains("palm") || cName.Contains("fist") || cName.Contains("handtag"))
            {
                handColliderCount++;
            }
        }

        if (handColliderCount > 0)
        {
            LogSuccess("VR Hands", $"{handColliderCount} interaktive Hand-Collider erkannt.");
        }
        else
        {
            LogNotice("VR Hands", "Keine Hand-Collider mit 'HandTag' gefunden. Klettern/Schlagen klappt, aber Hand-Trigger fehlen.");
        }

        EvaluateSingleHandAnimationSetup("LINKE HAND", leftHandObject);
        EvaluateSingleHandAnimationSetup("RECHTE HAND", rightHandObject);
        EvaluateAllHandAnimatorsInScene();
    }

    private void EvaluateSingleHandAnimationSetup(string label, GameObject handObj)
    {
        totalTests++;
        if (handObj == null)
        {
            if (zeigeDetailWarnungen)
            {
                LogNotice("VR Hand Animator", $"{label}: Automatische Suche fehlgeschlagen. Hände bewegen sich evtl. ohne Finger-Animation.");
            }
            return;
        }

        LogSuccess("VR Hand Animator", $"{label} automatisch gefunden: '{handObj.name}'.");

        totalTests++;
        Animator anim = handObj.GetComponent<Animator>() ?? handObj.GetComponentInChildren<Animator>();

        if (anim != null)
        {
            if (anim.runtimeAnimatorController != null)
            {
                LogSuccess("VR Hand Animator", $"{label}: Animator mit Controller ('{anim.runtimeAnimatorController.name}') verknüpft.");

                totalTests++;
                bool hasGrip = false, hasTrigger = false, hasSpeed = false;

                foreach (AnimatorControllerParameter param in anim.parameters)
                {
                    string pName = param.name.ToLower();
                    if (pName.Contains("grip") || pName.Contains("greifen")) hasGrip = true;
                    if (pName.Contains("trigger") || pName.Contains("druecken")) hasTrigger = true;
                    if (pName.Contains("speed") || pName.Contains("geschwindigkeit")) hasSpeed = true;
                }

                if (hasGrip || hasTrigger || hasSpeed)
                {
                    LogSuccess("VR Hand Animator", $"{label}: Controller enthält passende VR-Animations-Parameter.");
                }
                else if (zeigeDetailWarnungen)
                {
                    LogNotice("VR Hand Animator", $"{label}: Animator hat keine Grip/Trigger Parameter. Hand ist sichtbar, animiert sich aber nicht.");
                }
            }
            else
            {
                LogNotice("VR Hand Animator", $"{label}: Animator vorhanden, aber kein Controller. Hand bleibt in Standard-Pose.");
            }
        }
        else
        {
            LogNotice("VR Hand Animator", $"{label}: Kein Animator installiert. Hand-Statik wird genutzt.");
        }

        totalTests++;
        SkinnedMeshRenderer skinnedMesh = handObj.GetComponentInChildren<SkinnedMeshRenderer>();
        if (skinnedMesh != null)
        {
            LogSuccess("VR Hand Mesh", $"{label}: 3D SkinnedMeshRenderer gefunden ('{skinnedMesh.name}').");
        }
        else if (zeigeDetailWarnungen)
        {
            LogNotice("VR Hand Mesh", $"{label}: Kein SkinnedMeshRenderer gefunden.");
        }
    }

    private void EvaluateAllHandAnimatorsInScene()
    {
        totalTests++;
        Animator[] allAnimators = FindObjectsByType<Animator>(FindObjectsSortMode.None);
        int handAnimatorCount = 0;

        foreach (Animator a in allAnimators)
        {
            string nameLower = a.gameObject.name.ToLower();
            if (nameLower.Contains("hand") || nameLower.Contains("gorilla") || nameLower.Contains("arm") || nameLower.Contains("monkey"))
            {
                handAnimatorCount++;
            }
        }

        if (handAnimatorCount > 0)
        {
            LogSuccess("VR Hand System", $"{handAnimatorCount} aktive Hand/Arm-Animatoren bereit.");
        }
        else
        {
            LogNotice("VR Hand System", "Keine spezifischen Hand-Animatoren in der Szene erkannt.");
        }
    }

    #endregion

    #region 5. COMPUTER SYSTEM CHECKS

    private void CheckComputerSystemFull()
    {
        totalTests++;

        if (computerObject != null)
        {
            LogSuccess("GorillaComputer", $"Computer-Objekt gefunden: '{computerObject.name}'.");

            totalTests++;
            MeshRenderer screenRenderer = computerObject.GetComponentInChildren<MeshRenderer>();
            Text uiText = computerObject.GetComponentInChildren<Text>();

            if (screenRenderer != null || uiText != null)
            {
                LogSuccess("GorillaComputer", "Computer-Display (MeshRenderer / UI Text Element) erkannt.");
            }
            else
            {
                LogNotice("GorillaComputer", "Kein Standard-Textfeld am Computer. Display zeigt eventuell keinen Text an.");
            }

            totalTests++;
            Collider[] buttons = computerObject.GetComponentsInChildren<Collider>();
            int buttonCount = 0;
            foreach (Collider btn in buttons)
            {
                if (btn.gameObject != computerObject) buttonCount++;
            }

            if (buttonCount > 0)
            {
                LogSuccess("GorillaComputer", $"Computer-Tastatur OK: {buttonCount} Button-Collider gefunden.");
            }
            else
            {
                LogNotice("GorillaComputer", "Keine Tasten-Collider gefunden. Eingabe per Hand-Klick klappt eventuell nicht.");
            }

            totalTests++;
            MonoBehaviour[] scripts = computerObject.GetComponentsInChildren<MonoBehaviour>();
            if (scripts.Length > 0)
            {
                LogSuccess("GorillaComputer", $"{scripts.Length} Steuerungs-Skripte am Terminal aktiv.");
            }
            else
            {
                LogNotice("GorillaComputer", "Keine C#-Skripte am Computer. Terminal ist rein dekorativ.");
            }
        }
        else
        {
            LogNotice("GorillaComputer", "Kein Computer-Objekt in der Szene. Räume/Namen können nicht im Spiel gewählt werden.");
        }
    }

    #endregion

    #region 6. CARS & HAZARDS CHECKS

    private void CheckCarsAndHazardsFull()
    {
        totalTests++;

        if (streetTrigger != null)
        {
            LogSuccess("StreetTriggerNetwork", $"Skript an Objekt '{streetTrigger.gameObject.name}' gefunden.");

            CheckVehicleSetup("Car 1", streetTrigger.car1);
            CheckVehicleSetup("Car 2", streetTrigger.car2);

            totalTests++;
            if (streetTrigger.driveDuration > 0.5f)
            {
                LogSuccess("StreetTriggerNetwork", $"Fahrdauer korrekt konfiguriert ({streetTrigger.driveDuration}s).");
            }
            else
            {
                LogNotice("StreetTriggerNetwork", $"Fahrdauer ist sehr kurz ({streetTrigger.driveDuration}s). Autos fahren extrem schnell.");
            }
        }
        else
        {
            LogNotice("StreetTriggerNetwork", "Kein StreetTriggerNetwork gefunden. Straße hat keine aktiven Autos.");
        }
    }

    private void CheckVehicleSetup(string label, GameObject car)
    {
        totalTests++;
        if (car == null)
        {
            LogNotice("StreetTriggerNetwork / Car", $"{label}: Auto-Objekt nicht zugewiesen. Fahrzeug-Mechanik wird übersprungen.");
            return;
        }

        LogSuccess("Vehicle System", $"{label} gefunden ('{car.name}').");

        totalTests++;
        Animator anim = car.GetComponent<Animator>();
        if (anim != null)
        {
            if (anim.runtimeAnimatorController != null)
            {
                LogSuccess("Vehicle Animator", $"{label}: Animator mit Controller ('{anim.runtimeAnimatorController.name}') verbunden.");
            }
            else
            {
                LogNotice("Vehicle Animator", $"{label}: Animator ohne Controller. Auto bewegt sich nicht über Animationen.");
            }
        }
        else
        {
            LogNotice("Vehicle Animator", $"{label}: Keine Animator-Komponente. Fahrzeug nutzt physikalische/skriptbasierte Fahrten.");
        }

        totalTests++;
        CarHitbox hitbox = car.GetComponent<CarHitbox>();
        Collider carCol = car.GetComponent<Collider>();

        if (carCol != null)
        {
            if (carCol.isTrigger)
            {
                LogSuccess("CarHitbox Collider", $"{label}: Collider hat 'Is Trigger = true'.");
            }
            else
            {
                LogNotice("CarHitbox Collider", $"{label}: Collider 'Is Trigger' deaktiviert. Spieler wird weggeschoben statt gerammt.");
            }
        }
        else
        {
            LogNotice("CarHitbox Collider", $"{label}: Besitzt keinen Collider. Spieler fährt ohne Kollision durch das Auto.");
        }

        totalTests++;
        if (hitbox != null)
        {
            LogSuccess("CarHitbox Script", $"{label}: 'CarHitbox'-Skript installiert (Push: {hitbox.pushForce}, Up: {hitbox.upwardForce}).");
        }
        else
        {
            LogNotice("CarHitbox Script", $"{label}: 'CarHitbox'-Skript fehlt. Auto verursacht keinen Knockback/Schaden.");
        }
    }

    #endregion

    #region 7. MULTIPLAYER & PHOTON CHECKS

    private void CheckPhotonMultiplayerFull()
    {
        totalTests++;
        PhotonView[] views = FindObjectsByType<PhotonView>(FindObjectsSortMode.None);

        if (views.Length > 0)
        {
            LogSuccess("Photon PUN2", $"Multiplayer-Setup erkannt ({views.Length} PhotonViews in der Szene).");

            totalTests++;
            if (streetTrigger != null)
            {
                PhotonView triggerPV = streetTrigger.GetComponent<PhotonView>();
                if (triggerPV != null)
                {
                    LogSuccess("StreetTriggerNetwork PhotonView", "StreetTriggerNetwork besitzt eine eigene PhotonView.");
                }
                else
                {
                    LogNotice("StreetTriggerNetwork PhotonView", "StreetTriggerNetwork hat keine PhotonView. Autos werden lokal statt im Netzwerk synchronisiert.");
                }
            }
        }
        else
        {
            LogNotice("Photon PUN2", "Keine PhotonViews in der Szene. Spiel läuft einwandfrei im Singleplayer.");
        }
    }

    #endregion

    #region 8. AUDIO SYSTEM CHECKS

    private void CheckAudioSystem()
    {
        totalTests++;
        AudioSource[] sources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);

        if (sources.Length > 0)
        {
            LogSuccess("Audio System", $"{sources.Length} AudioSource-Komponenten in der Szene gefunden.");

            int spatial3DCount = 0;
            foreach (AudioSource src in sources)
            {
                if (src.spatialBlend > 0.5f) spatial3DCount++;
            }

            totalTests++;
            LogSuccess("Audio System", $"{spatial3DCount} AudioSources nutzen 3D Spatial Blend.");
        }
        else if (zeigeDetailWarnungen)
        {
            LogNotice("Audio System", "Keine AudioSources in der Szene. Spiel läuft stumm.");
        }
    }

    #endregion

    #region LOGGING & SAVING UTILITIES

    private void SectionHeader(string title)
    {
        Debug.Log($"<color=yellow><b>---> {title}</b></color>");
    }

    private void LogSuccess(string sourceScript, string message)
    {
        passedTests++;
        Debug.Log($"<color=lime>[OK] [{sourceScript}]</color> {message}");
    }

    private void LogWarning(string sourceScript, string message)
    {
        warningCount++;
        Debug.LogWarning($"<color=orange>[WARNUNG] [{sourceScript}]</color> {message}");

        savedLogs.Add(new HealthLogEntry
        {
            scriptOrSystem = sourceScript,
            message = message,
            type = LogType.Warning
        });
    }

    private void LogError(string sourceScript, string message)
    {
        failedTests++;
        Debug.LogError($"<color=red><b>[FEHLER] [{sourceScript}]</b></color> {message}");

        savedLogs.Add(new HealthLogEntry
        {
            scriptOrSystem = sourceScript,
            message = message,
            type = LogType.Error
        });
    }

    private void LogNotice(string sourceScript, string message)
    {
        noticeCount++;
        Debug.Log($"<color=#00AA00>[FUNKTIONIERT / RISIKO] [{sourceScript}]</color> {message}");

        savedLogs.Add(new HealthLogEntry
        {
            scriptOrSystem = sourceScript,
            message = message,
            type = LogType.Log
        });
    }

    private void PrintFinalReport()
    {
        Debug.Log("<color=cyan><b>================================================================================================</b></color>");
        Debug.Log("<color=cyan><b>                               DIAGNOSTIC SCAN COMPLETED REPORT                                 </b></color>");
        Debug.Log("<color=cyan><b>================================================================================================</b></color>");

        if (failedTests == 0 && warningCount == 0 && noticeCount == 0)
        {
            Debug.Log($"<color=lime><b>[SYSTEM STATUS: PERFECT] ALL CHECKS PASSED SUCCESSFULLY!</b></color>");
        }
        else
        {
            Debug.LogWarning($"<color=yellow><b>[SYSTEM SCAN COMPLETED] Fehler: {failedTests} | Warnungen: {warningCount} | Funktions-Risiken: {noticeCount}</b></color>");
        }

        Debug.Log("<color=cyan><b>================================================================================================</b></color>");
    }

    #endregion

    #region ONGUI IN-GAME KASTEN & UI BUTTON

    private void OnGUI()
    {
        if (!zeigeInGameKasten) return;

        float width = 500f;
        float height = 380f;
        Rect windowRect = new Rect(20, 20, width, height);

        GUI.Box(windowRect, "");
        GUILayout.BeginArea(new Rect(30, 25, width - 20, height - 10));

        GUILayout.Label("<b>MONKEY MADNESS - HEALTH CHECK DIAGNOSE</b>", GUI.skin.label);

        if (failedTests > 0)
        {
            GUI.color = Color.red;
            GUILayout.Label($"<b>[KRITISCHE FEHLER: {failedTests}]</b>");
        }

        if (warningCount > 0)
        {
            GUI.color = Color.yellow;
            GUILayout.Label($"<b>[WARNUNGEN / GEFAHR: {warningCount}]</b>");
        }

        if (noticeCount > 0)
        {
            GUI.color = new Color(0f, 0.7f, 0f);
            GUILayout.Label($"<b>[FUNKTIONIERT / KANN ZU FEHLER FÜHREN: {noticeCount}]</b>");
        }

        if (failedTests == 0 && warningCount == 0 && noticeCount == 0 && totalTests > 0)
        {
            GUI.color = Color.green;
            GUILayout.Label("<b>[PERFEKT: ALLES IN ORDNUNG! KEINE FEHLER]</b>");
        }

        GUI.color = Color.white;
        GUILayout.Label($"Tests Bestanden: {passedTests} / {totalTests} | Gemerkte Skripte: {knownScriptKeys.Count}");

        GUILayout.Space(5);

        guiScrollPosition = GUILayout.BeginScrollView(guiScrollPosition, GUILayout.Width(width - 30), GUILayout.Height(180));

        if (savedLogs.Count == 0)
        {
            if (totalTests == 0)
            {
                GUILayout.Label("Keine Diagnose durchgeführt. Starte den Play-Mode.");
            }
            else
            {
                GUI.color = Color.green;
                GUILayout.Label("Keine Fehler, Warnungen oder Risiken vorhanden!");
                GUI.color = Color.white;
            }
        }
        else
        {
            foreach (var log in savedLogs)
            {
                if (log.type == LogType.Error)
                {
                    GUI.color = Color.red;
                    GUILayout.Label($"[FEHLER] [{log.scriptOrSystem}] {log.message}");
                }
                else if (log.type == LogType.Warning)
                {
                    GUI.color = Color.yellow;
                    GUILayout.Label($"[WARNUNG] [{log.scriptOrSystem}] {log.message}");
                }
                else
                {
                    GUI.color = new Color(0.2f, 0.9f, 0.2f);
                    GUILayout.Label($"[INFO] [{log.scriptOrSystem}] {log.message}");
                }
            }
            GUI.color = Color.white;
        }

        GUILayout.EndScrollView();

        GUILayout.Space(5);

        if (GUILayout.Button("Diagnose Erneut Starten"))
        {
            RunFullSystemDiagnostics();
        }

        GUILayout.EndArea();
    }

    #endregion
}