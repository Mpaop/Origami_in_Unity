using UnityEngine;
using UnityEngine.EventSystems;
using Origami_Fold;
using Origami_Mesh;

namespace OrigamiDemo
{
	//作成者：Mpaop
	//メッシュを折る線を書くクラス
	public class MeshCreaseDrawer : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
	{

		//線を引く際の始点として使う座標。OnPointerDownで代入する
		public Vector3 CreaseStartPoint { get; private set; }

		//線を引く際の終点として使う座標。OnPointerUpで代入する
		public Vector3 CreaseEndPoint { get; private set; }

		//マウスがクリックされてから、離されるまでの間、trueとなり、Update内の処理を実行可能にする
		private bool m_IsDrawing = false;

		//折り目の線として表される線のLine Renderer
		private LineRenderer m_Crease = null;

		//折紙を折るクラスの参照
		private MeshFoldMachine m_FoldMachine = null;

		//コールバック
		protected System.Action m_EndPhaseCallBack;

		//折紙を回転させる際に使う時間のパラメータ
		private float m_RotationAngle;

		//折紙を折っている間の処理
		private bool m_IsFolding;

		//折る方向ベクトル
		private Vector3 m_CreaseDirectionVector;

		[SerializeField, Range(1.0f, 10.0f)]
		private float m_OrigamiSize;

		[SerializeField]
		private eFoldType m_foldType;

		//基本的にデバッグ用、マウスの左クリックで始点を指定する
		public void OnPointerDown(PointerEventData data)
		{
			if (m_IsFolding) return;

			//z軸の1.0fを加算しているのは、カメラより手前に表示されるようにするため
			CreaseStartPoint = Camera.main.ScreenToWorldPoint((Vector3)data.position + new Vector3(0, 0, 1.0f));
			CreaseEndPoint = Camera.main.ScreenToWorldPoint((Vector3)data.position + new Vector3(0, 0, 1.0f));

			m_Crease.SetPosition(0, CreaseStartPoint);
			m_IsDrawing = true;
		}

		//基本的にデバッグ用、マウスの左クリックを押している間に、終点をを指定する
		public void OnDrag(PointerEventData data)
		{
			if (m_IsFolding) return;

			CreaseEndPoint = Camera.main.ScreenToWorldPoint((Vector3)data.position + new Vector3(0, 0, 1.0f));
		}

		//基本的にデバッグ用、マウスの左クリックを離すことで終点指定する
		public void OnPointerUp(PointerEventData data)
		{
			if (m_IsFolding) return;

			CreaseEndPoint = Camera.main.ScreenToWorldPoint((Vector3)data.position + new Vector3(0, 0, 1.0f));
			m_IsDrawing = true;
		}

		//ボタンを押すと折る
		public void OnFoldPress()
		{
			if (m_IsFolding || (CreaseEndPoint - CreaseStartPoint).magnitude == 0) return;

			//線の長さが足りない場合を考慮して
			m_CreaseDirectionVector = (CreaseEndPoint - CreaseStartPoint) * 5.0f;

			var pos1 = new Vector3(CreaseStartPoint.x + -m_CreaseDirectionVector.x, CreaseStartPoint.y + -m_CreaseDirectionVector.y, 0.0f);
			var pos2 = new Vector3(CreaseEndPoint.x + m_CreaseDirectionVector.x, CreaseEndPoint.y + m_CreaseDirectionVector.y, 0.0f);

			m_FoldMachine.InitializeFold(pos1, pos2, m_foldType);

			m_IsDrawing = false;
			m_IsFolding = true;
		}

		//シーンを再ロードする
		public void ReloadScene()
		{
			UnityEngine.SceneManagement.SceneManager.LoadScene("FoldDemoScene");
		}

		void Start()
		{
			m_Crease = GameObject.FindGameObjectWithTag("MeshCrease").GetComponent<LineRenderer>();

			var parent = new GameObject();
			parent.name = "Mesh_Parent";

			var list = new System.Collections.Generic.List<OrigamiMesh>();
			var path = System.IO.Path.Combine("Material", "OrigamiMaterial");

			var connectedToCrease = new System.Collections.Generic.List<bool> { false, false, false };

			list.Add(new OrigamiMesh(MeshFoldMachine.GetVerticesSortedClockwise(new Vector3(-m_OrigamiSize, -m_OrigamiSize, 0), new Vector3(m_OrigamiSize, m_OrigamiSize, 0), new Vector3(m_OrigamiSize, -m_OrigamiSize, 0)), true, 0, connectedToCrease, path, parent.transform));
			list.Add(new OrigamiMesh(MeshFoldMachine.GetVerticesSortedClockwise(new Vector3(-m_OrigamiSize, -m_OrigamiSize, 0), new Vector3(-m_OrigamiSize, m_OrigamiSize, 0), new Vector3(m_OrigamiSize, m_OrigamiSize, 0)), true, 0, connectedToCrease, path, parent.transform));

			m_FoldMachine = new MeshFoldMachine(list, m_OrigamiSize, parent.transform, true);

			m_RotationAngle = 0f;
		}

		void Update()
		{
			if (m_IsDrawing)
			{

				if (m_Crease.positionCount != 2)
				{
					m_Crease.SetPositions(new Vector3[] { CreaseStartPoint, CreaseEndPoint });
					return;
				}

				m_Crease.SetPosition(1, CreaseEndPoint);
				return;
			}


			if (m_IsFolding)
			{
				if (m_RotationAngle <= Mathf.PI)
				{
					m_RotationAngle += (Mathf.PI / 180f);

					m_FoldMachine.FoldMeshToAngle(m_RotationAngle, m_foldType);
				}
				else
				{
					m_FoldMachine.EndFold(m_foldType);

					m_IsFolding = false;
					m_IsDrawing = true;
					m_RotationAngle = 0f;
					if (m_EndPhaseCallBack != null) m_EndPhaseCallBack();
				}
			}


		}
	}

}