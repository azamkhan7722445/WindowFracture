using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Spatial.Euclidean;
using Sirenix.OdinInspector;
using UnityEngine;
using static GlassSystem.Scripts.MathNetUtils;

namespace GlassSystem.Scripts
{
    public class GlassPanel : BaseGlass
    {
        protected List<Shard> _shards;

        [TitleGroup("Audio"), SerializeField] private AudioClip breakSound;
        [TitleGroup("Audio"), SerializeField, Min(0f)] private float normalBreakSoundVolume = 1f;
        [TitleGroup("Audio"), SerializeField, Range(0f, 1f)] private float normalBreakSoundSpatialBlend = 0f;
        [TitleGroup("Audio"), SerializeField] private AudioClip shatterSound;
        [TitleGroup("Audio"), SerializeField, Min(0f)] private float shatterSoundVolume = 1f;
        [TitleGroup("Audio"), SerializeField, Min(1)] private int shatterSoundRepeats = 3;
        [TitleGroup("Audio"), SerializeField, Min(0f)] private float shatterSoundRepeatDelay = 0.08f;
        [TitleGroup("Audio"), SerializeField, Range(0f, 1f)] private float shatterSoundSpatialBlend = 0f;

        [TitleGroup("Input Damage"), SerializeField, Range(0f, 1f)]
        [Tooltip("Damage radius as a ratio of the device-height-scaled gameplay area.")]
        private float inputDamageRadius = 0.35f;

        [TitleGroup("Camera Fit"), SerializeField]
        private Camera targetCamera;

        [TitleGroup("Camera Fit"), SerializeField]
        private float distanceFromCamera = 5f;

        [TitleGroup("Device Height Scale"), SerializeField]
        private bool scaleToDeviceHeightOnStart = true;

        [TitleGroup("Device Height Scale"), SerializeField, Range(0.1f, 1.5f)]
        private float deviceHeightFillPercent = 1f;

        [TitleGroup("Game`play View"), SerializeField]
        private bool limitGameplayToCameraView = true;

        [TitleGroup("Gameplay View"), SerializeField, Range(-0.25f, 0.25f)]
        private float gameplayViewportMargin = 0.02f;

        [TitleGroup("Level Shatter"), SerializeField, Min(0f)]
        private float levelShatterCameraForce = 0.15f;

        private int _totalShardCount;
        private int _brokenShardCount;
        private bool _canBreak = true;

        public AudioClip BreakSound => breakSound;
        public bool CanShardBreakFromInput => _canBreak;
        public bool IsBroken => _shards != null;
        public bool HasInputDamageRadius => inputDamageRadius > 0f;

        public event Action<float> OnRemainingHealthUpdated;

        public float BreakPercentage
        {
            get
            {
                if (_totalShardCount <= 0)
                    return 0f;

                return (_brokenShardCount / (float)_totalShardCount) * 100f;
            }
        }

        [TitleGroup("Debug"), ShowInInspector, ReadOnly]
        public float HealthPercentage => 100f - BreakPercentage;
        
        protected void Start()
        {
            _parentPanel = this;

            if (scaleToDeviceHeightOnStart)
                ScaleToDeviceHeight();
        }

        [Button]
        public void ScaleToDeviceHeight()
        {
            Camera camera = GetTargetCamera();
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();

            if (camera == null || meshRenderer == null)
                return;

            float currentWorldHeight = meshRenderer.bounds.size.y;
            if (currentWorldHeight <= 0f)
                return;

            Vector3 localScale = transform.localScale;
            float scaleMultiplier = GetGameplayWorldHeight(camera) / currentWorldHeight;
            float uniformLocalScale = localScale.y * scaleMultiplier;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.Undo.RecordObject(transform, "Scale Glass Panel To Device Height");
#endif

            transform.localScale = new Vector3(uniformLocalScale, uniformLocalScale, localScale.z);

#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorUtility.SetDirty(transform);
#endif
        }

        public void SetCanBreak(bool state)
        {
            _canBreak = state;
        }

