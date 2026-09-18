using System.Collections.Generic;
using UnityEngine;
using TrueDetective.Core;
using TrueDetective.Data;
using TrueDetective.UI;

namespace TrueDetective.World
{
    /// <summary>
    /// The walkable mode. Owns the camera, the player body, the current room and the
    /// nearest-spot prompt, and hands off to the existing screens the moment the player
    /// acts on something.
    ///
    /// It deliberately does not own any investigation rules. Whether a clue is available,
    /// what a question unlocks, whether a confrontation is earned - all of that stays in
    /// CaseSession, so the map cannot drift out of step with the case.
    /// </summary>
    public class WorldView : MonoBehaviour
    {
        /// <summary>Vertical world units the camera shows. Smaller = closer in.</summary>
        public float ViewHeight = 16f;

        /// <summary>How hard the camera chases the player. Lower is floatier.</summary>
        public float CameraLag = 7f;

        /// <summary>Player body height in world units.</summary>
        public float PlayerHeight = 2.2f;

        private Camera _cam;
        private Walker _player;
        private WorldBuilder _builder;
        private MapData _map;
        private CaseSession _session;

        private WorldSpot _nearest;
        private System.Action<SpotData> _onInteract;

        public Walker Player { get { return _player; } }
        public WorldSpot Nearest { get { return _nearest; } }
        public bool Active { get; private set; }

        /// <summary>Raised whenever the closest spot changes, so the UI can update its prompt.</summary>
        public event System.Action<WorldSpot> NearestChanged;

        public void Setup(MapData map, CaseSession session, System.Action<SpotData> onInteract)
        {
            _map = map;
            _session = session;
            _onInteract = onInteract;

            _map.BuildIndex();

            // ---- camera ----
            var camGo = new GameObject("world camera");
            camGo.transform.SetParent(transform, false);
            _cam = camGo.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = ViewHeight * 0.5f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color32(0x08, 0x0A, 0x0C, 0xFF);
            _cam.transform.position = new Vector3(0f, 0f, -10f);

            // Above the scene's Main Camera, which also clears to a solid colour: whichever
            // camera has the higher depth renders last, so a lower value here would let the
            // Main Camera paint straight over the world. The UI canvas is Screen Space
            // Overlay and draws after every camera regardless.
            _cam.depth = 10f;

            // ---- player ----
            var pgo = new GameObject("player");
            pgo.transform.SetParent(transform, false);

            var body = pgo.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;

            var col = pgo.AddComponent<CapsuleCollider2D>();
            // a small capsule at the feet: the body should slide past a desk corner the
            // way a person would, not collide with the whole painted silhouette
            col.size = new Vector2(0.75f, 0.55f);
            col.direction = CapsuleDirection2D.Horizontal;
            col.offset = new Vector2(0f, 0.15f);

            _player = pgo.AddComponent<Walker>();
            _player.Front = Art.Load("Maps/", "ch_detective_front");
            _player.Back  = Art.Load("Maps/", "ch_detective_back");
            _player.Side  = Art.Load("Maps/", "ch_detective_side");
            _player.AttachShadow(UIKit.SoftBlob, PlayerHeight * 0.45f);
            _player.SetHeight(PlayerHeight);

            // ---- room ----
            var bgo = new GameObject("builder");
            bgo.transform.SetParent(transform, false);
            _builder = bgo.AddComponent<WorldBuilder>();

            Physics2D.gravity = Vector2.zero;
        }

        /// <summary>Shows or hides the whole walkable mode.</summary>
        public void SetActive(bool on)
        {
            Active = on;
            gameObject.SetActive(true);
            if (_cam != null) _cam.enabled = on;
            if (_player != null)
            {
                _player.gameObject.SetActive(on);
                _player.CanMove = on;
            }
            if (_builder != null) _builder.gameObject.SetActive(on);
        }

        /// <summary>Freezes the body without hiding the world, for when a panel opens over it.</summary>
        public void SetInputEnabled(bool on)
        {
            if (_player != null)
            {
                _player.CanMove = on;
                if (!on) _player.SetInput(Vector2.zero);
            }
        }

        /// <summary>Does this case have a walkable version of that location?</summary>
        public bool HasRoom(string locationId)
        {
            return _map != null && _map.GetRoom(locationId) != null;
        }

