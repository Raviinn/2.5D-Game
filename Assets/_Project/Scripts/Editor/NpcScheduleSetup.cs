using System.Collections.Generic;
using System.IO;
using Beast.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Beast.EditorTools
{
    /// <summary>
    /// Milestone 31 setup (NPC schedules): daily routines for Oswin (merchant) and Brenna (blacksmith), with their places
    /// as empty objects under [NPC Places] (move them to change where they go). Each gets a house: the house door
    /// nearest their work spot. Safe to re-run: existing places and schedules are kept (delete [NPC Places] or a
    /// schedule to rebuild it).
    /// </summary>
    public static class NpcScheduleSetup
    {
        const string TestWorldScenePath = "Assets/_Project/Scenes/World_Test.unity";
        internal const string PlacesName = "[NPC Places]";

        [MenuItem("Beast/Setup/Run Milestone 31 Setup (NPC Schedules)", priority = 25)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(TestWorldScenePath))
            {
                Debug.LogError("[Setup] World_Test not found. Run the earlier setups first.");
                return;
            }
            var scene = EditorSceneManager.OpenScene(TestWorldScenePath);
            Physics.SyncTransforms();

            var merchant = GameObject.Find("Merchant");
            var smith = GameObject.Find("Blacksmith");
            var well = GameObject.Find("Well");
            var board = Object.FindFirstObjectByType<ContractBoard>();
            if (merchant == null || smith == null)
            {
                Debug.LogError("[Setup] Merchant or Blacksmith not found. Run the Milestone 6 and 7 setups first.");
                return;
            }

            var placesRoot = GameObject.Find(PlacesName);
            if (placesRoot == null) placesRoot = new GameObject(PlacesName);
            var doors = HouseDoors();
            var usedDoors = new HashSet<Transform>();
            var report = new List<string>();

            Vector3 wellCentre = well != null ? well.transform.position : merchant.transform.position + Vector3.right * 4f;
            Vector3 boardFront = board != null ? Ground(board.transform.position - board.transform.forward * 1.6f) : wellCentre + Vector3.left * 3f;

            if (!merchant.TryGetComponent(out NpcSchedule _))
            {
                var stall = Place(placesRoot.transform, "Oswin_Stall", Ground(merchant.transform.position), merchant.transform.forward);
                var wellSpot = Place(placesRoot.transform, "Oswin_Well", ClearSpotNear(wellCentre, 1.7f, 0), (wellCentre - Vector3.zero).normalized);
                FaceTowards(wellSpot, wellCentre);
                var home = HomeDoor(placesRoot.transform, "Oswin_Home", merchant.transform.position, doors, usedDoors);
                AddSchedule(merchant,
                    (6f, stall, "At the stall", false),
                    (12f, wellSpot, "Lunch at the well", false),
                    (13f, stall, "At the stall", false),
                    (19f, wellSpot, "Evening by the well", false),
                    (21.5f, home, "Asleep at home", true));
                report.Add("Oswin's routine");
            }
            if (!smith.TryGetComponent(out NpcSchedule _))
            {
                var forge = Place(placesRoot.transform, "Brenna_Forge", Ground(smith.transform.position), smith.transform.forward);
                var boardSpot = Place(placesRoot.transform, "Brenna_Board", boardFront, board != null ? board.transform.forward : Vector3.forward);
                if (board != null) FaceTowards(boardSpot, board.transform.position);
                var wellSpot = Place(placesRoot.transform, "Brenna_Well", ClearSpotNear(wellCentre, 1.7f, 4), Vector3.forward);
                FaceTowards(wellSpot, wellCentre);
                var home = HomeDoor(placesRoot.transform, "Brenna_Home", smith.transform.position, doors, usedDoors);
                AddSchedule(smith,
                    (6f, forge, "At the forge", false),
                    (13f, boardSpot, "Reading the contract board", false),
                    (14f, forge, "At the forge", false),
                    (19.5f, wellSpot, "Evening by the well", false),
                    (22.5f, home, "Asleep at home", true));
                report.Add("Brenna's routine");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Setup] Milestone 31 NPC schedules setup complete: {(report.Count > 0 ? string.Join(" and ", report) + " added" : "routines kept")}. " +
                      "Their places are under [NPC Places]; NPCs work 06:00 to evening and sleep at home at night.");
        }

        internal static void AddSchedule(GameObject npc, params (float hour, Transform place, string activity, bool indoors)[] entries)
        {
            var schedule = npc.AddComponent<NpcSchedule>();
            var so = new SerializedObject(schedule);
            var list = so.FindProperty("entries");
            list.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                var e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("StartHour").floatValue = entries[i].hour;
                e.FindPropertyRelative("Place").objectReferenceValue = entries[i].place;
                e.FindPropertyRelative("Activity").stringValue = entries[i].activity;
                e.FindPropertyRelative("Indoors").boolValue = entries[i].indoors;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static Transform Place(Transform parent, string name, Vector3 position, Vector3 facing)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            facing.y = 0f;
            go.transform.SetPositionAndRotation(position, facing.sqrMagnitude > 0.001f ? Quaternion.LookRotation(facing) : Quaternion.identity);
            return go.transform;
        }

        internal static void FaceTowards(Transform place, Vector3 target)
        {
            var to = target - place.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f) place.rotation = Quaternion.LookRotation(to);
        }

        /// <summary>The door (of a house nobody else uses) nearest this spot; the place stands just outside it, facing in.</summary>
        static Transform HomeDoor(Transform parent, string name, Vector3 near, List<Transform> doors, HashSet<Transform> used)
        {
            Transform best = null;
            float bestDistance = float.MaxValue;
            foreach (var door in doors)
            {
                if (used.Contains(door.parent)) continue;
                float d = (door.position - near).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = door; }
            }
            if (best == null) return Place(parent, name, Ground(near + Vector3.back * 6f), Vector3.forward);
            used.Add(best.parent);
            // A door decal's visible side faces -forward (EnvironmentSetup.Decal): stand there, facing the door.
            Vector3 outward = -best.forward;
            outward.y = 0f;
            outward.Normalize();
            return Place(parent, name, Ground(best.position + outward * 0.9f), -outward);
        }

        static List<Transform> HouseDoors()
        {
            var doors = new List<Transform>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.name == "Door" && UnderHouse(t)) doors.Add(t);
            return doors;
        }

        static bool UnderHouse(Transform t)
        {
            for (var p = t.parent; p != null; p = p.parent)
                if (p.name.StartsWith("House_")) return true;
            return false;
        }

        /// <summary>A spot about 'radius' from 'centre' with room to stand (tries round the circle from 'startOctant').</summary>
        internal static Vector3 ClearSpotNear(Vector3 centre, float radius, int startOctant)
        {
            for (int i = 0; i < 8; i++)
            {
                var direction = Quaternion.Euler(0f, (startOctant + i) * 45f, 0f) * Vector3.forward;
                var spot = Ground(centre + direction * radius);
                if (!Physics.CheckCapsule(spot + Vector3.up * 0.5f, spot + Vector3.up * 1.6f, 0.4f, ~0, QueryTriggerInteraction.Ignore)) return spot;
            }
            return Ground(centre + Vector3.forward * radius);
        }

        internal static Vector3 Ground(Vector3 at)
        {
            var from = new Vector3(at.x, 5f, at.z);
            foreach (var hit in Physics.RaycastAll(from, Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.GetComponentInParent<NpcController>() == null && hit.normal.y > 0.7f && hit.point.y < 1f)
                    return hit.point;
            return new Vector3(at.x, 0f, at.z);
        }
    }
}
