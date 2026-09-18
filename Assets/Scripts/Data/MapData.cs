using System;
using System.Collections.Generic;

namespace TrueDetective.Data
{
    /// <summary>
    /// The walkable world for one case: rooms, what blocks the player, where things are
    /// that can be interacted with, and how the rooms connect.
    ///
    /// Kept separate from CaseData on purpose. A case is a story; a map is a place. The
    /// same place can host more than one case, and a case can be authored before its map
    /// exists - the point-and-click screens still work without any of this.
    ///
    /// All coordinates are in world units with the origin at the centre of the room and
    /// +Y pointing up the screen, which is also how the room art is centred.
    /// </summary>
    [Serializable]
    public class MapData
    {
        public string caseId;
        public RoomData[] rooms;

        [NonSerialized] private Dictionary<string, RoomData> _rooms;

        public void BuildIndex()
        {
            _rooms = new Dictionary<string, RoomData>();
            if (rooms == null) return;
            foreach (var r in rooms)
                if (r != null && !string.IsNullOrEmpty(r.id)) _rooms[r.id] = r;
        }

        public RoomData GetRoom(string id)
        {
            RoomData v;
            return _rooms != null && _rooms.TryGetValue(id ?? "", out v) ? v : null;
        }
    }

    [Serializable]
    public class RoomData
    {
        /// <summary>Matches a Location id in the case file, so the two line up.</summary>
        public string id;
        public string name;

        /// <summary>Sprite in Resources/Maps, painted top-down.</summary>
        public string map;

        /// <summary>Size of the painted room in world units. The art is stretched to this.</summary>
        public float width = 20f;
        public float height = 20f;

        /// <summary>Where the player appears when there is no door to arrive through.</summary>
        public Vec2 spawn;

        /// <summary>Ambient tint for this room, as "r,g,b" 0-1. Empty means no tint.</summary>
        public string tint;

        public BlockerData[] blockers;
        public SpotData[] spots;
    }

    /// <summary>A rectangle the player cannot walk through: walls, shelves, desks.</summary>
    [Serializable]
    public class BlockerData
    {
        public float x, y, w, h;
    }

    /// <summary>
    /// Something in the room worth walking up to. Exactly one of the action fields is
    /// used; they mirror Hotspot in the case file so the same interactions are reachable
    /// from either the map or the old point-and-click screen.
    /// </summary>
    [Serializable]
    public class SpotData
    {
        public string id;
        public string label;

        public Vec2 at;
        /// <summary>How close the player has to be before the prompt appears.</summary>
        public float radius = 1.6f;

        /// <summary>Sprite in Resources/Maps for a person or object standing here.</summary>
        public string sprite;
        /// <summary>World height of that sprite. Width follows its aspect ratio.</summary>
        public float spriteHeight = 2.4f;

        /// <summary>Id of the matching Hotspot in the case file. Everything else is optional.</summary>
        public string hotspotId;

        public string givesEvidence;
        public string opensCharacter;
        public string travelTo;
        public string flavourText;

        /// <summary>Evidence or action id required before this appears at all.</summary>
        public string requires;
    }

    /// <summary>Plain 2D point. Not UnityEngine.Vector2, to keep this file engine-free.</summary>
    [Serializable]
    public class Vec2
    {
        public float x, y;

        public Vec2() { }
        public Vec2(float x, float y) { this.x = x; this.y = y; }
    }
}
