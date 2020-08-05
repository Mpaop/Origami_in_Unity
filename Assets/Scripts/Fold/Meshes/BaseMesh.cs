using UnityEngine;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Dynamic;
using System.Runtime.InteropServices.WindowsRuntime;

//作成者：Mpaop
//折紙用のメッシュ関連をまとめたソースファイル
namespace Origami_Mesh
{
	//折り紙と折り目のクラスを汎化させたクラス
	public abstract class OrigamiBase
	{
		/// <summary>
		/// メンバフィールド
		/// </summary>

		//表を上に向けているか
		public bool IsFacingUp { get; protected set; }

		// メッシュの情報をラップしたクラスへの参照を持つ配列。必ず三角となるので、三つ持つ
		private MeshVertexInfo[] m_vertexInfoGroup = new MeshVertexInfo[3];

		public MeshVertexInfo GetMeshVertexInfo(in int idx) => m_vertexInfoGroup[idx];

		/// <summary>
		/// 定数
		/// </summary>

		//代入するポリゴン座標の描画順を保持する。表向きの場合
		private static readonly List<int> m_origamiTriangles = new List<int> { 0, 2, 4, 5, 3, 1 };

		//代入するポリゴン座標の描画順を保持する。裏向きの場合
		private static readonly List<int> m_origamiTrianglesReversed = new List<int> { 4, 2, 0, 1, 3, 5 };

		/// <summary>
		/// コンストラクタ
		/// </summary>
		/// <param name="vertices">メッシュの頂点</param>
		/// <param name="facing">メッシュの向き</param>
		/// <param name="materialPath">マテリアルのパス</param>
		/// <param name="parent">オブジェクトの親</param>
		protected OrigamiBase(in MeshVertexInfo info1, in MeshVertexInfo info2, in MeshVertexInfo info3, bool facing)
		{
			m_vertexInfoGroup[0] = info1;
			m_vertexInfoGroup[1] = info2;
			m_vertexInfoGroup[2] = info3;
			IsFacingUp = facing;
		}


		/// 
		/// メソッド
		/// 		

		/// <summary>
		/// 与えた行列に従って回転されたベクトルを返す
		/// </summary>
		public static Vector3 GetRotatedVector3(in Vector3 origin, in Vector3 midpoint, in Matrix4x4 matX, in Matrix4x4 matZ)
		{
			var rotatedVec = origin - midpoint;

			rotatedVec = matZ.inverse.MultiplyPoint3x4(rotatedVec);
			rotatedVec = matX.MultiplyPoint3x4(rotatedVec);
			rotatedVec = matZ.MultiplyPoint3x4(rotatedVec);

			rotatedVec += midpoint;

			return rotatedVec;
		}

		public static Vector3 GetRotatedVector3_Lerped(in Vector3 origin, in Vector3 target, in Vector3 midpoint, in Vector3 offset, in float t, in Matrix4x4 matX, in Matrix4x4 matZ)
		{
			var tempTarget = target;
			tempTarget.z = origin.z;
			var lerped = FastLerp(origin, tempTarget, t);
			return GetRotatedVector3(lerped + offset, midpoint + offset, matX, matZ);
		}

		/// <summary>
		/// メッシュの頂点情報を更新する
		/// </summary>
		/// <param name="mesh">更新するメッシュ</param>
		/// <param name="vertices">メッシュの頂点</param>
		protected void UpdateOrigamiTriangleMesh(in Vector3 ver1, in Vector3 ver2, in Vector3 ver3)
		{
			m_mesh.Clear();

			{
				m_vertices[0] = ver1;
				m_vertices[1] = ver2;
				m_vertices[2] = ver3;
			}

			//値の代入と再計算
			m_vertices.setVertices(m_mesh);
			m_mesh.SetUVs(0, m_origamiUV);
			m_mesh.SetTriangles(m_triangles, 0, true);

			m_mesh.RecalculateNormals();
		}

		public void UpdateOrigamiTriangleMesh(in Vector3[] vertices)
		{
			if (IsFacingUp)
			{
				m_triangles = m_origamiTriangles;
			}
			else
			{
				m_triangles = m_origamiTrianglesReversed;
			}

			this.UpdateOrigamiTriangleMesh(vertices[0], vertices[1], vertices[2]);
		}

		public void UpdateOrigamiTriangleMesh(in IEnumerable<MeshVertexInfo> vertices)
		{
			if (IsFacingUp)
			{
				m_triangles = m_origamiTriangles;
			}
			else
			{
				m_triangles = m_origamiTrianglesReversed;
			}

			int i = 0;
			foreach (var vertex in vertices)
			{
				m_vertices.VertexInfoList[i++].IsConnectedToCrease = vertex.IsConnectedToCrease;
			}

			this.UpdateOrigamiTriangleMesh(vertices.ElementAt(0).Vertex, vertices.ElementAt(1).Vertex, vertices.ElementAt(2).Vertex);
		}

		public void UpdateOrigamiLayers(in int layer1, in int layer2, in int layer3)
		{
			m_vertices.VertexInfoList[0].Layer = layer1;
			m_vertices.VertexInfoList[1].Layer = layer2;
			m_vertices.VertexInfoList[2].Layer = layer3;
		}

		//tを正規化せずに行う線形補間
		protected static Vector3 FastLerp(in Vector3 start, in Vector3 end, in float t)
		{
			return start + (end - start) * t;
		}
	}
}