using UnityEngine;
using System.Collections.Generic;
using System.Linq;

//作成者：Mpaop
//折紙用のメッシュ関連をまとめたソースファイル
namespace Origami_Mesh
{

    public interface IFoldMeshCallbacks
    {
        void OnEndFold();
    }

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

	//上のMeshVertexをMeshクラスで扱いやすいようにそれぞれ分解したリストで持ち、管理する
	public class MeshVertices
	{
		private List<Vector3> m_vertices;

		public List<Vector3> Vertices => m_vertices;

		//頂点の値が更新された際に呼ばれるデリゲートの型
		public delegate void OnUpdateVertex(in Vector3 vertex);

		//各頂点の値が更新される時に呼ばれるデリゲート
		private OnUpdateVertex[] m_onupdateVertices;

		public void AddUpdateVertexEventAt(in int idx, OnUpdateVertex updateVertex)
		{
			if(0 > idx || m_onupdateVertices.Length <= idx)
			{
				Debug.LogError($"idx is invalid idx={idx}");
				return;
			}

			m_onupdateVertices[idx] += updateVertex;
		}

		public void RemoveUpdateVertexEventAt(in int idx, OnUpdateVertex updateVertex)
        {
            if (0 > idx || m_onupdateVertices.Length <= idx)
            {
                Debug.LogError($"idx is invalid idx={idx}");
                return;
            }

            m_onupdateVertices[idx] -= updateVertex;
		}

		public void EmptyUpdateVertexEventAt(in int idx)
        {
            if (0 > idx || m_onupdateVertices.Length <= idx)
            {
                Debug.LogError($"idx is invalid idx={idx}");
                return;
            }

            m_onupdateVertices[idx] = null;
		}

		//インデクサ
		public Vector3 this [int i]
		{
			get => m_vertices[i];
			set
			{
				m_vertices[i] = value;
				m_onupdateVertices[i]?.Invoke(in value);
			}
		}

		private List<int> m_layers;
		public List<int> Layers => m_layers;

		private List<bool> m_connectedList;
		public List<bool> ConnectedToCreaseList => m_connectedList;

		public readonly int Size;

		public MeshVertices(in IEnumerable<Vector3> vertices, in IEnumerable<int> layers, in IEnumerable<bool> connected, int size)
		{
			m_vertices = new List<Vector3>(size * 2);
            m_vertices.AddRange(vertices);
            m_vertices.AddRange(vertices);

			m_layers = new List<int>(size);
			m_layers.AddRange(layers);

			m_connectedList = new List<bool>(size);
			m_connectedList.AddRange(connected);

			m_onupdateVertices = new OnUpdateVertex[size];

			Size = size;
		}

		public void SetMeshVertexAt(int idx, in Vector3 vx, in int layer)
		{
			if(0 > idx || Size <= idx)
			{
				throw new System.ArgumentOutOfRangeException();
			}

			m_vertices[idx] = vx;
			m_layers[idx] = layer;
		}

		public void SetMeshVertexConnectedFlag(int idx, bool isConnected)
        {
            if (0 > idx || Size <= idx)
            {
                throw new System.ArgumentOutOfRangeException();
            }
		}

		public MeshVertex GetMeshVertexAt(in int idx)
		{
			if(0 > idx || Size <= idx)
			{
				throw new System.ArgumentOutOfRangeException();
			}

			return new MeshVertex(m_vertices[idx], m_layers[idx], m_connectedList[idx]);
		}

		public void ResetConnectionFlags()
		{
			for (int i = 0; i < m_connectedList.Count; ++i)
			{
				m_connectedList[i] = false;
			}
		}
	}

	//折り紙と折り目のクラスを汎化させたクラス
	public abstract class OrigamiBase
	{
		/// <summary>
		/// メンバフィールド
		/// </summary>

		//折紙の元となるメッシュ
		protected Mesh m_Mesh;

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

		//代入するUV値のデフォルト値を保持する. 多分テクスチャ作りで失敗したと思うのだが、0fに設定するとポリゴンの辺の部分が赤くなるので0.1fとしている
		private static readonly List<Vector2> m_OrigamiUV = new List<Vector2> { new Vector2(0.1f, 0.1f), new Vector2(0.1f, 0.2f), new Vector2(0.2f, 0.2f),
																				new Vector2(0.7f, 0.7f), new Vector2(0.7f, 0.9f), new Vector2(0.9f, 0.9f) };

		//代入するポリゴン座標の描画順を保持する。表向きの場合
		private static readonly List<int> m_OrigamiTriangles = new List<int> { 0, 1, 2, 5, 4, 3 };

		//代入するポリゴン座標の描画順を保持する。裏向きの場合
		private static readonly List<int> m_OrigamiTrianglesReversed = new List<int> { 2, 1, 0, 3, 4, 5 };

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
		protected OrigamiBase(in IEnumerable<Vector3> vertices, in IEnumerable<int> layers, IEnumerable<bool> connected, bool facing, in string materialPath, in Transform parent)
		{
            if (3 != vertices.Count())
            {
                Debug.LogError("Vertices count is wrong");
                return;
            }

            if (3 != layers.Count())
            {
                Debug.LogError("Layers count is wrong");
                return;
            }
			
			IsFacingUp = facing;

			m_vertices = new MeshVertices(vertices, layers, connected, vertices.Count());

			if (facing)
			{
				m_triangles = m_OrigamiTriangles;
			}
			else
			{
				m_triangles = m_OrigamiTrianglesReversed;
			}

			if(!m_material)
			{
				m_material = Resources.Load<Material>(materialPath);
			}

			//メッシュを生成
			m_Mesh = new Mesh();

			//メッシュを付与するゲームオブジェクトを生成
			m_meshObject = new GameObject();
			m_meshObject.AddComponent<MeshRenderer>().material = m_material;
			m_meshObject.AddComponent<MeshFilter>().mesh = m_Mesh;
			m_meshObject.transform.SetParent(parent);

			//メッシュを三角形として初期化
			UpdateOrigamiTriangleMesh(m_vertices.Vertices);
		}


		/// <summary>
		/// メソッド
		/// </summary>		

		/// <summary>
		/// 与えた行列に従って回転されたベクトルを返す
		/// </summary>
		/// <param name="res"></param>
		/// <param name="matX"></param>
		/// <param name="matZ"></param>
		/// <returns></returns>
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
			m_Mesh.Clear();

			{
				m_vertices[0] = m_vertices[3] = ver1;
				m_vertices[1] = m_vertices[4] = ver2;
				m_vertices[2] = m_vertices[5] = ver3;

			}

			//値の代入と再計算
			m_Mesh.SetVertices(m_vertices.Vertices);
			m_Mesh.SetUVs(0, m_OrigamiUV);
			m_Mesh.SetTriangles(m_triangles, 0, true);

			m_Mesh.RecalculateNormals();
		}

		public void UpdateOrigamiTriangleMesh(in IEnumerable<Vector3> vertices)
		{
			if (IsFacingUp)
			{
				m_triangles = m_OrigamiTriangles;
			}
			else
			{
				m_triangles = m_OrigamiTrianglesReversed;
			}

			this.UpdateOrigamiTriangleMesh(vertices.ElementAt(0), vertices.ElementAt(1), vertices.ElementAt(2));
		}

		public void UpdateOrigamiTriangleMesh(in IEnumerable<MeshVertex> vertices)
        {
            if (IsFacingUp)
            {
                m_triangles = m_OrigamiTriangles;
            }
            else
            {
                m_triangles = m_OrigamiTrianglesReversed;
            }

			int i = 0;
			foreach(var vertex in vertices)
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