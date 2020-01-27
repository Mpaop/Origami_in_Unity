using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using Origami_Fold;
using Origami_Result;
using Origami_Utility;

namespace Origami_Mesh
{
    //折り目のメッシュを管理するクラス
    public sealed class CreaseMesh : OrigamiBase
    {
        /// <summary>
        /// メンバフィールド
        /// </summary>

        //折り目の座標
        //private List<int> m_CreaseLayers = null;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="vertices">メッシュの頂点</param>
        /// <param name="facing">メッシュの向き</param>
        /// <param name="materialPath">マテリアルのパス</param>
        /// <param name="parent">オブジェクトの親</param>
        public CreaseMesh(in IEnumerable<Vector3> vertices, in IEnumerable<int> creaseLayers, bool facing, in string materialPath, in Transform parent) : base(vertices, creaseLayers, new List<bool> { false, false, false, false }, facing, materialPath, parent)
        {
            // m_CreaseLayers = new List<int>(creaseLayers.Count());
            // m_CreaseLayers.AddRange(creaseLayers);

            MeshObject.name = "crease" + MeshObject.transform.parent.childCount;
        }

        /// <summary>
        /// 折り目を伸ばす
        /// </summary>
        /// <param name="results"></param>
        /// <param name="res"></param>
        /// <param name="idx"></param>
        /// <param name="radians"></param>
        /// <param name="layer"></param>
        /// <param name="type"></param>
        /// <param name="matZ"></param>
        /// <returns></returns>
        private Vector3 ExtendCrease(in CreaseGenerateResults results, in CreaseGenerateResult res, int idx, in float radians, in float layer, in eFoldType type, in Matrix4x4 matZ, ref bool stopExtending)
        {
            if (idx < 0 || idx >= 3)
            {
                throw new System.ArgumentOutOfRangeException();
            }

            if (res.NoNeed2Shift)
            {
                return res.StartPoint;
            }

            float rad, dif, ratio;

            if (type == eFoldType.MoutainFold)
            {
                dif = m_vertices.Layers[idx] - layer;
                rad = radians + dif * OrigamiUtility.ANGLE_OFFSET;
                if (0 >= rad) return m_vertices.Vertices[idx];

                ratio = rad / results.TargetAngle;
            }
            else
            {
                dif = m_vertices.Layers[idx] - layer;
                rad = radians + dif * OrigamiUtility.ANGLE_OFFSET;
                if (OrigamiUtility.TWO_PI <= rad) return m_vertices.Vertices[idx];

                ratio = (results.StartAngle - rad) / (results.StartAngle - results.TargetAngle);
            }

            //var checkRad = OrigamiUtility.ConvertRadiansByFoldType(rad, type);

            Matrix4x4 matX;

            if (!results.CanUpdate(rad, type))
            {
                stopExtending = true;

                matX = OrigamiUtility.GetXRotationMatrix(results.TargetAngle);
                return res.MidPoint + GetRotatedVector3(results.CreaseOffset, Vector3.zero, matX, matZ);
            }

            matX = OrigamiUtility.GetXRotationMatrix(rad);

            var creaseOffset = GetRotatedVector3(results.CreaseOffset * ratio, Vector3.zero, matX, matZ);

            return res.MidPoint + creaseOffset;
        }

        /// <summary>
        /// 生成されたメッシュを伸ばす
        /// </summary>
        /// <param name="res"></param>
        /// <param name="radians"></param>
        /// <param name="layer"></param>
        /// <param name="type"></param>
        /// <param name="matZ"></param>
        public void ExtendGeneratedCreaseMeshByRadians(in Crease crease, in CreaseGenerateResults res, in float radians, in float layer, in eFoldType type, in Matrix4x4 matZ, ref bool hasExtended)
        {
            if (crease.HasExtended) return;

            Vector3 fold0, fold1, fold2;
            bool stopExtending = false;
            fold0 = ExtendCrease(res, res.Point0_Result, 0, radians, layer, type, matZ, ref stopExtending);
            fold1 = ExtendCrease(res, res.Point1_Result, 1, radians, layer, type, matZ, ref stopExtending);
            fold2 = ExtendCrease(res, res.Point2_Result, 2, radians, layer, type, matZ, ref stopExtending);

            hasExtended = stopExtending;

            UpdateOrigamiTriangleMesh(fold0, fold1, fold2);
        }

        public void UpdateCreaseFacing(bool facing) => IsFacingUp = facing;

