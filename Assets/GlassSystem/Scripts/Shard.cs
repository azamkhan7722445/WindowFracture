using System;
using MathNet.Spatial.Euclidean;
using UnityEngine;

namespace GlassSystem.Scripts
{
    public class Shard : BaseGlass
    {
        public void InitializeShard(GlassPanel parentPanel, Polygon2D polygon, Vector2[] uvs, float thickness)
        {
            _parentPanel = parentPanel;
            _polygon = polygon;
            _thickness = thickness;
            _uvs = uvs;
        }

        public override void Break(Vector3 breakPosition, Vector3 originVector, int patternIndex = -1,
            float rotation = Single.NaN)
        {
            if (_parentPanel != null && !_parentPanel.CanShardBreakFromInput)
                return;

            if (_parentPanel != null && _parentPanel.HasInputDamageRadius)
            {
                _parentPanel.BreakShardsInInputRadius(this, breakPosition, originVector, patternIndex, rotation);
                return;
            }

            BreakInternal(breakPosition, originVector, patternIndex, rotation, true);
        }

        public void BreakFromLevelComplete(Vector3 breakPosition, Vector3 originVector, int patternIndex = -1,
            float rotation = Single.NaN)
        {
            BreakInternal(breakPosition, originVector, patternIndex, rotation, false);
        }

        public void BreakFromInputDamage(Vector3 breakPosition, Vector3 originVector, int patternIndex = -1,
            float rotation = Single.NaN, bool playSound = false)
        {
            BreakInternal(breakPosition, originVector, patternIndex, rotation, playSound);
        }

        private void BreakInternal(Vector3 breakPosition, Vector3 originVector, int patternIndex, float rotation,
            bool playSound)
        {
            if (playSound && _parentPanel != null)
                _parentPanel.PlayBreakSound(transform.position);

            base.Break(breakPosition, originVector, patternIndex, rotation);
            _parentPanel?.OnShardDestroyed(this);
            Destroy(gameObject);
        }
    }
}