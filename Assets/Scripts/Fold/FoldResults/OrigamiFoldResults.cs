using UnityEngine;
using System.Collections.Generic;
using Origami_Fold;
using Origami_Mesh;
using Origami_Utility;

namespace Origami_Result
{
    /// <summary>
    /// 折紙を折る時に必要な折った後の座標と中点の座標、折り目からの距離を持つ
    /// </summary>
    public readonly struct OrigamiFoldResult
    {
        //折る結果
        public readonly FoldResult FoldResult;

        //他のメッシュと接しているか
        public readonly bool IsConnectedToOtherMesh;

        //メッシュへの参照
        public readonly OrigamiMesh OrigamiMesh;

        public bool IsEmpty() => OrigamiMesh == null;

        public OrigamiFoldResult(OrigamiMesh mesh, in Vector3 origin, in Vector3 midPoint, in Vector3 creaseOffset, in Matrix4x4 matX, in Matrix4x4 matZ, in bool isConnectedToMesh)
        {
            OrigamiMesh = mesh;

            IsConnectedToOtherMesh = isConnectedToMesh;
            FoldResult = new FoldResult(origin, midPoint, creaseOffset, matX, matZ, isConnectedToMesh);
        }
    }

    //上のOrigamiFoldResultをメッシュ単位にまとめたクラス
    public readonly struct OrigamiFoldResults : IFoldResults
    {

        public readonly OrigamiFoldResult Point0_Result;
        public readonly OrigamiFoldResult Point1_Result;
        public readonly OrigamiFoldResult Point2_Result;

        public readonly Vector3 CreaseOffset;

        //最終的に折りたい角度の情報(ラジアン)
        public readonly float FOLD_TARGETRADIANS;

        //折り方
        private readonly eFoldType FOLD_TYPE;

        //折る際の角度
        private readonly float FOLD_HALFWAYRADIANS;

        /// <summary>
        /// その時使うeFoldPointを求める
        /// </summary>
        /// <param name="rad"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public eFoldAngles GetOffsetData(in float rad, out float t)
        {
            float radians = OrigamiUtility.ConvertRadiansByFoldType(rad, FOLD_TYPE);

            if (radians <= FOLD_HALFWAYRADIANS)
            {
                t = radians / FOLD_HALFWAYRADIANS;
                return eFoldAngles.Point0;
            }
            else
            {
                t = 1f;
                return eFoldAngles.Point90;
            }
        }

        //折り続けるべきか確認する
        public bool ContinueFolding(in float rad)
        {

            if (FOLD_TYPE == eFoldType.MoutainFold)
            {
                if (rad <= FOLD_TARGETRADIANS) return true;
            }
            else
            {
                if (rad >= FOLD_TARGETRADIANS) return true;
            }

            return false;
        }

        public OrigamiFoldResults(
            in OrigamiMesh mesh,
            in Vector3 creasePoint,                                                                         //頂点
            in Vector3 creaseVec,                                                                           //方向ベクトル
            in Vector3 perpendicularVec,                                                                    //補完量
            in eFoldType type,                                                                              //折る種類
            in float innerLayer,                                                                            //最も内側にあるレイヤーの数値
            in float foldHalfwayRadians, in float targetRadians,                                            //角度
            in Matrix4x4 matX, in Matrix4x4 matZ,                                                           //回転行列
            in List<CreaseGenerationInfo> genInfoList                                                       //折り目生成に用いるリスト
            ) : this(mesh, creasePoint, creaseVec, perpendicularVec, type, innerLayer, foldHalfwayRadians, targetRadians, matX, matZ)
        {
            //折り目を生成するための情報を格納
            if (Point0_Result.IsConnectedToOtherMesh) genInfoList.Add(new CreaseGenerationInfo(mesh, Point0_Result, this));

            if (Point1_Result.IsConnectedToOtherMesh) genInfoList.Add(new CreaseGenerationInfo(mesh, Point1_Result, this));

            if (Point2_Result.IsConnectedToOtherMesh) genInfoList.Add(new CreaseGenerationInfo(mesh, Point2_Result, this));
        }

        public OrigamiFoldResults(
            in OrigamiMesh mesh,
            in Vector3 creasePoint,                                                                         //頂点
            in Vector3 creaseVec,                                                                           //方向ベクトル
            in Vector3 perpendicularVec,                                                                    //補完量
            in eFoldType type,                                                                              //折る種類
            in float innerLayer,                                                                            //最も内側にあるレイヤーの数値
            in float foldHalfwayRadians, in float targetRadians,                                            //角度
            in Matrix4x4 matX, in Matrix4x4 matZ                                                            //回転行列
        )
        {
            FOLD_TYPE = type;

            var vertices = mesh.MeshVertices;

            //直線ベクトルの長さを求める
            float magnitude = creaseVec.sqrMagnitude;

            {
                float dif = mesh.FoldLayer - innerLayer;
                if (dif < 0) dif = -dif;
                var offset = dif * 2.0f - 1.0f;

                //折り目を伸ばすためのベクトル
                CreaseOffset = perpendicularVec * offset;
            }

            FOLD_HALFWAYRADIANS = foldHalfwayRadians;

            FOLD_TARGETRADIANS = targetRadians;

            //交点を求める
            {
                var midPoint = OrigamiUtility.GetPerpendicularIntersectionPoint(vertices.Vertices[0], creasePoint, creaseVec, magnitude);
                Point0_Result = new OrigamiFoldResult(mesh, vertices.Vertices[0], midPoint, CreaseOffset, matX, matZ, vertices.ConnectedToCreaseList[0]);

                midPoint = OrigamiUtility.GetPerpendicularIntersectionPoint(vertices.Vertices[1], creasePoint, creaseVec, magnitude);
                Point1_Result = new OrigamiFoldResult(mesh, vertices.Vertices[1], midPoint, CreaseOffset, matX, matZ, vertices.ConnectedToCreaseList[1]);

                midPoint = OrigamiUtility.GetPerpendicularIntersectionPoint(vertices.Vertices[2], creasePoint, creaseVec, magnitude);
                Point2_Result = new OrigamiFoldResult(mesh, vertices.Vertices[2], midPoint, CreaseOffset, matX, matZ, vertices.ConnectedToCreaseList[2]);

            }
        }

        public OrigamiFoldResults(
            in OrigamiFoldResult point0, in OrigamiFoldResult point1, in OrigamiFoldResult point2,
            in Vector3 creaseOffset,
            in float halfWayRadians, in float targetRadians,
            in eFoldType type
        )
        {
            Point0_Result = point0;
            Point1_Result = point1;
            Point2_Result = point2;

            CreaseOffset = creaseOffset;
            FOLD_HALFWAYRADIANS = halfWayRadians;
            FOLD_TARGETRADIANS = targetRadians;

            FOLD_TYPE = type;
        }
    }