        /// <summary>
        /// Build polygon initialize a glass panel about to be broken for the first time,
        /// it is not used by shards which receives their data from InitializeShard instead.
        /// </summary>
        /// <param name="side">z position of the impact, used to discard the back face before building the polygon</param>
        /// <returns>2D polygon representing the glass panel</returns>
        protected override Polygon2D BuildPolygon(float side)
        {
            var targetMeshFilter = GetComponent<MeshFilter>();
            if (targetMeshFilter == null)
                return null;

            var targetMesh = targetMeshFilter.sharedMesh;
            var targetVertices = targetMesh.vertices;

            if (targetVertices.Length is > 100 or < 3)
            {
                Debug.LogWarning($"Invalid mesh ({targetVertices.Length})");
                return null;
            }

            var scale = _transform.lossyScale;
            var scalingMatrix = new DiagonalMatrix(2, 2, new double[] { scale.x, scale.y });

            var verticesZ = targetVertices.Select(p => p.z).ToList();
            _thickness = (verticesZ.Max() + Mathf.Abs(verticesZ.Min())) * scale.z;

            var targetPoints = targetVertices.Select((p, i) => new IndexedPoint(p, i)).ToList();
            targetPoints.RemoveAll(p => Mathf.Abs(p.Z - side) > Tolerance);
            targetPoints = targetPoints.Distinct(new Point2DComparer(Tolerance)).ToList();

            foreach (var point in targetPoints)
                point.TransformBy(scalingMatrix);

            targetPoints.Sort((a, b) => CompareVectorAngle(new Point2D(0, 0), a, b));

            Polygon2D targetPolygon = new Polygon2D(targetPoints.Select(p => p.Point2D));

            var uvs = targetMesh.uv;
            if (uvs != null && uvs.Length > 0)
            {
                _uvs = new Vector2[targetPoints.Count];

                for (int i = 0; i < targetPoints.Count; i++)
                    _uvs[i] = uvs[targetPoints[i].Index];
            }

            return targetPolygon;
        }

        public override void Break(
            Vector3 breakPosition,
            Vector3 originVector,
            int patternIndex = -1,
            float rotation = float.NaN
        )
        {
            if (!_canBreak)
                return;

            if (_shards is not null)
            {
                Debug.LogError("GlassPanel broken twice");
                return;
            }

            _shards = new List<Shard>();
            _totalShardCount = 0;
            _brokenShardCount = 0;
            _canBreak = true;

            VibrationsHandler.GlassBreak();

            PlayBreakSound(transform.position);

            base.Break(breakPosition, originVector, patternIndex, rotation);

            BreakShardsInInputRadius(null, breakPosition, originVector, patternIndex, rotation, false);

            Destroy(GetComponent<MeshFilter>());
            Destroy(GetComponent<MeshRenderer>());
            Destroy(GetComponent<Collider>());

            NotifyRemainingHealthUpdated();
        }

        public void OnShardDestroyed(Shard shard)
        {
            if (_shards == null)
                return;

            if (_shards.Remove(shard))
            {
                _brokenShardCount++;
                TriggerShardBreakHaptic();
                NotifyRemainingHealthUpdated();
            }
        }

        public void OnNewShard(Shard shard)
        {
            if (_shards == null)
                _shards = new List<Shard>();

            if (limitGameplayToCameraView && !IsShardInGameplayView(shard))
            {
                Destroy(shard.gameObject);
                return;
            }

            _shards.Add(shard);
            _totalShardCount++;
        }

        public void OnNewSHard(Shard shard)
        {
            OnNewShard(shard);
        }

        public Vector3[] GetRemainingShardPositions()
        {
            if (_shards == null || _shards.Count == 0)
                return Array.Empty<Vector3>();

            return _shards
                .Where(shard => shard != null)
                .Select(shard => shard.transform.position)
                .ToArray();
        }

        public void BreakShardsInInputRadius(
            Shard sourceShard,
            Vector3 breakPosition,
            Vector3 originVector,
            int patternIndex = -1,
            float rotation = float.NaN,
            bool playFirstSound = true
        )
        {
            if (_shards == null || _shards.Count == 0)
                return;

            float radius = GetInputDamageRadiusWorld();
            Shard[] targets = _shards
                .Where(shard => shard != null
                                && (shard == sourceShard ||
                                    (radius > 0f &&
                                     Vector3.Distance(shard.transform.position, breakPosition) <= radius)))
                .OrderBy(shard => shard == sourceShard ? -1f : Vector3.SqrMagnitude(shard.transform.position - breakPosition))
                .ToArray();

            bool soundPlayed = !playFirstSound;
            float forceMagnitude = Mathf.Max(1f, originVector.magnitude);

            foreach (Shard shard in targets)
            {
                if (shard == null || !_shards.Contains(shard))
                    continue;

                Vector3 shardBreakPosition = shard == sourceShard ? breakPosition : shard.transform.position;
                Vector3 shardDirection = shard == sourceShard
                    ? originVector
                    : (shard.transform.position - breakPosition).normalized * forceMagnitude;

                if (shardDirection == Vector3.zero)
                    shardDirection = originVector;

                shard.BreakFromInputDamage(shardBreakPosition, shardDirection, patternIndex, rotation, !soundPlayed);
                soundPlayed = true;
            }
        }

