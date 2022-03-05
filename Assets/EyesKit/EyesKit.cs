using UnityEngine;
using VRC.SDK3.Avatars.Components;

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine.Animations;
#endif

public struct Headset
{
	public string name;
	public int screenWidth;
	public int screenHeight;

	public Headset(string name, int screenWidth, int screenHeight)
	{
		this.name = name;
		this.screenWidth = screenWidth;
		this.screenHeight = screenHeight;
	}
}

public partial class EyesKitBehavior : MonoBehaviour
{
	const int ANIMATION_LAYER_FX = 4;

	public string prefix = "GENERATED_";

	public VRCAvatarDescriptor avatarDescriptor;

	public GameObject leftWrist;
	public GameObject rightWrist;

	public GameObject head;
	public GameObject leftCamera;
	public GameObject rightCamera;

	public GameObject leftEyeQuad;
	public GameObject rightEyeQuad;

	public RenderTexture leftEyeTexture;
	public RenderTexture rightEyeTexture;

	public SkinnedMeshRenderer leftHandMesh;
	public SkinnedMeshRenderer rightHandMesh;
	public SkinnedMeshRenderer leftEyeMesh;
	public SkinnedMeshRenderer rightEyeMesh;

	public string leftEyeBlendshape;
	public string rightEyeBlendshape;

	public bool usingBlendshapesForHands = true;
	public string leftHandBlendshape;
	public string rightHandBlendshape;
	public GameObject leftHandChalkEye;
	public GameObject rightHandChalkEye;

	public SkinnedMeshRenderer singleMesh;

	public static Headset[] headsets = new Headset[]
	{
		new Headset("Valve Index", 1600, 1400),
		new Headset("Oculus Quest 1", 1440, 1600),
		new Headset("Oculus Quest 2", 1832, 1920),
		new Headset("Oculus Rift S", 1280, 1440),
		new Headset("Oculus Rift", 1080, 1200),
		new Headset("HP Reverb G2", 2160, 2160),
		new Headset("HTC Vive Pro Eye", 1440, 1600),
		new Headset("HTC Vive Pro 2", 2448, 2448),
		new Headset("HTC Vive", 1080, 1200),
		new Headset("Pimax 5k", 2560, 1440),
		new Headset("Pimax 8k", 3840, 2160),
	};

	public int headsetPreset = 0;

	public int screenWidth = 1600;
	public int screenHeight = 1440;
}

#if UNITY_EDITOR
public partial class EyesKitBehavior : MonoBehaviour
{
	const string ANIMATION_LAYER_NAME = "EyesKit";

