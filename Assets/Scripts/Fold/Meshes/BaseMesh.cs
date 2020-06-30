using UnityEngine;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

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

        //折紙の元となるメッシュ
        protected Mesh m_mesh;

        //折紙のメッシュのオブジェクトの参照
        private GameObject m_meshObject = null;
        public GameObject MeshObject => m_meshObject;

        //表を上に向けているか
        public bool IsFacingUp { get; protected set; }

        //頂点情報のキャッシュ用
        protected MeshVertices m_vertices;

        //外部公開用の頂点情報
        public MeshVertices MeshVertices { get => m_vertices; }

        /// <summary>
        /// 定数
        /// </summary>

        //代入するUV値のデフォルト値を保持する。0fに設定するとポリゴンのエッジがメッシュの反対側の色となるため、0.1fとしている
        private static readonly List<Vector2> m_origamiUV = new List<Vector2> { new Vector2(0.1f, 0.1f), new Vector2(0.7f, 0.7f), new Vector2(0.1f, 0.2f), 
                                                                                new Vector2(0.7f, 0.9f), new Vector2(0.2f, 0.2f), new Vector2(0.9f, 0.9f) };

        //代入するポリゴン座標の描画順を保持する。表向きの場合
        private static readonly List<int> m_origamiTriangles = new List<int> { 0, 2, 4, 5, 3, 1 };

        //代入するポリゴン座標の描画順を保持する。裏向きの場合
        private static readonly List<int> m_origamiTrianglesReversed = new List<int> { 4, 2, 0, 1, 3, 5 };

        //添字参照用のリスト
        private List<int> m_triangles;

        //マテリアル
        private static Material m_material;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="vertices">メッシュの頂点</param>
        /// <param name="facing">メッシュの向き</param>
        /// <param name="materialPath">マテリアルのパス</param>
        /// <param name="parent">オブジェクトの親</param>
        protected OrigamiBase(in Vector3[] vertices, in IEnumerable<int> layers, IEnumerable<bool> connected, bool facing, in string materialPath, in Transform parent)
        {
            if (3 != vertices.Count())
            {
                Debug.LogError("Vertex count is wrong");
                return;
            }

            if (3 != layers.Count())
            {
                Debug.LogError("Layers count is wrong");
                return;
            }

            IsFacingUp = facing;

            m_vertices = new MeshVertices(vertices, layers, connected, vertices.Length);

            if (facing)
            {
                m_triangles = m_origamiTriangles;
            }
            else
            {
                m_triangles = m_origamiTrianglesReversed;
            }

            if (!m_material)
            {
                m_material = Resources.Load<Material>(materialPath);
            }

            //メッシュを生成
            m_mesh = new Mesh();

            //メッシュを付与するゲームオブジェクトを生成
            m_meshObject = new GameObject();
            m_meshObject.AddComponent<MeshRenderer>().material = m_material;
            m_meshObject.AddComponent<MeshFilter>().mesh = m_mesh;
            m_meshObject.transform.SetParent(parent);

            //メッシュを三角形として初期化
            UpdateOrigamiTriangleMesh(vertices);
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
                m_vertices.ConnectedToCreaseList[i++] = vertex.IsConnectedToCrease;
            }

            this.UpdateOrigamiTriangleMesh(vertices.ElementAt(0).Vertex, vertices.ElementAt(1).Vertex, vertices.ElementAt(2).Vertex);
        }

        public void UpdateOrigamiLayers(in int layer1, in int layer2, in int layer3)
        {
            m_vertices.Layers[0] = layer1;
            m_vertices.Layers[1] = layer2;
            m_vertices.Layers[2] = layer3;
        }

        //tを正規化せずに行う線形補間
        protected static Vector3 FastLerp(in Vector3 start, in Vector3 end, in float t)
        {
            return start + (end - start) * t;
        }
    }
}