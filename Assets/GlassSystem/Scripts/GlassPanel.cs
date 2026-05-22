using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Spatial.Euclidean;
using UnityEngine;
using static GlassSystem.Scripts.MathNetUtils;

namespace GlassSystem.Scripts
{
    public class GlassPanel : BaseGlass
    {
        protected List<Shard> _shards;

        public int health = 2;

        [Header("Sound")]
        public AudioClip breakSound;

        [Header("Camera Fit")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float distanceFromCamera = 5f;

        [Header("Level Complete")]
        [SerializeField, Range(0f, 100f)] private float levelCompletePercent = 90f;
        [SerializeField] private GameObject levelCompleteUI;

        private int _totalShardCount;
        private int _brokenShardCount;
        private bool _levelCompleteShown;

        public float BreakPercentage
        {
            get
            {
                if (_totalShardCount <= 0)
                    return 0f;

                return (_brokenShardCount / (float)_totalShardCount) * 100f;
            }
        }

        protected void Start()
        {
            _parentPanel = this;

            if (levelCompleteUI != null)
                levelCompleteUI.SetActive(false);

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
            if (_shards is not null)
            {
                Debug.LogError("GlassPanel broken twice");
                return;
            }

            _shards = new List<Shard>();
            _totalShardCount = 0;
            _brokenShardCount = 0;
            _levelCompleteShown = false;

            if (breakSound != null)
                AudioSource.PlayClipAtPoint(breakSound, transform.position);

            base.Break(breakPosition, originVector, patternIndex, rotation);

            Destroy(GetComponent<MeshFilter>());
            Destroy(GetComponent<MeshRenderer>());
            Destroy(GetComponent<Collider>());
        }

        public void OnShardDestroyed(Shard shard)
        {
            if (_shards == null)
                return;

            if (_shards.Remove(shard))
            {
                _brokenShardCount++;
                CheckLevelComplete();
            }

            if (health > 0)
            {
                health -= 1;

                if (health == 0)
                {
                    foreach (Shard s in _shards)
                    {
                        s.Fall();
                    }
                }
            }
        }

        public void OnNewSHard(Shard shard)
        {
            if (_shards == null)
                _shards = new List<Shard>();

            _shards.Add(shard);
            _totalShardCount++;
        }

        private void CheckLevelComplete()
        {
            float percent = BreakPercentage;

            Debug.Log($"Glass broken: {percent:0.0}%");

            if (_levelCompleteShown)
                return;

            if (percent >= levelCompletePercent)
            {
                _levelCompleteShown = true;
                ShowLevelComplete();
            }
        }

        private void ShowLevelComplete()
        {
            Debug.Log("Level Complete");

            if (levelCompleteUI != null)
                levelCompleteUI.SetActive(true);
        }
    }
}