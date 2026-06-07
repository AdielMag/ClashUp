#nullable enable
using UnityEngine;
using AetherNet.Collision;
using AetherNet.Queries;

namespace AetherNet
{
    /// <summary>
    /// Bridge between the AetherNet simulation and the Unity scene.
    /// One instance per scene — attach to a persistent manager GameObject.
    /// </summary>
    [AddComponentMenu("AetherNet/View Manager")]
    [DisallowMultipleComponent]
    public sealed class AetherViewManager : MonoBehaviour, IFullCollisionSink
    {
        [SerializeField] private Vector2 _gravity       = new Vector2(0f, -9.81f);
        [SerializeField] private bool    _allowSleeping = true;
        [SerializeField] private int     _maxBodies     = SimulationConstants.MaxBodies;

        private PhysicsWorldManager          _physicsWorld;
        private AetherRigidbody[]            _bodies;
        private IAetherCollisionHandler?[]   _collisionHandlers;
        private IAetherTriggerHandler?[]     _triggerHandlers;
        private int                          _bodyCount;
        private PhysicsQueryBuffer           _queryBuffer;

        public static AetherViewManager? Instance { get; private set; }
        public PhysicsWorldManager Physics => _physicsWorld;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[AetherNet] Duplicate AetherViewManager destroyed.");
                Destroy(gameObject);
                return;
            }
            Instance = this;

            var config = new WorldConfig
            {
                Gravity       = MathBridge.ToNumerics(_gravity),
                AllowSleeping = _allowSleeping,
                MaxBodies     = _maxBodies,
            };
            _physicsWorld      = new PhysicsWorldManager(in config);
            _bodies            = new AetherRigidbody[_maxBodies];
            _collisionHandlers = new IAetherCollisionHandler?[_maxBodies];
            _triggerHandlers   = new IAetherTriggerHandler?[_maxBodies];
            _queryBuffer       = new PhysicsQueryBuffer();

            Time.fixedDeltaTime = SimulationConstants.FixedTimestep;
            AetherPhysicsQueries.Initialize(_physicsWorld, _queryBuffer);
            RegisterSceneBodies();
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void RegisterSceneBodies()
        {
#if UNITY_2022_1_OR_NEWER
            var rigidbodies = FindObjectsByType<AetherRigidbody>(FindObjectsSortMode.None);
#else
            var rigidbodies = FindObjectsOfType<AetherRigidbody>();
#endif
            for (int i = 0; i < rigidbodies.Length; i++)
                RegisterBody(rigidbodies[i], i);
        }

        private void RegisterBody(AetherRigidbody rb, int entityId)
        {
            if (_bodyCount >= _maxBodies)
            {
                Debug.LogError($"[AetherNet] MaxBodies ({_maxBodies}) exceeded. '{rb.name}' not registered.");
                return;
            }
            var def = new BodyDef
            {
                BodyType       = rb.BodyType,
                Position       = MathBridge.WorldToSim(rb.transform.position),
                Angle          = MathExtensions.ToSimAngle(rb.transform.eulerAngles.z),
                LinearDamping  = rb.LinearDamping,
                AngularDamping = rb.AngularDamping,
                GravityScale   = rb.GravityScale,
                FixedRotation  = rb.FixedRotation,
                Constraints    = rb.Constraints,
            };
            var body = _physicsWorld.CreateBody(in def, entityId);

            var colliders = rb.GetComponents<IAetherColliderProvider>();
            for (int c = 0; c < colliders.Length; c++)
                colliders[c].AttachToBody(body, _physicsWorld);

            rb.Register(_physicsWorld, entityId);
            var state = _physicsWorld.GetBodyState(entityId);
            rb.SnapshotPreTick(in state.Position, state.Angle);
            rb.SnapshotPreTick(in state.Position, state.Angle);

            _bodies[entityId]            = rb;
            _collisionHandlers[entityId] = rb.GetComponent<IAetherCollisionHandler>();
            _triggerHandlers[entityId]   = rb.GetComponent<IAetherTriggerHandler>();
            _bodyCount++;
        }

        private void FixedUpdate()
        {
            for (int i = 0; i < _maxBodies; i++)
            {
                var rb = _bodies[i];
                if (rb == null) continue;
                var s = _physicsWorld.GetBodyState(i);
                rb.SnapshotPreTick(in s.Position, s.Angle);
            }
            _physicsWorld.Advance(Time.fixedDeltaTime);
            _physicsWorld.Events.DrainAll(this);
        }

        private void LateUpdate()
        {
            float alpha = _physicsWorld.InterpolationAlpha;
            for (int i = 0; i < _maxBodies; i++)
            {
                var rb = _bodies[i];
                if (rb != null) rb.ApplyInterpolatedTransform(alpha);
            }
        }

        public int RegisterDynamic(AetherRigidbody rb)
        {
            for (int i = 0; i < _maxBodies; i++)
                if (_bodies[i] == null) { RegisterBody(rb, i); return i; }
            return -1;
        }

        public void Unregister(int entityId)
        {
            if ((uint)entityId >= (uint)_maxBodies) return;
            _physicsWorld.DestroyBody(entityId);
            _bodies[entityId]            = null;
            _collisionHandlers[entityId] = null;
            _triggerHandlers[entityId]   = null;
        }

        void ICollisionEnterSink.OnCollisionEnter(ref CollisionData d)
        {
            _collisionHandlers[d.EntityIdA]?.OnCollisionEnter(ref d);
            _collisionHandlers[d.EntityIdB]?.OnCollisionEnter(ref d);
        }
        void ICollisionExitSink.OnCollisionExit(ref CollisionData d)
        {
            _collisionHandlers[d.EntityIdA]?.OnCollisionExit(ref d);
            _collisionHandlers[d.EntityIdB]?.OnCollisionExit(ref d);
        }
        void ITriggerEnterSink.OnTriggerEnter(ref TriggerData d)
        {
            _triggerHandlers[d.TriggerEntityId]?.OnTriggerEnter(ref d);
            _triggerHandlers[d.OtherEntityId]?.OnTriggerEnter(ref d);
        }
        void ITriggerExitSink.OnTriggerExit(ref TriggerData d)
        {
            _triggerHandlers[d.TriggerEntityId]?.OnTriggerExit(ref d);
            _triggerHandlers[d.OtherEntityId]?.OnTriggerExit(ref d);
        }
    }
}
