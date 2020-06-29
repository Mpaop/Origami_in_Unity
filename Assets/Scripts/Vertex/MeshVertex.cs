using UnityEngine;

namespace Origami_Mesh
{
    //折り紙のメッシュで使う頂点、レイヤー情報、折り目と接しているフラグという3つの情報を持つ
    public readonly struct MeshVertex
    {
        public readonly Vector3 Vertex;
        public readonly int Layer;

        public readonly bool IsConnectedToCrease;

        public MeshVertex(in Vector3 vx, in int l, bool isConnectedToCrease)
        {
            Vertex = vx;
            Layer = l;
            IsConnectedToCrease = isConnectedToCrease;
        }
    }
}