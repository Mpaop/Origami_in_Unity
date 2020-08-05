using System;
using System.Collections.Generic;
using UnityEngine;
using Origami_Utility;

namespace Origami_Mesh
{
	public class Origami
	{
		// 頂点情報を管理するクラス
		private MeshVertices m_vertices;

		// ポリゴンを生成するための情報をまとめるクラスのリスト
		private List<OrigamiMesh> m_meshTriangles;

		// Unity上で表現されるメッシュのオブジェクト
		private Mesh m_mesh;

		// メッシュをアタッチするオブジェクト
		private GameObject m_meshObject = null;
		// 外部参照用のGetter
		public GameObject GameObject => m_meshObject;

		//マテリアル
		private Material m_material;

		// コンストラクタ
		public Origami(in Vector3[] vertices, in Vector3 facingAxis, in string materialPath, in Transform parent)
		{
			// 折り紙は初期化時に必ず四角から始まるので、確認する
			if (4 != vertices.Length)
			{
				throw new System.Exception("LOG: The number of vertices for an Origami have to exactly start with 4!");
			}

			// マテリアルの生成
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
			m_meshObject.name = "Origami";

			m_vertices = new MeshVertices();
			m_meshTriangles = new List<OrigamiMesh>();

			// 頂点やポリゴンの初期化
			// verticesをソート。以下のような順番となっている
			// 0--------1
			// |        |
			// 3--------2
			getSortedSquareIndices(vertices, facingAxis, out int[] outIndices);
			Vector2[] uvs = new Vector2[4] { new Vector2(0.0f, 0.0f), new Vector2(1.0f, 0.0f), new Vector2(1.0f, 1.0f), new Vector2(0.0f, 1.0f) };
			for (int i = 0; i < 4; ++i)
			{
				m_vertices.AddVertex(vertices[i], outIndices[i], 0, uvs[i], false);
			}

			m_meshTriangles.Add(new OrigamiMesh(m_vertices.GetVertexInfo(0), m_vertices.GetVertexInfo(1), m_vertices.GetVertexInfo(2), true));
			m_meshTriangles.Add(new OrigamiMesh(m_vertices.GetVertexInfo(0), m_vertices.GetVertexInfo(2), m_vertices.GetVertexInfo(3), true));

			foreach (var tri in m_meshTriangles) m_vertices.SetTriangle(tri);

			m_vertices.UpdateMesh(m_mesh);
		}

		/// <summary>
		/// 三角ポリゴンが正しい向きに表示されるようにソートされたインデックスを返す
		/// </summary>
		/// <param name="vertices">頂点</param>
		/// <param name="facingAxis">ユーザーが表と見なす方向の軸ベクトル</param>
		/// <param name="indices">インデックス</param>
		private static void getSortedTriangleIndices(in Vector3[] vertices, in Vector3 facingAxis, out int[] indices)
		{
			if (3 != vertices.Length) throw new System.Exception("LOG: The number of vertices in the array must be exactly 3!");
			indices = new int[vertices.Length];

			var dir10 = vertices[1] - vertices[0];
			var dir20 = vertices[2] - vertices[0];

			// 外積及び内積を行い、facingAxisに対してvertices012が同じ向きかどうかをチェックする
			var cross = Vector3.Cross(dir10, dir20);
			var dot = Vector3.Dot(facingAxis, cross);

			if (0 < dot)
			{
				indices[0] = 0;
				indices[1] = 2;
				indices[2] = 1;
			}
			else
			{
				indices[0] = 0;
				indices[1] = 1;
				indices[2] = 2;
			}
		}

		/// <summary>
		/// 4つの座標が正しい向きに表示されるようにソートされたインデックスを返す
		/// </summary>
		/// <param name="vertices">頂点</param>
		/// <param name="facingAxis">ユーザーが表と見なす方向の軸ベクトル</param>
		/// <param name="indices">インデックス</param>
		private static void getSortedSquareIndices(in Vector3[] vertices, in Vector3 facingAxis, out int[] indices)
		{
			if (4 != vertices.Length) throw new System.Exception("LOG: The number of vertices in the array must be exactly 3!");
			indices = new int[vertices.Length];

			// 内積による比較を行うため、正規化する
			var dir10 = (vertices[1] - vertices[0]).normalized;
			var dir20 = (vertices[2] - vertices[0]).normalized;
			var dir30 = (vertices[3] - vertices[0]).normalized;

			// dir10に対してdir20 と dir30 が左右のどちらにあるのかを判定する
			var cross1 = Vector3.Cross(dir10, dir20);
			var cross2 = Vector3.Cross(dir10, dir30);

			// まずcross1 と cross2の内積を行い、vertices[2]と[3]がdir10の左右一方に偏っているをチェック
			var dot = Vector3.Dot(cross1, cross2);
			// facingAxisと内積し、正の値を返した方がdir10より右側にあると判断する
			var dot2 = Vector3.Dot(facingAxis, cross1);

			// インデックスは四角がこのようになるように返す
			// 0--------1
			// |        |
			// 3--------2

			// vertices[2]と[3]が左右どちらかに偏っている
			if (0 < dot)
			{
				indices[0] = 0;

				// 偏っていることは分かったが、vertices[2]と[3]の位置関係は不明であるため、内積で判定する
				var d1 = Vector3.Dot(dir10, dir20);
				var d2 = Vector3.Dot(dir10, dir30);

				// vertices[2]と[3]が右側にある
				if (0 < dot2)
				{
					indices[1] = 1;

					// dir20の方がdir10に近い
					if (d1 > d2)
					{
						indices[2] = 2;
						indices[3] = 3;
					}
					else
					{
						indices[2] = 3;
						indices[3] = 2;
					}
				}
				else
				{
					indices[3] = 1;

					// dir20の方がdir10に近い
					if (d1 > d2)
					{
						indices[1] = 3;
						indices[2] = 2;
					}
					else
					{
						indices[1] = 2;
						indices[2] = 3;
					}
				}
			}
			// 偏っていない
			else
			{
				indices[0] = 0;
				indices[2] = 1;

				// vertices[2]が右側にあるので
				if (0 < dot2)
				{
					indices[1] = 3;
					indices[3] = 2;
				}
				else
				{
					indices[1] = 2;
					indices[3] = 3;
				}
			}
		}
	}
}
