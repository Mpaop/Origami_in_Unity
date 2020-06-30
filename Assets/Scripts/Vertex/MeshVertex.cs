using UnityEngine;

namespace Origami_Mesh
{
    //折り紙のメッシュで使う頂点、レイヤー情報、折り目と接しているフラグという3つの情報を持つ
    public class MeshVertexInfo
    {
        //頂点の値が更新された際に呼ばれるデリゲートの型
        public delegate void OnUpdateMeshVertex(in Vector3 vertex);

        // メッシュの頂点を管理しているクラスへの参照
        private MeshVertices m_vertices;

        // 頂点のインデックス
        private int m_idx;

        //各頂点の値が更新される時に呼ばれるデリゲート
        private OnUpdateMeshVertex m_onupdateVertex;
        public Vector3 Vertex 
        {
            get => m_vertices[m_idx];
            set 
            {
                m_vertices[m_idx] = value;
                m_onupdateVertex?.Invoke(value);
            }
        }

        public event OnUpdateMeshVertex OnUpdateVertex
        {
            add => m_onupdateVertex += value;
            remove => m_onupdateVertex -= value;
        }

        public int Layer {get; set; }

        public bool IsConnectedToCrease { get; set;}

        public MeshVertexInfo(in MeshVertices vertices, in Vector3 vx, in int l, bool isConnectedToCrease, OnUpdateMeshVertex onUpdate = null)
        {
            m_vertices = vertices;
            m_idx = 0;
            m_onupdateVertex = onUpdate;
            Layer = l;
            IsConnectedToCrease = isConnectedToCrease;
        }
    }
}