        public void UpdateCreaseVertices(in Vector3 pt1, in Vector3 pt2, in Vector3 pt3)
        {
            UpdateOrigamiTriangleMesh(pt1, pt2, pt3);
        }
    }

    //折り目に用いる頂点
    public class CreaseVertex
    {
        public Vector3 Vertex {get; private set; }
        public OrigamiMesh Mesh {get; private set; }

        //メッシュと接している頂点の添字
        private int m_meshIdx;

        public CreaseVertex (in Vector3 vec, in OrigamiMesh mesh, in int idx)
        {
            Vertex = vec;
            Mesh = mesh;
            m_meshIdx = idx;
            mesh.MeshVertices.AddUpdateVertexEventAt(idx, UpdateVertex);
        }

        //頂点情報の更新のみ行う時に使う
        public void UpdateVertex (in Vector3 vertex)
        {
            Vertex = vertex;
        }

        //折り目が分割される場合など、値を全て更新がある時に使う
        public void RenewValues(in Vector3 vertex, in OrigamiMesh mesh, int idx)
        {
            Vertex = vertex;
            Mesh.MeshVertices.RemoveUpdateVertexEventAt(m_meshIdx, UpdateVertex);

            Mesh = mesh;
            m_meshIdx = idx;
            mesh.MeshVertices.AddUpdateVertexEventAt(idx, UpdateVertex);
        }
    }

    //折り目のメッシュをひとまとめにして管理するクラス
    public class Crease : IFoldMeshCallbacks
    {
        private CreaseMesh[] m_creases;

        private Vector3[] m_vertices;

        public IReadOnlyList<Vector3> MeshVertices { get; }

        //折り目の頂点を管理するために用いる。上は破棄予定
        private List<CreaseVertex> m_creaseVertices;

        //折り目の向き
        public bool CreaseFacing => m_creases[0].IsFacingUp;

        public string CreaseName => m_creases[0].MeshObject.name;

        //紙が既に伸びているか
        public bool HasExtended { get; private set; }

        //折り目は4つのCreaseMeshによって構成されるため、その分類
        public enum eCreaseTypes
        {
            Bottom,
            Top,
            MAX
        }

        //折り目の座標を管理する用
        //時計回りのイメージ
        // Bottom 0 - 1
        // Top	  0 - 1
        public enum eCreaseVertices
        {
            Bottom0,
            Bottom1,
            Top1,
            Top0,
            MAX
        }

        public Crease()
        {
            m_creases = new CreaseMesh[(int)eCreaseTypes.MAX];

            int size = (int)eCreaseTypes.MAX;
            m_vertices = new Vector3[size];
            for (int i = 0; i < size; i++)
            {
                m_vertices[i] = Vector3.zero;
            }

            MeshVertices = new ReadOnlyCollection<Vector3>(m_vertices);

            m_creaseVertices = new List<CreaseVertex>(size);
        }

        //折り目のレイヤーを取得する
        public int GetCreaseLayer(in int idx)
        {

            //良くない実装である自覚はある
            if (idx == 0) return m_creases[0].MeshVertices.Layers[0];
            else if (idx == 1) return m_creases[1].MeshVertices.Layers[2];
            else
            {
                Debug.LogError($"Log: Method: GetCreaseLayer(in int idx). \nCause: argument ({idx}) is out of range");
                throw new System.ArgumentOutOfRangeException();
            }
        }

        public MeshVertex GetMeshVertexAt(eCreaseVertices ver)
        {
            switch (ver)
            {
                case eCreaseVertices.Bottom0: return m_creases[0].MeshVertices.GetMeshVertexAt(0);
                case eCreaseVertices.Bottom1: return m_creases[0].MeshVertices.GetMeshVertexAt(1);
                case eCreaseVertices.Top1: return m_creases[0].MeshVertices.GetMeshVertexAt(2);
                case eCreaseVertices.Top0: return m_creases[1].MeshVertices.GetMeshVertexAt(2);
                default:
                    throw new System.ArgumentOutOfRangeException();
            }
        }

        private void SetLayers(in int bottomLayer, in int topLayer)
        {
            m_creases[0].UpdateOrigamiLayers(bottomLayer, bottomLayer, topLayer);
            m_creases[1].UpdateOrigamiLayers(bottomLayer, topLayer, topLayer);
        }

        public void UpdateCreaseInfo(in int bottomLayer, in int topLayer, in bool facing)
        {
            SetLayers(bottomLayer, topLayer);

            //Debug.Log("Updated Layer: " + CreaseName + "  Bottom: " + bottomLayer + "  Top: " + topLayer);

            foreach (var mesh in m_creases)
            {
                mesh.UpdateCreaseFacing(facing);
            }
        }