        private void NotifyRemainingHealthUpdated()
        {
            Debug.Log($"Glass health: {HealthPercentage:0.0}%");
            OnRemainingHealthUpdated?.Invoke(HealthPercentage);
        }

        private void TriggerShardBreakHaptic()
        {
            if (!_canBreak)
                return;

            VibrationsHandler.GlassTap();
        }

        private float GetInputDamageRadiusWorld()
        {
            return Mathf.Max(0f, inputDamageRadius) * GetGameplayWorldHeight(GetTargetCamera());
        }

        private Camera GetTargetCamera()
        {
            return targetCamera != null ? targetCamera : Camera.main;
        }

        private float GetGameplayWorldHeight(Camera camera)
        {
            float visibleHeight = GetVisibleWorldHeight(camera);
            float margin = Mathf.Clamp(gameplayViewportMargin, -0.45f, 0.45f);
            float usableHeight = visibleHeight * (1f - margin * 2f);

            return usableHeight * deviceHeightFillPercent;
        }

        private float GetVisibleWorldHeight(Camera camera)
        {
            if (camera == null)
                return Mathf.Max(transform.lossyScale.y, 0f);

            float depth = Vector3.Dot(transform.position - camera.transform.position, camera.transform.forward);
            if (depth <= camera.nearClipPlane)
                depth = Mathf.Max(distanceFromCamera, camera.nearClipPlane + 0.1f);

            return camera.orthographic
                ? camera.orthographicSize * 2f
                : 2f * depth * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        }

        private bool IsShardInGameplayView(Shard shard)
        {
            if (shard == null)
                return false;

            Camera camera = targetCamera != null ? targetCamera : Camera.main;
            if (camera == null)
                return true;

            if (!shard.TryGetComponent(out Renderer shardRenderer))
                return true;

            Bounds bounds = shardRenderer.bounds;
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            float viewportMinX = float.PositiveInfinity;
            float viewportMinY = float.PositiveInfinity;
            float viewportMaxX = float.NegativeInfinity;
            float viewportMaxY = float.NegativeInfinity;
            bool hasPointInFront = false;

            for (int x = 0; x <= 1; x++)
            {
                for (int y = 0; y <= 1; y++)
                {
                    for (int z = 0; z <= 1; z++)
                    {
                        Vector3 corner = new Vector3(
                            x == 0 ? min.x : max.x,
                            y == 0 ? min.y : max.y,
                            z == 0 ? min.z : max.z
                        );
                        Vector3 viewportPoint = camera.WorldToViewportPoint(corner);

                        if (viewportPoint.z <= 0f)
                            continue;

                        hasPointInFront = true;
                        viewportMinX = Mathf.Min(viewportMinX, viewportPoint.x);
                        viewportMinY = Mathf.Min(viewportMinY, viewportPoint.y);
                        viewportMaxX = Mathf.Max(viewportMaxX, viewportPoint.x);
                        viewportMaxY = Mathf.Max(viewportMaxY, viewportPoint.y);
                    }
                }
            }

            if (!hasPointInFront)
                return false;

            float margin = Mathf.Clamp(gameplayViewportMargin, -0.45f, 0.45f);
            return viewportMaxX >= margin
                   && viewportMinX <= 1f - margin
                   && viewportMaxY >= margin
                   && viewportMinY <= 1f - margin;
        }

        public void PlayBreakSound(Vector3 position)
        {
            if (breakSound != null)
                PlayAudioSnapshot(breakSound, position, normalBreakSoundVolume, normalBreakSoundSpatialBlend);
        }

        private void PlayShatterSound()
        {
            if (shatterSound == null)
            {
                PlayBreakSound(transform.position);
                return;
            }

            StartCoroutine(PlayShatterSoundSnapshots(shatterSound));
        }

        private IEnumerator PlayShatterSoundSnapshots(AudioClip clip)
        {
            int repeatCount = Mathf.Max(1, shatterSoundRepeats);

            for (int i = 0; i < repeatCount; i++)
            {
                PlayShatterSoundSnapshot(clip);

                if (shatterSoundRepeatDelay > 0f && i < repeatCount - 1)
                    yield return new WaitForSeconds(shatterSoundRepeatDelay);
            }
        }

