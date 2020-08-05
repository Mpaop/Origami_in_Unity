using UnityEngine;

namespace Origami_Utility
{
	//作成者：Mpaop
	//便利なメソッドをまとめたクラス
	public static class OrigamiUtility
	{
		//円周360度
		public const float TWO_PI = Mathf.PI * 2f;

		//90度
		public const float HALF_PI = Mathf.PI * 0.5f;


		//角度をずらすための数値
		public const float ANGLE_OFFSET = Mathf.PI / 180f;

		//誤差判定用の閾値。桁数を多め
		public const double ALLOWABLE_MARGIN_HIGH_PRECISION = 0.00001;

		//誤差判定用の閾値。桁数を少なめ
		public const double ALLOWABLE_MARGIN_LOW_PRECISION = 0.0001;

		//折り目の長さを求める用
		public const float SINCOS_45 = 0.7071068f;

		//0度
		public static readonly Matrix4x4 XROTATION_MAT_0DEG = new Matrix4x4(new Vector4(1, 0, 0, 0),
																			new Vector4(0, 1, 0, 0),
																			new Vector4(0, 0, 1, 0),
																			new Vector4(0, 0, 0, 1));
		//90度
		public static readonly Matrix4x4 XROTATION_MAT__90DEG = new Matrix4x4(new Vector4(1, 0, 0, 0),
																			  new Vector4(0, 0, 1, 0),
																			  new Vector4(0, -1, 0, 0),
																			  new Vector4(0, 0, 0, 1));
		//180度
		public static readonly Matrix4x4 XROTATION_MAT__180DEG = new Matrix4x4(new Vector4(1, 0, 0, 0),
																			   new Vector4(0, -1, 0, 0),
																			   new Vector4(0, 0, -1, 0),
																			   new Vector4(0, 0, 0, 1));
		//270度
		public static readonly Matrix4x4 XROTATION_MAT__270DEG = new Matrix4x4(new Vector4(1, 0, 0, 0),
																			   new Vector4(0, 0, -1, 0),
																			   new Vector4(0, 1, 0, 0),
																			   new Vector4(0, 0, 0, 1));

		//eFoldTypeに従ってラジアンを変換する
		public static float ConvertRadiansByFoldType(in float radians, in Origami_Fold.eFoldType type)
		{
			float rad;

			if (type == Origami_Fold.eFoldType.MoutainFold)
			{
				rad = radians;
			}
			else
			{
				rad = TWO_PI - radians;
			}

			return rad;
		}

		//X軸の回転行列を取得する
		public static Matrix4x4 GetXRotationMatrix(in float rad)
		{
			var sin = Mathf.Sin(rad);
			var cos = Mathf.Cos(rad);

			//※この記述だとVector4が行に見えるが、実は列
			Matrix4x4 matX = new Matrix4x4(new Vector4(1, 0, 0, 0),
										   new Vector4(0, cos, sin, 0),
										   new Vector4(0, -sin, cos, 0),
										   new Vector4(0, 0, 0, 1));

			return matX;
		}

		//y軸の回転行列を取得する
		public static Matrix4x4 GetYRotationMatrix(in float rad)
		{
			var sin = Mathf.Sin(rad);
			var cos = Mathf.Cos(rad);

			Matrix4x4 matY = new Matrix4x4(new Vector4(cos, 0, -sin, 0),
										   new Vector4(0, 1, 0, 0),
										   new Vector4(sin, 0, cos, 0),
										   new Vector4(0, 0, 0, 1));

			return matY;
		}

		//Z軸の回転行列を取得する
		public static Matrix4x4 GetZRotationMatrix(in float rad)
		{
			var sin = Mathf.Sin(rad);
			var cos = Mathf.Cos(rad);

			Matrix4x4 matZ = new Matrix4x4(new Vector4(cos, sin, 0, 0),
										   new Vector4(-sin, cos, 0, 0),
										   new Vector4(0, 0, 1, 0),
										   new Vector4(0, 0, 0, 1));

			return matZ;
		}

		//座標から方向ベクトルに対して垂直な直線を引いた時の交点を得る
		// origin: 座標
		// creasePoint: 始点
		// creaseVec: 方向ベクトル
		// magnitude: 方向ベクトルの大きさ
		public static Vector3 GetPerpendicularIntersectionPoint(in Vector3 point, in Vector3 creasePoint, in Vector3 creaseVec, in float sqrMagnitude)
		{
			//0除算を避けたいのでチェック
			if (sqrMagnitude == 0.0f) return Vector3.zero;

			//直線ベクトルに対し、(交点-変換する頂点)を内積すると、垂直であるため0となる。
			//また交点は直線の始点から直線ベクトル(=終点-始点)の間にあるため、始点+直線ベクトル*係数tと表せる
			//Z値を計算に入れたくないのでVector2.Dotを使う
			float t = Vector2.Dot(creaseVec, point - creasePoint) / sqrMagnitude;

			//交点を求める
			return creasePoint + t * creaseVec;
		}

		//上と同じだがmagnitudeをメソッド内で求める場合
		public static Vector3 GetPerpendicularIntersectionPoint(in Vector3 point, in Vector3 creasePoint, in Vector3 creaseVec)
		{
			var mag = creaseVec.sqrMagnitude;
			return GetPerpendicularIntersectionPoint(point, creasePoint, creaseVec, mag);
		}

		//スワップ
		public static void Swap<T>(ref T val1, ref T val2)
		{
			var temp = val1;
			val1 = val2;
			val2 = temp;
		}

	}
}