        /// <summary>
        /// Loads a room and places the player. <paramref name="arriveFrom"/> names the
        /// room they came from; if a spot in the new room travels back there, the player
        /// appears next to it, which is what makes a doorway feel like a doorway.
        /// </summary>
        public void EnterRoom(string roomId, string arriveFrom)
        {
            var room = _map.GetRoom(roomId);
            if (room == null) return;

            _builder.Build(room, IsSpotVisible);

            Vector2 at = room.spawn != null ? new Vector2(room.spawn.x, room.spawn.y) : Vector2.zero;

            if (!string.IsNullOrEmpty(arriveFrom))
            {
                foreach (var s in _builder.Spots)
                {
                    if (s.Data == null || s.Data.travelTo != arriveFrom) continue;
                    // stand just inside the door, not on top of it, or the player
                    // immediately re-triggers the way back
                    Vector2 door = s.transform.position;
                    at = door + (Vector2.zero - door).normalized * 1.8f;
                    break;
                }
            }

            _player.Teleport(at);
            SnapCamera();

            _nearest = null;
            if (NearestChanged != null) NearestChanged(null);
        }

        /// <summary>Rebuilds the current room, picking up anything the session has changed.</summary>
        public void RefreshRoom()
        {
            if (_builder == null || _builder.Room == null) return;
            var keep = _player.transform.position;
            _builder.Build(_builder.Room, IsSpotVisible);
            _player.Teleport(keep);
        }

        private bool IsSpotVisible(SpotData s)
        {
            if (s == null) return false;
            if (_session == null) return true;
            if (!_session.RequirementMet(s.requires)) return false;
            // a clue already in the notebook is gone from the floor
            if (!string.IsNullOrEmpty(s.givesEvidence) && _session.HasEvidence(s.givesEvidence))
                return false;
            return true;
        }

        public void SetInput(Vector2 dir)
        {
            if (_player != null) _player.SetInput(dir);
        }

        /// <summary>Acts on the nearest spot, if there is one in reach.</summary>
        public void Interact()
        {
            if (_nearest == null || _nearest.Data == null) return;
            if (_onInteract != null) _onInteract(_nearest.Data);
        }

        private void Update()
        {
            if (!Active || _player == null) return;

            TrackNearest();
            SortByDepth();

            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space)) Interact();
        }

        private void LateUpdate()
        {
            if (!Active) return;
            FollowCamera();
        }

        /// <summary>Finds the closest spot in range and reports a change exactly once.</summary>
        private void TrackNearest()
        {
            Vector2 p = _player.transform.position;
            WorldSpot best = null;
            float bestDist = float.MaxValue;

            foreach (var s in _builder.Spots)
            {
                if (s == null || s.Data == null) continue;
                float d = s.DistanceTo(p);
                // highlight ramps up over the last stretch of the approach
                s.SetHighlight(Mathf.InverseLerp(s.Data.radius * 2.2f, s.Data.radius, d));
                if (d > s.Data.radius || d >= bestDist) continue;
                best = s; bestDist = d;
            }

            if (best == _nearest) return;
            _nearest = best;
            if (NearestChanged != null) NearestChanged(best);
        }

        /// <summary>
        /// Painter's order by Y: whatever is lower on the screen is nearer the camera and
        /// draws in front. Without this the player walks behind a desk they are standing
        /// in front of.
        /// </summary>
        private void SortByDepth()
        {
            SetOrder(_player.transform, _player.transform.position.y);
            foreach (var s in _builder.Spots)
                if (s != null) SetOrder(s.transform, s.transform.position.y);
        }

        private static void SetOrder(Transform t, float y)
        {
            int order = Mathf.RoundToInt(-y * 100f);
            var renderers = t.GetComponentsInChildren<SpriteRenderer>();
            foreach (var r in renderers)
            {
                if (r.gameObject.name == "shadow") { r.sortingOrder = order - 1; continue; }
                r.sortingOrder = order;
            }
        }

        private void FollowCamera()
        {
            if (_cam == null || _player == null) return;

            Vector3 want = ClampToRoom(_player.transform.position);
            want.z = -10f;

            // exponential ease, framerate independent
            float k = 1f - Mathf.Exp(-CameraLag * Time.deltaTime);
            _cam.transform.position = Vector3.Lerp(_cam.transform.position, want, k);
        }

        private void SnapCamera()
        {
            if (_cam == null || _player == null) return;
            var want = ClampToRoom(_player.transform.position);
            _cam.transform.position = new Vector3(want.x, want.y, -10f);
        }

        /// <summary>
        /// Keeps the view inside the painted room, and centres an axis outright when the
        /// room is smaller than the view on that axis.
        /// </summary>
        private Vector3 ClampToRoom(Vector3 target)
        {
            var room = _builder != null ? _builder.Room : null;
            if (room == null || _cam == null) return target;

            float halfH = _cam.orthographicSize;
            float halfW = halfH * _cam.aspect;

            float limitX = room.width * 0.5f - halfW;
            float limitY = room.height * 0.5f - halfH;

            float x = limitX <= 0f ? 0f : Mathf.Clamp(target.x, -limitX, limitX);
            float y = limitY <= 0f ? 0f : Mathf.Clamp(target.y, -limitY, limitY);
            return new Vector3(x, y, target.z);
        }
    }
}