        //生成されたばかりの折り目を伸ばすために用いる
        public void ExtendGeneratedCreases(in CreaseGenerateResults[] results, in float radians, in float layer, in eFoldType type, in Matrix4x4 matZ)
        {
            if (HasExtended) return;

            int max = (int)eCreaseTypes.MAX;

            bool extended = HasExtended;

            for (int i = 0; i < max; i++)
            {
                m_creases[i].ExtendGeneratedCreaseMeshByRadians(this, results[i], radians, layer, type, matZ, ref extended);
            }

            HasExtended = extended;
        }

        //折り目のメッシュを生成する。折り目の頂点クラスは呼び出し元で宣言してから渡す
        public void GenerateCreaseMesh(List<CreaseVertex> vertices)
        {
            for(int i = 0; i < m_creaseVertices.Count; ++i) m_creaseVertices[i] = vertices[i];
        }

        /// <summary>
        /// 折った時に伸びる折り目を生成する
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="bottomLayer"></param>
        /// <param name="mesh"></param>
        /// <param name="creaseOffset"></param>
        /// <param name="startAngle"></param>
        /// <param name="finalAngle"></param>
        /// <param name="materialPath"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        public CreaseGenerateResults[] GenerateSquashedCrease(in Vector3[] vertices, in int bottomLayer, in int topLayer, in bool facing, in Vector3 creaseOffset, in float startAngle, in float finalAngle, in string materialPath, in Transform parent)
        {
            if (vertices.Length != (int)eCreaseVertices.MAX)
            {
                Debug.LogError("Wrong size");
                throw new System.ArgumentException();
            }

            CreaseGenerateResults[] results = new CreaseGenerateResults[(int)eCreaseTypes.MAX];

            Vector3[] startPoints = { vertices[(int)eCreaseVertices.Bottom0], vertices[(int)eCreaseVertices.Bottom1], vertices[(int)eCreaseVertices.Bottom1] };
            Vector3[] endPoints = { vertices[(int)eCreaseVertices.Bottom0], vertices[(int)eCreaseVertices.Bottom1], vertices[(int)eCreaseVertices.Top1] };
            Vector3[] midPoints011 = { vertices[(int)eCreaseVertices.Bottom0], vertices[(int)eCreaseVertices.Bottom1], vertices[(int)eCreaseVertices.Bottom1] };
            Vector3[] midPoints010 = { vertices[(int)eCreaseVertices.Bottom0], vertices[(int)eCreaseVertices.Bottom1], vertices[(int)eCreaseVertices.Bottom0] };

            int[] creaseLayers = { bottomLayer, topLayer, bottomLayer };

            //折り始めた瞬間から表示され始めるメッシュ二枚
            m_creases[(int)eCreaseTypes.Bottom] = new CreaseMesh(startPoints, creaseLayers, facing, materialPath, parent);
            results[(int)Crease.eCreaseTypes.Bottom] = new CreaseGenerateResults(startPoints, endPoints, midPoints011, creaseOffset, startAngle, finalAngle);

            startPoints = new Vector3[3] { vertices[(int)eCreaseVertices.Bottom0], vertices[(int)eCreaseVertices.Bottom1], vertices[(int)eCreaseVertices.Bottom0] };
            endPoints = new Vector3[3] { vertices[(int)eCreaseVertices.Bottom0], vertices[(int)eCreaseVertices.Top1], vertices[(int)eCreaseVertices.Top0] };
            creaseLayers = new int[3] { topLayer, topLayer, bottomLayer };

            m_creases[(int)eCreaseTypes.Top] = new CreaseMesh(startPoints, creaseLayers, facing, materialPath, parent);
            results[(int)Crease.eCreaseTypes.Top] = new CreaseGenerateResults(startPoints, endPoints, midPoints010, creaseOffset, startAngle, finalAngle);

            for (int i = 0; i < (int)eCreaseVertices.MAX; i++)
            {
                m_vertices[i] = vertices[i];
            }

            // m_creaseLayers[(int)eCreaseTypes.Bottom] = bottomLayer;
            // m_creaseLayers[(int)eCreaseTypes.Top] = topLayer;
            SetLayers(bottomLayer, topLayer);

            HasExtended = false;

            return results;
        }

