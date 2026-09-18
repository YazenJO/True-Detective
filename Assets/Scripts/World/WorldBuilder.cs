using System.Collections.Generic;
using UnityEngine;
using TrueDetective.Data;
using TrueDetective.UI;

namespace TrueDetective.World
{
    /// <summary>
    /// Assembles one room from its RoomData: the painted floor, invisible colliders for
    /// anything solid, the props and people standing in it, and a wall of colliders round
    /// the edge so the player cannot walk off the art.
    ///
    /// Rebuilt from scratch on every room change. Rooms are small and this keeps the
    /// world stateless - there is no partially-torn-down room to reason about.
    /// </summary>
    public class WorldBuilder : MonoBehaviour
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private Transform _root;

        public RoomData Room { get; private set; }

        /// <summary>Live spots in the current room, for the interaction prompt to scan.</summary>
        public readonly List<WorldSpot> Spots = new List<WorldSpot>();

        private void EnsureRoot()
        {
            if (_root != null) return;
            var go = new GameObject("room");
            go.transform.SetParent(transform, false);
            _root = go.transform;
        }

        public void Clear()
        {
            foreach (var go in _spawned) if (go != null) Destroy(go);
            _spawned.Clear();
            Spots.Clear();
            Room = null;
        }

        /// <summary>
        /// Builds the room. <paramref name="isVisible"/> decides whether each spot should
        /// appear at all, so a locked door or an ungathered clue can be filtered by the
        /// same rules the point-and-click screens use.
        /// </summary>
        public void Build(RoomData room, System.Func<SpotData, bool> isVisible)
        {
            Clear();
            EnsureRoot();
            Room = room;
            if (room == null) return;

            BuildFloor(room);
            BuildBlockers(room);
            BuildBounds(room);
            BuildSpots(room, isVisible);
        }

