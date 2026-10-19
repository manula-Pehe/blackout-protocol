using System.Collections;
using UnityEngine;
using BlackoutProtocol.Core;

namespace BlackoutProtocol.AI.Sentinel.Testing
{
    /// <summary>Runs a repeatable sight, track, occlusion, search and return scenario in the Sentinel sandbox.</summary>
    public sealed class SentinelHsmScenarioRunner : MonoBehaviour
    {
        [SerializeField] private SentinelDroneBrain sentinel;
        [SerializeField] private SentinelDroneBody body;
        [SerializeField] private SentinelTestPlayerState player;
        [SerializeField] private Transform occlusionWall;

        private bool alarmReceived;
        private bool finished;
        [SerializeField] private string status = "Waiting to start";

        private void OnEnable() => GameEvents.AlarmRaised += HandleAlarm;
        private void OnDisable() => GameEvents.AlarmRaised -= HandleAlarm;

        private IEnumerator Start()
        {
            if (sentinel == null || body == null || player == null || occlusionWall == null)
            {
                Fail("Scenario references are incomplete.");
                yield break;
            }

            player.SetPose(new Vector3(14f, 1f, 8f), Vector3.zero);
            status = "Waiting: starts patrolling outside the player's sight range";
            float deadline = Time.time + 3f;
            while (sentinel.State != SentinelState.Patrol && Time.time < deadline) yield return null;
            if (sentinel.State != SentinelState.Patrol) { Fail($"Timed out waiting for Patrol; current state is {sentinel.State}."); yield break; }
            Debug.Log("SENTINEL_TEST PASS: starts patrolling outside the player's sight range.", this);
            yield return new WaitForSeconds(0.35f);
            if (!body.IsMoving)
            {
                Fail("The Sentinel did not start moving along its configured patrol route.");
                yield break;
            }

            player.SetPose(new Vector3(0f, 1f, 0f), Vector3.forward * 1.5f);
            status = "Waiting: spots the player and raises an alarm";
            deadline = Time.time + 3f;
            while (sentinel.State != SentinelState.Alert && Time.time < deadline) yield return null;
            if (sentinel.State != SentinelState.Alert) { Fail($"Timed out waiting for Alert; current state is {sentinel.State}."); yield break; }
            Debug.Log("SENTINEL_TEST PASS: spots the player and raises an alarm.", this);
            if (!alarmReceived)
            {
                Fail("The Sentinel entered Alert without broadcasting GameEvents.AlarmRaised.");
                yield break;
            }

            status = "Waiting: transitions from Alert to Track";
            deadline = Time.time + 3f;
            while (sentinel.State != SentinelState.Track && Time.time < deadline) yield return null;
            if (sentinel.State != SentinelState.Track) { Fail($"Timed out waiting for Track; current state is {sentinel.State}."); yield break; }
            Debug.Log("SENTINEL_TEST PASS: transitions from Alert to Track.", this);
            if (!body.IsMoving)
            {
                Fail("The Sentinel entered Track but did not issue a movement path.");
                yield break;
            }

            yield return new WaitForSeconds(0.4f);
            Vector3 hiddenPosition = new Vector3(4f, 1f, 0f);
            Transform sensorOrigin = null;
            Transform[] sentinelParts = sentinel.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < sentinelParts.Length; i++)
            {
                if (sentinelParts[i].name == "Camera_Aim_Pivot")
                {
                    sensorOrigin = sentinelParts[i];
                    break;
                }
            }
            Vector3 sensor = sensorOrigin != null ? sensorOrigin.position : sentinel.transform.position;
            Vector3 sightVector = hiddenPosition - sensor;
            bool wallBlocksSight = false;
            RaycastHit[] lineHits = Physics.RaycastAll(sensor, sightVector.normalized, sightVector.magnitude,
                Physics.AllLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < lineHits.Length; i++)
            {
                Transform hitTransform = lineHits[i].transform;
                if (hitTransform != sentinel.transform && !hitTransform.IsChildOf(sentinel.transform) &&
                    hitTransform == occlusionWall)
                {
                    wallBlocksSight = true;
                    break;
                }
            }
            if (!wallBlocksSight)
            {
                Fail($"Test setup error: the wall does not occlude the ray (sensor {sensor}, target {hiddenPosition}).");
                yield break;
            }

            player.SetPose(hiddenPosition, Vector3.zero);
            status = "Waiting: loses sight behind the wall and starts searching";
            deadline = Time.time + 3f;
            while (sentinel.State != SentinelState.Search && Time.time < deadline) yield return null;
            if (sentinel.State != SentinelState.Search) { Fail($"Timed out waiting for Search; current state is {sentinel.State}."); yield break; }
            Debug.Log("SENTINEL_TEST PASS: loses sight behind the wall and starts searching.", this);
            player.gameObject.SetActive(false);
            status = "Waiting: finishes its predictive search and returns to patrol";
            deadline = Time.time + 40f;
            while (sentinel.State != SentinelState.Return && Time.time < deadline) yield return null;
            if (sentinel.State != SentinelState.Return) { Fail($"Timed out waiting for Return; current state is {sentinel.State}."); yield break; }
            Debug.Log("SENTINEL_TEST PASS: finishes its predictive search and returns to patrol.", this);
            status = "Waiting: reaches its patrol route and resumes patrol";
            deadline = Time.time + 20f;
            while (sentinel.State != SentinelState.Patrol && Time.time < deadline) yield return null;
            if (sentinel.State != SentinelState.Patrol) { Fail($"Timed out waiting for resumed Patrol; current state is {sentinel.State}."); yield break; }
            Debug.Log("SENTINEL_TEST PASS: reaches its patrol route and resumes patrol.", this);

            finished = true;
            status = "PASS — all Sentinel HSM scenario checks completed";
            Debug.Log("SENTINEL_TEST PASS: patrol, sighting, alarm, tracking, occlusion, search, return, and patrol resume.", this);
        }

        private void HandleAlarm(Vector3 playerPosition)
        {
            if (alarmReceived) return;
            alarmReceived = true;
            if ((playerPosition - new Vector3(0f, 1f, 0f)).sqrMagnitude > 1f)
            {
                Fail("The alarm broadcast did not carry the player's last seen position.");
            }
        }

        private void Fail(string message)
        {
            if (finished) return;
            finished = true;
            status = "FAIL — " + message;
            Debug.LogError("SENTINEL_TEST FAIL: " + message, this);
        }

        private void OnGUI()
        {
            if (!Application.isPlaying) return;
            GUI.Box(new Rect(14f, 14f, 470f, 62f), "Sentinel HSM Scenario\n" + status + "\nState: " + (sentinel != null ? sentinel.State.ToString() : "missing"));
        }
    }
}