	public void AddAnimationLayer()
	{
		var animationLayers = avatarDescriptor.baseAnimationLayers;
		if (animationLayers.Length == 0)
		{
			Debug.LogWarning("No animation layers found!");
			return;
		}

		var animationController = (AnimatorController)animationLayers[ANIMATION_LAYER_FX].animatorController;
		if (animationController == null)
		{
			Debug.LogWarning("No animation controller found!");
			return;
		}

		// I don't even know how this happens but it might (maybe)
		if (!(animationController is AnimatorController))
		{
			Debug.LogWarning("Animation controller is not an AnimatorController!");
			return;
		}

		var templateAnimator = (AnimatorController)AssetDatabase.LoadAssetAtPath("Assets/EyesKit/Animator.controller", typeof(AnimatorController));
		if (templateAnimator == null)
		{
			Debug.LogError("Couldn't find animation controller to copy from!");
			return;
		}

		var eyesLayer = templateAnimator.layers[0];
		if (eyesLayer == null)
		{
			Debug.LogError("Couldn't find eyes layer!");
			return;
		}

		AddParameterIfNotExists(animationController, "IsLocal", AnimatorControllerParameterType.Bool);
		AddParameterIfNotExists(animationController, "Right_Camera", AnimatorControllerParameterType.Bool);
		AddParameterIfNotExists(animationController, "Left_Camera", AnimatorControllerParameterType.Bool);

		var originalStateMachine = eyesLayer.stateMachine;
		var stateMachine = new AnimatorStateMachine();

		var duplicateStates = new Dictionary<string, AnimatorState>();

		var animationControllerPath = AssetDatabase.GetAssetPath(animationController);

		foreach (var packedState in originalStateMachine.states)
		{
			AnimationClip animationClip = null;
			AnimationClip originalMotion = (AnimationClip)packedState.state.motion;

			if (originalMotion != null)
			{
				animationClip = (AnimationClip)Motion.Instantiate(originalMotion);
				animationClip.name = string.Format("{0}_{1}", prefix, originalMotion.name);
				animationClip.ClearCurves();

				foreach (var curveBinding in AnimationUtility.GetCurveBindings(originalMotion))
				{
					var curve = AnimationUtility.GetEditorCurve(originalMotion, curveBinding);
					string path = null;
					string propertyName = curveBinding.propertyName;
					System.Type type = curveBinding.type;

					switch (curveBinding.path)
					{
						case "Left Eye Quad":
							path = leftEyeQuad.name;
							break;
						case "Right Eye Quad":
							path = rightEyeQuad.name;
							break;
						case "Armature/Hips/Spine/Chest/Left shoulder/Left arm/Left elbow/Left wrist/Left Camera Holder/Left Camera":
							path = GetPath(leftCamera) + "/Camera";
							break;
						case "Armature/Hips/Spine/Chest/Right shoulder/Right arm/Right elbow/Right wrist/Right Camera Holder/Right Camera":
							path = GetPath(rightCamera) + "/Camera";
							break;
						case "Body":
							switch (curveBinding.propertyName)
							{
								case "blendShape.Left_Hand_Eye":
									GetAnimationPropertyForHand(
										leftHandBlendshape,
										leftHandChalkEye,
										leftHandMesh.gameObject,
										out path,
										out propertyName,
										out type
									);

									break;
								case "blendShape.Right_Hand_Eye":
									GetAnimationPropertyForHand(
										rightHandBlendshape,
										rightHandChalkEye,
										rightHandMesh.gameObject,
										out path,
										out propertyName,
										out type
									);

									break;
								case "blendShape.eyeL_close":
									path = GetPath(leftEyeMesh.gameObject);
									propertyName = string.Format("blendShape.{0}", leftEyeBlendshape);
									break;
								case "blendShape.eyeR_close":
									path = GetPath(rightEyeMesh.gameObject);
									propertyName = string.Format("blendShape.{0}", rightEyeBlendshape);
									break;
								default:
									Debug.LogWarning(string.Format("Unexpected property name for Body: {0}", curveBinding.propertyName));
									continue;
							}

							// This never gets hit but C# can't figure that out
							break;
						default:
							Debug.LogWarning(string.Format("Unexpected path for property {0}: {1}", curveBinding.propertyName, curveBinding.path));
							continue;
					}

					AnimationUtility.SetEditorCurve(
						animationClip,
						EditorCurveBinding.FloatCurve(
							path,
							type,
							propertyName
						),
						curve
					);

				}

				AssetDatabase.AddObjectToAsset(animationClip, animationControllerPath);
			}

			var duplicateState = new AnimatorState();
			duplicateState.name = packedState.state.name;
			duplicateState.motion = animationClip;
			duplicateStates.Add(duplicateState.name, duplicateState);
			stateMachine.AddState(duplicateState, packedState.position);
			AssetDatabase.AddObjectToAsset(duplicateState, animationControllerPath);
		}

		var stateMachinePath = AssetDatabase.GetAssetPath(stateMachine);

		foreach (var packedState in originalStateMachine.states)
		{
			var originalState = packedState.state;
			var duplicateState = duplicateStates[originalState.name];

			foreach (var transition in originalState.transitions)
			{
				var newTransition = duplicateState.AddTransition(duplicateStates[transition.destinationState.name]);
				newTransition.conditions = transition.conditions;
				newTransition.duration = transition.duration;
				newTransition.exitTime = transition.exitTime;
				newTransition.hasExitTime = transition.hasExitTime;
				EditorUtility.SetDirty(newTransition);
			}

			if (stateMachinePath != "")
			{
				duplicateState.hideFlags = HideFlags.HideInHierarchy;
				AssetDatabase.AddObjectToAsset(duplicateState, stateMachinePath);
			}
		}

		stateMachine.states = stateMachine.states;

		animationController.AddLayer(new AnimatorControllerLayer
		{
			name = ANIMATION_LAYER_NAME,
			defaultWeight = 1,
			stateMachine = stateMachine,
		});

		AssetDatabase.AddObjectToAsset(stateMachine, animationControllerPath);
	}

