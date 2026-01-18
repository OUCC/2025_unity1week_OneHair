#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UI;
#endif
using UnityEngine;
using UnityEngine.UI;

namespace ScreenPocket
{
	/// <summary>
	/// 4すみの色を指定できるRawImage
	/// </summary>
	public class RawImage4Color : RawImage
	{
		/// <summary>
		/// 左上頂点の色
		/// </summary>
		[SerializeField]
		private Color _leftTopColor = Color.white;
		public Color leftTopColor
		{
			get => _leftTopColor;
			set
			{
				if (_leftTopColor != value)
				{
					_leftTopColor = value;
					SetVerticesDirty(); // ★これが必要！メッシュ再生成を予約
				}
			}
		}

		/// <summary>
		/// 右上頂点の色
		/// </summary>
		[SerializeField]
		private Color _rightTopColor = Color.white;
		public Color rightTopColor
		{
			get => _rightTopColor;
			set
			{
				if (_rightTopColor != value)
				{
					_rightTopColor = value;
					SetVerticesDirty(); // ★これが必要
				}
			}
		}

		/// <summary>
		/// 左下頂点の色
		/// </summary>
		[SerializeField]
		private Color _leftBottomColor = Color.white;
		public Color leftBottomColor
		{
			get => _leftBottomColor;
			set
			{
				if (_leftBottomColor != value)
				{
					_leftBottomColor = value;
					SetVerticesDirty(); // ★これが必要
				}
			}
		}

		/// <summary>
		/// 右下頂点の色
		/// </summary>
		[SerializeField]
		private Color _rightBottomColor = Color.white;
		public Color rightBottomColor
		{
			get => _rightBottomColor;
			set
			{
				if (_rightBottomColor != value)
				{
					_rightBottomColor = value;
					SetVerticesDirty(); // ★これが必要
				}
			}
		}

		/// <summary>
		/// インスペクターで値を変更したときに即座に反映させる用
		/// </summary>
		protected override void OnValidate()
		{
			base.OnValidate();
			SetVerticesDirty();
		}

		/// <summary>
		/// メッシュ生成
		/// </summary>
		protected override void OnPopulateMesh(VertexHelper vh)
		{
			base.OnPopulateMesh(vh);

			// 頂点が4つ（四角形1つ）であることを前提とした処理
			// RawImageやImageは通常4頂点で作られますが、念のためチェックしても良いです
			if (vh.currentVertCount < 4) return;

			// 頂点の順番は左下(0) -> 左上(1) -> 右上(2) -> 右下(3) の順であることが多いですが、
			// UIVertexを取得して確実に処理します

			SetVertexColor(vh, _leftBottomColor, 0);
			SetVertexColor(vh, _leftTopColor, 1);
			SetVertexColor(vh, _rightTopColor, 2);
			SetVertexColor(vh, _rightBottomColor, 3);
		}

		private void SetVertexColor(VertexHelper vh, Color color, int index)
		{
			UIVertex vertex = new UIVertex();
			vh.PopulateUIVertex(ref vertex, index);

			// base.color (全体の色) と掛け合わせることで、透明度などを維持します
			vertex.color = color * this.color;

			vh.SetUIVertex(vertex, index);
		}
	}

#if UNITY_EDITOR
	// Editor拡張側はそのままで大丈夫ですが、
	// OnValidateを追加したので、Editor拡張なしでもインスペクター更新は動くようになります。
	// そのまま残しておいても問題ありません。
	[CustomEditor(typeof(RawImage4Color))]
	public class RawImage4ColorEditor : RawImageEditor
	{
		private SerializedProperty _leftTopColor;
		private SerializedProperty _rightTopColor;
		private SerializedProperty _leftBottomColor;
		private SerializedProperty _rightBottomColor;

		protected override void OnEnable()
		{
			base.OnEnable();
			_leftTopColor = serializedObject.FindProperty("_leftTopColor");
			_rightTopColor = serializedObject.FindProperty("_rightTopColor");
			_leftBottomColor = serializedObject.FindProperty("_leftBottomColor");
			_rightBottomColor = serializedObject.FindProperty("_rightBottomColor");
		}

		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();

			EditorGUI.BeginChangeCheck();
			EditorGUI.indentLevel++;
			EditorGUILayout.PropertyField(_leftTopColor);
			EditorGUILayout.PropertyField(_rightTopColor);
			EditorGUILayout.PropertyField(_leftBottomColor);
			EditorGUILayout.PropertyField(_rightBottomColor);
			EditorGUI.indentLevel--;

			if (EditorGUI.EndChangeCheck())
			{
				serializedObject.ApplyModifiedProperties();
				// エディタ拡張側から強制的に再描画させる場合
				// (OnValidateがあるので無くても動く場合が多いですが、念のため)
				var graphic = target as RawImage4Color;
				if (graphic != null) graphic.SetVerticesDirty();
			}
		}
	}
#endif
}