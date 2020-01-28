using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Origami_Mesh;
using Origami_Result;
using Origami_Utility;

namespace Origami_Fold
{	
	//作成者：Mpaop
	//メッシュ(折紙)を折るクラス
	sealed public class MeshFoldMachine
	{

		//折り紙のメッシュを格納するゲーム内のオブジェクト
		public Transform MeshParent {private set; get;}

		//折紙を構成する全てのメッシュを保持するリスト
		private List<OrigamiMesh> m_AllOrigamiMeshGroup;

		public IReadOnlyList<OrigamiMesh> AllOrigamiMeshes => m_AllOrigamiMeshGroup;
		//折紙のメッシュの内折る側のメッシュを保持するリスト
		private List<OrigamiMesh> m_OrigamiFoldGroup;
		//折った後の頂点情報を持つ
		private List<OrigamiFoldResults> m_OrigamiFoldResults;

		//折らない側の折り紙メッシュを保持するリスト
		private List<OrigamiMesh> m_OrigamiNonFoldGroup;

		//折り紙を折る側の折り目に接しているメッシュを折り目に沿うように調整するためのリスト
		private List<OrigamiAdjustedVertexResults> m_OrigamiFoldGroupAdjustResults;

		//折り紙を折らない側の折り目に接しているメッシュを折り目に沿うように調整するためのリスト
		private List<OrigamiAdjustedVertexResults> m_OrigamiNonFoldGroupAdjustResults;

		//新しく生成される折り目のメッシュを保持するリスト
		private List<Crease> m_GeneratedCreaseGroup;

		//折る前からある折り目のメッシュを保持するリスト
		private List<Crease> m_AllCreaseGroup;

		//折る折り目のリスト
		private List<Crease> m_CreaseFoldGroup;

		//折り目を生成時のリスト
		private List<CreaseGenerateResults[]> m_CreaseGenerateResults;

		//既に生成された折り目の折る時のリスト
		private List<CreaseFoldResults> m_CreaseFoldResults;

		private float m_OrigamiSize;

		//紙のゲーム開始時の辺の長さを取得する
		public float GetOriginalOrigamiSideLength => m_OrigamiSize * 2.0f;

		private Matrix4x4 m_MatZ;

		//紙の折り目の幅
		private const float CREASE_WIDTH = 0.003f;

		//角度の大きさ
		private const float ANGLE_OFFSET = 1f;

		//折る時の内側のメッシュのレイヤーの番号
		private int m_InnerLayer;

		//FoldMachineが動作しているのかを判定する
		public bool IsActive
		{
			get;
			set;
		}

		//マテリアルのパス
		private string m_MaterialPath = null;

		//疑似外積
		private static double Cross2DXY(in Vector3 lhs, in Vector3 rhs)
		{
			return lhs.x * rhs.y - rhs.x * lhs.y;
		}

		private static double Cross2DXZ(in Vector3 lhs, in Vector3 rhs)
		{
			return lhs.x * rhs.z - rhs.x * lhs.z;
		}

		private static double Cross2DYZ(in Vector3 lhs, in Vector3 rhs)
		{
			return lhs.y * rhs.z - rhs.z * lhs.y;
		}

		//三次元ベクトルの線形補間
		private static Vector3 Lerp(in Vector3 val1, in Vector3 val2, in float t)
		{
			return (1.0f - t) * val1 + t * val2;
		}

		//浮動小数点数の線形補間
		private static float Lerp(float val1, float val2, in float t)
		{
			return (1.0f - t) * val1 + t * val2;
		}

		private void UpdateIfFurtherFromPoint(in Vector3 basePoint, in Vector3 vertexToCompareWith, ref MeshVertex updateVertex, in bool isConnectedToCrease)
        {
			var dir = updateVertex.Vertex - basePoint;
            var dis = dir.x * dir.x + dir.y * dir.y;
            var compDir = vertexToCompareWith - basePoint;
			var compDis = compDir.x * compDir.x + compDir.y * compDir.y;
            if (compDis > dis) updateVertex = new MeshVertex(vertexToCompareWith, updateVertex.Layer, isConnectedToCrease);
		}

		//vertex を closest と furthest と比較し、それぞれ距離がstartPoint により近い/遠い場合は、値を上書きする
		private static void SetClosestAndFurthestVertices(in Vector3 startPoint, in Vector3 vertex, ref Vector3 closest, ref MeshVertex furthest)
		{
            var closestDir = closest - startPoint;
            var closestSqrMag = closestDir.x * closestDir.x + closestDir.y * closestDir.y;
            var vDir = vertex - startPoint;
            var vSqrMag = vDir.x * vDir.x + vDir.y * vDir.y;

            if (vSqrMag < closestSqrMag)
            {
                closest = vertex;
            }

            var furthestDir = furthest.Vertex - startPoint;
            var furthestSqrMag = furthestDir.x * furthestDir.x + furthestDir.y * furthestDir.y;

            if (vSqrMag > furthestSqrMag)
            {
                furthest = new MeshVertex(vertex, furthest.Layer, furthest.IsConnectedToCrease);
            }
		}

		//折った後の最も外側にあるレイヤーの数値を返す
        private static int GetUpdatedLayerValueOfOuterLayer(in int inner, in int outer, in eFoldType type)
        {
            int outerLayer;

            if (type == eFoldType.MoutainFold) outerLayer = inner - 1 + (inner - outer);
            else outerLayer = inner + 1 + (inner - outer);

            return outerLayer;
        }

		//整数の差分を取得
		private static int GetIntegerDif(in int layer1, in int layer2)
		{
			var temp = layer1 - layer2;
			if(temp < 0) temp = -temp;
			return temp;
		}

		//始点の座標のズレが最も大きい(最も内側のレイヤーに使う)座標を返す
		private static Vector3 GetLowestCreasePoint(in Vector3 baseCreasePoint, in Vector3 perpendicularVec, in int newOuter, in int oldInner, in eFoldType type)
		{
			int dif;
			if(type == eFoldType.MoutainFold)
			{
				dif = -(newOuter - (oldInner - 1));
			}
			else
			{
				dif = newOuter - (oldInner + 1);
			}


			return baseCreasePoint - perpendicularVec * dif;
		}

		//正規化したベクトルの外積結果を返す
		private void GetNormalizedCross2DResults(in Vector3 creasePoint, in Vector3 normalizedDir, in IReadOnlyList<Vector3> vertices, out double[] res, int resSize)
		{			
			if(vertices.Count < resSize)
			{
				Debug.LogError("Wrong Size");
				throw new System.ArgumentOutOfRangeException();
			}

			res = new double[resSize];

			for (int i = 0; i < res.Length; i++)
			{
				var nVec = (vertices[i] - creasePoint).normalized;
				var temp = Cross2DXY(normalizedDir, nVec);
                //誤差に対応
                var abs = temp >= 0.0 ? temp : -temp;
				if(abs <= OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION) temp = 0.0;
				
				res[i] = temp;
			}

		}

        //二つの座標を始点とする方向ベクトルの交点を求める。
        //あくまで線分同士の交点なので、z値は計算で扱わず、引数として渡す
        //始点と方向ベクトルを使って直線の方程式を計算する
        //式：
        //point = 始点、vec = 方向ベクトル
        //y = (vec.y / vec.x) * (x - point.x) + point.y
        public static bool GetIntersectionPointForDirections(in Vector3 p1, in Vector3 p1Dir, in Vector3 p2, in Vector3 p2Dir, in float zValue, out Vector3 intersection)
		{
				intersection = Vector3.zero;

                //ベクトルのx成分が0だと計算方法が変わるのでチェック
				var checkX1 = p1Dir.x < 0 ? -p1Dir.x : p1Dir.x;
				var checkX2 = p2Dir.x < 0 ? -p2Dir.x : p2Dir.x;


				if(checkX1 < OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION && checkX2 < OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION)
				{
					return false;
				}

				//交点が線分の範囲内にあるのかを判定するために用いる
				float t1, t2;
				bool withinBorders = false;

                if (checkX1 < OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION)
                {
                    intersection.x = p1.x;
                    intersection.y = (p2Dir.y / p2Dir.x) * (intersection.x - p2.x) + p2.y;

					t1 = (intersection.y - p1.y) / p1Dir.y;
					t2 = (intersection.x - p2.x) / p2Dir.x;
                }
                else if (checkX2 < OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION)
                {
                    intersection.x = p2.x;
                    intersection.y = (p1Dir.y / p1Dir.x) * (intersection.x - p1.x) + p1.y;

					t1 = (intersection.x - p1.x) / p1Dir.x;
					t2 = (intersection.y - p2.y) / p2Dir.y;
                }
                else
                {
                    //線と辺の始点と方向ベクトルの値を使って交点を計算
                    //式：
                    //point1 = 始点1、vec1 = 方向ベクトル1  /  point2 = 始点2、vec2 = 方向ベクトル2
                    // x = (((vec1.y / vec1.x) * point1.x) - ((vec2.y / vec2.x) * point2.x) + point2.y - point1.y) / ((vec1.y / vec1.x) - (vec2.y / vec2.x))
                    // y は上の式に得たxの値を代入する

                    float p1VecSlope = p1Dir.y / p1Dir.x;
					float p2VecSlope = p2Dir.y / p2Dir.x;

                    intersection.x = ((p2VecSlope * p2.x) - (p1VecSlope * p1.x) + p1.y - p2.y) / (p2VecSlope - p1VecSlope);
                    intersection.y = (p2VecSlope * (intersection.x - p2.x) + p2.y);

					t1 = (intersection.x - p1.x) / p1Dir.x;
					t2 = (intersection.x - p2.x) / p2Dir.x;
                }

				intersection.z = zValue;

				withinBorders = ((0.0f <= t1) && (t1 <= 1.0f) && (0.0f <= t2) && (t2 <= 1.0f));

				return withinBorders;
		}

		//レイヤーの差分によってoffsetを減算した座標を返す
		private Vector3 GetPointSubtractedByLayerDifference(in Vector3 pt, in Vector3 offset, in int layer1, in int layer2, in eFoldType type)
		{
			float dif = (float)GetIntegerDif(layer1, layer2);
            return pt - offset * dif;
		}

		//レイヤーの差分によってoffsetを加算した座標を返す
		private Vector3 GetPointAddedByLayerDifference(in Vector3 pt, in Vector3 offset, in int layer1, in int layer2, in eFoldType type)
        {
            float dif = (float)GetIntegerDif(layer1, layer2);
            return pt + offset * dif;
		}

		//メッシュのレイヤー情報を更新する際、山折か谷折りかによって計算式が少し異なるため、それぞれの式をラムダ式で返す
		private System.Func<int, int, int> GetUpdateLayerFunc(eFoldType type)
		{
			//ラムダ式は山折りか谷折りかで異なる
			if (type == eFoldType.MoutainFold)
			{
				//レイヤーの値は折られるレイヤーの中で最小の値からの距離に準じて設定する
				return (int x, int innerLayerNum) => innerLayerNum + (innerLayerNum - x) - 1;
			}
			else
			{
				//レイヤーの値は折られるレイヤーの中で最大の値からの距離に準じて設定する
				return (int x, int innerLayerNum) => innerLayerNum + (innerLayerNum - x) + 1;
			}
		}

