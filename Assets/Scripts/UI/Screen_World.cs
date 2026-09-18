using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TrueDetective.Core;
using TrueDetective.Data;
using TrueDetective.World;

namespace TrueDetective.UI
{
    /// <summary>
    /// The walkable screen. Owns nothing but the overlay: the stick, the interaction
    /// prompt and the same bottom bar every other investigation screen carries.
    ///
    /// The world underneath is a real 2D scene on its own camera; this panel is a hole
    /// punched through the UI so that camera shows through.
    /// </summary>
    public partial class Game
    {
        private RectTransform _worldBody;
        private WorldView _world;
        private TouchStick _stick;
        private MapData _mapData;

        private RectTransform _promptRoot;
        private TextMeshProUGUI _promptLabel;
        private Button _promptButton;

        /// <summary>The room the world currently holds, so a revisit does not rebuild it.</summary>
        private string _builtRoomId;

        /// <summary>True when this case ships a walkable map for its locations.</summary>
        public bool HasWorld { get { return _world != null && _mapData != null; } }

        private void BuildWorld()
        {
            var s = NewScreen("world", new Color(0f, 0f, 0f, 0f));
            _worldBody = UIKit.Node("body", s);

            // the panel background must not paint over the world camera
            var panelImg = s.GetComponent<Image>();
            if (panelImg != null) panelImg.color = new Color(0f, 0f, 0f, 0f);

            _mapData = LoadMap(caseId);
            if (_mapData == null) return;

            var worldGo = new GameObject("world");
            worldGo.transform.SetParent(transform, false);
            _world = worldGo.AddComponent<WorldView>();
            _world.Setup(_mapData, Session, OnWorldInteract);
            _world.NearestChanged += OnNearestChanged;
            _world.SetActive(false);
        }

        private static MapData LoadMap(string id)
        {
            var text = Resources.Load<TextAsset>("Maps/" + id);
            if (text == null) return null;   // a case without a map still plays point-and-click
            try
            {
                var m = JsonUtility.FromJson<MapData>(text.text);
                if (m != null) m.BuildIndex();
                return m;
            }
            catch (System.Exception e)
            {
                Debug.LogError("[World] Maps/" + id + " is not valid JSON: " + e.Message);
                return null;
            }
        }

        private void RefreshWorld()
        {
            Clear(_worldBody);
            if (_world == null)
            {
                // no map for this case: fall back to the painted-scene screen
                Show("location", false);
                return;
            }

            _world.SetActive(true);
            _world.SetInputEnabled(true);

            TopBar(_worldBody, Session.CurrentLocation != null ? Session.CurrentLocation.name : "", null);
            BottomBar(_worldBody);

            var body = Body(_worldBody);

            // ---- interaction prompt, above the stick so a thumb never covers it ----
            _promptRoot = UIKit.Node("prompt", body);
            _promptRoot.anchorMin = new Vector2(0.5f, 0f);
            _promptRoot.anchorMax = new Vector2(0.5f, 0f);
            _promptRoot.pivot = new Vector2(0.5f, 0f);
            _promptRoot.sizeDelta = new Vector2(620f, 130f);
            _promptRoot.anchoredPosition = new Vector2(0f, 40f);

            _promptButton = UIKit.Btn("act", _promptRoot, "", () =>
            {
                if (_world != null) _world.Interact();
            }, Theme.Amber, Theme.Ink, Theme.SizeBody, 130f);
            Destroy(_promptButton.GetComponent<LayoutElement>());
            UIKit.Stretch(_promptButton.GetComponent<RectTransform>());

            _promptLabel = _promptButton.GetComponentInChildren<TextMeshProUGUI>();
            _promptRoot.gameObject.SetActive(false);

            // ---- movement stick, beneath the prompt in the hierarchy ----
            _stick = TouchStick.Create(body, _worldBody.GetComponentInParent<Canvas>(),
                                       UIKit.Ring, UIKit.SoftBlob);
            _stick.transform.SetAsFirstSibling();

            // Coming back from the notebook or an interrogation must not move the player.
            // The room is only rebuilt when it actually changed; otherwise the spots are
            // refreshed in place so a clue just collected disappears without a teleport.
            if (_builtRoomId != Session.CurrentLocationId)
                EnterWorldRoom(Session.CurrentLocationId, null);
            else
            {
                _world.RefreshRoom();
                SetWorldTitle();
            }
        }

        private void SetWorldTitle()
        {
            var bar = _worldBody.Find("topbar");
            if (bar == null) return;
            var title = bar.Find("title");
            if (title == null) return;
            var t = title.GetComponent<TextMeshProUGUI>();
            if (t != null && Session.CurrentLocation != null)
                UIKit.SetText(t, Session.CurrentLocation.name);
        }

        /// <summary>Moves the session and the world together, so neither can lead the other.</summary>
        private void EnterWorldRoom(string locationId, string cameFrom)
        {
            if (_world == null) return;
            if (Session.CurrentLocationId != locationId) Session.TravelTo(locationId);
            _world.EnterRoom(locationId, cameFrom);
            _builtRoomId = locationId;
            SetWorldTitle();
        }

        private void OnNearestChanged(WorldSpot spot)
        {
            if (_promptRoot == null) return;

            if (spot == null || spot.Data == null)
            {
                _promptRoot.gameObject.SetActive(false);
                return;
            }

            string verb =
                !string.IsNullOrEmpty(spot.Data.opensCharacter) ? "تحدّث إلى " :
                !string.IsNullOrEmpty(spot.Data.travelTo)       ? "" :
                !string.IsNullOrEmpty(spot.Data.givesEvidence)  ? "افحص " : "افحص ";

            UIKit.SetText(_promptLabel, verb + spot.Data.label);
            _promptRoot.gameObject.SetActive(true);
        }

        /// <summary>
        /// Everything the player can do from the map routes through the same session
        /// calls the point-and-click screens use, so the two modes cannot disagree about
        /// what has been found.
        /// </summary>
        private void OnWorldInteract(SpotData s)
        {
            if (s == null) return;
            Sfx.Play(Sfx.Cue.Tap);

            if (!string.IsNullOrEmpty(s.travelTo))
            {
                string from = Session.CurrentLocationId;
                EnterWorldRoom(s.travelTo, from);
                return;
            }

            if (!string.IsNullOrEmpty(s.opensCharacter))
            {
                _interrogateCharacterId = s.opensCharacter;
                PauseWorld();
                Show("interrogate");
                return;
            }

            if (!string.IsNullOrEmpty(s.givesEvidence))
            {
                var e = Case.GetEvidence(s.givesEvidence);
                if (Session.CollectEvidence(s.givesEvidence))
                {
                    OpenEvidenceDocument(e, true);
                    _world.RefreshRoom();
                }
                else if (e != null) OpenEvidenceDocument(e, false);
                return;
            }

            OpenNote(s.label, s.flavourText);
        }

        /// <summary>Stops the body and drops any touch when a panel opens over the world.</summary>
        private void PauseWorld()
        {
            if (_world != null) _world.SetInputEnabled(false);
            if (_stick != null) _stick.Release();
        }

        private void Update()
        {
            if (_world == null || !_world.Active) return;

            // the world only takes input while its own screen is the one on top
            bool onTop = _current == "world" && (_docModal == null || !_docModal.gameObject.activeSelf)
                                             && (_hintModal == null || !_hintModal.gameObject.activeSelf);

            _world.SetInputEnabled(onTop);
            if (onTop && _stick != null) _world.SetInput(_stick.Value);
            else if (_stick != null) _stick.Release();
        }
    }
}