    //折り目に接している頂点を持つメッシュが折られる際、その頂点が折り目に沿うように補間するデータを持つ構造体
    public readonly struct OrigamiAdjustedVertexResults
    {
        //対象とするメッシュ
        public readonly OrigamiMesh Mesh;

        public readonly bool NeedsAdjustment;
        public readonly float TargetRadians;

        public readonly Vector3 Result0;
        public readonly Vector3 Result1;
        public readonly Vector3 Result2;

        public OrigamiAdjustedVertexResults(in OrigamiMesh mesh, in float rad, in Vector3 startPoint, in Vector3 closestIntersection, in Vector3 dir, in Vector3 baseDir, in float baseMagnitude, in float baseSqrMagnitude, in Matrix4x4 matZ)
        {
            Mesh = mesh;

            TargetRadians = rad;

            NeedsAdjustment = true;

            var vertices = mesh.MeshVertices;

            var results = new Vector3[3];

            for (int i = 0; i < 3; ++i)
            {
                Vector3 v;
                if (vertices.ConnectedToCreaseList[i])
                {
                    var mag = (vertices.Vertices[i] - closestIntersection).magnitude;
                    var t = mag / baseMagnitude;

                    v = closestIntersection + dir * t;

                    v.z = vertices.Vertices[i].z;
                }
                else v = Vector3.zero;

                results[i] = v;
            }

            Result0 = results[0];
            Result1 = results[1];
            Result2 = results[2];
        }

        public OrigamiAdjustedVertexResults(in OrigamiMesh mesh, in float rad, in OrigamiFoldResults res, in Vector3 startPoint, in Vector3 closestIntersection, in Vector3 dir, in Vector3 baseDir, in float baseMagnitude, in float baseSqrMagnitude, in Matrix4x4 matZ)
        : this(mesh, rad, startPoint, closestIntersection, dir, baseDir, baseMagnitude, baseSqrMagnitude, matZ)
        {
            if (mesh.MeshVertices.ConnectedToCreaseList[0])
            {
                Result0.z = res.Point0_Result.FoldResult.FoldOriginalPoint90.z;
            }
            if (mesh.MeshVertices.ConnectedToCreaseList[1])
            {
                Result1.z = res.Point1_Result.FoldResult.FoldOriginalPoint90.z;
            }
            if (mesh.MeshVertices.ConnectedToCreaseList[2])
            {
                Result2.z = res.Point2_Result.FoldResult.FoldOriginalPoint90.z;
            }
        }

        private OrigamiAdjustedVertexResults(bool needAdjustment)
        {
            Mesh = null;
            Result0 = Vector3.zero;
            Result1 = Vector3.zero;
            Result2 = Vector3.zero;

            TargetRadians = 0.0f;

            NeedsAdjustment = needAdjustment;
        }

        //空の
        public static OrigamiAdjustedVertexResults CreateEmptyOffsetResult()
        {
            return new OrigamiAdjustedVertexResults(false);
        }
    }
}