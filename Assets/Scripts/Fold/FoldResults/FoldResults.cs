using UnityEngine;
using Origami_Mesh;
using Origami_Utility;

//作成者: Mpaop
//折る時に必要な構造体をまとめたファイル
namespace Origami_Result
{
	//折る際に使う角度によって使う座標の判別用
	public enum eFoldAngles
	{
		Point0,
		Point90
	}

	//折る処理で用いるインターフェース
	public interface IFoldResults
	{

		eFoldAngles GetOffsetData(in float rad, out float t);
		bool ContinueFolding(in float rad);
	}

	//最も基本的な折った時の結果を持つ構造体
	public readonly struct FoldResult
	{
		//折る直前までの座標
		public readonly Vector3 FoldOriginalPoint0;
		//FoldOriginalPointから折り目に垂線を下ろした時に交差する座標
		public readonly Vector3 FoldMidPoint0;

		//90度まで折った後、基準とする座標を再び変えるため、その変換処理を行った座標
		public readonly Vector3 FoldOriginalPoint90;
		public readonly Vector3 FoldMidPoint90;

		public FoldResult(in Vector3 origin, in Vector3 midPoint, in Vector3 creaseOffset, in Matrix4x4 matX, in Matrix4x4 matZ, out bool isConnectedToOtherMesh)
		{
			//midPointはメッシュと二次元的に交差した値で取っているため、z値をoriginに合わせる
			var mag = new Vector2(origin.x - midPoint.x, origin.y - midPoint.y).magnitude;
			isConnectedToOtherMesh = mag <= OrigamiUtility.ALLOWABLE_MARGIN_HIGH_PRECISION;

			var mid = midPoint;
			mid.z = origin.z;

			FoldOriginalPoint0 = origin;
			FoldMidPoint0 = mid;

			// 原点が折り目に接している点と接していない点で分ける
			// また、接している場合は、
			if (isConnectedToOtherMesh)
			{
				FoldMidPoint90 = OrigamiMesh.GetRotatedVector3(FoldMidPoint0 + creaseOffset, FoldMidPoint0, matX, matZ);
				FoldOriginalPoint90 = FoldMidPoint90;
			}
			else
			{
				FoldMidPoint90 = OrigamiMesh.GetRotatedVector3(FoldMidPoint0 + creaseOffset, FoldMidPoint0, matX, matZ);
				FoldOriginalPoint90 = OrigamiMesh.GetRotatedVector3(origin + creaseOffset, origin, matX, matZ);
			}
		}

		public FoldResult(in Vector3 origin, in Vector3 midPoint, in Vector3 creaseOffset, in Matrix4x4 matX, in Matrix4x4 matZ, bool isConnectedToOtherMesh)
		{
			//midPointはメッシュと二次元的に交差した値で取っているため、z値をoriginに合わせる
			var mid = midPoint;
			mid.z = origin.z;

			FoldOriginalPoint0 = origin;

			FoldMidPoint0 = mid;
			FoldMidPoint90 = OrigamiMesh.GetRotatedVector3(FoldMidPoint0 + creaseOffset, FoldMidPoint0, matX, matZ);

			//原点が折り目に接している点と接していない点で分ける
			if (isConnectedToOtherMesh)
			{
				FoldOriginalPoint90 = FoldMidPoint90;
			}
			else
			{
				FoldOriginalPoint90 = OrigamiMesh.GetRotatedVector3(origin + creaseOffset, origin, matX, matZ);
			}
		}
	}
}