		//MeshVertexのリストをソートする
		public MeshVertex[] SortMeshVertexArrayByLayer(in MeshVertex[] array, in eFoldType type)
		{
			if(type == eFoldType.MoutainFold)
			{
				return array.OrderBy(x => x.Layer).ToArray();
			}
			else
            {
                return array.OrderByDescending(x => x.Layer).ToArray();
			}
		}

		/// <summary>
		/// 折り紙のメッシュを生成する
		/// </summary>
		/// <param name="pt1"></param>
		/// <param name="pt2"></param>
		/// <param name="pt3"></param>
		/// <param name="isFacingUp"></param>
		/// <param name="layer"></param>
		/// <returns></returns>
		public OrigamiMesh GenerateOrigamiMesh(in Vector3 pt1, in Vector3 pt2, in Vector3 pt3, in bool isFacingUp, in int layer, IEnumerable<bool> isConnectedToCrease)
		{
			return new OrigamiMesh(GetVerticesSortedClockwise(pt1, pt2, pt3), isFacingUp, layer, isConnectedToCrease, m_MaterialPath, MeshParent);
		}

		public OrigamiMesh GenerateOrigamiMesh(OrigamiMesh mesh, in MeshVertex pt1, in MeshVertex pt2, in MeshVertex pt3)
		{
			var sorted = GetVerticesSortedClockwise(pt1, pt2, pt3);
			return new OrigamiMesh(sorted.Select((x) => (x.Vertex)).ToArray(), mesh.IsFacingUp, mesh.FoldLayer, sorted.Select((x) => x.IsConnectedToCrease), m_MaterialPath, MeshParent);
		}

		/// <summary>
		/// 折り目や折り紙など、隙間を埋めるためのメッシュ(最初は潰れた状態のもの)を生成する
		/// </summary>
		private void GenerateSquashedMeshes(List<SplitMeshInfo> splitInfoList, List<OrigamiMesh> fillerMeshParentList,
											in Vector3 startPoint, in Vector3 creaseDir, in Vector3 creaseDirNorm, in Vector3 perpendicularVec,
											in int innerLayer, in int outerLayer, in Matrix4x4 matZ, eFoldType type)
		{

			if(splitInfoList.Count == 0)
			{
				Debug.LogError("No creasese to generate");
				return;
			}

            //折り目の始点に最も近い座標と最も遠い座標を取得する
            //midPointListをレイヤーの情報によってソートする
            splitInfoList = splitInfoList.OrderBy(x => x.NonFoldMesh.FoldLayer).ToList();

			//同様にこちらもソート
			fillerMeshParentList = fillerMeshParentList.OrderByDescending(x => x.FoldLayer).ToList();

			//ループ用の変数
			int start, end;

			start = splitInfoList[0].NonFoldMesh.FoldLayer;
            end = splitInfoList[splitInfoList.Count - 1].NonFoldMesh.FoldLayer + 1;

            //折り目の種類によってX軸の行列を変える
            Matrix4x4 matX;
			//折る角度をeFoldTypeで判定
			float startAngle, targetAngle;

            if (type == eFoldType.MoutainFold)
            {
                matX = OrigamiUtility.XROTATION_MAT__90DEG;
                startAngle = 0f;
                targetAngle = OrigamiUtility.HALF_PI;
            }
            else
            {
                matX = OrigamiUtility.XROTATION_MAT__270DEG;
				startAngle = OrigamiUtility.TWO_PI;
				targetAngle = OrigamiUtility.TWO_PI - OrigamiUtility.HALF_PI;
            }
			//genInfoListにアクセスするための添え字番号
			int idx = 0;

			//親リストにアクセスするための添字
			int parentIdx = 0;

			//折る側から見て最奥のレイヤーの値が折られた(更新された)後の値を求める
			int newOuter = GetUpdatedLayerValueOfOuterLayer(innerLayer, outerLayer, type);

			for (int layer = start; layer != end; ++layer)
			{
				if(idx >= splitInfoList.Count) break;

				//レイヤー値ごとにソートしてあるので、レイヤー値と一致しない場合、そのレイヤーのメッシュとの交点がないということになるのでcontinue
				if(layer != splitInfoList[idx].NonFoldMesh.FoldLayer) continue;

                //折り目のレイヤー情報を取得. 新しいレイヤーの値とinnerLayerとの差分から分割前までのレイヤーの値を取得
                int dif, originalLayer;
                if (type == eFoldType.MoutainFold)
                {
                    dif = layer - (innerLayer - 1);
                    originalLayer = innerLayer - dif;
                }
                else
                {
                    dif = layer - (innerLayer + 1);
                    originalLayer = innerLayer - dif;
                }

				//今回のゲームに関しては問題ないが、仮に同じレイヤー情報を持つが、紙として繋がっていないメッシュが両方とも折る対象になった場合
				//例えば、端と端を少し折った状態
				//このやり方だと上手くいかないので、変える必要がある
				SplitMeshInfo closest = splitInfoList[idx], furthest = splitInfoList[idx];

				float furthestDis = 0.0f;

				do
				{
					var dis = (splitInfoList[idx].Vertex - startPoint).sqrMagnitude;
					var closestDis = (closest.Vertex - startPoint).sqrMagnitude;
					
					if(dis < closestDis)
					{
						closest = splitInfoList[idx];
					}

					furthestDis = (furthest.Vertex - startPoint).sqrMagnitude;

					if(Mathf.Abs(dis - furthestDis) <= OrigamiUtility.ALLOWABLE_MARGIN_LOW_PRECISION)
					{
                        //ここで最も近い距離にあるメッシュの判定を行う
                        //判定方法としては三角メッシュの重心を求め、m_CreaseVecNormalizedとの内積で値が "1" に近い方を判定する
						var furthestVertices = furthest.NonFoldMesh.MeshVertices.GetMeshVertices();
						var furthestCenter = new Vector2((furthestVertices[0].x + furthestVertices[1].x + furthestVertices[2].x) / 3f, (furthestVertices[0].y + furthestVertices[1].y + furthestVertices[2].y) / 3f);
						var genVertices = splitInfoList[idx].NonFoldMesh.MeshVertices.GetMeshVertices();
						var genCenter = new Vector2((genVertices[0].x + genVertices[1].x + genVertices[2].x) / 3f, (genVertices[0].y + genVertices[1].y + genVertices[2].y) / 3f);

						var fDir = (furthestCenter - (Vector2)furthest.Vertex).normalized;
						var gDir = (genCenter - (Vector2)furthest.Vertex).normalized;

                        var fDot = Vector2.Dot(creaseDirNorm, fDir);
                        var gDot = Vector2.Dot(creaseDirNorm, gDir);

                        if(gDot > fDot) furthest = splitInfoList[idx];	
					}
					else if (dis > furthestDis)
					{
						furthest = splitInfoList[idx];
					}

				} while(layer == splitInfoList[idx++].NonFoldMesh.FoldLayer && idx < splitInfoList.Count);

				//Vector3 creaseOffset = splitInfoList[idx - 1].OrigamiResults.CreaseOffset;

				//新しい折り目と、折る時に使う情報を追加
				//var crease = new Crease();


				//CreaseGenerateResults[] results;

                //bool facing = type == eFoldType.MoutainFold ? !closest.NonFoldMesh.IsFacingUp : closest.OrigamiMesh.IsFacingUp;

                //レイヤーの差分が1以上ある場合はZファイティング対策でずらしたいため、処理を分ける
				// MeshVertex vx1, vx2, vx3, vx4;
                // if (newOuter == layer)
                // {
                //     vx1 = new MeshVertex(closest.Result.FoldResult.FoldOriginalPoint0, originalLayer, false);
				// 	vx2 = new MeshVertex(furthest.Result.FoldResult.FoldOriginalPoint0, originalLayer, false);
                //     vx3 = new MeshVertex(furthest.Result.FoldResult.FoldOriginalPoint90, furthest.OrigamiMesh.FoldLayer, false);
				// 	vx4 = new MeshVertex(closest.Result.FoldResult.FoldOriginalPoint90, closest.OrigamiMesh.FoldLayer, false);
                // }
				// else
				// {


                //     vx1 = new MeshVertex(closest.Result.FoldResult.FoldOriginalPoint0, originalLayer, false);
                //     vx2 = new MeshVertex(new Vector3(vertex.Vertex.x, vertex.Vertex.y, furthest.Result.FoldResult.FoldOriginalPoint0.z), originalLayer, false);
                //     vx3 = new MeshVertex(new Vector3(vertex.Vertex.x, vertex.Vertex.y, furthest.Result.FoldResult.FoldOriginalPoint90.z), furthest.OrigamiMesh.FoldLayer, false);
                //     vx4 = new MeshVertex(closest.Result.FoldResult.FoldOriginalPoint90, closest.OrigamiMesh.FoldLayer, false);

					//頂点が折り目に接している場合隙間が生じるため、埋める
					// if(vertex.IsConnectedToCrease)
					// {
					// 	var fillerCrease = new Crease();
					// 	CreaseGenerateResults[] fillerResults;

					// 	var vx5 = new MeshVertex(furthest.Result.FoldResult.FoldOriginalPoint0, originalLayer, false);
					// 	var vx6 = new MeshVertex(furthest.Result.FoldResult.FoldOriginalPoint90, furthest.OrigamiMesh.FoldLayer, false);

					// 	var sorted = GetCreaseOrderedVertices(vx2, vx5, vx6, vx3);

					// 	fillerResults = fillerCrease.GenerateSquashedCrease(sorted, originalLayer, furthest.OrigamiMesh.FoldLayer, facing, creaseOffset, startAngle, targetAngle, m_MaterialPath, MeshParent);

					// 	m_GeneratedCreaseGroup.Add(fillerCrease);
					// 	m_CreaseGenerateResults.Add(fillerResults);
                    // }

                    //隙間メッシュを生み出す親を探す
                    // if (fillerMeshParentList.Count > 0 && fillerMeshParentList.Count > parentIdx)
                    // {

                    //     //隙間このままだと、紙に隙間が生じるため、それを埋めるメッシュを生成する
                    //     OrigamiMesh fillerParent = fillerMeshParentList[parentIdx];

                    //     var update = GetUpdateLayerFunc(type);

					// 	//そもそも fillerMeshParentList[parentIdx]はメッシュを生成しても大丈夫なレイヤー値なのか
					// 	bool isValid = layer == update(fillerMeshParentList[parentIdx].FoldLayer, innerLayer);

                    //     while (layer == update(fillerMeshParentList[parentIdx].FoldLayer, innerLayer))
                    //     {
                    //         for (int i = 0; i < fillerMeshParentList[parentIdx].MeshVertices.ConnectedToCreaseList.Count; ++i)
                    //         {
                    //             if (!fillerMeshParentList[parentIdx].MeshVertices.ConnectedToCreaseList[i]) continue;

                    //             var dis = (fillerMeshParentList[parentIdx].MeshVertices.Vertices[i] - startPoint).sqrMagnitude;

                    //             if (Mathf.Abs(dis - furthestDis) <= OrigamiUtility.ALLOWABLE_MARGIN_LOW_PRECISION)
                    //             {
                    //                 //ここで最も近い距離にあるメッシュの判定を行う
                    //                 //判定方法としては三角メッシュの重心を求め、m_CreaseVecNormalizedとの内積で値が "1" に近い方を判定する
                    //                 var furthestVertices = furthest.OrigamiMesh.MeshVertices.Vertices;
                    //                 var furthestCenter = new Vector2((furthestVertices[0].x + furthestVertices[1].x + furthestVertices[2].x) / 3f, (furthestVertices[0].y + furthestVertices[1].y + furthestVertices[2].y) / 3f);
                    //                 var parVertices = fillerMeshParentList[parentIdx].MeshVertices.Vertices;
                    //                 var parCenter = new Vector2((parVertices[0].x + parVertices[1].x + parVertices[2].x) / 3f, (parVertices[0].y + parVertices[1].y + parVertices[2].y) / 3f);

                    //                 var fDir = (furthestCenter - (Vector2)furthest.Result.FoldResult.FoldOriginalPoint0).normalized;
                    //                 var pDir = (parCenter - (Vector2)furthest.Result.FoldResult.FoldOriginalPoint0).normalized;

                    //                 var fDot = Vector2.Dot(creaseDirNorm, fDir);
                    //                 var pDot = Vector2.Dot(creaseDirNorm, pDir);

                    //                 if (pDot > fDot) fillerParent = fillerMeshParentList[parentIdx];
                    //             }
                    //         }

                    //         if (++parentIdx >= fillerMeshParentList.Count) break;
                    //     }

                    //     if (isValid)
                    //     {
                    //         //隙間の頂点
                    //         MeshVertex pt1, pt2, pt3;
                    //         pt1 = new MeshVertex(furthest.Result.FoldResult.FoldOriginalPoint0, originalLayer, furthest.Result.IsConnectedToOtherMesh);
                    //         pt2 = new MeshVertex(new Vector3(vertex.Vertex.x, vertex.Vertex.y, furthest.Result.FoldResult.FoldOriginalPoint0.z), originalLayer, false);

                    //         //3つ目の座標は内積で求める。他にも方法はあると思う
                    //         var pointCandidates = new List<Vector3>();
                    //         var vertices = fillerParent.MeshVertices;
                    //         for (int i = 0; i < vertices.ConnectedToCreaseList.Count; ++i) if (!vertices.ConnectedToCreaseList[i]) pointCandidates.Add(vertices.Vertices[i]);

                    //         if (pointCandidates.Count == 1)
                    //         {
                    //             pt3 = new MeshVertex(pointCandidates[0], originalLayer, false);
                    //         }
                    //         else
                    //         {
                    //             Vector3 basePt = pt1.Vertex;
                    //             Vector3 baseDir = (pt2.Vertex - pt1.Vertex).normalized;

                    //             var dir0 = (pointCandidates[0] - basePt).normalized;
                    //             var dot0 = Vector2.Dot(baseDir, dir0);

                    //             var dir1 = (pointCandidates[1] - basePt).normalized;
                    //             var dot1 = Vector2.Dot(baseDir, dir1);

                    //             if (dot0 < dot1) pt3 = new MeshVertex(pointCandidates[0], originalLayer, false);
                    //             else pt3 = new MeshVertex(pointCandidates[1], originalLayer, false);

                    //         }

                    //         OrigamiMesh fillerMesh = new OrigamiMesh(new List<Vector3>(3) { pt1.Vertex, pt2.Vertex, pt3.Vertex }, furthest.OrigamiMesh.IsFacingUp, originalLayer, new List<bool>(3) { pt1.IsConnectedToCrease, pt2.IsConnectedToCrease, pt3.IsConnectedToCrease }, m_MaterialPath, MeshParent);

                    //         m_AllOrigamiMeshGroup.Add(fillerMesh);
                    //     }
                    // }
                //}


                //var orderedVertices = GetCreaseOrderedVertices(vx1, vx2, vx3, vx4);
                //results = crease.GenerateSquashedCrease(orderedVertices, originalLayer, closest.OrigamiMesh.FoldLayer, facing, creaseOffset, startAngle, targetAngle, m_MaterialPath, MeshParent);

				var crease = new Crease();

                // 折る時、法線ベクトルが他のメッシュと同じとなるようにしたいので、頂点を以下のように配置する
                // 0折るのclosest   -   1折るのfurthest
                //  |						|
                // 3折らぬのclosest     2折らぬのfurthest
                // という順番に配置する
                crease.GenerateCreaseMesh(new List<CreaseVertex>(){ new CreaseVertex(closest.Vertex, closest.FoldMesh, closest.FoldIdx), new CreaseVertex(furthest.Vertex, furthest.FoldMesh, furthest.FoldIdx),
                                                                        new CreaseVertex(furthest.Vertex, furthest.NonFoldMesh, furthest.NonFoldIdx), new CreaseVertex(closest.Vertex, closest.NonFoldMesh, closest.NonFoldIdx)},
                                                                    closest.NonFoldMesh.IsFacingUp, m_MaterialPath, MeshParent);



                m_AllCreaseGroup.Add(crease);
				//m_CreaseGenerateResults.Add(results);
			}
		}