        private void BuildFloor(RoomData room)
        {
            var sprite = Art.Load("Maps/", room.map);
            if (sprite == null)
            {
                Debug.LogWarning("[World] no map art at Resources/Maps/" + room.map);
                return;
            }

            var go = new GameObject("floor");
            go.transform.SetParent(_root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = -100;          // always beneath everything that walks

            if (!string.IsNullOrEmpty(room.tint))
            {
                Color c;
                if (TryParseColor(room.tint, out c)) sr.color = c;
            }

            // stretch the painted room onto the declared world size
            var b = sprite.bounds.size;
            if (b.x > 0.0001f && b.y > 0.0001f)
                go.transform.localScale = new Vector3(room.width / b.x, room.height / b.y, 1f);

            _spawned.Add(go);
        }

        private void BuildBlockers(RoomData room)
        {
            if (room.blockers == null) return;
            foreach (var b in room.blockers)
            {
                if (b == null) continue;
                var go = new GameObject("blocker");
                go.transform.SetParent(_root, false);
                go.transform.position = new Vector3(b.x, b.y, 0f);

                var col = go.AddComponent<BoxCollider2D>();
                col.size = new Vector2(Mathf.Max(b.w, 0.05f), Mathf.Max(b.h, 0.05f));

                _spawned.Add(go);
            }
        }

        /// <summary>Four slabs around the room so the player never leaves the painted area.</summary>
        private void BuildBounds(RoomData room)
        {
            float hw = room.width * 0.5f;
            float hh = room.height * 0.5f;
            const float t = 2f;   // thick, so fast movement cannot tunnel through

            AddWall(new Vector2(0f, hh + t * 0.5f), new Vector2(room.width + t * 2f, t));
            AddWall(new Vector2(0f, -hh - t * 0.5f), new Vector2(room.width + t * 2f, t));
            AddWall(new Vector2(-hw - t * 0.5f, 0f), new Vector2(t, room.height + t * 2f));
            AddWall(new Vector2(hw + t * 0.5f, 0f), new Vector2(t, room.height + t * 2f));
        }

        private void AddWall(Vector2 centre, Vector2 size)
        {
            var go = new GameObject("bound");
            go.transform.SetParent(_root, false);
            go.transform.position = centre;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = size;
            _spawned.Add(go);
        }

        private void BuildSpots(RoomData room, System.Func<SpotData, bool> isVisible)
        {
            if (room.spots == null) return;

            foreach (var s in room.spots)
            {
                if (s == null) continue;
                if (isVisible != null && !isVisible(s)) continue;

                var go = new GameObject("spot_" + s.id);
                go.transform.SetParent(_root, false);
                Vector2 at = s.at != null ? new Vector2(s.at.x, s.at.y) : Vector2.zero;
                go.transform.position = at;

                var spot = go.AddComponent<WorldSpot>();
                spot.Data = s;
                Spots.Add(spot);

                var art = Art.Load("Maps/", s.sprite);
                if (art != null)
                {
                    var artGo = new GameObject("art");
                    artGo.transform.SetParent(go.transform, false);
                    var sr = artGo.AddComponent<SpriteRenderer>();
                    sr.sprite = art;

                    float h = art.bounds.size.y;
                    if (h > 0.0001f)
                    {
                        float k = s.spriteHeight / h;
                        artGo.transform.localScale = new Vector3(k, k, 1f);
                        // feet at the spot, body above, like the player
                        artGo.transform.localPosition = new Vector3(0f, s.spriteHeight * 0.5f, 0f);
                    }

                    spot.Art = sr;

                    // a standing person blocks the way; a clue on a table does not
                    if (!string.IsNullOrEmpty(s.opensCharacter))
                    {
                        var col = go.AddComponent<CapsuleCollider2D>();
                        col.size = new Vector2(0.9f, 0.7f);
                        col.direction = CapsuleDirection2D.Horizontal;
                    }

                    AddShadow(go.transform, s.spriteHeight * 0.42f);
                }

                _spawned.Add(go);
            }
        }

        private void AddShadow(Transform parent, float width)
        {
            var blob = UIKit.SoftBlob;
            if (blob == null) return;

            var go = new GameObject("shadow");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = blob;
            sr.color = new Color(0f, 0f, 0f, 0.40f);
            sr.sortingOrder = -1;

            float bw = blob.bounds.size.x;
            if (bw > 0.0001f)
            {
                float k = width / bw;
                go.transform.localScale = new Vector3(k, k * 0.45f, 1f);
            }
        }

        private static bool TryParseColor(string csv, out Color c)
        {
            c = Color.white;
            if (string.IsNullOrEmpty(csv)) return false;
            var parts = csv.Split(',');
            if (parts.Length < 3) return false;
            float r, g, b;
            if (!float.TryParse(parts[0], out r)) return false;
            if (!float.TryParse(parts[1], out g)) return false;
            if (!float.TryParse(parts[2], out b)) return false;
            c = new Color(r, g, b, 1f);
            return true;
        }
    }

    /// <summary>A thing in the room the player can walk up to.</summary>
    public class WorldSpot : MonoBehaviour
    {
        public SpotData Data;
        public SpriteRenderer Art;

        private float _glow;

        /// <summary>Distance from a point, measured on the floor.</summary>
        public float DistanceTo(Vector2 p)
        {
            return Vector2.Distance(p, transform.position);
        }

        public bool InRange(Vector2 p)
        {
            return DistanceTo(p) <= (Data != null ? Data.radius : 1.6f);
        }

        /// <summary>
        /// Lifts the sprite toward white as the player closes in. A highlight that fades
        /// with distance tells the player they are getting warm without any UI at all.
        /// </summary>
        public void SetHighlight(float amount)
        {
            _glow = Mathf.MoveTowards(_glow, Mathf.Clamp01(amount), Time.deltaTime * 6f);
            if (Art != null)
                Art.color = Color.Lerp(Color.white, new Color(1.35f, 1.28f, 1.05f, 1f), _glow);
        }
    }
}