	public bool HasEyesAnimator()
	{
		var animationLayers = avatarDescriptor.baseAnimationLayers;
		if (animationLayers.Length == 0)
		{
			return false;
		}

		var animationController = (AnimatorController)animationLayers[ANIMATION_LAYER_FX].animatorController;
		if (animationController == null)
		{
			return false;
		}

		// I don't even know how this happens but it might
		if (!(animationController is AnimatorController))
		{
			Debug.LogWarning("Animation controller is not an AnimatorController!");
			return false;
		}

		return animationController.layers.Any(layer => layer.name == ANIMATION_LAYER_NAME);
	}

	public void ResetAnimationLayer()
	{
		var animationLayers = avatarDescriptor.baseAnimationLayers;
		if (animationLayers.Length == 0)
		{
			return;
		}

		var animationController = (AnimatorController)animationLayers[ANIMATION_LAYER_FX].animatorController;
		if (animationController == null)
		{
			return;
		}

		// I don't even know how this happens but it might
		if (!(animationController is AnimatorController))
		{
			Debug.LogWarning("Animation controller is not an AnimatorController!");
			return;
		}

		animationController.layers = animationController.layers.Where(layer => layer.name != ANIMATION_LAYER_NAME).ToArray();
	}

	public void GetAnimationPropertyForHand(
		string blendshape,
		GameObject chalkEye,
		GameObject mesh,
		out string path,
		out string propertyName,
		out System.Type type
	)
	{
		if (usingBlendshapesForHands)
		{
			path = GetPath(mesh);
			propertyName = string.Format("blendShape.{0}", blendshape);
			type = typeof(SkinnedMeshRenderer);
		}
		else
		{
			path = GetPath(chalkEye);
			propertyName = "m_IsActive";
			type = typeof(GameObject);
		}
	}

	public void MakeEyeQuads()
	{
		leftEyeQuad = MakeEyeQuad("LeftEye", 0, out leftEyeTexture);
		rightEyeQuad = MakeEyeQuad("RightEye", 1, out rightEyeTexture);
	}

	GameObject MakeEyeQuad(string name, int whichEye, out RenderTexture texture)
	{
		texture = new RenderTexture(screenWidth, screenHeight, 24);
		texture.name = string.Format("{0}_Texture", name);

		if (!AssetDatabase.IsValidFolder("Assets/EyesKit/Materials"))
		{
			AssetDatabase.CreateFolder("Assets/EyesKit", "Materials");
		}

		AssetDatabase.CreateAsset(texture, string.Format("Assets/EyesKit/Materials/{0}{1}.asset", prefix, name));

		var shader = (Shader)AssetDatabase.LoadAssetAtPath("Assets/EyesKit/QuadToScreenRenderOnlyInVR.shader", typeof(Shader));

		var material = new Material(shader);
		material.SetInt("_WhichEye", whichEye);
		material.mainTexture = texture;
		material.name = string.Format("{0}_Material", name);

		GameObject quad = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath("Assets/EyesKit/Eye Quad.prefab", typeof(GameObject))) as GameObject;
		quad.name = string.Format("{0}_Quad", name);

		var avatarTransform = avatarDescriptor.gameObject.transform;
		quad.transform.parent = avatarTransform;

		var parentConstraint = quad.GetComponent<ParentConstraint>();
		parentConstraint.AddSource(new ConstraintSource() { sourceTransform = head.transform, weight = 1.0f });
		parentConstraint.SetTranslationOffset(0, new Vector3(0, 0, 0.3f));

		AssetDatabase.CreateAsset(material, string.Format("Assets/EyesKit/Materials/{0}{1}.mat", prefix, name));
		AssetDatabase.Refresh();
		AssetDatabase.SaveAssets();

		var meshRenderer = quad.GetComponent<MeshRenderer>();
		meshRenderer.sharedMaterial = material;