		/// <summary>
		/// メッシュを生成する時、代入する頂点の順番が重要となるので、その順番にEnumerableを返す
		///	Unityは左手座標系なので、時計回りとなるように返す
		/// </summary>
		/// <param name="vec1 / vec2 / vec3"> 各頂点 </param>
		/// <returns></returns>
		public static Vector3[] GetVerticesSortedClockwise(Vector3 vec1, Vector3 vec2, Vector3 vec3)
		{
			var vec21 = vec2 - vec1;
			var vec31 = vec3 - vec1;

			//z値はいらないので二次元外積で済ます
			var cross = Cross2DXY(vec21, vec31);

			if(cross > 0)
			{
				return new Vector3[3] { vec1, vec3, vec2 };
			}
			else
			{
				return new Vector3[3] { vec1, vec2, vec3 };
			}
		}

		//OrigamiMeshで、connectedToMeshフラグを持っていたかったため
		public static MeshVertex[] GetVerticesSortedClockwise(MeshVertex vec1, MeshVertex vec2, MeshVertex vec3)
		{
			var vec21 = vec2.Vertex - vec1.Vertex;
			var vec31 = vec3.Vertex - vec1.Vertex;

			//z値はいらないので二次元外積で済ます
			var cross = Cross2DXY(vec21, vec31);

			if(cross > 0)
			{
				return new MeshVertex[3] { vec1, vec3, vec2 };
			}
			else
			{
				return new MeshVertex[3] { vec1, vec2, vec3 };
			}
		}

		//折り目で使う4つの座標が四角として扱えるように並べ替える
		private static Vector3[] GetCreaseOrderedVertices(MeshVertex mVec1, MeshVertex mVec2, MeshVertex mVec3, MeshVertex mVec4)
		{
			Vector3 vec1 = mVec1.Vertex, vec2 = mVec2.Vertex, vec3 = mVec3.Vertex, vec4 = mVec4.Vertex;

			//z値が同じ値のペアで計算したいためスワップを行う
			if(mVec1.Layer != mVec2.Layer)
			{
				if(mVec1.Layer == mVec3.Layer)
				{
					OrigamiUtility.Swap(ref vec2, ref vec3);
				}
				else
				{
					OrigamiUtility.Swap(ref vec2, ref vec4);
				}
			}


			var vec21 = (vec2 - vec1).normalized;
			var vec31 = (vec3 - vec1).normalized;
			var vec41 = (vec4 - vec1).normalized;

			//外積で左右判定
			var cross1 = Cross2DXZ(vec21, vec31);
			var cross2 = Cross2DXZ(vec21, vec41);

			// Debug.Log("Cross1: " + cross1 + ". Cross2: " + cross2);

			//vec31とvec41がvec21 より左側にある場合
			if(cross1 > 0 && cross2 > 0)
            {
                //vec31とvec41を内積で角度判定
                var dot1 = Vector3.Dot(vec21, vec31);
                var dot2 = Vector3.Dot(vec21, vec41);

				//Debug.Log("Both on left");

				//vec3がvec21とvec41の間にある
				if(dot1 > dot2) return new Vector3[4] { vec2, vec1, vec4, vec3 };
				//vec4がvec21とvec31の間にある
				else return new Vector3[4] {vec2, vec1, vec3, vec4 };
			}
			//vec31はvec21 より左側にあるが、vec41は右側に場合
			else if(cross1 > 0 && cross2 <= 0)
			{
				//Debug.Log("One left one right");
				return new Vector3[4] {vec1, vec3, vec2, vec4 };
			}
			//vec31はvec21 より右側にあるが、vec41は左側に場合
			else if(cross1 <= 0 && cross2 > 0)
			{
				//Debug.Log("One right one left");
				return new Vector3[4] {vec1, vec4, vec2, vec3 };
			}
			//vec31とvec41がvec21 より右側にある場合
			else
            {
                //vec31とvec41を内積で角度判定
                var dot1 = Vector3.Dot(vec21, vec31);
                var dot2 = Vector3.Dot(vec21, vec41);

				//Debug.Log("Both on right");
				//vec3がvec21とvec41の間にある
				if(dot1 > dot2) return new Vector3[4] { vec1, vec2, vec3, vec4 };
				//vec4がvec21とvec31の間にある
				else return new Vector3 [4] {vec1, vec2, vec4, vec3 };
			}
		}