        private void PlayShatterSoundSnapshot(AudioClip clip)
        {
            PlayAudioSnapshot(clip, transform.position, shatterSoundVolume, shatterSoundSpatialBlend);
        }

        private static void PlayAudioSnapshot(AudioClip clip, Vector3 position, float volume, float spatialBlend)
        {
            var audioObject = new GameObject($"{nameof(GlassPanel)} Audio Snapshot");
            audioObject.transform.position = position;

            AudioSource audioSource = audioObject.AddComponent<AudioSource>();
            audioSource.clip = clip;
            audioSource.volume = volume;
            audioSource.spatialBlend = spatialBlend;
            audioSource.Play();

            Destroy(audioObject, clip.length + 0.1f);
        }

        public IEnumerator ShatterRemainingGlassSmoothly(int shardsPerFrame)
        {
            if (_shards == null || _shards.Count == 0)
                yield break;

            _canBreak = false;
            PlayShatterSound();

            Shard[] remainingShards = _shards.ToArray();
            int shatteredThisFrame = 0;
            shardsPerFrame = Mathf.Max(1, shardsPerFrame);

            foreach (Shard shard in remainingShards)
            {
                if (shard == null)
                    continue;

                Vector3 shatterPosition = shard.transform.position;
                Vector3 shatterDirection = (shatterPosition - transform.position).normalized;

                if (shatterDirection == Vector3.zero)
                    shatterDirection = transform.forward;

                shatterDirection += GetDirectionTowardsCamera(shatterPosition) * levelShatterCameraForce;

                shard.BreakFromLevelComplete(shatterPosition, shatterDirection);

                shatteredThisFrame++;

                if (shatteredThisFrame >= shardsPerFrame)
                {
                    shatteredThisFrame = 0;
                    yield return null;
                }
            }
        }

        public IEnumerator BlastRemainingGlassTowards(Vector3 targetPosition, float force, float torque, int shardsPerFrame)
        {
            if (_shards == null || _shards.Count == 0)
                yield break;

            _canBreak = false;
            PlayShatterSound();

            Shard[] remainingShards = _shards.ToArray();
            int blastedThisFrame = 0;
            shardsPerFrame = Mathf.Max(1, shardsPerFrame);

            foreach (Shard shard in remainingShards)
            {
                if (shard == null)
                    continue;

                _shards.Remove(shard);
                _brokenShardCount++;
                AddMinorForceTowardsCamera(shard);
                BlastShardTowards(shard, targetPosition, force, torque);

                blastedThisFrame++;

                if (blastedThisFrame >= shardsPerFrame)
                {
                    blastedThisFrame = 0;
                    yield return null;
                }
            }

            NotifyRemainingHealthUpdated();
        }

        private void AddMinorForceTowardsCamera(Shard shard)
        {
            if (levelShatterCameraForce <= 0f || shard == null)
                return;

            if (!shard.TryGetComponent(out Rigidbody shardRigidbody))
                shardRigidbody = shard.gameObject.AddComponent<Rigidbody>();

            shardRigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
            shardRigidbody.AddForce(
                GetDirectionTowardsCamera(shard.transform.position) * levelShatterCameraForce,
                ForceMode.Impulse
            );
        }

        private Vector3 GetDirectionTowardsCamera(Vector3 position)
        {
            Camera camera = GetTargetCamera();

            if (camera != null)
            {
                Vector3 direction = camera.transform.position - position;
                if (direction != Vector3.zero)
                    return direction.normalized;
            }

            return -transform.forward;
        }

        private static void BlastShardTowards(Shard shard, Vector3 targetPosition, float force, float torque)
        {
            Transform shardTransform = shard.transform;
            Vector3 direction = (targetPosition - shardTransform.position).normalized;

            if (direction == Vector3.zero)
                direction = -shardTransform.forward;

            if (!shard.TryGetComponent(out Rigidbody shardRigidbody))
                shardRigidbody = shard.gameObject.AddComponent<Rigidbody>();

            shardRigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
            shardRigidbody.AddForce(direction * force, ForceMode.Impulse);
            shardRigidbody.AddTorque(UnityEngine.Random.insideUnitSphere * torque, ForceMode.Impulse);

            Destroy(shard);
            Destroy(shard.gameObject, 4f);
        }
    }
}