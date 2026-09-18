using UnityEngine;
using UnityEngine.EventSystems;
using TrueDetective.Core;
using TrueDetective.Data;
using TrueDetective.UI;

namespace TrueDetective.World
{
    /// <summary>
    /// The walkable mode: camera, player body, current room, and tap handling.
    ///
    /// Tap a spot and the detective walks over and acts on it. Tap bare floor and he
    /// walks there. There is no held input anywhere in this file, which is what makes
    /// it recoverable - any tap at any time replaces whatever came before it.
    ///
    /// No investigation rules live here. What is available, what a question opens, when
    /// a confrontation is earned: all of that stays in CaseSession.
    /// </summary>
    public class WorldView : MonoBehaviour
    {
        /// <summary>
        /// Extra world units of room shown beyond its own height. 0 frames the room's
        /// full height exactly, which on a portrait screen still leaves the width
        /// scrolling - a square room cannot fill a 9:16 frame without either cropping
        /// the sides or stranding the player in a sea of background.
        /// </summary>
        public float ViewPadding = 1.0f;

        public float CameraLag = 8f;
        public float PlayerHeight = 2.9f;

        /// <summary>How close to a spot the body stops before acting on it.</summary>
        public float StandOff = 1.35f;

        private Camera _cam;
        private Walker _player;
        private WorldBuilder _builder;
        private MapData _map;
        private CaseSession _session;
        private Transform _marker;

        private WorldSpot _nearest;
        private WorldSpot _pending;          // walking over to act on this one
        private System.Action<SpotData> _onInteract;

        public Walker Player { get { return _player; } }
        public bool Active { get; private set; }

        public event System.Action<WorldSpot> NearestChanged;

        public void Setup(MapData map, CaseSession session, System.Action<SpotData> onInteract)
        {
            _map = map;
            _session = session;
            _onInteract = onInteract;
            _map.BuildIndex();

            Physics2D.gravity = Vector2.zero;

            // ---- camera ----
            var camGo = new GameObject("world camera");
            camGo.transform.SetParent(transform, false);
            _cam = camGo.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = 10f;            // replaced per room by FitToRoom
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color32(0x08, 0x0A, 0x0C, 0xFF);
            _cam.transform.position = new Vector3(0f, 0f, -10f);
            // above the scene's Main Camera, which also clears to a solid colour
            _cam.depth = 10f;

            // ---- player ----
            var pgo = new GameObject("player");
            pgo.transform.SetParent(transform, false);

            var body = pgo.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;

            var col = pgo.AddComponent<CapsuleCollider2D>();
            col.size = new Vector2(0.75f, 0.55f);
            col.direction = CapsuleDirection2D.Horizontal;
            col.offset = new Vector2(0f, 0.15f);

            _player = pgo.AddComponent<Walker>();
            _player.Front = Art.Load("Maps/", "ch_detective_front");
            _player.Back  = Art.Load("Maps/", "ch_detective_back");
            _player.Side  = Art.Load("Maps/", "ch_detective_side");
            _player.AttachShadow(UIKit.SoftBlob, PlayerHeight * 0.45f);
            _player.SetHeight(PlayerHeight);

            _player.Arrived += OnArrived;
            _player.Stalled += OnStalled;

            // ---- destination marker ----
            var mgo = new GameObject("marker");
            mgo.transform.SetParent(transform, false);
            var msr = mgo.AddComponent<SpriteRenderer>();
            msr.sprite = UIKit.Ring;
            msr.color = new Color(0.88f, 0.63f, 0.24f, 0.85f);
            msr.sortingOrder = 30000;
            mgo.transform.localScale = Vector3.one * 0.9f;
            _marker = mgo.transform;
            _marker.gameObject.SetActive(false);

            // ---- room ----
            var bgo = new GameObject("builder");
            bgo.transform.SetParent(transform, false);
            _builder = bgo.AddComponent<WorldBuilder>();
        }

        public void SetActive(bool on)
        {
            Active = on;
            if (_cam != null) _cam.enabled = on;
            if (_player != null)
            {
                _player.gameObject.SetActive(on);
                if (!on) _player.Halt();
            }
            if (_builder != null) _builder.gameObject.SetActive(on);
            if (!on && _marker != null) _marker.gameObject.SetActive(false);
        }

        /// <summary>Stops the body and cancels its errand when a panel opens over the world.</summary>
        public void SetInputEnabled(bool on)
        {
            if (_player == null) return;
            _player.CanMove = on;
            if (!on)
            {
                _player.Halt();
                ClearPending();
            }
        }

        public bool HasRoom(string locationId)
        {
            return _map != null && _map.GetRoom(locationId) != null;
        }