		/// <summary>
		/// 折られる前の状態において、折られる対象となるメッシュの中で最も大きなレイヤーの数値を求める
		/// </summary>
		/// <returns name="innerLayer">現在管理しているメッシュの中でレイヤーの数値が山折りであれば最も小さいもの、谷折りであれば最も大きいもの。つまり、折る側から見て最も手前にあるレイヤーを指す</returns>
		/// <return name="outerLayer"> 現在管理しているメッシュの中でレイヤーの数値が山折りであれば最も大きいもの、谷折りであれば最も小さいもの。つまり、折る側から見て最も奥にあるメッシュのレイヤーを指す</returns>
		/// <returns name="hasFoldTarget">そもそも折る対象がない場合はfalseを返す.空間を折る現象を避ける</returns>
		private (int innerLayer, int outerLayer, bool hasFoldTarget) GetPreFoldInfo(eFoldType type, in Vector3 startCreasePoint, in Vector3 endCreasePoint,
				 out List<Vector3> cachedStartPoints, out Vector3 outDir, out Vector3 outDirNorm, out Vector3 outPerpendicular, out Matrix4x4 outMatZ)
		{
			
			//折る対象の有無を確認するフラグ
			bool hasFoldTarget = false;

			//キャッシュリストの初期化
			cachedStartPoints = new List<Vector3>();
			//1つ目は引数のstartCreasePoint
			cachedStartPoints.Add(startCreasePoint);

			//メッシュをレイヤーでソートしたコレクションを取得
			List<OrigamiMesh> orderedMeshes;
			if(type == eFoldType.MoutainFold)
			{
				orderedMeshes = m_AllOrigamiMeshGroup.OrderByDescending(x => x.FoldLayer).ToList();
			}
			else
            {
                orderedMeshes = m_AllOrigamiMeshGroup.OrderBy(x => x.FoldLayer).ToList();
            }

            {
				//最も外側に属するレイヤーの方向ベクトルと垂直ベクトルはstartCreasePointとendCreasePointで得られるので求める

                //折り目の線に垂直なベクトルを求める.
                //垂直ということは内積すると0のベクトルなので
                //vec・perpendicularVec = 0 という式が出来る.
                var creaseDir = endCreasePoint - startCreasePoint;
                var creaseDirNormalized = creaseDir.normalized;

                Vector3 perpendicularVec = Vector3.zero;

				var absX = creaseDirNormalized.x < 0 ? -creaseDirNormalized.x : creaseDirNormalized.x;
				var absY = creaseDirNormalized.y < 0 ? -creaseDirNormalized.y : creaseDirNormalized.y;
                //垂直チェック. 基本折るのは左側というルールにしているので、マイナス記号はx成分に付ける
                if (OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION <= absX && OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION <= absY)
                {
                    perpendicularVec.x = -creaseDirNormalized.y;
                    perpendicularVec.y = creaseDirNormalized.x;
                }
                else if (absX < OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION)
                {
                    perpendicularVec.x = -creaseDirNormalized.y;
                }
                else
                {
                    perpendicularVec.y = creaseDirNormalized.x;
                }

                //折り目は小さくしたいのでとりあえず
                perpendicularVec *= CREASE_WIDTH;       //0.003fが理想?

				outDir = creaseDir;
				outDirNorm = creaseDirNormalized;
				outPerpendicular = perpendicularVec;
            }

            //Z軸に回転する行列を計算し、キャッシュ
            var rad = Mathf.Atan2(outDir.y, outDir.x);
			if(rad < 0.0f) rad += OrigamiUtility.TWO_PI;
            outMatZ = OrigamiUtility.GetZRotationMatrix(rad);

			//キャッシュリストの生成
			int end = GetIntegerDif(orderedMeshes.First().FoldLayer, orderedMeshes.Last().FoldLayer);
			//endは番兵にしたいので
			end += 1;

			//最初の座標は取得済みなので取らない
			for (int i = 1; i < end; ++i)
			{
				var startPoint = startCreasePoint + outPerpendicular * i;
				cachedStartPoints.Add(startPoint);
			}

			//正面から見て、最も奥側のレイヤー
			int outerLayer = 0;

			foreach (var mesh in orderedMeshes)
			{
				var vertices = mesh.MeshVertices.GetMeshVertices();
				//疑似外積
				double[] res = new double[3];
				GetNormalizedCross2DResults(startCreasePoint, outDirNorm, vertices, out res, 3);

				//折る側(左側)のみにしかないメッシュ、または分割の対象となるメッシュ
				if (-OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION <= res[0] && -OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION <= res[1] && -OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION <= res[2] || 
				  !(OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION > res[0] && OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION > res[1] && OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION > res[2]))
				{
					outerLayer = mesh.FoldLayer;
					hasFoldTarget = true;
					break;
				}
			}

			//折る対象がないため終了
			if(!hasFoldTarget)
			{
				return (0, 0, hasFoldTarget);
			}

			var innerLayer = outerLayer;

			//各メッシュの座標と折り目の線の疑似外積を行い、折る側をm_foldGroupに格納する
			foreach (var mesh in orderedMeshes)
			{
				//無駄な比較は避ける
				if (mesh.FoldLayer == outerLayer || mesh.FoldLayer == innerLayer) continue;
				var vertices = mesh.MeshVertices.GetMeshVertices();

				//疑似外積
				GetNormalizedCross2DResults(startCreasePoint, outDirNorm, vertices, out double[] res, 3);

				//折る側(左側)のみにしかないメッシュ、または分割の対象となるメッシュ
				if (-OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION <= res[0] && -OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION <= res[1] && -OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION <= res[2] || 
					!(OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION > res[0] && OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION > res[1] && OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION > res[2]))
				{
					innerLayer = mesh.FoldLayer;
				}
			}

			return (innerLayer, outerLayer, hasFoldTarget);
		}

		//メッシュ分割時に取得する情報を生成して返す
		private SplitMeshInfo CreateSplitInfo(in OrigamiMesh nonFoldMesh, in int nonFoldIdx, in OrigamiMesh foldMesh, in int foldIdx)
		{
            if (nonFoldMesh.FoldLayer != foldMesh.FoldLayer)
            {
                throw new System.Exception($"Log: Cannot create Split info from meshes with different layer values");
            }

			if(0 > nonFoldIdx || nonFoldIdx >= nonFoldMesh.MeshVertices.ConnectedToCreaseList.Count || 0 > foldIdx || foldIdx >= foldMesh.MeshVertices.ConnectedToCreaseList.Count)
			{
				throw new System.ArgumentOutOfRangeException($"Log: idx is out of range. idx1 = {nonFoldIdx}, idx2 = {foldIdx}");
			}

			var vec1 = nonFoldMesh.MeshVertices[nonFoldIdx];
			var vec2 = foldMesh.MeshVertices[foldIdx];

            // 頂点が同じ値か比較する
            // 将来的にはメッシュ一枚で管理していくため、この処理自体は不要となるが、今は必要となるので実装
            if ((vec1 - vec2).sqrMagnitude < OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION)
            {
                return new SplitMeshInfo(vec1, nonFoldMesh, nonFoldIdx, foldMesh, foldIdx);
            }
			else
			{
				throw new System.Exception($"Log: indices do not refer to same vertex. mesh1.MeshVertices[{nonFoldIdx}] = {vec1}, mesh2.MeshVertices[{foldIdx}] = {vec2}.");
			}
		}

		//メッシュを分割時に取得する情報を生成して返す。mesh2の添字が分からない場合に使う
		private SplitMeshInfo CreateSplitInfo(in OrigamiMesh nonFoldMesh, in int nonFoldIdx, in OrigamiMesh foldMesh)
        {
			if(nonFoldMesh.FoldLayer != foldMesh.FoldLayer)
			{
				throw new System.Exception($"Log: Cannot create Split info from meshes with different layer values");
			}

            if (0 > nonFoldIdx || nonFoldIdx >= nonFoldMesh.MeshVertices.ConnectedToCreaseList.Count)
            {
                throw new System.ArgumentOutOfRangeException($"Log: idx is out of range. idx1 = {nonFoldIdx}");
            }

			var vec1 = nonFoldMesh.MeshVertices[nonFoldIdx];
			
			int idx = -1;

			for(int i = 0; i < foldMesh.MeshVertices.ConnectedToCreaseList.Count; ++i)
			{
				//折り目に接していない場合はスキップ
				if(!foldMesh.MeshVertices.ConnectedToCreaseList[i]) continue;

				var vec = foldMesh.MeshVertices[i];

				if((vec1 - vec).sqrMagnitude < OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION)
				{
					idx = i;
					break;
				}
			}

			if(-1 == idx)
			{
				throw new System.Exception($"Log: Failed searching for index of vertex {vec1} in {foldMesh.MeshObject.name}. Vertices of {foldMesh.MeshObject.name} are: {foldMesh.MeshVertices[0]}, {foldMesh.MeshVertices[1]}, {foldMesh.MeshVertices[2]}");
			}
			else
			{
				return new SplitMeshInfo(vec1, nonFoldMesh, nonFoldIdx, foldMesh, idx);
			}
		}

		//メッシュを分割時に取得する情報を生成して返すどちらの添字も分からない場合に用いる
		private List<SplitMeshInfo> CreateSplitInfo(in OrigamiMesh nonFoldMesh, in OrigamiMesh foldMesh, bool canThrowException)
		{
			var splitList = new List<SplitMeshInfo>();

			//メッシュのレイヤー値が異なる場合、折り目を作れないので、空の状態で返す
			if(nonFoldMesh.FoldLayer != foldMesh.FoldLayer)
			{
				if(canThrowException) throw new System.Exception($"Log: Cannot create Split info from meshes with different layer values");
				return splitList;
			}

			for(int i = 0; i < nonFoldMesh.MeshVertices.ConnectedToCreaseList.Count; ++i)
				for(int k = 0; k < foldMesh.MeshVertices.ConnectedToCreaseList.Count; ++k)
				{
					if((nonFoldMesh.MeshVertices[i] - foldMesh.MeshVertices[k]).sqrMagnitude < OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION)
					{
						splitList.Add(new SplitMeshInfo(nonFoldMesh.MeshVertices[i], nonFoldMesh, i, foldMesh, k));
					}
				}

			if(splitList.Count == 0 && canThrowException) throw new System.Exception($"Log: Failed to find any vertices shared between {nonFoldMesh.MeshObject.name} and {foldMesh.MeshObject.name}");

			return splitList;
		}

