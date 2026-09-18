using UnityEngine;

namespace TrueDetective.World
{
    /// <summary>
    /// The player's body. Moves on a velocity with acceleration rather than snapping to
    /// the stick, because instant start and stop reads as a cursor, not a person.
    ///
    /// The whole feel lives in four numbers - Speed, Accel, Drag and the bob - and they
    /// are the first thing to tune if walking ever feels wrong.
    ///
    /// Facing uses three sprites (front, back, side) with the side flipped for left.
    /// Four painted angles is enough for a top-down camera; the illusion comes from the
    /// bob and lean, not from frame count.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class Walker : MonoBehaviour
    {
        [Header("feel")]
        public float Speed = 5.2f;
        public float Accel = 42f;
        public float Drag = 26f;

        [Header("walk cycle")]
        [Tooltip("Vertical bob height in world units at full speed.")]
        public float BobHeight = 0.075f;
        [Tooltip("Bob cycles per second at full speed.")]
        public float BobRate = 4.4f;
        [Tooltip("Degrees the body leans into its direction of travel.")]
        public float LeanDegrees = 4.5f;

        public Sprite Front, Back, Side;

        private Rigidbody2D _body;
        private SpriteRenderer _art;
        private Transform _artPivot;
        private Transform _shadow;
        private Vector2 _shadowBase = Vector2.one;

        private Vector2 _input;
        private Vector2 _velocity;
        private float _cyclePhase;

        /// <summary>Set false while a dialogue or menu is up.</summary>
        public bool CanMove = true;

        public Vector2 Velocity { get { return _velocity; } }
        public bool IsMoving { get { return _velocity.sqrMagnitude > 0.35f; } }

        /// <summary>Where the character is looking, for line-of-sight work later.</summary>
        public Vector2 Facing { get; private set; }

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _body.gravityScale = 0f;
            _body.freezeRotation = true;
            _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _body.interpolation = RigidbodyInterpolation2D.Interpolate;

            // the art hangs off a pivot so the bob and lean never fight the physics body
            var pivotGo = new GameObject("art");
            _artPivot = pivotGo.transform;
            _artPivot.SetParent(transform, false);

            var artGo = new GameObject("sprite");
            artGo.transform.SetParent(_artPivot, false);
            _art = artGo.AddComponent<SpriteRenderer>();
            _art.sortingLayerName = "Default";

            Facing = Vector2.down;
        }

        /// <summary>A ground shadow, so the body reads as standing on the floor.</summary>
        public void AttachShadow(Sprite blob, float width)
        {
            var go = new GameObject("shadow");
            _shadow = go.transform;
            _shadow.SetParent(transform, false);
            _shadow.localPosition = Vector3.zero;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = blob;
            sr.color = new Color(0f, 0f, 0f, 0.42f);
            sr.sortingOrder = -1;

            if (blob != null && blob.bounds.size.x > 0.0001f)
            {
                float k = width / blob.bounds.size.x;
                _shadowBase = new Vector2(k, k * 0.45f);   // squashed: a floor, not a wall
                _shadow.localScale = new Vector3(_shadowBase.x, _shadowBase.y, 1f);
            }
        }

        /// <summary>Scales the body sprite so it stands the requested height in world units.</summary>
        public void SetHeight(float worldHeight)
        {
            if (_art.sprite == null) return;
            float h = _art.sprite.bounds.size.y;
            if (h < 0.0001f) return;
            float k = worldHeight / h;
            _artPivot.localScale = new Vector3(k, k, 1f);
            // feet at the transform origin, body above it
            _art.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);
        }

        /// <summary>Called every frame by whatever is driving this body.</summary>
        public void SetInput(Vector2 direction)
        {
            _input = direction.sqrMagnitude > 1f ? direction.normalized : direction;
        }

        private void FixedUpdate()
        {
            Vector2 target = CanMove ? _input * Speed : Vector2.zero;

            // accelerate toward the target, and fall back faster than we speed up so
            // letting go of the stick stops crisply without the start feeling twitchy
            float rate = target.sqrMagnitude > 0.01f ? Accel : Drag;
            _velocity = Vector2.MoveTowards(_velocity, target, rate * Time.fixedDeltaTime);

            _body.linearVelocity = _velocity;
        }

        private void Update()
        {
            float speed01 = Mathf.Clamp01(_velocity.magnitude / Mathf.Max(Speed, 0.01f));

            if (_velocity.sqrMagnitude > 0.35f)
            {
                Facing = _velocity.normalized;
                _cyclePhase += Time.deltaTime * BobRate * speed01;
            }
            else
            {
                // settle the cycle to a rest pose instead of freezing mid-step
                _cyclePhase = Mathf.MoveTowards(_cyclePhase % 1f, 0f, Time.deltaTime * 3f);
            }

            ApplyFacing();
            ApplyWalkCycle(speed01);
        }

        private void ApplyFacing()
        {
            // a clear sideways push wins over the vertical one, so walking diagonally
            // shows the side view rather than flickering between two sprites
            bool sideways = Mathf.Abs(Facing.x) > 0.45f;

            if (sideways && Side != null)
            {
                _art.sprite = Side;
                var s = _artPivot.localScale;
                _artPivot.localScale = new Vector3(Mathf.Abs(s.x) * (Facing.x < 0f ? -1f : 1f), s.y, s.z);
            }
            else
            {
                _art.sprite = Facing.y > 0f && Back != null ? Back : Front;
                var s = _artPivot.localScale;
                _artPivot.localScale = new Vector3(Mathf.Abs(s.x), s.y, s.z);
            }
        }

        private void ApplyWalkCycle(float speed01)
        {
            // two bobs per stride: the body rises on each footfall
            float bob = Mathf.Abs(Mathf.Sin(_cyclePhase * Mathf.PI * 2f)) * BobHeight * speed01;

            // and squashes very slightly as it lands, which is what sells the weight
            float squash = 1f - bob * 0.45f;

            var p = _art.transform.localPosition;
            _art.transform.localPosition = new Vector3(p.x, Mathf.Abs(p.y) + bob, p.z);
            _art.transform.localScale = new Vector3(1f, squash, 1f);

            float lean = -Facing.x * LeanDegrees * speed01;
            _artPivot.localRotation = Quaternion.Euler(0f, 0f, lean);

            if (_shadow != null)
            {
                // the shadow shrinks as the body lifts. Scaled from the stored base each
                // frame, never from its current value, or the shrink compounds away.
                float k = Mathf.Max(1f - bob * 1.6f, 0.72f);
                _shadow.localScale = new Vector3(_shadowBase.x * k, _shadowBase.y * k, 1f);
            }
        }

        /// <summary>Drops the body at a point and kills any momentum.</summary>
        public void Teleport(Vector2 position)
        {
            _velocity = Vector2.zero;
            _body.linearVelocity = Vector2.zero;
            _body.position = position;
            transform.position = position;
        }
    }
}
