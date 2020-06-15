using UnityEngine;
using Origami_Mesh;
using Origami_Utility;
using Origami_Fold;

namespace Origami_Result
{
    //メッシュ分割時に取得する情報を頂点単位で有する構造体
    public readonly struct SplitMeshInfo
    {
        //頂点
        public readonly Vector3 Vertex;

        //この頂点を持ち、折り目の始点から見て右側のメッシュ
        public readonly OrigamiMesh RightMesh;
        
        //NonFoldMeshが有する頂点の添字
        public readonly int RightIdx;

        //この頂点を持ち、折り目の始点から見て左側のメッシュ
        public readonly OrigamiMesh LeftMesh;

        //FoldMeshが有する頂点の添字
        public readonly int LeftIdx;


        public SplitMeshInfo(in Vector3 vertex, in OrigamiMesh rightMesh, in int rightIdx, in OrigamiMesh leftMesh, in int leftIdx)
        {
            Vertex = vertex;
            
            RightMesh = rightMesh;
            RightIdx = rightIdx;

            LeftMesh = leftMesh;
            LeftIdx = leftIdx;
        }
    }


    //折り目の生成時に用いる構造体
    public readonly struct CreaseGenerateResult
    {
        //折り目の折り始める時の座標
        public readonly Vector3 StartPoint;
        //折り目の回転として使う中心座標
        public readonly Vector3 MidPoint;

        public readonly bool NoNeed2Shift;

        public CreaseGenerateResult(in Vector3 startPt, in Vector3 endPt, in Vector3 midPt)
        {
            StartPoint = startPt;
            MidPoint = midPt;

            NoNeed2Shift = startPt == endPt;
        }
    }

    //上の構造体をメッシュ単位で有する
    public readonly struct CreaseGenerateResults
    {
        public readonly CreaseGenerateResult Point0_Result;
        public readonly CreaseGenerateResult Point1_Result;
        public readonly CreaseGenerateResult Point2_Result;

        //折り目の長さを表す
        public readonly Vector3 CreaseOffset;

        public readonly float StartAngle;
        public readonly float TargetAngle;

        public bool CanUpdate(in float rad, in eFoldType type)
        {
            if (type == eFoldType.MoutainFold) return StartAngle <= rad && rad <= TargetAngle;
            else return StartAngle >= rad && rad >= TargetAngle;
        }

        public CreaseGenerateResults(in Vector3[] startPts, in Vector3[] endPts, in Vector3[] midPts, in Vector3 creaseOffset, in float start, in float target)
        {
            if (startPts.Length != 3 || endPts.Length != 3 || midPts.Length != 3)
            {
                Debug.LogError("Wrong Array Size");
                Debug.Log("Start Points.Lenght = " + startPts.Length);
                Debug.Log("End Points.Lenght = " + endPts.Length);
                Debug.Log("Mid Points.Lenght = " + midPts.Length);
            }

            Point0_Result = new CreaseGenerateResult(startPts[0], endPts[0], midPts[0]);
            Point1_Result = new CreaseGenerateResult(startPts[1], endPts[1], midPts[1]);
            Point2_Result = new CreaseGenerateResult(startPts[2], endPts[2], midPts[2]);

            CreaseOffset = creaseOffset;

            StartAngle = start;
            TargetAngle = target;
        }
    }

    public readonly struct CreaseFoldResults : IFoldResults
    {
        public readonly FoldResult BottomPoint0_Result;
        public readonly FoldResult BottomPoint1_Result;
        public readonly FoldResult TopPoint1_Result;
        public readonly FoldResult TopPoint0_Result;

        public readonly Vector3 BottomCreaseOffset;

        public readonly Vector3 TopCreaseOffset;

        public readonly eFoldType FOLD_TYPE;

        public readonly float FOLD_ANGLE;

        public readonly float FOLD_TARGETRADIANS;

        /// <summary>
        /// その時使うeFoldPointを求める
        /// </summary>
        /// <param name="rad"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public eFoldAngles GetOffsetData(in float rad, out float t)
        {
            float radians = OrigamiUtility.ConvertRadiansByFoldType(rad, FOLD_TYPE);

            if (radians <= FOLD_ANGLE)
            {
                t = radians / FOLD_ANGLE;
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
                if ((OrigamiUtility.TWO_PI - rad) <= FOLD_TARGETRADIANS) return true;
            }

            return false;
        }

        public CreaseFoldResults(in Crease crease, in Vector3 creasePoint, in Vector3 creaseVec, in Vector3 perpendicularVec, in eFoldType type, in float innerLayer,
                                 in Matrix4x4 matX, in Matrix4x4 matZ)
        {
            FOLD_TYPE = type;

            var vertices = crease.Vertices;

            //直線ベクトルの長さを求める
            float magnitude = creaseVec.sqrMagnitude;

            //int bottom = (int)Crease.eCreaseTypes.Bottom; // = 0
            //int top = (int)Crease.eCreaseTypes.Top;		// = 1

            // public enum eCreaseVertices
            // {
            //     Bottom0, = 0
            //     Bottom1, = 1
            //     Top1, 	= 2
            //     Top0, 	= 3
            // }

            {
                float dif = crease.GetCreaseLayer(0) - innerLayer;
                if (dif < 0) dif = -dif;
                var offset = dif * 2.0f - 1.0f;

                //折り目を伸ばすためのベクトル
                BottomCreaseOffset = perpendicularVec * offset;

                dif = crease.GetCreaseLayer(1) - innerLayer;
                if (dif < 0) dif = -dif;
                offset = dif * 2.0f - 1.0f;

                TopCreaseOffset = perpendicularVec * offset;
            }

            //交点を求める
            {
                var midPoint = OrigamiUtility.GetPerpendicularIntersectionPoint(vertices[0], creasePoint, creaseVec, magnitude);
                BottomPoint0_Result = new FoldResult(vertices[0], midPoint, BottomCreaseOffset, matX, matZ, out bool connected);

                midPoint = OrigamiUtility.GetPerpendicularIntersectionPoint(vertices[1], creasePoint, creaseVec, magnitude);
                BottomPoint1_Result = new FoldResult(vertices[1], midPoint, BottomCreaseOffset, matX, matZ, out connected);

                midPoint = OrigamiUtility.GetPerpendicularIntersectionPoint(vertices[2], creasePoint, creaseVec, magnitude);
                TopPoint1_Result = new FoldResult(vertices[2], midPoint, TopCreaseOffset, matX, matZ, out connected);

                midPoint = OrigamiUtility.GetPerpendicularIntersectionPoint(vertices[3], creasePoint, creaseVec, magnitude);
                TopPoint0_Result = new FoldResult(vertices[3], midPoint, TopCreaseOffset, matX, matZ, out connected);
            }

            FOLD_ANGLE = OrigamiUtility.HALF_PI;

            FOLD_TARGETRADIANS = Mathf.PI;
        }

    }
}