        public void EnterRoom(string roomId, string arriveFrom)
        {
            var room = _map.GetRoom(roomId);
            if (room == null) return;

            ClearPending();
            SetNearest(null);
            _builder.Build(room, IsSpotVisible);

            Vector2 at = room.spawn != null ? new Vector2(room.spawn.x, room.spawn.y) : Vector2.zero;

            if (!string.IsNullOrEmpty(arriveFrom))
            {
                foreach (var s in _builder.Spots)
                {
                    if (s == null || s.Data == null || s.Data.travelTo != arriveFrom) continue;
                    // stand just inside the door, or the player instantly walks back out
                    Vector2 door = s.transform.position;
                    Vector2 inward = (Vector2.zero - door);
                    if (inward.sqrMagnitude < 0.01f) inward = Vector2.up;
                    at = door + inward.normalized * 2.2f;
                    break;
                }
            }

            FitToRoom(room);
            _player.Teleport(at);
            SnapCamera();
        }

        /// <summary>
        /// Frames the room's full height. Anything tighter and the player cannot see
        /// where the doors and the people are, which is what made the rooms confusing
        /// to move around rather than merely small.
        /// </summary>
        private void FitToRoom(RoomData room)
        {
            if (_cam == null || room == null) return;
            _cam.orthographicSize = (room.height + ViewPadding) * 0.5f;
        }

        /// <summary>
        /// Rebuilds the room in place, keeping the player where they stand.
        ///
        /// Every cached spot reference has to be dropped first: Build destroys the old
        /// objects, and a pointer left behind would keep a prompt on screen for a thing
        /// that no longer exists and can no longer be acted on.
        /// </summary>
        public void RefreshRoom()
        {
            if (_builder == null || _builder.Room == null) return;
            ClearPending();
            SetNearest(null);

            var keep = _player.transform.position;
            _builder.Build(_builder.Room, IsSpotVisible);
            _player.Teleport(keep);
        }

        private bool IsSpotVisible(SpotData s)
        {
            if (s == null) return false;
            if (_session == null) return true;
            if (!_session.RequirementMet(s.requires)) return false;
            if (!string.IsNullOrEmpty(s.givesEvidence) && _session.HasEvidence(s.givesEvidence))
                return false;
            return true;
        }

        // ------------------------------------------------------------------
        // tapping
        // ------------------------------------------------------------------