        /// <summary>
        /// 折紙を折るために各メッシュを必要に応じて分割する
        /// </summary>
        /// <param name="baseCreasePoint">折り目の線の座標1</param>
        private bool SplitMesh(in Vector3 startPoint, in Vector3 creaseDir, in Vector3 creaseDirNorm, in List<Vector3> startPointList, in int innerLayer, in int outerLayer, 
							   out Vector3 outClosestIntersection, out MeshVertex[] outFurthestIntersectionsPerLayerOnCrease, out MeshVertex[] outFurthestAlteredIntersections, out List<SplitMeshInfo> outSplitInfo)
		{			
			//新たに追加するメッシュのリスト
			var newMeshList = new List<OrigamiMesh>();
			//メッシュを
			outSplitInfo = new List<SplitMeshInfo>();

			outClosestIntersection = startPoint + creaseDir;
			int layerDif = GetIntegerDif(innerLayer, outerLayer) + 1;
			outFurthestIntersectionsPerLayerOnCrease = new MeshVertex[layerDif];
			outFurthestAlteredIntersections = new MeshVertex[layerDif];

			int inc = innerLayer > outerLayer ? 1 : -1;

			for(int i = 0; i < layerDif; ++i)
			{
				outFurthestIntersectionsPerLayerOnCrease[i] = new MeshVertex(startPoint, outerLayer + inc * i, true);
				outFurthestAlteredIntersections[i] = new MeshVertex(startPoint, outerLayer + inc * i, true);
			}

			// 辺が折り目と重なっているメッシュは折り目の方向ベクトルが二つのメッシュのちょうど間を通っているのか、紙の端に接しているのかが判断できないので、異なるロジックでチェックする
			// その時、折る側か折らない側か判断できるようにしたいので、タプルのリストで管理する
			var meshesWithOneSideOverCrease = new List<(double res, OrigamiMesh mesh)>();

			// 二つのメッシュが同じ頂点を持っていることが確定しており、その添字がどちらも0ではないことが確定している場合に使う匿名メソッド
			(int lIdx, int rIdx) getIndices1or2 (OrigamiMesh mesh1, OrigamiMesh mesh2) => (mesh1.MeshVertices.ConnectedToCreaseList[1] ? 1 : 2, mesh2.MeshVertices.ConnectedToCreaseList[1] ? 1 : 2);

			//各メッシュの座標と折り目の線の疑似外積を行い、メッシュを折る(分割する)必要があるのかを判定する
			foreach (var mesh in m_AllOrigamiMeshGroup)
			{
				var vertices = mesh.MeshVertices.GetMeshVertices();

				GetNormalizedCross2DResults(startPoint, creaseDirNorm, vertices, out double[] res, 3);

				//全ての座標が線の左右一方にのみある場合は、分割する必要がない
				if ((-OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION < res[0] && -OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION < res[1] && -OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION < res[2]) || 
					(OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION > res[0] && OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION > res[1] && OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION > res[2]))
				{
					//Debug.Log($"Mesh: {mesh.MeshObject.name} does not need to be split");
					//リセット処理を行う
					mesh.ResetCreaseConnectionFlags();
					continue;
				}

				//結果が0に極めて近い、つまり座標が線上にある場合はまた別の処理をする必要がある
				if ((-OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION <= res[0] && res[0] <= OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION) || 
					(-OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION <= res[1] && res[1] <= OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION) || 
					(-OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION <= res[2] && res[2] <= OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION))
				{
					//線上の座標が一つあるか2つあるかで分けるか分けないかが変わるので添字を調べる
					List<int> resIsZero = new List<int>();
					List<int> resIsNonZero = new List<int>();
					for (int i = 0; i < res.Length; i++)
					{
						if (-OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION <= res[i] && res[i] <= OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION) resIsZero.Add(i);
						else resIsNonZero.Add(i);
					}

					if(resIsZero.Count + resIsNonZero.Count != 3)
					{
						Debug.LogError("resIsZero.Count + resIsNonZero.Count != 3");
						Debug.Log("resIsZero.Count = " + resIsZero.Count);
						Debug.Log("resIsNonZero.Count = " + resIsNonZero.Count);
					}

					if (resIsZero.Count == 2)
					{
						MeshVertex vx1 = new MeshVertex(vertices[resIsZero[0]], mesh.FoldLayer, true);
						MeshVertex vx2 = new MeshVertex(vertices[resIsZero[1]], mesh.FoldLayer, true);
						MeshVertex vx3 = new MeshVertex(vertices[resIsNonZero[0]], mesh.FoldLayer, false);
						
						int dif = GetIntegerDif(mesh.FoldLayer, outerLayer);

                        foreach (var zero in resIsZero) SetClosestAndFurthestVertices(startPoint, vertices[zero], ref outClosestIntersection, ref outFurthestIntersectionsPerLayerOnCrease[dif]);
						bool connected = mesh.MeshVertices.ConnectedToCreaseList[resIsZero[0]] & mesh.MeshVertices.ConnectedToCreaseList[resIsZero[1]];
						UpdateIfFurtherFromPoint(startPoint, vertices[resIsZero[0]], ref outFurthestAlteredIntersections[dif], connected);
						UpdateIfFurtherFromPoint(startPoint, vertices[resIsZero[1]], ref outFurthestAlteredIntersections[dif], connected);

						mesh.UpdateOrigamiTriangleMesh(GetVerticesSortedClockwise(vx1, vx2, vx3));

						// splitInfoは二つのメッシュがないと成り立たないが、この場合、メッシュが折り目に接しているのか、
						// 判断出来ない。従って異なる異なるサーチを行う対象とする
						meshesWithOneSideOverCrease.Add((res[resIsNonZero[0]], mesh));

						continue;
					}
					else if (resIsZero.Count == 1)			//0に極めて近いresが1つなら2つのメッシュに分割すればいいので中点を求める
					{
						#region DivideToTwoTriangles
						//Debug.Log($"Dividing {mesh.MeshObject.name} into 2 triangles");

						var vecOfVertices = vertices[resIsNonZero[0]] - vertices[resIsNonZero[1]];

						if(GetIntersectionPointForDirections(startPoint, creaseDir, vertices[resIsNonZero[1]], vecOfVertices, vertices[resIsNonZero[1]].z, out Vector3 midPoint))
                        {
							//交点は折り目を元に生成しているため、必ず繋がっている
							var mid = new MeshVertex(midPoint, mesh.FoldLayer, true);
							var vx1 = new MeshVertex(vertices[resIsZero[0]], mesh.FoldLayer, true);
							var vx2 = new MeshVertex(vertices[resIsNonZero[0]], mesh.FoldLayer, false);
							var vx3 = new MeshVertex(vertices[resIsNonZero[1]], mesh.FoldLayer, false);

                            // 折った時に折り目が重なるとZファイティングを起こすため、それをずらすための頂点の候補を算出する
                            // もし、ここで判定がfalseとなった場合、折る側にあるメッシュの範囲が狭すぎることを意味するため、その時点でfalseを返して分割を終了する
                            //まずメッシュのレイヤーとouterLayerの差分を取得する。0だと不要なのでスキップ
                            var dif = GetIntegerDif(mesh.FoldLayer, outerLayer);

                            //resIsZeroとはつまり、この座標も交点であることを意味するので
                            SetClosestAndFurthestVertices(startPoint, vertices[resIsZero[0]], ref outClosestIntersection, ref outFurthestIntersectionsPerLayerOnCrease[dif]);

                            // 折り目に接したメッシュの頂点を調整するために、少しずらした交点も求める。差が0の場合、midPointと同じ結果が返されるため、midPointで更新チェックを掛ける
                            bool connected = mesh.MeshVertices.ConnectedToCreaseList[resIsNonZero[0]] & mesh.MeshVertices.ConnectedToCreaseList[resIsNonZero[1]];
							if(dif == 0)
							{
								UpdateIfFurtherFromPoint(startPoint, midPoint, ref outFurthestAlteredIntersections[dif], connected);
							}
                            else if (GetIntersectionPointForDirections(startPointList[dif], creaseDir, vertices[resIsNonZero[1]], vecOfVertices, 0, out Vector3 point))
                            {
                                UpdateIfFurtherFromPoint(startPoint, point, ref outFurthestAlteredIntersections[dif], connected);
                            }

                            //新しいメッシュを生成
                            OrigamiMesh newMesh = GenerateOrigamiMesh(mesh, vx1, mid, vx2);
                            newMeshList.Add(newMesh);

                            //現在あるメッシュを三角の一つとして使い回す
                            mesh.UpdateOrigamiTriangleMesh(GetVerticesSortedClockwise(vx1, mid, vx3));

                            //交点を格納
							// GetVerticesSortedClockwise(GenerateOrigamiMesh) は第1引数(vx1)から見て、第2と第3引数が時計回りとなるように順番を決めるので、0は共通となる
							OrigamiMesh foldMesh, nonFoldMesh;
							//res[resIsNonZero[0]] が正の数であれば、newMeshがfoldMeshとなり、meshがnonFoldMeshとなる。負なら逆
							if(res[resIsNonZero[0]] > -OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION)
							{
								foldMesh = newMesh;
								nonFoldMesh = mesh;
							}
							else
							{
								foldMesh = mesh;
								nonFoldMesh = newMesh;
							}

                            outSplitInfo.Add(CreateSplitInfo(nonFoldMesh, 0, foldMesh, 0));
							// ここで探したいのはmidの行方だが、折り目に接していることは確定しているので、フラグチェックで済ませる
							var indices = getIndices1or2(nonFoldMesh, foldMesh);
							outSplitInfo.Add(CreateSplitInfo(nonFoldMesh, indices.lIdx, foldMesh, indices.rIdx));

                            //outClosest を必要あれば更新
                            SetClosestAndFurthestVertices(startPoint, midPoint, ref outClosestIntersection, ref outFurthestIntersectionsPerLayerOnCrease[dif]);
                        }

                        #endregion
                    }
					else
					{
						//ここに来る場合、頂点が3つとも折り目上にあるということになる
						Debug.LogError("All points are found on crease");
						for (int i = 0; i < res.Length; i++)
						{
							Debug.Log($"res[{i}] = {res[i]}");
							Debug.Log($"vertices[{i}] = {vertices[i]}");
						}

						Debug.Log($"creaseDir = {creaseDir}");
						Debug.Log($"startPoint = {startPoint}");
					}
				}
				else
				{
					#region DivideToThreeTriangles
					//線がメッシュのどの辺と交わっているのかを調べる
					//外積の結果の正負によってそれぞれの頂点を分ける
					List<MeshVertex> positiveResList = new List<MeshVertex>(2);
					List<MeshVertex> negativeResList = new List<MeshVertex>(2);

					//折り目の左右で、頂点が一つしかない側を調べたいため、添字も格納する。一つしかない側が欲しいため、二つある側の添字はどちらでもよいものとする
					int negIdx = -1, posIdx = -1;
					for (int i = 0; i < res.Length; i++)
					{
						if (res[i] > 0) 
						{
							positiveResList.Add(mesh.MeshVertices.GetMeshVertexAt(i));
							posIdx = i;
						}
						else
						{
							negativeResList.Add(mesh.MeshVertices.GetMeshVertexAt(i));
							negIdx = i;
						}
					}

					//合計が3以外だとおかしいので、エラーを吐く
					if (positiveResList.Count + negativeResList.Count != 3)
					{
						Debug.LogError("Expected sum of results is not 3. Actual Result sum: " + (positiveResList.Count + negativeResList.Count));
						return false;
					}
					else if(positiveResList.Count == 0 || negativeResList.Count == 0)
                    {
                        Debug.LogError("Split error: Positive Result Count: " + positiveResList.Count + " Negative Result Count: " + negativeResList.Count);
                        return false;
					}

					//線が三角を横切る際、一方に座標が二つ、反対側に座標が一つとなるので、上で取得したリストから得る
					MeshVertex[] verticesOfNotSplitSegment;
					MeshVertex vertexOfSplitSegments;

					//頂点が一点しかない側の添字
					int idxOfSplitSegments;

					if (positiveResList.Count > negativeResList.Count)
					{
						verticesOfNotSplitSegment = new MeshVertex[2] { positiveResList[0], positiveResList[1] };
						vertexOfSplitSegments = negativeResList[0];
						idxOfSplitSegments = negIdx;
					}
					else
					{
						verticesOfNotSplitSegment = new MeshVertex[2] { negativeResList[0], negativeResList[1] };
						vertexOfSplitSegments = positiveResList[0];
						idxOfSplitSegments = posIdx;
					}

					//その交点を求める
					//交点を格納する配列
					MeshVertex[] midPoints = new MeshVertex[2];

                    // 折った時に折り目が重なるとZファイティングを起こすため、それをずらすための頂点の候補を算出する
                    // もし、ここで判定がfalseとなった場合、折る側にあるメッシュの範囲が狭すぎることを意味するため、その時点でfalseを返して分割を終了する
                    //まずメッシュのレイヤーとouterLayerの差分を取得する。0だと不要なのでスキップ
                    var dif = GetIntegerDif(mesh.FoldLayer, outerLayer);

					//この時点で交わっていることは確定しているので、ベクトルのxもしくはy成分が両方0であるかなどといったチェックは行わない
					for (int i = 0; i < verticesOfNotSplitSegment.Length; i++)
					{
						//辺のベクトルを取得
						var dir = verticesOfNotSplitSegment[i].Vertex - vertexOfSplitSegments.Vertex;

						GetIntersectionPointForDirections(startPoint, creaseDir, vertexOfSplitSegments.Vertex, dir, vertexOfSplitSegments.Vertex.z, out Vector3 midPoint);

						midPoints[i] = new MeshVertex(midPoint, mesh.FoldLayer, true);

						SetClosestAndFurthestVertices(startPoint, midPoint, ref outClosestIntersection, ref outFurthestIntersectionsPerLayerOnCrease[dif]);

                        bool connected = verticesOfNotSplitSegment[i].IsConnectedToCrease & vertexOfSplitSegments.IsConnectedToCrease;
                        if(dif == 0)
						{
							UpdateIfFurtherFromPoint(startPoint, midPoint, ref outFurthestAlteredIntersections[dif], connected);
						}
						else if (GetIntersectionPointForDirections(startPointList[dif], creaseDir, vertexOfSplitSegments.Vertex, dir, 0, out Vector3 point))
                        {
                            UpdateIfFurtherFromPoint(startPoint, point, ref outFurthestAlteredIntersections[dif], connected);
                        }
                    }

					//新しいメッシュを2つ生成
					//頂点が近い方の交点を判定し、それを元にメッシュを作る
					OrigamiMesh mesh1, mesh2;

					//更新する前にフラグをリセット
					vertexOfSplitSegments = new MeshVertex(vertexOfSplitSegments.Vertex, mesh.FoldLayer, false);
					verticesOfNotSplitSegment[0] = new MeshVertex(verticesOfNotSplitSegment[0].Vertex, mesh.FoldLayer, false);
					verticesOfNotSplitSegment[1] = new MeshVertex(verticesOfNotSplitSegment[1].Vertex, mesh.FoldLayer, false);

					//mesh1はどちらの場合でも変わらないので決め打ち
					mesh1 = GenerateOrigamiMesh(mesh, midPoints[0], verticesOfNotSplitSegment[0], midPoints[1]);

					//角度判定
					{
						//軸となるベクトルとその大きさ
						var dir0To1 = (verticesOfNotSplitSegment[1].Vertex - verticesOfNotSplitSegment[0].Vertex);
						var vec01mag = Mathf.Sqrt(dir0To1.x * dir0To1.x + dir0To1.y * dir0To1.y);

						//中点0(midPoints[0])へのベクトルとなす角を求める
						var dir0ToMid0 = (midPoints[0].Vertex - verticesOfNotSplitSegment[0].Vertex);
						var cosTheta0 = (dir0To1.x * dir0ToMid0.x + dir0To1.y * dir0ToMid0.y) / (vec01mag * Mathf.Sqrt(dir0ToMid0.x * dir0ToMid0.x + dir0ToMid0.y * dir0ToMid0.y));

						////中点1(midPoints[1])へのベクトルとなす角を求める
						var dir0ToMid1 = (midPoints[1].Vertex - verticesOfNotSplitSegment[0].Vertex);
						var cosTheta1 = (dir0To1.x * dir0ToMid1.x + dir0To1.y * dir0ToMid1.y) / (vec01mag * Mathf.Sqrt(dir0ToMid1.x * dir0ToMid1.x + dir0ToMid1.y * dir0ToMid1.y));

						//cosθの値が大きい方、つまり角度が小さい方がどちらか判定する
						if (cosTheta0 > cosTheta1)
						{
							mesh2 = GenerateOrigamiMesh(mesh, midPoints[0], verticesOfNotSplitSegment[0], verticesOfNotSplitSegment[1]);
						}
						else
						{
							mesh2 = GenerateOrigamiMesh(mesh, midPoints[1], verticesOfNotSplitSegment[0], verticesOfNotSplitSegment[1]);
						}
					}

					newMeshList.Add(mesh1);
					newMeshList.Add(mesh2);

					//現在あるメッシュを三角の一つとして使い回す
					mesh.UpdateOrigamiTriangleMesh( GetVerticesSortedClockwise(midPoints[0], vertexOfSplitSegments, midPoints[1]));

					//交点を格納する
					//二つに分割する時と同じく、resの値を調べる
					OrigamiMesh foldMesh, nonFoldMesh;
					if(res[idxOfSplitSegments] > -OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION)
					{
						foldMesh = mesh;
						nonFoldMesh = mesh1;
					}
					else
					{
						foldMesh = mesh1;
						nonFoldMesh = mesh;
					}

					outSplitInfo.Add(CreateSplitInfo(nonFoldMesh, 0, foldMesh, 0));
                    // ここで探したいのはmidの行方だが、折り目に接していることは確定しているので、フラグチェックで済ませる
                    var indices = getIndices1or2(nonFoldMesh, foldMesh);
                    outSplitInfo.Add(CreateSplitInfo(nonFoldMesh, indices.lIdx, foldMesh, indices.rIdx));

					#endregion
				}
			}

			//2個以上無ければ成立しないので
			if(meshesWithOneSideOverCrease.Count >= 2)
			{
				for(int i = 0; i < meshesWithOneSideOverCrease.Count - 1; ++i)
				{
					for(int k = i + 1; k < meshesWithOneSideOverCrease.Count; ++k)
					{
						//どちらも同じ折る/折らない側にある場合は調べる必要がない
						if(meshesWithOneSideOverCrease[i].res > 0 && meshesWithOneSideOverCrease[k].res > 0) continue;

						var infoList = CreateSplitInfo(meshesWithOneSideOverCrease[i].mesh, meshesWithOneSideOverCrease[k].mesh, false);
						if(infoList.Count > 0)
						{
							foreach(var info in infoList)
							{
								outSplitInfo.Add(info);
							}
						}
					}
				}
			}

			//新たに生成したメッシュがあればリストに追加
			if (newMeshList.Count > 0)
			{
				m_AllOrigamiMeshGroup.AddRange(newMeshList);
				return true;
			}
			else
            {
				return false;
			}
		}

