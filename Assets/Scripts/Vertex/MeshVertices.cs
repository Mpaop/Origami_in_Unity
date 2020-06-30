using UnityEngine;
using System.Collections.Generic;

namespace Origami_Mesh
{
    //上のMeshVertexをMeshクラスで扱いやすいようにそれぞれ分解したリストで持ち、管理する
    sealed public class MeshVertices
    {
        private List<Vector3> m_vertices;

        private List<MeshVertexInfo> m_vertexInfoList;

        private List<int> m_layers;
        public List<int> Layers => m_layers;

        private List<bool> m_connectedList;
        public List<bool> ConnectedToCreaseList => m_connectedList;

        public readonly int Size;

        //頂点の値が更新された際に呼ばれるデリゲートの型
        public delegate void OnUpdateVertex(in Vector3 vertex);

        //各頂点の値が更新される時に呼ばれるデリゲート
        private OnUpdateVertex[] m_onupdateVertices;

        public Vector3[] GetMeshVertices()
        {
            return new Vector3[3] { m_vertices[0], m_vertices[2], m_vertices[4] };
        }

        public void setVertices(Mesh mesh) => mesh.SetVertices(m_vertices);

        public void AddUpdateVertexEventAt(in int idx, OnUpdateVertex updateVertex)
        {
            if (0 > idx || m_onupdateVertices.Length <= idx)
            {
                Debug.LogError($"idx is invalid idx={idx}");
                throw new System.ArgumentOutOfRangeException();
            }

            m_onupdateVertices[idx] += updateVertex;
        }

        public void RemoveUpdateVertexEventAt(in int idx, OnUpdateVertex updateVertex)
        {
            if (0 > idx || m_onupdateVertices.Length <= idx)
            {
                Debug.LogError($"idx is invalid idx={idx}");
                throw new System.ArgumentOutOfRangeException();
            }

            m_onupdateVertices[idx] -= updateVertex;
        }

        public void EmptyUpdateVertexEventAt(in int idx)
        {
            if (0 > idx || m_onupdateVertices.Length <= idx)
            {
                Debug.LogError($"idx is invalid idx={idx}");
                throw new System.ArgumentOutOfRangeException();
            }

            m_onupdateVertices[idx] = null;
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

        public MeshVertices(in Vector3[] vertices, in IEnumerable<int> layers, in IEnumerable<bool> connected, int size)
        {
            m_layers = new List<int>(size);
            m_layers.AddRange(layers);

            m_connectedList = new List<bool>(size);
            m_connectedList.AddRange(connected);

            m_onupdateVertices = new OnUpdateVertex[size];

            m_vertices = new List<Vector3>(size * 2);
            //同じ値の頂点は偶数と奇数に分ける
            for (int i = 0; i < size; ++i)
            {
                this[i] = vertices[i];
            }

            Size = size;
        }

        public void SetMeshVertexAt(int idx, in Vector3 vx, in int layer)
        {
            if (0 > idx || Size <= idx)
            {
                throw new System.ArgumentOutOfRangeException();
            }

            this[idx] = vx;
            m_layers[idx] = layer;
        }

        public void SetMeshVertexConnectedFlag(int idx, bool isConnected)
        {
            if (0 > idx || Size <= idx)
            {
                throw new System.ArgumentOutOfRangeException();
            }

            m_connectedList[idx] = isConnected;
        }

        public MeshVertexInfo GetMeshVertexAt(in int idx)
        {
            if (0 > idx || Size <= idx)
            {
                throw new System.ArgumentOutOfRangeException();
            }

            return new MeshVertexInfo(this, this[idx], m_layers[idx], m_connectedList[idx]);
        }

        public void ResetConnectionFlags()
        {
            for (int i = 0; i < m_connectedList.Count; ++i)
            {
                m_connectedList[i] = false;
            }
        }
    }
}