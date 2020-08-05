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
		/// コンストラクタ
		/// </summary>
		/// <param name="vertices">メッシュの頂点</param>
		/// <param name="facing">メッシュの向き</param>
		/// <param name="materialPath">マテリアルのパス</param>
		/// <param name="parent">オブジェクトの親</param>
		public CreaseMesh(in Vector3[] vertices, in IEnumerable<int> creaseLayers, bool facing, in string materialPath, in Transform parent) : base(vertices, creaseLayers, new List<bool> { false, false, false, false }, facing, materialPath, parent)
		{
			MeshObject.name = "crease" + MeshObject.transform.parent.childCount;
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
		public Vector3 Vertex => Mesh.MeshVertices.VertexInfoList[m_meshIdx].Vertex;
		public OrigamiMesh Mesh { get; private set; }

		// その頂点を含む折り目のメッシュ
		private Crease m_parentCrease;

		//メッシュと接している頂点の添字
		private int m_meshIdx;

		// 折り目が持つ頂点の添字
		private int m_creaseIdx;

		public CreaseVertex(in OrigamiMesh mesh, in int meshIdx, in Crease crease, in int creaseIdx)
		{
			Mesh = mesh;
			m_meshIdx = meshIdx;
			m_parentCrease = crease;
			m_creaseIdx = creaseIdx;
			mesh.MeshVertices.AddUpdateVertexEventAt(meshIdx, UpdateVertex);
		}

		//頂点情報の更新のみ行う時に使う
		public void UpdateVertex(in Vector3 vertex)
		{
			m_parentCrease.UpdateCreaseVertex(vertex, m_creaseIdx);
		}

		//折り目が分割される場合など、値を全て更新がある時に使う
		public void RenewValues(in OrigamiMesh mesh, in int meshIdx, in Crease crease, in int creaseIdx)
		{
			Mesh.MeshVertices.RemoveUpdateVertexEventAt(m_meshIdx, UpdateVertex);

			Mesh = mesh;
			m_meshIdx = meshIdx;
			mesh.MeshVertices.AddUpdateVertexEventAt(meshIdx, UpdateVertex);
			m_parentCrease = crease;
			m_creaseIdx = creaseIdx;
		}
	}

	//折り目のメッシュをひとまとめにして管理するクラス
	public class Crease : IFoldMeshCallbacks
	{
		private CreaseMesh[] m_creases;

		private Vector3[] m_vertices;

		public IReadOnlyList<Vector3> Vertices { get; }

		//折り目の頂点を管理するために用いる。上は破棄予定
		private CreaseVertex[] m_creaseVertices;

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

			int size = (int)eCreaseVertices.MAX;
			m_vertices = new Vector3[size];
			for (int i = 0; i < size; i++)
			{
				m_vertices[i] = Vector3.zero;
			}

			Vertices = new ReadOnlyCollection<Vector3>(m_vertices);

			m_creaseVertices = new CreaseVertex[size];
		}

		//折り目のレイヤーを取得する
		public int GetCreaseLayer(in int idx)
		{

			//良くない実装である自覚はある
			if (idx == 0) return m_creases[0].MeshVertices.VertexInfoList[0].Layer;
			else if (idx == 1) return m_creases[1].MeshVertices.VertexInfoList[2].Layer;
			else
			{
				Debug.LogError($"Log: Method: GetCreaseLayer(in int idx). \nCause: argument ({idx}) is out of range");
				throw new System.ArgumentOutOfRangeException();
			}
		}

		public MeshVertexInfo GetMeshVertexAt(eCreaseVertices ver)
		{
			//      アクセスする折り目の図
			//      Crease0:Idx0 --------------------- Crease0:Idx1
			//                   |                   |
			//      Crease1:Idx2 --------------------- Crease0:Idx2

			switch (ver)
			{
				case eCreaseVertices.Bottom0: return m_creases[0].MeshVertices.VertexInfoList[0];      // Crease0:Idx0
				case eCreaseVertices.Bottom1: return m_creases[0].MeshVertices.VertexInfoList[1];      // Crease0:Idx1
				case eCreaseVertices.Top1: return m_creases[0].MeshVertices.VertexInfoList[2];      // Crease0:Idx2
				case eCreaseVertices.Top0: return m_creases[1].MeshVertices.VertexInfoList[2];      // Crease1:Idx2
				default:
					throw new System.ArgumentOutOfRangeException();
			}
		}

		public CreaseVertex GetCreaseVertexAt(in eCreaseVertices vertex)
		{
			if (vertex >= eCreaseVertices.MAX) throw new System.ArgumentOutOfRangeException();

			return m_creaseVertices[(int)vertex];
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

		//折り目のメッシュを生成する。折り目の頂点クラスは呼び出し元で宣言してから渡す
		public void GenerateCreaseMesh(List<CreaseVertex> vertices, in bool facing, in string materialPath, in Transform parent)
		{
			for (int i = 0; i < m_creaseVertices.Length; ++i)
			{
				m_creaseVertices[i] = vertices[i];
				m_vertices[i] = vertices[i].Vertex;
			}

			m_creases[(int)eCreaseTypes.Bottom] = new CreaseMesh(new Vector3[3] { m_vertices[0], m_vertices[1], m_vertices[2] },
																 new List<int>(3) { vertices[0].Mesh.FoldLayer, vertices[1].Mesh.FoldLayer, vertices[2].Mesh.FoldLayer }, facing, materialPath, parent);
			m_creases[(int)eCreaseTypes.Top] = new CreaseMesh(new Vector3[3] { m_vertices[0], m_vertices[2], m_vertices[3] },
															  new List<int>(3) { vertices[0].Mesh.FoldLayer, vertices[2].Mesh.FoldLayer, vertices[3].Mesh.FoldLayer }, facing, materialPath, parent);
		}

		/// <summary>
		/// 分割した新しい折り目を生成する
		/// </summary>
		/// <param name="vertices"></param>
		/// <param name="creaseLayers"></param>
		/// <param name="mesh"></param>
		/// <param name="materialPath"></param>
		/// <param name="parent"></param>
		public Crease GenerateSplitCreaseMeshes(in List<CreaseVertex> vertices, in string materialPath, in Transform parent)
		{
			if (vertices.Count != (int)eCreaseVertices.MAX)
			{
				Debug.LogError("Wrong size");
				throw new System.ArgumentException();
			}

			//新しい折り目
			var crease = new Crease();
			crease.GenerateCreaseMesh(vertices, this.CreaseFacing, materialPath, parent);

			// Vector3[] creaseMeshVertices = { vertices[0].Vertex, vertices[1].Vertex, vertices[2].Vertex };
			// int[] layers = { m_creases[0].MeshVertices.Layers[0], m_creases[0].MeshVertices.Layers[1], m_creases[0].MeshVertices.Layers[2] }; //{ m_creaseLayers[0], m_creaseLayers[0], m_creaseLayers[1] };
			// var facing = m_creases[0].IsFacingUp;

			// crease.m_creases[(int)eCreaseTypes.Bottom] = new CreaseMesh(creaseMeshVertices, layers, facing, materialPath, parent);

			// creaseMeshVertices = new Vector3[3] { vertices[0].Vertex, vertices[2].Vertex, vertices[3].Vertex };
			// layers = new int[3] { m_creases[1].MeshVertices.Layers[0], m_creases[1].MeshVertices.Layers[1], m_creases[1].MeshVertices.Layers[2] };//{ m_creaseLayers[0], m_creaseLayers[1], m_creaseLayers[1] };

			// crease.m_creases[(int)eCreaseTypes.Top] = new CreaseMesh(creaseMeshVertices, layers, facing, materialPath, parent);

			// for (int i = 0; i < (int)eCreaseVertices.MAX; i++) crease.m_vertices[i] = vertices[i].Vertex;

			// // crease.m_creaseLayers[(int)eCreaseTypes.Bottom] = m_creaseLayers[(int)eCreaseTypes.Bottom];
			// // crease.m_creaseLayers[(int)eCreaseTypes.Top] = m_creaseLayers[(int)eCreaseTypes.Top];
			// crease.SetLayers(this.GetCreaseLayer(0), this.GetCreaseLayer(1));

			// HasExtended = true;

			return crease;
		}

		public void UpdateCreaseVertices(in MeshVertexInfo[] vertices)
		{
			if (m_vertices.Length != vertices.Length)
			{
				Debug.LogError("Wrong Size");
				throw new System.ArgumentOutOfRangeException();
			}

			for (int i = 0; i < vertices.Length; i++)
			{
				m_vertices[i] = vertices[i].Vertex;
				// m_creaseVertices[i].RenewValues( , this, i);
			}

			m_creases[(int)eCreaseTypes.Bottom].UpdateOrigamiTriangleMesh(new Vector3[3] { m_vertices[0], m_vertices[1], m_vertices[2] });
			m_creases[(int)eCreaseTypes.Top].UpdateOrigamiTriangleMesh(new Vector3[3] { m_vertices[0], m_vertices[2], m_vertices[3] });
		}

		public void UpdateCreaseVertex(in Vector3 vec, in int idx)
		{
			if (m_vertices.Length <= idx || 0 > idx)
			{
				Debug.LogError("LOG: index exceeds length of m_vertices!");
				throw new System.ArgumentOutOfRangeException();
			}

			m_vertices[idx] = vec;
		}

		//折紙のメッシュを渡されたラジアンに折る
		public void FoldCreaseMeshByRadians(in CreaseFoldResults res, in float bottomRad, in float topRad, in Matrix4x4 matZ)
		{
			////折り目の下部の頂点と上部の頂点でレイヤー情報が異なるため、分ける
			// Vector3 bottomFold0, bottomFold1, topFold1, topFold0;
			// //X軸の回転
			// Matrix4x4 matX;
			// //係数
			// float t;
			// eFoldAngles angles;

			// {
			//     var continueBottomLayerFold = res.ContinueFolding(bottomRad);
			//     var continueTopLayerFold = res.ContinueFolding(topRad);

			//     //どちらも更新する必要がなければ戻る
			//     if (!continueBottomLayerFold && !continueTopLayerFold) return;

			//     //下部の処理
			//     if (continueBottomLayerFold)
			//     {
			//         matX = OrigamiUtility.GetXRotationMatrix(bottomRad);
			//         angles = res.GetOffsetData(bottomRad, out t);
			//         //90度で折られている場合と折られていない場合で異なる値を渡す
			//         if (angles == eFoldAngles.Point0)
			//         {
			//             var bottomOffset = OrigamiBase.GetRotatedVector3(res.BottomCreaseOffset, Vector3.zero, matX, matZ) * t;

			//             bottomFold0 = OrigamiBase.GetRotatedVector3_Lerped(res.BottomPoint0_Result.FoldOriginalPoint0, res.BottomPoint0_Result.FoldOriginalPoint90, res.BottomPoint0_Result.FoldMidPoint0, bottomOffset, t, matX, matZ);
			//             bottomFold1 = OrigamiBase.GetRotatedVector3_Lerped(res.BottomPoint1_Result.FoldOriginalPoint0, res.BottomPoint1_Result.FoldOriginalPoint90, res.BottomPoint1_Result.FoldMidPoint0, bottomOffset, t, matX, matZ);
			//         }
			//         else
			//         {
			//             bottomFold0 = OrigamiBase.GetRotatedVector3(res.BottomPoint0_Result.FoldOriginalPoint90, res.BottomPoint0_Result.FoldMidPoint90, matX, matZ);
			//             bottomFold1 = OrigamiBase.GetRotatedVector3(res.BottomPoint1_Result.FoldOriginalPoint90, res.BottomPoint1_Result.FoldMidPoint90, matX, matZ);
			//         }
			//     }
			//     else
			//     {
			//         bottomFold0 = m_vertices[0];
			//         bottomFold1 = m_vertices[1];
			//     }

			//     //上部の処理
			//     if (continueTopLayerFold)
			//     {
			//         matX = OrigamiUtility.GetXRotationMatrix(topRad);
			//         angles = res.GetOffsetData(topRad, out t);

			//         //90度で折られている場合と折られていない場合で異なる値を渡す
			//         if (angles == eFoldAngles.Point0)
			//         {
			//             var topOffset = OrigamiBase.GetRotatedVector3(res.TopCreaseOffset, Vector3.zero, matX, matZ) * t;

			//             topFold1 = OrigamiBase.GetRotatedVector3_Lerped(res.TopPoint1_Result.FoldOriginalPoint0, res.TopPoint1_Result.FoldOriginalPoint90, res.TopPoint1_Result.FoldMidPoint0, topOffset, t, matX, matZ);
			//             topFold0 = OrigamiBase.GetRotatedVector3_Lerped(res.TopPoint0_Result.FoldOriginalPoint0, res.TopPoint0_Result.FoldOriginalPoint90, res.TopPoint0_Result.FoldMidPoint0, topOffset, t, matX, matZ);
			//         }
			//         else
			//         {
			//             topFold1 = OrigamiBase.GetRotatedVector3(res.TopPoint1_Result.FoldOriginalPoint90, res.TopPoint1_Result.FoldMidPoint90, matX, matZ);
			//             topFold0 = OrigamiBase.GetRotatedVector3(res.TopPoint0_Result.FoldOriginalPoint90, res.TopPoint0_Result.FoldMidPoint90, matX, matZ);
			//         }
			//     }
			//     else
			//     {
			//         topFold1 = m_vertices[2];
			//         topFold0 = m_vertices[3];
			//     }
			// }
			//ここは順番が既に確定しているため、GetOrderedVerticesを使わずに入れる
			UpdateCreaseMesh();

			// m_creases[0].UpdateCreaseVertices(bottomFold0, bottomFold1, topFold1);
			// m_creases[1].UpdateCreaseVertices(bottomFold0, topFold1, topFold0);

			// m_vertices[0] = bottomFold0;
			// m_vertices[1] = bottomFold1;
			// m_vertices[2] = topFold1;
			// m_vertices[3] = topFold0;
		}

		public void UpdateCreaseMesh()
		{
			//ここは順番が既に確定しているため、GetOrderedVerticesを使わずに入れる
			m_creases[0].UpdateCreaseVertices(m_vertices[0], m_vertices[1], m_vertices[2]);
			m_creases[1].UpdateCreaseVertices(m_vertices[0], m_vertices[2], m_vertices[3]);
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

			m_creases[0].UpdateOrigamiTriangleMesh(new Vector3[3] { m_vertices[0], m_vertices[1], m_vertices[2] });
			m_creases[1].UpdateOrigamiTriangleMesh(new Vector3[3] { m_vertices[0], m_vertices[2], m_vertices[3] });
		}
	}
}