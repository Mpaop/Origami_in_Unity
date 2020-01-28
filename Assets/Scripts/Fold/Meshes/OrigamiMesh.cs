using System.Collections.Generic;
using UnityEngine;
using Origami_Result;

namespace Origami_Mesh
{

    //折り紙のメッシュを管理するクラス
    public sealed class OrigamiMesh : OrigamiBase, IFoldMeshCallbacks
    {
        /// <summary>
        /// メンバフィールド
        /// </summary>

        //折紙の折った層を示す
        //public int FoldLayer { get; private set; }
        public int FoldLayer { get => m_vertices.Layers[0]; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="vertices">メッシュの頂点</param>
        /// <param name="facing">メッシュの向き</param>
        /// <param name="materialPath">マテリアルのパス</param>
        /// <param name="parent">オブジェクトの親</param>
        public OrigamiMesh(in Vector3[] vertices, bool facing, int layer, in IEnumerable<bool> connectedList, in string materialPath, in Transform parent) : base(vertices, new List<int>() { layer, layer, layer }, connectedList, facing, materialPath, parent)
        {
            MeshObject.name = "mesh" + parent.childCount;
        }

        /// <summary>
        /// OrigamiFoldResultのIsConnectedToMeshによって分岐させたい場合に使う
        /// </summary>
        /// <param name="foldRes"></param>
        /// <param name="creaseOffset"></param>
        /// <param name="t"></param>
        /// <param name="matX"></param>
        /// <param name="matZ"></param>
        /// <returns></returns>
        public static Vector3 GetRotatedVector3(in OrigamiFoldResult foldRes, in OrigamiAdjustedVertexResults adjustRes, in Vector3 adjustVertex, in Vector3 origin, in Vector3 target, in Vector3 midpoint, in Vector3 creaseOffset, in float t, in Matrix4x4 matX, in Matrix4x4 matZ)
        {
            if (foldRes.IsConnectedToOtherMesh)
            {
                Vector3 v;
                if (adjustRes.NeedsAdjustment)
                {
                    v = FastLerp(origin, adjustVertex, t);
                    v.z = origin.z;
                }
                else
                {
                    v = origin;
                }
                return v + creaseOffset;
            }
            else
            {
                return GetRotatedVector3_Lerped(origin, target, midpoint, creaseOffset, t, matX, matZ);
            }
        }

        //折紙のメッシュを渡されたラジアンに折る
        public void FoldOrigamiMeshByRadians(in OrigamiFoldResults foldRes, in OrigamiAdjustedVertexResults adjustRes, in float rad, in Matrix4x4 matZ)
        {
            if (!foldRes.ContinueFolding(rad)) return; //もう折る必要がなければ折らない

            Matrix4x4 matX = Origami_Utility.OrigamiUtility.GetXRotationMatrix(rad);

            Vector3 fold0, fold1, fold2;

            float t;
            var point = foldRes.GetOffsetData(rad, out t);
            //Debug.DrawLine(res.Point0_Result.FoldMidpoint0, res.Point0_Result.FoldMidpoint0 + offset, Color.red);
            //90度で折られている場合と折られていない場合で異なる値を渡す
            if (point == eFoldAngles.Point0)
            {
                var creaseOffset = GetRotatedVector3(foldRes.CreaseOffset, Vector3.zero, matX, matZ) * t;
                fold0 = GetRotatedVector3(foldRes.Point0_Result, adjustRes, adjustRes.Result0, foldRes.Point0_Result.FoldResult.FoldOriginalPoint0, foldRes.Point0_Result.FoldResult.FoldOriginalPoint90, foldRes.Point0_Result.FoldResult.FoldMidPoint0, creaseOffset, t, matX, matZ);
                fold1 = GetRotatedVector3(foldRes.Point1_Result, adjustRes, adjustRes.Result1, foldRes.Point1_Result.FoldResult.FoldOriginalPoint0, foldRes.Point1_Result.FoldResult.FoldOriginalPoint90, foldRes.Point1_Result.FoldResult.FoldMidPoint0, creaseOffset, t, matX, matZ);
                fold2 = GetRotatedVector3(foldRes.Point2_Result, adjustRes, adjustRes.Result2, foldRes.Point2_Result.FoldResult.FoldOriginalPoint0, foldRes.Point2_Result.FoldResult.FoldOriginalPoint90, foldRes.Point2_Result.FoldResult.FoldMidPoint0, creaseOffset, t, matX, matZ);
            }
            else
            {
                if (MeshVertices.ConnectedToCreaseList[0])
                {
                    if (adjustRes.NeedsAdjustment)
                    {
                        fold0 = adjustRes.Result0;
                        fold0.z = foldRes.Point0_Result.FoldResult.FoldOriginalPoint90.z;
                    }
                    else fold0 = foldRes.Point0_Result.FoldResult.FoldOriginalPoint90;
                }
                else
                    fold0 = GetRotatedVector3(foldRes.Point0_Result.FoldResult.FoldOriginalPoint90, foldRes.Point0_Result.FoldResult.FoldMidPoint90, matX, matZ);
                if (MeshVertices.ConnectedToCreaseList[1])
                {
                    if (adjustRes.NeedsAdjustment)
                    {
                        fold1 = adjustRes.Result1;
                        fold1.z = foldRes.Point1_Result.FoldResult.FoldOriginalPoint90.z;
                    }
                    else fold1 = foldRes.Point1_Result.FoldResult.FoldOriginalPoint90;
                }
                else
                    fold1 = GetRotatedVector3(foldRes.Point1_Result.FoldResult.FoldOriginalPoint90, foldRes.Point1_Result.FoldResult.FoldMidPoint90, matX, matZ);
                if (MeshVertices.ConnectedToCreaseList[2])
                {
                    if (adjustRes.NeedsAdjustment)
                    {
                        fold2 = adjustRes.Result2;
                        fold2.z = foldRes.Point2_Result.FoldResult.FoldOriginalPoint90.z;
                    }
                    else fold2 = foldRes.Point2_Result.FoldResult.FoldOriginalPoint90;
                }
                else
                    fold2 = GetRotatedVector3(foldRes.Point2_Result.FoldResult.FoldOriginalPoint90, foldRes.Point2_Result.FoldResult.FoldMidPoint90, matX, matZ);
            }

            //ここは順番が既に確定しているため、GetOrderedVerticesを使わずに入れる
            this.UpdateOrigamiTriangleMesh(fold0, fold1, fold2);
        }

        public void AdjustOrigamiMeshByRadians(in OrigamiAdjustedVertexResults adjustedVertexResults, in float rad)
        {
            if (!adjustedVertexResults.NeedsAdjustment) return;

            var t = rad / adjustedVertexResults.TargetRadians;

            if (t >= 1.0f) t = 1.0f;

            Vector3 vec0, vec1, vec2;

            if (this.MeshVertices.ConnectedToCreaseList[0]) vec0 = FastLerp(this.MeshVertices[0], adjustedVertexResults.Result0, t);
            else vec0 = this.MeshVertices[0];

            if (this.MeshVertices.ConnectedToCreaseList[1]) vec1 = FastLerp(this.MeshVertices[1], adjustedVertexResults.Result1, t);
            else vec1 = this.MeshVertices[1];

            if (this.MeshVertices.ConnectedToCreaseList[2]) vec2 = FastLerp(this.MeshVertices[2], adjustedVertexResults.Result2, t);
            else vec2 = this.MeshVertices[2];

            this.UpdateOrigamiTriangleMesh(vec0, vec1, vec2);
        }

        //折紙メッシュの情報を更新する。
        public void UpdateFoldInfo(bool facing, int layerNum)
        {
            IsFacingUp = facing;

            UpdateOrigamiLayers(layerNum, layerNum, layerNum);
        }

        //折り終わった時に折り目との接続を切る
        public void OnEndFold()
        {
            //将来的に使う予定
        }

        //折る結果を求める前に折り目と繋がっているフラグをリセットする
        public void ResetCreaseConnectionFlags()
        {
            m_vertices.ResetConnectionFlags();
        }
    }
}