		/// <summary>
		/// 折り目を分割する
		/// </summary>
		private void SplitCrease(in Vector3 startPoint, in Vector3 creaseDir, in Vector3 creaseDirNorm, in Vector3 perpendicularVec, in int innerLayer, in int outerLayer, in Vector3 furthest, out bool splitsOnFurthestIntersection)
		{
			//新たに追加する折り目のクラス
			var newCreases = new List<Crease>();

			splitsOnFurthestIntersection = false;

			//各折り目をチェック
			foreach (var crease in m_AllCreaseGroup)
			{
				//疑似外積でチェック
				var vertices = crease.MeshVertices;

				var bottomLayer = crease.GetCreaseLayer(0);
				var topLayer = crease.GetCreaseLayer(1);

				//折り目は正面を向いていないため、二点のみの比較で済む
				var nVec = (vertices[0] - startPoint).normalized;
				var res1 = Cross2DXY(creaseDirNorm, nVec);

				nVec = (vertices[2] - startPoint).normalized;
				var res2 = Cross2DXY(creaseDirNorm, nVec);

                //全ての座標が線の左右一方にのみある場合は、分割する必要がない
                if ((0.0 <= res1 && 0.0 <= res2) || (0.0 >= res1 && 0.0 >= res2))
                {
                    continue;
                }

                //やること
                //1.新しい折り目のベクトルと既に生成されている折り目の中点を取得
                //2. その中点から新しい折り目を生成し、既存の折り目の頂点を更新

                //creaseの線ベクトル
                var dir = vertices[1] - vertices[0];

                if (GetIntersectionPointForDirections(startPoint, creaseDir, vertices[0], dir, vertices[0].z, out Vector3 midPoint1))
                {
                    var meshVx1 = new MeshVertex(midPoint1, bottomLayer, false);

                    var midPoint2 = new Vector3(midPoint1.x, midPoint1.y, vertices[2].z);
                    var meshVx2 = new MeshVertex(midPoint2, topLayer, false);

                    var orderedVertices = GetCreaseOrderedVertices(meshVx1, crease.GetMeshVertexAt(Crease.eCreaseVertices.Bottom1), crease.GetMeshVertexAt(Crease.eCreaseVertices.Top1), meshVx2);

                    var splitCrease = crease.GenerateSplitCreaseMeshes(orderedVertices, m_MaterialPath, MeshParent);

                    orderedVertices = GetCreaseOrderedVertices(crease.GetMeshVertexAt(Crease.eCreaseVertices.Bottom0), meshVx1, meshVx2, crease.GetMeshVertexAt(Crease.eCreaseVertices.Top0));
                    crease.UpdateCreaseVertices(orderedVertices);

                    newCreases.Add(splitCrease);

					//始点より最も遠い交点で分割が行われているのかをチェック
					var dis = (midPoint1 - furthest);
					dis.z = 0.0f;
					if(dis.sqrMagnitude < OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION) splitsOnFurthestIntersection = true;
                }
			}

			if(newCreases.Count > 0) 
			{
				m_AllCreaseGroup.AddRange(newCreases);
			}
		}

		/// <summary>
		/// 折る側の折り紙メッシュを特定し、m_FoldGroupに格納する
		/// </summary>
		/// <param name="baseCreasePoint">折り目の線/座標1</param>
		/// <param name="creasePoint2">折り目の線/座標2</param>
		private void SetOrigamiFoldGroup(in Vector3 startPoint, in Vector3 creaseDirNorm, eFoldType type, in int innerLayer, in int outerLayer)
		{
			//レイヤー情報を更新するラムダ式
			System.Func<int, int, int> GetUpdatedLayerNum = GetUpdateLayerFunc(type);

			//各メッシュの座標と折り目の線の疑似外積を行い、折る側をm_foldGroupに格納する
			foreach (var mesh in m_AllOrigamiMeshGroup)
			{
				var vertices = mesh.MeshVertices.GetMeshVertices();
				//疑似外積
				//var p1 = GetPointSubtractedByLayerDifference(baseCreasePoint, perpendicularVec, mesh.FoldLayer, outerLayer, type);

				//Debug.DrawLine(p1, p1 + m_CreaseVec * 100f, Color.red, 10f);
				var dif = GetIntegerDif(outerLayer, mesh.FoldLayer);

				GetNormalizedCross2DResults(startPoint, creaseDirNorm, vertices, out double[] res, 3);

				//誤差が生じるので-0.0001f以上とする
				if (-OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION <= res[0] && -OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION <= res[1] && -OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION <= res[2]) 
				{
					m_OrigamiFoldGroup.Add(mesh);
				}
				else
				{
					m_OrigamiNonFoldGroup.Add(mesh);
				}
			}

			//折るメッシュのレイヤ情報と向きを更新
			foreach (var mesh in m_OrigamiFoldGroup)
			{
				var layer = GetUpdatedLayerNum(mesh.FoldLayer, innerLayer);
				//Debug.Log("Mesh Fold Layer: " + mesh.FoldLayer + " updated layer: " + layer);
				mesh.UpdateFoldInfo(!mesh.IsFacingUp, layer);
			}			
		}

		//折り目の折られる側に位置する折り目メッシュを判別し、m_CreaseFoldGroupに格納する
		private void SetCreaseFoldGroup(in Vector3 startPoint, in Vector3 creaseDirNorm, eFoldType type, in int innerLayer, in int outerLayer)
		{
			//GetNormalizedCross2DResultsに使う
			int max = (int)Crease.eCreaseTypes.MAX;
						//レイヤー情報を更新するラムダ式
			System.Func<int, int, int> GetUpdatedLayerNum = GetUpdateLayerFunc(type);

			//各折り目をチェック
			foreach (var crease in m_AllCreaseGroup)
			{
				//疑似外積でチェック
				var vertices = crease.MeshVertices;

				//var p1 = GetPointAddedByLayerDifference(baseCreasePoint, perpendicularVec, outerLayer, crease.GetCreaseLayer(1), type);

				var bottomLayer = crease.GetCreaseLayer(1);
				var dif = GetIntegerDif(bottomLayer, outerLayer);

				//折り目は正面を向いていないため、二点のみの比較で済む
				GetNormalizedCross2DResults(startPoint, creaseDirNorm, vertices, out double[] res, max);

                //誤差が生じるので-0.0001f以上とする
				if (-OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION <= res[0] && -OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION <= res[1]) 
				{
					m_CreaseFoldGroup.Add(crease);
				}
			}

			//int bottom = (int)Crease.eCreaseTypes.Bottom;	// = 0
			//int top = (int)Crease.eCreaseTypes.Top;		// = 1

			//折り目のレイヤ情報を更新
			foreach(var crease in m_CreaseFoldGroup)
			{
				var bottomLayer = crease.GetCreaseLayer(0);
				var topLayer = crease.GetCreaseLayer(1);

				bottomLayer = GetUpdatedLayerNum(bottomLayer, innerLayer);
				topLayer = GetUpdatedLayerNum(topLayer, innerLayer);

				crease.UpdateCreaseInfo(bottomLayer, topLayer, !crease.CreaseFacing);
			}
		}

