using UnityEngine;

namespace TrueDetective.World
{
    /// <summary>
    /// The player's body. Walks to a destination rather than following a held stick:
    /// the player taps a point, the body goes there and stops.
    ///
    /// That choice removes whole classes of bug. There is no held input to lose track
    /// of, so the body can never be left walking or left frozen by a missed release,
    /// and a tap that cannot be reached simply stalls out and clears itself.
    ///
    /// The feel lives in Speed, Accel, Drag and the bob; those are the first things to
    /// change if walking ever reads wrong.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class Walker : MonoBehaviour
    {
        [Header("feel")]
        public float Speed = 5.2f;
        public float Accel = 40f;
        public float Drag = 30f;

        [Header("arrival")]
        [Tooltip("Stop once this close to the destination.")]
        public float ArriveRadius = 0.28f;
        [Tooltip("Start easing down this far out, so the stop is not a hard snap.")]
        public float SlowRadius = 1.5f;

        [Header("stall")]
        [Tooltip("Give up if blocked and barely moving for this long.")]
        public float StallTime = 0.35f;
        [Tooltip("Movement per second below this counts as blocked.")]
        public float StallSpeed = 0.55f;

        [Header("walk cycle")]
        public float BobHeight = 0.075f;
        public float BobRate = 4.4f;
        public float LeanDegrees = 4.5f;

        public Sprite Front, Back, Side;

        private Rigidbody2D _body;
        private SpriteRenderer _art;
        private Transform _artPivot;
        private Transform _shadow;
        private Vector2 _shadowBase = Vector2.one;
        private float _artRestY;

        private Vector2 _velocity;
        private float _cyclePhase;

        private bool _hasTarget;
        private Vector2 _target;
        private Vector2 _steer;
        private float _stallTimer;
        private Vector2 _lastPosition;

        /// <summary>Set false while a panel is open over the world.</summary>
        public bool CanMove = true;

        public bool IsMoving { get { return _velocity.sqrMagnitude > 0.35f; } }
        public bool HasTarget { get { return _hasTarget; } }
        public Vector2 Target { get { return _target; } }
        public Vector2 Facing { get; private set; }

        /// <summary>Raised when the body reaches its destination under its own power.</summary>
        public event System.Action Arrived;
        /// <summary>Raised when the body gave up because something was in the way.</summary>
        public event System.Action Stalled;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _body.gravityScale = 0f;
            _body.freezeRotation = true;
            _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _body.interpolation = RigidbodyInterpolation2D.Interpolate;

            var pivotGo = new GameObject("art");
            _artPivot = pivotGo.transform;
            _artPivot.SetParent(transform, false);

            var artGo = new GameObject("sprite");
            artGo.transform.SetParent(_artPivot, false);
            _art = artGo.AddComponent<SpriteRenderer>();

            Facing = Vector2.down;
            _lastPosition = transform.position;
        }

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
                _shadowBase = new Vector2(k, k * 0.45f);
                _shadow.localScale = new Vector3(_shadowBase.x, _shadowBase.y, 1f);
            }
        }

        public void SetHeight(float worldHeight)
        {
            if (_art.sprite == null) return;
            float h = _art.sprite.bounds.size.y;
            if (h < 0.0001f) return;
            float k = worldHeight / h;
            _artPivot.localScale = new Vector3(k, k, 1f);
            _artRestY = h * 0.5f;                       // feet at the origin, body above
            _art.transform.localPosition = new Vector3(0f, _artRestY, 0f);
        }

        // ------------------------------------------------------------------
        // orders
        // ------------------------------------------------------------------

        /// <summary>Walk to a point. Replaces any previous destination.</summary>
        public void WalkTo(Vector2 worldPoint)
        {
            _target = worldPoint;
            _hasTarget = true;
            _stallTimer = 0f;
            _lastPosition = transform.position;
        }

        /// <summary>
        /// Direct steering, for a keyboard. Any held direction takes over from the
        /// current destination, so the two control schemes cannot fight each other.
        /// </summary>
        public void Steer(Vector2 direction)
        {
            _steer = direction.sqrMagnitude > 1f ? direction.normalized : direction;
            if (_steer.sqrMagnitude > 0.01f && _hasTarget) _hasTarget = false;
        }

        /// <summary>Stop where we are and forget the destination.</summary>
        public void Halt()
        {
            _hasTarget = false;
            _stallTimer = 0f;
            _velocity = Vector2.zero;
            if (_body != null) _body.linearVelocity = Vector2.zero;
        }

        public void Teleport(Vector2 position)
        {
            Halt();
            _body.position = position;
            transform.position = position;
            _lastPosition = position;
        }

        // ------------------------------------------------------------------
        // movement
        // ------------------------------------------------------------------

        private void FixedUpdate()
        {
            Vector2 want = Vector2.zero;

            if (CanMove && _steer.sqrMagnitude > 0.01f)
            {
                want = _steer * Speed;
                _stallTimer = 0f;
                _lastPosition = _body.position;
            }
            else if (_hasTarget && CanMove)
            {
                Vector2 here = _body.position;
                Vector2 toTarget = _target - here;
                float dist = toTarget.magnitude;

                if (dist <= ArriveRadius)
                {
                    _hasTarget = false;
                    _velocity = Vector2.zero;
                    _body.linearVelocity = Vector2.zero;
                    if (Arrived != null) Arrived();
                }
                else
                {
                    // ease down over the last stretch so arrival is a stop, not a stab
                    float throttle = Mathf.Clamp01(dist / Mathf.Max(SlowRadius, 0.01f));
                    throttle = Mathf.Max(throttle, 0.35f);
                    want = toTarget / dist * Speed * throttle;

                    // if the body is being asked to move but is not actually getting
                    // anywhere, something solid is in the way: give up rather than
                    // grinding against it forever
                    float moved = Vector2.Distance(here, _lastPosition) / Time.fixedDeltaTime;
                    if (moved < StallSpeed)
                    {
                        _stallTimer += Time.fixedDeltaTime;
                        if (_stallTimer >= StallTime)
                        {
                            _hasTarget = false;
                            _stallTimer = 0f;
                            if (Stalled != null) Stalled();
                        }
                    }
                    else _stallTimer = 0f;
                }

                _lastPosition = here;
            }

            float rate = want.sqrMagnitude > 0.01f ? Accel : Drag;
            _velocity = Vector2.MoveTowards(_velocity, want, rate * Time.fixedDeltaTime);
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
                _cyclePhase = Mathf.MoveTowards(_cyclePhase % 1f, 0f, Time.deltaTime * 3f);
            }

            ApplyFacing();
            ApplyWalkCycle(speed01);
        }

        private void ApplyFacing()
        {
            bool sideways = Mathf.Abs(Facing.x) > 0.45f;
            float scaleX = Mathf.Abs(_artPivot.localScale.x);

            if (sideways && Side != null)
            {
                _art.sprite = Side;
                _artPivot.localScale = new Vector3(scaleX * (Facing.x < 0f ? -1f : 1f),
                                                   _artPivot.localScale.y, 1f);
            }
            else
            {
                _art.sprite = Facing.y > 0f && Back != null ? Back : Front;
                _artPivot.localScale = new Vector3(scaleX, _artPivot.localScale.y, 1f);
            }
        }

        private void ApplyWalkCycle(float speed01)
        {
            // two bobs per stride: the body rises on each footfall
            float bob = Mathf.Abs(Mathf.Sin(_cyclePhase * Mathf.PI * 2f)) * BobHeight * speed01;

            // measured from the stored rest height, never from the current value, or the
            // offset accumulates and the body drifts up the screen
            _art.transform.localPosition = new Vector3(0f, _artRestY + bob, 0f);
            _art.transform.localScale = new Vector3(1f, 1f - bob * 0.45f, 1f);

            _artPivot.localRotation = Quaternion.Euler(0f, 0f, -Facing.x * LeanDegrees * speed01);

            if (_shadow != null)
            {
                float k = Mathf.Max(1f - bob * 1.6f, 0.72f);
                _shadow.localScale = new Vector3(_shadowBase.x * k, _shadowBase.y * k, 1f);
            }
        }
    }
}
