using UnityEngine;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Origami_Mesh
{
	//上のMeshVertexをMeshクラスで扱いやすいようにそれぞれ分解したリストで持ち、管理する
	sealed public class MeshVertices
	{
		// 頂点の座標値
		private List<Vector3> m_vertices;

		// 頂点のインデックス値
		private List<int> m_triangles;

		// 頂点のUV値
		private List<Vector2> m_uvs;

		private List<MeshVertexInfo> m_vertexInfoList;
		public ReadOnlyCollection<MeshVertexInfo> VertexInfoList => m_vertexInfoList.AsReadOnly();

		public int Size => m_vertexInfoList.Count;

		//頂点の値が更新された際に呼ばれるデリゲートの型
		public delegate void OnUpdateVertex(in Vector3 vertex);

		public void setVertices(Mesh mesh) => mesh.SetVertices(m_vertices);

		public void AddUpdateVertexEventAt(in int idx, OnUpdateVertex updateVertex)
		{

		}

		public void RemoveUpdateVertexEventAt(in int idx, OnUpdateVertex updateVertex)
		{

		}

		public void EmptyUpdateVertexEventAt(in int idx)
		{

		}

		//インデクサ
		// 現状の仕様ではポリゴンの表と裏で値が同じ頂点を用いているため、i*2でアクセスする
		public Vector3 this[in int i]
		{
			get
			{
				return m_vertices[i * 2];
			}
			set
			{
				int idx = i * 2;
				m_vertices[idx] = m_vertices[idx + 1] = value;
			}
		}

		public void AddVertex(in Vector3 vec, in int idx, in int layer, in Vector2 uv, in bool connected)
		{
			// 裏表で分けないと影が掛かってしまうので、暫定的処理として、二回Addする。
			//将来的にはシェーダーで解決したい
			m_vertices.Add(vec);
			m_vertices.Add(vec);
			m_uvs.Add(uv);
			m_vertexInfoList.Add(new MeshVertexInfo(this, idx, layer, connected));
		}

		public void SetTriangle(in OrigamiMesh triangle)
		{
			m_triangles.AddRange(new List<int> { triangle.GetMeshVertexInfo(0).Index, triangle.GetMeshVertexInfo(1).Index, triangle.GetMeshVertexInfo(2).Index });
		}

		public MeshVertexInfo GetVertexInfo(in int idx)
		{
			if (m_vertexInfoList.Count <= idx) throw new System.ArgumentOutOfRangeException("LOG: idx is out of range!");
			return m_vertexInfoList[idx];
		}

		// メッシュの情報を更新する
		public void UpdateMesh(Mesh mesh)
		{
			mesh.Clear();

			//値の代入と再計算
			mesh.SetVertices(m_vertices);
			mesh.SetUVs(0, m_uvs);
			mesh.SetTriangles(m_triangles, 0, true);
			mesh.RecalculateNormals();
		}

		public MeshVertices()
		{
			m_vertices = new List<Vector3>();
			m_vertexInfoList = new List<MeshVertexInfo>();
		}
	}
}