        /// <summary>
        /// 分割した新しい折り目を生成する
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="creaseLayers"></param>
        /// <param name="mesh"></param>
        /// <param name="materialPath"></param>
        /// <param name="parent"></param>
        public Crease GenerateSplitCreaseMeshes(in Vector3[] vertices, in string materialPath, in Transform parent)
        {
            if (vertices.Length != (int)eCreaseVertices.MAX)
            {
                Debug.LogError("Wrong size");
                throw new System.ArgumentException();
            }

            //新しい折り目
            var crease = new Crease();

            Vector3[] creaseMeshVertices = { vertices[0], vertices[1], vertices[2] };
            int[] layers = { m_creases[0].MeshVertices.Layers[0], m_creases[0].MeshVertices.Layers[1], m_creases[0].MeshVertices.Layers[2] }; //{ m_creaseLayers[0], m_creaseLayers[0], m_creaseLayers[1] };
            var facing = m_creases[0].IsFacingUp;

            crease.m_creases[(int)eCreaseTypes.Bottom] = new CreaseMesh(creaseMeshVertices, layers, facing, materialPath, parent);

            creaseMeshVertices = new Vector3[3] { vertices[0], vertices[2], vertices[3] };
            layers = new int[3] { m_creases[1].MeshVertices.Layers[0], m_creases[1].MeshVertices.Layers[1], m_creases[1].MeshVertices.Layers[2] };//{ m_creaseLayers[0], m_creaseLayers[1], m_creaseLayers[1] };

            crease.m_creases[(int)eCreaseTypes.Top] = new CreaseMesh(creaseMeshVertices, layers, facing, materialPath, parent);

            for (int i = 0; i < (int)eCreaseVertices.MAX; i++) crease.m_vertices[i] = vertices[i];

            // crease.m_creaseLayers[(int)eCreaseTypes.Bottom] = m_creaseLayers[(int)eCreaseTypes.Bottom];
            // crease.m_creaseLayers[(int)eCreaseTypes.Top] = m_creaseLayers[(int)eCreaseTypes.Top];
            crease.SetLayers(this.GetCreaseLayer(0), this.GetCreaseLayer(1));

            HasExtended = true;

            return crease;
        }

        public void UpdateCreaseVertices(in Vector3[] vertices)
        {
            if (m_vertices.Length != vertices.Length)
            {
                Debug.LogError("Wrong Size");
                throw new System.ArgumentOutOfRangeException();
            }

            for (int i = 0; i < vertices.Length; i++) m_vertices[i] = vertices[i];

            m_creases[(int)eCreaseTypes.Bottom].UpdateOrigamiTriangleMesh(new List<Vector3>(3) { m_vertices[0], m_vertices[1], m_vertices[2] });
            m_creases[(int)eCreaseTypes.Top].UpdateOrigamiTriangleMesh(new List<Vector3>(3) { m_vertices[0], m_vertices[2], m_vertices[3] });
        }