        private void Update()
        {
            if (!Active || _player == null || _cam == null) return;

            if (Input.GetMouseButtonDown(0) && _player.CanMove) HandleTap(Input.mousePosition);

            // keyboard steering, so the game is testable on a desktop without tapping
            float kx = 0f, ky = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  kx -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) kx += 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    ky += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  ky -= 1f;
            _player.Steer(new Vector2(kx, ky));
            if (kx != 0f || ky != 0f) ClearPending();

            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space)) Interact();

            TrackNearest();
            SortByDepth();
            UpdateMarker();
        }

        private void HandleTap(Vector3 screenPoint)
        {
            // a tap that landed on a button belongs to that button
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Vector2 world = _cam.ScreenToWorldPoint(screenPoint);

            // a tap on or near a spot is an order to go and use it
            WorldSpot hit = null;
            float bestDist = float.MaxValue;
            foreach (var s in _builder.Spots)
            {
                if (s == null || s.Data == null) continue;
                float d = Vector2.Distance(world, s.transform.position);
                // generous: the target is a thing in a room, not a pixel
                if (d > Mathf.Max(s.Data.radius, 1.4f) || d >= bestDist) continue;
                hit = s; bestDist = d;
            }

            if (hit != null)
            {
                Vector2 spot = hit.transform.position;
                Vector2 from = _player.transform.position;

                // already close enough: act now instead of shuffling into position
                if (Vector2.Distance(from, spot) <= StandOff + 0.5f) { Act(hit); return; }

                _pending = hit;
                _player.WalkTo(ApproachPoint(spot, from));
                Sfx.Play(Sfx.Cue.Tap, 0.5f);
                return;
            }

            _pending = null;
            _player.WalkTo(NearestFreePoint(world));
            Sfx.Play(Sfx.Cue.Tap, 0.35f);
        }

        /// <summary>
        /// Somewhere clear to stand next to a spot. The straight-line approach is tried
        /// first, then angles either side of it, because in a narrow aisle the point
        /// directly between the player and a shelf is usually inside that shelf.
        /// </summary>
        private Vector2 ApproachPoint(Vector2 spot, Vector2 from)
        {
            Vector2 away = from - spot;
            if (away.sqrMagnitude < 0.01f) away = Vector2.down;
            away.Normalize();

            float[] turns = { 0f, 35f, -35f, 70f, -70f, 110f, -110f, 150f, -150f, 180f };
            foreach (float deg in turns)
            {
                Vector2 dir = Quaternion.Euler(0f, 0f, deg) * away;
                Vector2 candidate = spot + dir * StandOff;
                if (IsClear(candidate)) return candidate;
            }
            return spot + away * StandOff;   // nothing clear: let the stall detector handle it
        }

        /// <summary>The tapped point, nudged out of scenery if it landed inside some.</summary>
        private Vector2 NearestFreePoint(Vector2 wanted)
        {
            if (IsClear(wanted)) return wanted;

            for (float r = 0.6f; r <= 2.4f; r += 0.6f)
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI * 0.25f;
                    Vector2 c = wanted + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    if (IsClear(c)) return c;
                }
            return wanted;
        }

        /// <summary>
        /// Is there room for the body to stand here? The player's own collider is
        /// skipped, or every point near the detective would read as blocked by him.
        /// </summary>
        private bool IsClear(Vector2 point)
        {
            int n = Physics2D.OverlapCircleNonAlloc(point, 0.42f, _overlapBuffer);
            for (int i = 0; i < n; i++)
            {
                var c = _overlapBuffer[i];
                if (c == null) continue;
                if (_player != null && c.transform.IsChildOf(_player.transform)) continue;
                return false;
            }
            return true;
        }

        private readonly Collider2D[] _overlapBuffer = new Collider2D[8];

        private void OnArrived()
        {
            if (_pending == null) return;
            var target = _pending;
            _pending = null;
            Act(target);
        }

        /// <summary>
        /// Blocked on the way. The errand is dropped rather than retried, so the player
        /// is never left watching the body grind into a shelf.
        /// </summary>
        private void OnStalled()
        {
            ClearPending();
        }

        private void Act(WorldSpot spot)
        {
            _pending = null;
            if (spot == null || spot.Data == null) return;
            if (_onInteract != null) _onInteract(spot.Data);
        }

        private void ClearPending() { _pending = null; }

        /// <summary>Acts on whatever is in reach, for the on-screen action button.</summary>
        public void Interact()
        {
            if (_nearest != null) Act(_nearest);
        }

        // ------------------------------------------------------------------
        // presentation
        // ------------------------------------------------------------------

        private void UpdateMarker()
        {
            if (_marker == null) return;
            bool show = _player.HasTarget;
            if (_marker.gameObject.activeSelf != show) _marker.gameObject.SetActive(show);
            if (!show) return;

            _marker.position = _player.Target;
            // a gentle pulse so it reads as a live order, not a decal
            float k = 0.8f + 0.12f * Mathf.Sin(Time.time * 7f);
            _marker.localScale = new Vector3(k, k * 0.5f, 1f);
        }

        private void TrackNearest()
        {
            Vector2 p = _player.transform.position;
            WorldSpot best = null;
            float bestDist = float.MaxValue;

            foreach (var s in _builder.Spots)
            {
                if (s == null || s.Data == null) continue;
                float d = s.DistanceTo(p);
                s.SetHighlight(Mathf.InverseLerp(s.Data.radius * 2.2f, s.Data.radius, d));
                if (d > s.Data.radius || d >= bestDist) continue;
                best = s; bestDist = d;
            }

            SetNearest(best);
        }

        /// <summary>Reports a change once, and treats a destroyed spot as no spot at all.</summary>
        private void SetNearest(WorldSpot s)
        {
            if (s == null) s = null;                  // collapses a destroyed object to null
            if (_nearest == null && s == null) return;
            if (ReferenceEquals(_nearest, s) && s != null) return;

            _nearest = s;
            if (NearestChanged != null) NearestChanged(s);
        }

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
                r.sortingOrder = r.gameObject.name == "shadow" ? order - 1 : order;
        }

        private void LateUpdate()
        {
            if (!Active) return;
            FollowCamera();
        }

        private void FollowCamera()
        {
            if (_cam == null || _player == null) return;
            Vector3 want = FrameOn(_player.transform.position);
            float k = 1f - Mathf.Exp(-CameraLag * Time.deltaTime);
            _cam.transform.position = Vector3.Lerp(_cam.transform.position, want, k);
        }

        private void SnapCamera()
        {
            if (_cam == null || _player == null) return;
            _cam.transform.position = FrameOn(_player.transform.position);
        }

        /// <summary>
        /// Where the camera wants to be. It stays inside the painted room, but never at
        /// the cost of losing the player: if clamping to the room would push them out of
        /// frame, the player wins and a strip of background shows instead.
        /// </summary>
        private Vector3 FrameOn(Vector3 target)
        {
            var room = _builder != null ? _builder.Room : null;
            if (room == null || _cam == null) return new Vector3(target.x, target.y, -10f);

            float halfH = _cam.orthographicSize;
            float halfW = halfH * _cam.aspect;

            float limitX = room.width * 0.5f - halfW;
            float limitY = room.height * 0.5f - halfH;

            float x = limitX <= 0f ? 0f : Mathf.Clamp(target.x, -limitX, limitX);
            float y = limitY <= 0f ? 0f : Mathf.Clamp(target.y, -limitY, limitY);

            // keep the body inside a margin of the view no matter what the clamp wanted
            float marginX = Mathf.Max(halfW - 1.6f, 0.1f);
            float marginY = Mathf.Max(halfH - 1.8f, 0.1f);
            x = Mathf.Clamp(x, target.x - marginX, target.x + marginX);
            y = Mathf.Clamp(y, target.y - marginY, target.y + marginY);

            return new Vector3(x, y, -10f);
        }
    }
}
