using UnityEngine;

namespace Origami_Mesh
{
	//折り紙のメッシュで使う頂点、レイヤー情報、折り目と接しているフラグという3つの情報を持つ
	public class MeshVertexInfo
	{
		// インデックスを書き換える際に呼ばれるデリゲートの型
		public delegate void OnUpdateMeshIndex(in int idx);

		//頂点の値が更新された際に呼ばれるデリゲートの型
		public delegate void OnUpdateMeshVertex(in Vector3 vertex);

		// メッシュの頂点を管理しているクラスへの参照
		private MeshVertices m_vertices;
		public int Layer { get; set; }
		public bool IsConnectedToCrease { get; set; }

		// 頂点のインデックス
		private int m_idx;
		// インデックス更新時に呼ばれるデリゲート
		private OnUpdateMeshIndex m_onUpdateIdx;
		public int Index
		{
			get => m_idx;
			set
			{
				m_idx = value;
				m_onUpdateIdx?.Invoke(value);
			}
		}

		public event OnUpdateMeshIndex OnUpdateIndex
		{
			add => m_onUpdateIdx += value;
			remove => m_onUpdateIdx -= value;
		}

		//各頂点の値が更新される時に呼ばれるデリゲート
		private OnUpdateMeshVertex m_onUpdateVtx;
		public Vector3 Vertex
		{
			get => m_vertices[m_idx];
			set
			{
				m_vertices[m_idx] = value;
				m_onUpdateVtx?.Invoke(value);
			}
		}

		public event OnUpdateMeshVertex OnUpdateVertex
		{
			add => m_onUpdateVtx += value;
			remove => m_onUpdateVtx -= value;
		}

		public MeshVertexInfo(in MeshVertices vertices, in int idx, in int layer, in bool isConnectedToCrease, OnUpdateMeshVertex updateVtx = null, OnUpdateMeshIndex updateIdx = null)
		{
			m_vertices = vertices;
			m_idx = idx;
			Layer = layer;
			IsConnectedToCrease = isConnectedToCrease;
			m_onUpdateVtx = updateVtx;
			m_onUpdateIdx = updateIdx;
		}
	}
}