        /// <summary>
        /// メッシュを折った後の頂点情報を設定する
        /// </summary>
        /// <param name="baseCreasePoint">折り目の線の座標1</param>
        /// <param name="creasePoint2">折り目の線の座標2</param>
        private void SetFoldResults(in Vector3 startPoint, in Vector3 creaseDir, in Vector3 perpendicularVec, in eFoldType type, in int innerLayer, in int outerLayer, in float halfwayRad, in float targetRad, Matrix4x4 matX, Matrix4x4 matZ)
		{
            //ここでやりたいこと：
            //折る側にあるメッシュの頂点と直線ベクトルを使い、折った後の頂点の情報を得る。
            //
            //実際にやること：
            //1.頂点Aと直線ベクトルを内積し、交点Bを求める
            //2.AとBを元に折った先の頂点Cを求める

            int outer = GetUpdatedLayerValueOfOuterLayer(innerLayer, outerLayer, type);

			//折る層の中で最も座標の高さが低い所から伸びるベクトルの始点を求める
			var newInner = type == eFoldType.MoutainFold ? innerLayer - 1 : innerLayer + 1;

			for (int i = 0; i < m_OrigamiFoldGroup.Count; i++)
			{
				var dif = GetIntegerDif(m_OrigamiFoldGroup[i].FoldLayer, outer);

				//頂点の結果を入れるためにnewする
				m_OrigamiFoldResults.Add(new OrigamiFoldResults(m_OrigamiFoldGroup[i], startPoint, creaseDir, perpendicularVec, type, innerLayer, halfwayRad, targetRad, matX, matZ));
			}

			//折り目のCreaseFoldResultsを設定
			for (int i = 0; i < m_CreaseFoldGroup.Count; ++i)
			{
				var dif = GetIntegerDif(m_CreaseFoldGroup[i].GetCreaseLayer(1), outer);

				//頂点の結果を入れるためにnewする
				m_CreaseFoldResults.Add(new CreaseFoldResults(m_CreaseFoldGroup[i], startPoint, creaseDir, perpendicularVec, type, innerLayer, matX, matZ));
			}
		}

		//折った際、折り目に接している辺が折り目に沿うように調整するためのデータを算出
		private void SetAdjustmentResults(in Vector3 closestIntersection, in Vector3 startPoint, in Vector3 endPoint, in MeshVertex[] endPointsPerLayer, in MeshVertex[] alteredVerticesPerLayer, in int oldInnerLayer, in int oldOuterLayer, in float targetRad, in Matrix4x4 matZ, in eFoldType type,
										  out List<OrigamiMesh> fillerMeshParent, bool isSplitOnEndPoints)
		{
			//リサイズ
			m_OrigamiFoldGroupAdjustResults.Capacity = m_OrigamiFoldGroup.Count;
			m_OrigamiNonFoldGroupAdjustResults.Capacity = m_OrigamiNonFoldGroup.Count;

			//隙間メッシュを生む可能性のあるリスト
			fillerMeshParent = new List<OrigamiMesh>();

            // リストをソート
            var endPointArray = SortMeshVertexArrayByLayer(endPointsPerLayer, type);
			var alteredEndPointArray = SortMeshVertexArrayByLayer(alteredVerticesPerLayer, type);

			//折られる側のメッシュのinner と outerLayer
			var update = GetUpdateLayerFunc(type);
			int newInnerLayer = update(oldInnerLayer, oldInnerLayer);
			int newOuterLayer = update(oldOuterLayer, oldInnerLayer);
			
			//軸となる方向ベクトル
			var baseDir = endPoint - closestIntersection;
			var baseSqrMag = baseDir.sqrMagnitude;
			//var baseDirMag = Mathf.Sqrt(baseSqrMag);

			//方向ベクトルをキャッシュする
			var  dirList = new List<Vector3>();
			//各方向ベクトル大きさのリスト
			var magList = new List<float>();

			int i = 0;
			
			foreach(var layer in alteredEndPointArray)
			{
				var dir = layer.Vertex - closestIntersection;
				dirList.Add(dir);
				magList.Add((endPointArray[i++].Vertex - closestIntersection).magnitude);
			}

			//折るメッシュを対象とする
			for(i = 0; i < m_OrigamiFoldGroup.Count; ++i)
			{
                //方向ベクトルを算出するために、tipVerticesPerLayerに用いる添字を計算する
                int idx;
                //この時点で折り紙は折る物と折られない物に分かれ、前者のレイヤー情報は既に更新されている。従って、まずレイヤー情報が更新されているのか確認する
				//折らない側の場合
				var mesh = m_OrigamiFoldGroup[i];

                idx = GetIntegerDif(mesh.FoldLayer, newOuterLayer);

                bool needAdjust = mesh.MeshVertices.ConnectedToCreaseList[0] | mesh.MeshVertices.ConnectedToCreaseList[1] | mesh.MeshVertices.ConnectedToCreaseList[2];

				if(!needAdjust || idx == 0) m_OrigamiFoldGroupAdjustResults.Add(OrigamiAdjustedVertexResults.CreateEmptyOffsetResult());
                else m_OrigamiFoldGroupAdjustResults.Add(new OrigamiAdjustedVertexResults(mesh, targetRad, m_OrigamiFoldResults[i], startPoint, closestIntersection, dirList[idx], baseDir, magList[idx], baseSqrMag, matZ));
			}

			//折らないメッシュを対象とする
			foreach(var mesh in m_OrigamiNonFoldGroup)
            {
                //方向ベクトルを算出するために、tipVerticesPerLayerに用いる添字を算出
                int idx = GetIntegerDif(mesh.FoldLayer, oldInnerLayer);

				bool needAdjust = mesh.MeshVertices.ConnectedToCreaseList[0] | mesh.MeshVertices.ConnectedToCreaseList[1] | mesh.MeshVertices.ConnectedToCreaseList[2];

				if(!needAdjust || idx == 0) m_OrigamiNonFoldGroupAdjustResults.Add(OrigamiAdjustedVertexResults.CreateEmptyOffsetResult());
                else
                {
                    m_OrigamiNonFoldGroupAdjustResults.Add(new OrigamiAdjustedVertexResults(mesh, targetRad, startPoint, closestIntersection, dirList[idx], baseDir, magList[idx], baseSqrMag, matZ));  //折らない側は折り始めた時点で補間処理を始める
					//隙間メッシュを生成させるメッシュである条件は、
					// 1.頂点が折り目に一つだけしか接していないこと。
					// 2.折り目の終点が既に作られた折り目を分割していること
					var count = 0;
					foreach(var connection in mesh.MeshVertices.ConnectedToCreaseList) if(connection) ++count;
					if(isSplitOnEndPoints && count == 1)
						fillerMeshParent.Add(mesh);
                }
			}
		}

		/// <summary>
		/// 線の折り目に沿って紙を角度に従って折る。
		/// </summary>
		/// <param name="radians">係数</param>
		/// <returns></returns>
		public void FoldMeshToAngle (float radians, eFoldType type)
        {
            var oldInner = type == eFoldType.MoutainFold ? m_InnerLayer + 1 : m_InnerLayer - 1;

			if (type == eFoldType.MoutainFold)
			{
				float rad;
				//紙を折り曲げる
				for (int i = 0; i < m_OrigamiFoldGroup.Count; ++i)
				{
					var dif = GetIntegerDif(m_OrigamiFoldGroup[i].FoldLayer, m_InnerLayer);
					rad = radians - dif * OrigamiUtility.ANGLE_OFFSET;
					if (rad <= 0) continue;

					m_OrigamiFoldGroup[i].FoldOrigamiMeshByRadians(m_OrigamiFoldResults[i], m_OrigamiFoldGroupAdjustResults[i], rad, m_MatZ);
                }

                for (int i = 0; i < m_OrigamiNonFoldGroup.Count; ++i)
                {
                    var dif = GetIntegerDif(m_OrigamiNonFoldGroup[i].FoldLayer, oldInner);
                    rad = radians - dif * OrigamiUtility.ANGLE_OFFSET;
                    if (rad <= 0) continue;

                    m_OrigamiNonFoldGroup[i].AdjustOrigamiMeshByRadians(m_OrigamiNonFoldGroupAdjustResults[i], m_OrigamiNonFoldGroupAdjustResults[i].TargetRadians);
                }

				// float bottomRad, topRad;
                // //折り目を折る
                // for (int i = 0; i < m_CreaseFoldGroup.Count; ++i)
                // {
				// 	var dif = GetIntegerDif(m_CreaseFoldGroup[i].GetCreaseLayer(0), m_InnerLayer);
				// 	bottomRad = radians - dif * OrigamiUtility.ANGLE_OFFSET;
				// 	if (bottomRad <= 0) bottomRad = 0f;

				// 	dif = GetIntegerDif(m_CreaseFoldGroup[i].GetCreaseLayer(1), m_InnerLayer);
				// 	topRad = radians - dif * OrigamiUtility.ANGLE_OFFSET;
				// 	if (topRad <= 0) topRad = 0f;

                //     m_CreaseFoldGroup[i].FoldCreaseMeshByRadians(m_CreaseFoldResults[i], bottomRad, topRad, m_MatZ);
                // }

                // for (int i = 0; i < m_GeneratedCreaseGroup.Count; ++i)
                // {
				// 	var dif = GetIntegerDif(m_GeneratedCreaseGroup[i].GetCreaseLayer(1), m_InnerLayer);
                //     m_GeneratedCreaseGroup[i].ExtendGeneratedCreases(m_CreaseGenerateResults[i], radians, m_InnerLayer, type, m_MatZ);
                // }

			}
			else
			{
				var tempRad = OrigamiUtility.TWO_PI - radians;

				float rad;
				//紙を折り曲げる
				for (int i = 0; i < m_OrigamiFoldGroup.Count; i++)
				{
					var dif = GetIntegerDif(m_OrigamiFoldGroup[i].FoldLayer, m_InnerLayer);
					rad = tempRad + dif * OrigamiUtility.ANGLE_OFFSET;
					if (rad >= OrigamiUtility.TWO_PI) continue;

					m_OrigamiFoldGroup[i].FoldOrigamiMeshByRadians(m_OrigamiFoldResults[i], m_OrigamiFoldGroupAdjustResults[i], rad, m_MatZ);
				}

                for (int i = 0; i < m_OrigamiNonFoldGroup.Count; ++i)
                {
                    var dif = GetIntegerDif(m_OrigamiNonFoldGroup[i].FoldLayer, oldInner);
                    rad = tempRad + dif * OrigamiUtility.ANGLE_OFFSET;
					if (rad >= OrigamiUtility.TWO_PI) continue;

                    m_OrigamiNonFoldGroup[i].AdjustOrigamiMeshByRadians(m_OrigamiNonFoldGroupAdjustResults[i], m_OrigamiNonFoldGroupAdjustResults[i].TargetRadians);
                }

				// float bottomRad, topRad;
                // //折り目を折る
                // for (int i = 0; i < m_CreaseFoldGroup.Count; i++)
                // {
                //     var dif = GetIntegerDif(m_CreaseFoldGroup[i].GetCreaseLayer(0), m_InnerLayer);
				// 	bottomRad = tempRad + dif * OrigamiUtility.ANGLE_OFFSET;
				// 	if (bottomRad >= OrigamiUtility.TWO_PI) bottomRad = OrigamiUtility.TWO_PI;

				// 	dif = GetIntegerDif(m_CreaseFoldGroup[i].GetCreaseLayer(1), m_InnerLayer);
				// 	topRad = tempRad + dif * OrigamiUtility.ANGLE_OFFSET;
				// 	if (topRad >= OrigamiUtility.TWO_PI) topRad = OrigamiUtility.TWO_PI;

                //     m_CreaseFoldGroup[i].FoldCreaseMeshByRadians(m_CreaseFoldResults[i], bottomRad, topRad, m_MatZ);
                // }

                // for (int i = 0; i < m_GeneratedCreaseGroup.Count; i++)
                // {
				// 	var dif = GetIntegerDif(m_GeneratedCreaseGroup[i].GetCreaseLayer(1), m_InnerLayer);
                //     m_GeneratedCreaseGroup[i].ExtendGeneratedCreases(m_CreaseGenerateResults[i], tempRad, m_InnerLayer, type, m_MatZ);
                // }
			}

			foreach(var crease in m_AllCreaseGroup)
			{
				crease.UpdateCreaseMesh();
			}
		}