        //折紙のメッシュを渡されたラジアンに折る
        public void FoldCreaseMeshByRadians(in CreaseFoldResults res, in float bottomRad, in float topRad, in Matrix4x4 matZ)
        {
            //折り目の下部の頂点と上部の頂点でレイヤー情報が異なるため、分ける
            Vector3 bottomFold0, bottomFold1, topFold1, topFold0;
            //X軸の回転
            Matrix4x4 matX;
            //係数
            float t;
            eFoldAngles angles;

            {
                var continueBottom = res.ContinueFolding(bottomRad);
                var continueTop = res.ContinueFolding(topRad);

                //どちらも更新する必要がなければ戻る
                if (!continueBottom && !continueTop) return;

                //下部の処理
                if (continueBottom)
                {
                    matX = OrigamiUtility.GetXRotationMatrix(bottomRad);
                    angles = res.GetOffsetData(bottomRad, out t);
                    //90度で折られている場合と折られていない場合で異なる値を渡す
                    if (angles == eFoldAngles.Point0)
                    {
                        var bottomOffset = OrigamiBase.GetRotatedVector3(res.BottomCreaseOffset, Vector3.zero, matX, matZ) * t;

                        bottomFold0 = OrigamiBase.GetRotatedVector3_Lerped(res.BottomPoint0_Result.FoldOriginalPoint0, res.BottomPoint0_Result.FoldOriginalPoint90, res.BottomPoint0_Result.FoldMidPoint0, bottomOffset, t, matX, matZ);
                        bottomFold1 = OrigamiBase.GetRotatedVector3_Lerped(res.BottomPoint1_Result.FoldOriginalPoint0, res.BottomPoint1_Result.FoldOriginalPoint90, res.BottomPoint1_Result.FoldMidPoint0, bottomOffset, t, matX, matZ);
                    }
                    else
                    {
                        bottomFold0 = OrigamiBase.GetRotatedVector3(res.BottomPoint0_Result.FoldOriginalPoint90, res.BottomPoint0_Result.FoldMidPoint90, matX, matZ);
                        bottomFold1 = OrigamiBase.GetRotatedVector3(res.BottomPoint1_Result.FoldOriginalPoint90, res.BottomPoint1_Result.FoldMidPoint90, matX, matZ);
                    }
                }
                else
                {
                    bottomFold0 = m_vertices[0];
                    bottomFold1 = m_vertices[1];
                }

                //上部の処理
                if (continueTop)
                {
                    matX = OrigamiUtility.GetXRotationMatrix(topRad);
                    angles = res.GetOffsetData(topRad, out t);

                    //90度で折られている場合と折られていない場合で異なる値を渡す
                    if (angles == eFoldAngles.Point0)
                    {
                        var topOffset = OrigamiBase.GetRotatedVector3(res.TopCreaseOffset, Vector3.zero, matX, matZ) * t;

                        topFold1 = OrigamiBase.GetRotatedVector3_Lerped(res.TopPoint1_Result.FoldOriginalPoint0, res.TopPoint1_Result.FoldOriginalPoint90, res.TopPoint1_Result.FoldMidPoint0, topOffset, t, matX, matZ);
                        topFold0 = OrigamiBase.GetRotatedVector3_Lerped(res.TopPoint0_Result.FoldOriginalPoint0, res.TopPoint0_Result.FoldOriginalPoint90, res.TopPoint0_Result.FoldMidPoint0, topOffset, t, matX, matZ);
                    }
                    else
                    {
                        topFold1 = OrigamiBase.GetRotatedVector3(res.TopPoint1_Result.FoldOriginalPoint90, res.TopPoint1_Result.FoldMidPoint90, matX, matZ);
                        topFold0 = OrigamiBase.GetRotatedVector3(res.TopPoint0_Result.FoldOriginalPoint90, res.TopPoint0_Result.FoldMidPoint90, matX, matZ);
                    }
                }
                else
                {
                    topFold1 = m_vertices[2];
                    topFold0 = m_vertices[3];
                }
            }
            //ここは順番が既に確定しているため、GetOrderedVerticesを使わずに入れる
            m_creases[0].UpdateCreaseVertices(bottomFold0, bottomFold1, topFold1);
            m_creases[1].UpdateCreaseVertices(bottomFold0, topFold1, topFold0);

            m_vertices[0] = bottomFold0;
            m_vertices[1] = bottomFold1;
            m_vertices[2] = topFold1;
            m_vertices[3] = topFold0;
        }

        //折り終えた時の後処理
        public void OnEndFold()
        {
            //折ると、BottomとTopが入れ替わるのでスワップ
            //OrigamiUtility.Swap(ref m_creaseLayers[0], ref m_creaseLayers[1]);
            var oldBottom = GetCreaseLayer(0);
            var oldTop = GetCreaseLayer(1);

            SetLayers(oldTop, oldBottom);

            m_vertices[0] = GetMeshVertexAt(eCreaseVertices.Bottom0).Vertex;
            m_vertices[1] = GetMeshVertexAt(eCreaseVertices.Bottom1).Vertex;
            m_vertices[2] = GetMeshVertexAt(eCreaseVertices.Top1).Vertex;
            m_vertices[3] = GetMeshVertexAt(eCreaseVertices.Top0).Vertex;

            OrigamiUtility.Swap(ref m_vertices[0], ref m_vertices[3]);
            OrigamiUtility.Swap(ref m_vertices[1], ref m_vertices[2]);

            m_creases[0].UpdateOrigamiTriangleMesh(new List<Vector3>(3) { m_vertices[0], m_vertices[1], m_vertices[2] });
            m_creases[1].UpdateOrigamiTriangleMesh(new List<Vector3>(3) { m_vertices[0], m_vertices[2], m_vertices[3] });
        }

        //伸縮を終えた時の後処理

        public void OnEndExtend()
        {
            m_vertices[0] = GetMeshVertexAt(eCreaseVertices.Bottom0).Vertex;
            m_vertices[1] = GetMeshVertexAt(eCreaseVertices.Bottom1).Vertex;
            m_vertices[2] = GetMeshVertexAt(eCreaseVertices.Top1).Vertex;
            m_vertices[3] = GetMeshVertexAt(eCreaseVertices.Top0).Vertex;
        }

    }
}