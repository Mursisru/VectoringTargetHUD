using UnityEngine;
using UnityEngine.UI;

namespace VectoringTargetHUD_Engine
{
    internal sealed class PrismPointerGraphic : MaskableGraphic
    {
        private float _length = 80f;
        private float _baseWidth = 18f;
        private float _tipWidth = 2.5f;
        private float _depthSkew = 0.35f;
        private float _alphaGradient = 0.45f;
        private float _lineThickness = 2f;

        public void SetGeometry(float length, float baseWidth, float tipWidth, float depthSkew, float alphaGradient, Color color)
        {
            _length = Mathf.Clamp(length, 2f, 3000f);
            _baseWidth = Mathf.Clamp(baseWidth, 2f, 300f);
            _tipWidth = Mathf.Clamp(tipWidth, 1f, _baseWidth);
            _depthSkew = Mathf.Clamp01(depthSkew);
            _alphaGradient = Mathf.Clamp01(alphaGradient);
            _lineThickness = Mathf.Clamp(_baseWidth * 0.09f, 1.2f, 4.5f);
            this.color = color;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            // Local-space points (X forward, Y lateral) with origin at target center.
            float halfW = _baseWidth * 0.5f;
            float depth = Mathf.Max(2f, _baseWidth * (0.4f + _depthSkew));
            float tipHalf = Mathf.Max(0.5f, _tipWidth * 0.5f);

            Vector2 apex = new Vector2(_length, 0f);
            Vector2 tipTop = new Vector2(_length - tipHalf * 0.65f, tipHalf);
            Vector2 tipBottom = new Vector2(_length - tipHalf * 0.65f, -tipHalf);
            Vector2 leftBase = new Vector2(0f, halfW);
            Vector2 rightBase = new Vector2(0f, -halfW);
            Vector2 core = new Vector2(depth, 0f);
            Vector2 leftFacet = new Vector2(depth * 0.35f, halfW * 0.45f);
            Vector2 rightFacet = new Vector2(depth * 0.35f, -halfW * 0.45f);

            Color cTip = color;
            Color cMid = color;
            Color cBase = color;
            cMid.a *= Mathf.Clamp01(1f - _alphaGradient * 0.35f);
            cBase.a *= Mathf.Clamp01(1f - _alphaGradient);

            // Very light fill to keep shape readable without losing wireframe look.
            Color cFillTip = cTip;
            Color cFillBase = cBase;
            cFillTip.a *= 0.30f;
            cFillBase.a *= 0.24f;

            AddFill(vh, apex, leftBase, rightBase, cFillTip, cFillBase, cFillBase);
            AddFill(vh, apex, leftBase, core, cFillTip, cFillBase, cFillBase);
            AddFill(vh, apex, core, rightBase, cFillTip, cFillBase, cFillBase);

            // Wireframe crystal: contour + inner ribs, no solid fill.
            AddLine(vh, apex, tipTop, _lineThickness * 0.95f, cTip, cTip);
            AddLine(vh, apex, tipBottom, _lineThickness * 0.95f, cTip, cTip);
            AddLine(vh, apex, leftBase, _lineThickness, cTip, cMid);
            AddLine(vh, apex, rightBase, _lineThickness, cTip, cMid);
            AddLine(vh, leftBase, rightBase, _lineThickness * 0.9f, cBase, cBase);

            AddLine(vh, apex, core, _lineThickness * 0.85f, cTip, cMid);
            AddLine(vh, core, leftBase, _lineThickness * 0.8f, cMid, cBase);
            AddLine(vh, core, rightBase, _lineThickness * 0.8f, cMid, cBase);

            AddLine(vh, leftBase, leftFacet, _lineThickness * 0.75f, cBase, cMid);
            AddLine(vh, rightBase, rightFacet, _lineThickness * 0.75f, cBase, cMid);
            AddLine(vh, leftFacet, core, _lineThickness * 0.7f, cMid, cMid);
            AddLine(vh, rightFacet, core, _lineThickness * 0.7f, cMid, cMid);
        }

        private static void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color ca, Color cb)
        {
            Vector2 dir = b - a;
            float lenSq = dir.sqrMagnitude;
            if (lenSq < 0.0001f)
            {
                return;
            }

            dir /= Mathf.Sqrt(lenSq);
            Vector2 n = new Vector2(-dir.y, dir.x) * (thickness * 0.5f);

            int i0 = AddVert(vh, a - n, ca);
            int i1 = AddVert(vh, a + n, ca);
            int i2 = AddVert(vh, b + n, cb);
            int i3 = AddVert(vh, b - n, cb);

            vh.AddTriangle(i0, i1, i2);
            vh.AddTriangle(i0, i2, i3);
        }

        private static void AddFill(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color ca, Color cb, Color cc)
        {
            int ia = AddVert(vh, a, ca);
            int ib = AddVert(vh, b, cb);
            int ic = AddVert(vh, c, cc);
            vh.AddTriangle(ia, ib, ic);
        }

        private static int AddVert(VertexHelper vh, Vector2 position, Color vertexColor)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = vertexColor;
            vh.AddVert(vertex);
            return vh.currentVertCount - 1;
        }
    }
}