		return quad;
	}

	public void ResetEyeQuads()
	{
		if (leftEyeQuad != null)
		{
			DestroyImmediate(leftEyeQuad);
			leftEyeQuad = null;
		}

		if (rightEyeQuad != null)
		{
			DestroyImmediate(rightEyeQuad);
			rightEyeQuad = null;
		}
	}

	// Returns false if the object exists and should not be destroyed
	public bool OfferToDestroy(string name, GameObject gameObject)
	{
		if (gameObject == null)
		{
			return true;
		}

		if (EditorUtility.DisplayDialog(
			string.Format("{0} already exists", name),
			"Are you sure you want to make another? This will destroy the previous one.",
			"Destroy and Recreate",
			"Cancel"
		))
		{
			DestroyImmediate(gameObject);
			return true;
		}

		return false;
	}

	public void AddLeftCamera()
	{
		if (!OfferToDestroy("Left eye camera", leftCamera))
		{
			return;
		}

		leftCamera = AddCamera("LeftEyeCamera", leftEyeTexture, leftWrist, Quaternion.LookRotation(
			Vector3.down,
			Vector3.Cross(Vector3.up, head.transform.forward * -1).normalized
		));
	}

	public void AddRightCamera()
	{
		if (!OfferToDestroy("Right eye camera", rightCamera))
		{
			return;
		}

		rightCamera = AddCamera("RightEyeCamera", rightEyeTexture, rightWrist, Quaternion.LookRotation(
			Vector3.down,
			Vector3.Cross(Vector3.up, head.transform.forward).normalized
		));
	}

	GameObject AddCamera(string name, RenderTexture renderTexture, GameObject parent, Quaternion rotation)
	{
		GameObject quad = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath("Assets/EyesKit/CameraHolder.prefab", typeof(GameObject))) as GameObject;
		quad.transform.Find("Camera").GetComponent<Camera>().targetTexture = renderTexture;
		quad.transform.parent = parent.transform;
		quad.transform.localPosition = new Vector3();
		quad.transform.rotation = rotation;
		Selection.activeGameObject = quad;
		return quad;
	}

	public void CopySingleMesh()
	{
		leftEyeMesh = singleMesh;
		rightEyeMesh = singleMesh;
		leftHandMesh = singleMesh;
		rightHandMesh = singleMesh;
	}

	public void ResetCameras()
	{
		if (leftCamera != null)
		{
			DestroyImmediate(leftCamera);
			leftCamera = null;
		}

		if (rightCamera != null)
		{
			DestroyImmediate(rightCamera);
			rightCamera = null;
		}
	}

	GameObject AddChalkEye(string name, GameObject camera, GameObject parent)
	{
		var chalkEye = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath("Assets/EyesKit/ChalkEye.prefab", typeof(GameObject))) as GameObject;

		PrefabUtility.UnpackPrefabInstance(chalkEye, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

		chalkEye.name = name;
		chalkEye.transform.position = camera.transform.position;
		chalkEye.transform.rotation = camera.transform.rotation;
		chalkEye.transform.localScale = new Vector3(60, 60, 60);
		chalkEye.transform.parent = parent.transform;
		Selection.activeGameObject = chalkEye;

		return chalkEye;
	}

	public void AddLeftChalkEye()
	{
		if (!OfferToDestroy("Left chalk eye", leftHandChalkEye))
		{
			return;
		}

		leftHandChalkEye = AddChalkEye("LeftHandChalkEye", leftCamera, leftWrist);
	}

	public void AddRightChalkEye()
	{
		if (!OfferToDestroy("Right chalk eye", rightHandChalkEye))
		{
			return;
		}

		rightHandChalkEye = AddChalkEye("RightHandChalkEye", rightCamera, rightWrist);
	}

	public bool LeftHandReady()
	{
		return usingBlendshapesForHands ? leftHandBlendshape != null : leftHandChalkEye != null;
	}

	public bool RightHandReady()
	{
		return usingBlendshapesForHands ? rightHandBlendshape != null : rightHandChalkEye != null;
	}


	public void ResetChalkEyes()
	{
		if (leftHandChalkEye != null)
		{
			DestroyImmediate(leftHandChalkEye);
			leftHandChalkEye = null;
		}

		if (rightHandChalkEye != null)
		{
			DestroyImmediate(rightHandChalkEye);
			rightHandChalkEye = null;
		}
	}

	string GetPath(GameObject gameObject)
	{
		string path = gameObject.name;
		while (gameObject.transform.parent != null && gameObject.transform.parent.gameObject != avatarDescriptor.gameObject)
		{
			gameObject = gameObject.transform.parent.gameObject;
			path = gameObject.name + "/" + path;
		}

		return path;
	}

	static void AddParameterIfNotExists(AnimatorController controller, string name, AnimatorControllerParameterType type)
	{
		if (controller.parameters.Any(parameter => parameter.name == name))
		{
			return;
		}

		controller.AddParameter(name, type);
	}

	[MenuItem("EyesKit/Create")]
	static void EditorCreate()
	{
		var handlerObject = new GameObject();
		handlerObject.name = "EyesKit";
		handlerObject.AddComponent<EyesKitBehavior>();
		Selection.activeGameObject = handlerObject;
	}
}

[CustomEditor(typeof(EyesKitBehavior))]
[CanEditMultipleObjects]
public class EyesKitBehaviorEditor : Editor
{
	bool openAllProperties = false;
	bool openMeshProperties = false;