		/// <summary>
		/// メッシュの角度を目標のものに調整する
		/// </summary>
		/// <param name="type">折り目の種類</param>
		private void FinalizeMeshAngle (eFoldType type)
		{
			//紙を折り曲げる
			for (int i = 0; i < m_OrigamiFoldGroup.Count; ++i)
			{
				m_OrigamiFoldGroup[i].FoldOrigamiMeshByRadians(m_OrigamiFoldResults[i], m_OrigamiFoldGroupAdjustResults[i], m_OrigamiFoldResults[i].FOLD_TARGETRADIANS, m_MatZ);
			}

			for(int i = 0; i < m_OrigamiNonFoldGroup.Count; ++i)
			{
				m_OrigamiNonFoldGroup[i].AdjustOrigamiMeshByRadians(m_OrigamiNonFoldGroupAdjustResults[i], m_OrigamiNonFoldGroupAdjustResults[i].TargetRadians);
			}

			for (int i = 0; i < m_CreaseFoldGroup.Count; ++i)
			{
				m_CreaseFoldGroup[i].FoldCreaseMeshByRadians(m_CreaseFoldResults[i], m_CreaseFoldResults[i].FOLD_TARGETRADIANS, m_CreaseFoldResults[i].FOLD_TARGETRADIANS, m_MatZ);
			}

			for (int i = 0; i < m_GeneratedCreaseGroup.Count; ++i)
			{
				m_GeneratedCreaseGroup[i].ExtendGeneratedCreases(m_CreaseGenerateResults[i], m_CreaseGenerateResults[i][0].TargetAngle, m_InnerLayer, type, m_MatZ);
			}
		}

		/// <summary>
		/// メッシュを折る処理のための初期化を行う。折り目はp1とp2を繋いだ線であり、必ず線の左側のメッシュを右へ畳む
		/// </summary>
		/// <param name="p1">折り目の線の始点</param>
		/// <param name="p2">折り目の線の終点</param>
		/// <param name="type"></param>
		public void InitializeFold(in Vector3 p1, in Vector3 p2, eFoldType type)
		{
			//折り目として線が引けるか確認
			if(p1 == p2)
			{
				Debug.LogError("Crease values are not valid");
				return;
			}

			//前回の情報が格納されていればクリアする
			if (m_OrigamiFoldResults.Count > 0) m_OrigamiFoldResults.Clear();
			if (m_OrigamiFoldGroupAdjustResults.Count > 0) m_OrigamiFoldGroupAdjustResults.Clear();
			if (m_OrigamiNonFoldGroupAdjustResults.Count > 0) m_OrigamiNonFoldGroupAdjustResults.Clear();
			if (m_CreaseGenerateResults.Count > 0) m_CreaseGenerateResults.Clear();
			if (m_CreaseFoldResults.Count > 0) m_CreaseFoldResults.Clear();

			//m_FoldGroup内に前の折るメッシュの情報が含まれていればクリアする
			if (m_OrigamiFoldGroup.Count > 0) m_OrigamiFoldGroup.Clear();
			if (m_OrigamiNonFoldGroup.Count > 0) m_OrigamiNonFoldGroup.Clear();
			if (m_CreaseFoldGroup.Count > 0) m_CreaseFoldGroup.Clear();

			//生成された折り目は移動する
			if (m_GeneratedCreaseGroup.Count > 0)
			{
				m_AllCreaseGroup.AddRange(m_GeneratedCreaseGroup);
				m_GeneratedCreaseGroup.Clear();
			}

			//非アクティブなら情報を初期化だけして終了
            if (!IsActive) return;

			//キャッシュする方向ベクトル、正規化された方向ベクトル、垂直ベクトルのリスト

			//折る対象となるメッシュの中でレイヤーの値が最も大きいものを求める
			var tempP1 = p1;
			tempP1.z = 0.0f;
			var tempP2 = p2;
			tempP2.z = 0.0f;

			var prefoldInfo = GetPreFoldInfo(type, p1, p2, out List<Vector3> cachedStartPoints, out Vector3 outDir, out Vector3 outDirNorm, out Vector3 outPerpendiculatVec, out m_MatZ);

			if (!prefoldInfo.hasFoldTarget)
			{
				Debug.Log("No Meshes to fold, ending fold");
				return;
			}

            //折り目を作成するために、折り目の線に接している座標を取得するリスト
            List<SplitMeshInfo> splitInfoList;

			//メッシュを二回分割し、折り目を作る
			if (SplitMesh(p1, outDir, outDirNorm, cachedStartPoints, prefoldInfo.innerLayer, prefoldInfo.outerLayer, 
						  out Vector3 outClosest, out MeshVertex[] outFurthestIntersectionsPerLayerOnCrease, out MeshVertex[] outFurthestAlteredIntersections, out splitInfoList))
            {				
				//ずれや余分な演算を減らすために、ここで各頂点の値を調整
				for(int i = 0; i < outFurthestAlteredIntersections.Length; ++i)
                {
                    //頂点が折られた後の座標を取得
                    var mid = OrigamiUtility.GetPerpendicularIntersectionPoint(outFurthestAlteredIntersections[i].Vertex, p1, outDir);
                    var foldedVec = OrigamiBase.GetRotatedVector3(outFurthestAlteredIntersections[i].Vertex, mid, OrigamiUtility.XROTATION_MAT__180DEG, m_MatZ);
					outFurthestAlteredIntersections[i] = new MeshVertex(foldedVec, outFurthestAlteredIntersections[i].Layer, outFurthestAlteredIntersections[i].IsConnectedToCrease);
				}

				//折り目を分割する
				int idx = outFurthestIntersectionsPerLayerOnCrease[0].Layer == prefoldInfo.outerLayer ? 0 : outFurthestIntersectionsPerLayerOnCrease.Length - 1;
				SplitCrease(p1, outDir, outDirNorm, outPerpendiculatVec, prefoldInfo.innerLayer, prefoldInfo.outerLayer, outFurthestIntersectionsPerLayerOnCrease[idx].Vertex, out bool splitsOnEndPoints);

				//分割後に折る側のメッシュを特定
				SetOrigamiFoldGroup(p1, outDirNorm, type, prefoldInfo.innerLayer, prefoldInfo.outerLayer);

				SetCreaseFoldGroup(p1, outDirNorm, type, prefoldInfo.innerLayer, prefoldInfo.outerLayer);

                //デバッグ用に折る線を描画
                //Debug.DrawLine(p1, p2, Color.red, 10);

				//折る上で用いるラジアンや行列
                float halfwayRad = OrigamiUtility.HALF_PI;
                float targetRad = Mathf.PI;

                Matrix4x4 matX;
                //折り目の種類によってX軸の行列を変える
                if (type == eFoldType.MoutainFold)
                {
                    matX = OrigamiUtility.XROTATION_MAT__90DEG;
                }
                else
                {
                    matX = OrigamiUtility.XROTATION_MAT__270DEG;
                }

				//現在の紙を折った後の頂点を計算
				SetFoldResults(p1, outDir, outPerpendiculatVec, type, prefoldInfo.innerLayer, prefoldInfo.outerLayer, halfwayRad, targetRad, matX, m_MatZ);

				SetAdjustmentResults(outClosest, p1, p2, outFurthestIntersectionsPerLayerOnCrease, outFurthestAlteredIntersections, prefoldInfo.innerLayer, prefoldInfo.outerLayer, halfwayRad, m_MatZ, type, out List<OrigamiMesh> fillerMeshParent, splitsOnEndPoints);

				GenerateSquashedMeshes(splitInfoList, fillerMeshParent, p1, outDir, outDirNorm, outPerpendiculatVec, prefoldInfo.innerLayer, prefoldInfo.outerLayer, m_MatZ, type);
				
				if(type == eFoldType.MoutainFold)
				{
					m_InnerLayer = prefoldInfo.innerLayer - 1;
				}
                else
                {
                    m_InnerLayer = prefoldInfo.innerLayer + 1;
                }
			}
		}

		//折り終えた後に呼ぶメソッド
		public void EndFold(eFoldType type)
		{
			FinalizeMeshAngle(type);

			foreach(var origami in m_OrigamiFoldGroup)
			{
				origami.OnEndFold();
			}

			foreach(var crease in m_CreaseFoldGroup)
			{
				crease.OnEndFold();
			}

			foreach(var crease in m_GeneratedCreaseGroup)
			{
				crease.OnEndExtend();
			}
		}

		//外部より折り紙や折り目のメッシュを追加する
		public void AddMeshesAndCreases(in IEnumerable<OrigamiMesh> origamiMeshes, in IEnumerable<Crease> creases)
		{
			if(origamiMeshes != null) m_AllOrigamiMeshGroup.AddRange(origamiMeshes);
			if(creases != null) m_AllCreaseGroup.AddRange(creases);
		}

		public void InitializeLists()
		{
			//折紙に使うデータの初期化
			m_OrigamiFoldGroup = new List<OrigamiMesh>();
			m_OrigamiNonFoldGroup = new List<OrigamiMesh>();
			m_AllOrigamiMeshGroup = new List<OrigamiMesh>();
			m_OrigamiFoldResults = new List<OrigamiFoldResults>();
			m_OrigamiFoldGroupAdjustResults = new List<OrigamiAdjustedVertexResults>();
			m_OrigamiNonFoldGroupAdjustResults = new List<OrigamiAdjustedVertexResults>();

			//折り目のデータの初期化
			m_GeneratedCreaseGroup = new List<Crease>();
			m_CreaseFoldGroup = new List<Crease>();
			m_AllCreaseGroup = new List<Crease>();
			m_CreaseGenerateResults = new List<CreaseGenerateResults[]>();
			m_CreaseFoldResults = new List<CreaseFoldResults>();
		}

		public MeshFoldMachine(List<OrigamiMesh> meshes, in float origamiSize, in Transform meshParent, in bool activeOnConstruction)
		{
			InitializeLists();

			m_MaterialPath = System.IO.Path.Combine("Material", "OrigamiMaterial");

			m_OrigamiSize = origamiSize;


			if(meshes != null) m_AllOrigamiMeshGroup.AddRange(meshes);
			
			MeshParent = meshParent;

			IsActive = activeOnConstruction;
		}
	}

    //折る向きのenum. ゲーム開始時のカメラの向きを基準とする
    public enum eFoldType
    {
        MoutainFold,        //山折り Z値が折る前より大きくなる
        ValleyFold          //谷折り Z値が折る前より小さくなる
    }
}