	public override void OnInspectorGUI()
	{
		EditorGUILayout.PropertyField(serializedObject.FindProperty("avatarDescriptor"));
		EditorGUILayout.PropertyField(serializedObject.FindProperty("prefix"));

		openAllProperties = EditorGUILayout.Foldout(openAllProperties, "All Properties");

		if (openAllProperties)
		{
			base.OnInspectorGUI();
		}

		var eyesKitBehavior = (EyesKitBehavior)target;

		if (eyesKitBehavior.avatarDescriptor != null)
		{
			EditorGUILayout.LabelField("Avatar", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(serializedObject.FindProperty("head"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("leftWrist"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("rightWrist"));
		}

		EditorGUILayout.LabelField("Eye Quads", EditorStyles.boldLabel);

		eyesKitBehavior.headsetPreset = EditorGUILayout.Popup(
			"Headset",
			eyesKitBehavior.headsetPreset,
			EyesKitBehavior.headsets.Select(headset =>
				string.Format(
					"{0} ({1}x{2})",
					headset.name,
					headset.screenWidth,
					headset.screenHeight
				)
			).Prepend("Custom").ToArray()
		);

		if (eyesKitBehavior.headsetPreset != 0)
		{
			var headset = EyesKitBehavior.headsets[eyesKitBehavior.headsetPreset - 1];
			eyesKitBehavior.screenWidth = headset.screenWidth;
			eyesKitBehavior.screenHeight = headset.screenHeight;
		}

		EditorGUI.BeginDisabledGroup(eyesKitBehavior.headsetPreset != 0);
		eyesKitBehavior.screenWidth = EditorGUILayout.IntField("Screen Width", eyesKitBehavior.screenWidth);
		eyesKitBehavior.screenHeight = EditorGUILayout.IntField("Screen Height", eyesKitBehavior.screenHeight);
		EditorGUI.EndDisabledGroup();

		if (eyesKitBehavior.head != null)
		{
			if (GUILayout.Button("Make Eye Quads"))
			{
				eyesKitBehavior.MakeEyeQuads();
			}

			if (GUILayout.Button("Reset Eye Quads"))
			{
				eyesKitBehavior.ResetEyeQuads();
			}
		}

		if (
			eyesKitBehavior.leftEyeTexture != null
			&& eyesKitBehavior.leftWrist != null
			&& eyesKitBehavior.leftEyeQuad != null
			&& eyesKitBehavior.rightEyeTexture != null
			&& eyesKitBehavior.rightWrist != null
			&& eyesKitBehavior.rightEyeQuad != null
		)
		{
			EditorGUILayout.LabelField("Cameras", EditorStyles.boldLabel);

			if (GUILayout.Button("Setup Left Camera"))
			{
				eyesKitBehavior.AddLeftCamera();
			}

			if (GUILayout.Button("Setup Right Camera"))
			{
				eyesKitBehavior.AddRightCamera();
			}

			if (GUILayout.Button("Reset Cameras"))
			{
				eyesKitBehavior.ResetCameras();
			}
		}

		EditorGUILayout.LabelField("Blendshapes", EditorStyles.boldLabel);

		EditorGUILayout.PropertyField(serializedObject.FindProperty("singleMesh"));

		if (GUILayout.Button("Copy Mesh to All"))
		{
			eyesKitBehavior.CopySingleMesh();
		}

		openMeshProperties = EditorGUILayout.Foldout(openMeshProperties, "Non-Single Mesh Properties");
		if (openMeshProperties)
		{
			EditorGUILayout.PropertyField(serializedObject.FindProperty("leftHandMesh"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("rightHandMesh"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("leftEyeMesh"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("rightEyeMesh"));
		}

		if (
			eyesKitBehavior.leftEyeMesh != null
			&& eyesKitBehavior.rightEyeMesh != null
			&& eyesKitBehavior.leftCamera != null
			&& eyesKitBehavior.rightCamera != null
		)
		{
			EyeSelection(eyesKitBehavior);
		}

		if (
			eyesKitBehavior.leftEyeQuad != null
			&& eyesKitBehavior.rightEyeQuad != null
			&& eyesKitBehavior.leftCamera != null
			&& eyesKitBehavior.rightCamera != null
			&& eyesKitBehavior.leftHandMesh != null
			&& eyesKitBehavior.rightHandMesh != null
			&& eyesKitBehavior.LeftHandReady()
			&& eyesKitBehavior.RightHandReady()
		)
		{
			EditorGUILayout.LabelField("Animation Controllers", EditorStyles.boldLabel);

			if (GUILayout.Button("Setup Animation Layer"))
			{
				eyesKitBehavior.AddAnimationLayer();
			}

			if (eyesKitBehavior.HasEyesAnimator())
			{
				if (GUILayout.Button("Reset Animation Layer"))
				{
					eyesKitBehavior.ResetAnimationLayer();
				}
			}
			else
			{
				GUILayout.Label("Animation controller not setup yet.");
			}
		}

		serializedObject.ApplyModifiedProperties();
		serializedObject.Update();
	}

	void EyeSelection(EyesKitBehavior eyesKitBehavior)
	{
		EditorGUILayout.LabelField("Eyes (on face)", EditorStyles.boldLabel);

		CreateBlendshapeSelector(
			"Left Eye Blendshape",
			eyesKitBehavior.leftEyeMesh,
			eyesKitBehavior.leftEyeBlendshape,
			out eyesKitBehavior.leftEyeBlendshape
		);

		CreateBlendshapeSelector(
			"Right Eye Blendshape",
			eyesKitBehavior.rightEyeMesh,
			eyesKitBehavior.rightEyeBlendshape,
			out eyesKitBehavior.rightEyeBlendshape
		);

		EditorGUILayout.LabelField("Eyes (on hands)", EditorStyles.boldLabel);

		eyesKitBehavior.usingBlendshapesForHands = EditorGUILayout.Popup(
			"Hand Behavior",
			eyesKitBehavior.usingBlendshapesForHands ? 1 : 0,
			new string[] { "Chalk eyes", "Blendshapes" }
		) == 1;

		if (eyesKitBehavior.usingBlendshapesForHands)
		{
			HandBlendshapeInspector(eyesKitBehavior);
		}
		else
		{
			HandChalkEyesInspector(eyesKitBehavior);
		}

		if (eyesKitBehavior.leftHandChalkEye != null || eyesKitBehavior.rightHandChalkEye)
		{
			if (GUILayout.Button("Reset Chalk Eyes"))
			{
				eyesKitBehavior.ResetChalkEyes();
			}
		}
	}

	void HandBlendshapeInspector(EyesKitBehavior eyesKitBehavior)
	{
		CreateBlendshapeSelector(
			"Left Hand Blendshape",
			eyesKitBehavior.leftHandMesh,
			eyesKitBehavior.leftHandBlendshape,
			out eyesKitBehavior.leftHandBlendshape
		);

		CreateBlendshapeSelector(
			"Right Hand Blendshape",
			eyesKitBehavior.rightHandMesh,
			eyesKitBehavior.rightHandBlendshape,
			out eyesKitBehavior.rightHandBlendshape
		);
	}

	void HandChalkEyesInspector(EyesKitBehavior eyesKitBehavior)
	{
		if (GUILayout.Button("Setup Left Chalk Eye"))
		{
			eyesKitBehavior.AddLeftChalkEye();
		}

		if (GUILayout.Button("Setup Right Chalk Eye"))
		{
			eyesKitBehavior.AddRightChalkEye();
		}
	}

	void CreateBlendshapeSelector(
		string name,
		SkinnedMeshRenderer meshRenderer,
		string currentBlendshape,
		out string setBlendshape
	)
	{
		var eyesKitBehavior = (EyesKitBehavior)target;

		if (meshRenderer == null)
		{
			setBlendshape = null;
			return;
		}

		var blendshapeEnumerable = Enumerable.Range(0, meshRenderer.sharedMesh.blendShapeCount)
			.Where(i => meshRenderer.sharedMesh.GetBlendShapeName(i) == currentBlendshape);

		var blendshapeIndex = blendshapeEnumerable.FirstOrDefault();

		var newIndex = EditorGUILayout.Popup(
			name,
			blendshapeEnumerable.Count() == 0 ? 0 : blendshapeIndex + 1,
			Enumerable.Range(0, meshRenderer.sharedMesh.blendShapeCount).Select(
				i => meshRenderer.sharedMesh.GetBlendShapeName(i)
			).Prepend(string.Format("<No {0}>", name)).ToArray()
		);

		if (newIndex == 0)
		{
			setBlendshape = null;
		}
		else
		{
			setBlendshape = meshRenderer.sharedMesh.GetBlendShapeName(newIndex - 1);
		}
	}
}
#endif
