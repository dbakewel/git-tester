using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using System;

public class Settings : MonoBehaviour {
	[Header("User Setting UI Elements")]
	public Toggle uiNavigateDirs; //if true then Next and Prev buttons will cross dir boundrys.
	public Slider uiFileSize;
	public Slider uiFolderSize;
	public Slider uiMaxLevel;
	public Slider uiMinHeight;
	public Slider uiDegrees;
	public Toggle uiDirSubHeight;

	[Header("Default Folder Color")]
	public GameObject uiDefaultFolderColor;
	public GameObject uiFolderColorPrefab;
	private int defaultFolderColor;
	private string FolderColorsPath = "Textures/filePalette";
	private int folderTextureSlices = 16;
	[HideInInspector]
	public int folderTextureHeight;
	private Color[] FolderColors;

	[Header("Default File Color")]
	public GameObject uiDefaultFileColor;
	public GameObject uiDefaultFileColorPrefab;
	private int defaultFileColor;
	private string FileColorsPath = "Textures/filePalette";
	[HideInInspector]
	public int fileTextureSlices = 16;
	[HideInInspector]
	public int fileTextureHeight;
	private Color[] FileColors;


	[Header("Assinged File Color")]
	public GameObject uiAssingedFileColor;
	public GameObject uiAssingedFileColorPrefab;
	public Text assingedInstructions;
	private Dictionary<string,int> assignedColor = new Dictionary<string,int>();

	private GameController g;

	void Start () {
		g = this.GetComponent<GameController> ();
		NavigateDirs = PlayerPrefs.GetInt("NavigateDirs", 1) == 1 ? true : false;
		DirSubHeight = PlayerPrefs.GetInt("DirSubHeight", 0) == 1 ? true : false;
		FileSize = PlayerPrefs.GetFloat("FileSize", 2f);
		FolderSize = PlayerPrefs.GetFloat("FolderSize", 4f);
		MinHeight = PlayerPrefs.GetFloat("MinHeight", 512f);
		MaxLevel = PlayerPrefs.GetFloat("MaxLevel", 21f);
		Degrees = PlayerPrefs.GetFloat("Degrees", 360f);

		FolderColors = createColorToggles (FolderColorsPath, folderTextureSlices, uiDefaultFolderColor, uiFolderColorPrefab, setFolderColorCallBack, ref folderTextureHeight);
		setFolderColor(PlayerPrefs.GetInt("defaultFolderColor", 2));

		FileColors = createColorToggles (FileColorsPath, fileTextureSlices, uiDefaultFileColor, uiDefaultFileColorPrefab, setDefaultFileColorCallBack, ref fileTextureHeight);
		setDefaultFileColor(PlayerPrefs.GetInt("defaultFileColor", 6));

		createColorToggles (FileColorsPath, fileTextureSlices, uiAssingedFileColor, uiAssingedFileColorPrefab, setAssingedFileColorCallBack, ref fileTextureHeight);
		clearAssignedColorToggles (false);
		initAssingedColorLabels ();
	}

	public void resetSettings() {
		NavigateDirs = true;
		DirSubHeight = false;
		FileSize = 2f;
		FolderSize = 4f;
		MinHeight = 512f;
		MaxLevel = 21f;
		Degrees = 360;
	}

	[HideInInspector]
	public bool NavigateDirs {
		get {
			return uiNavigateDirs.isOn;
		}
		set {
			if (uiNavigateDirs.isOn != value)
				uiNavigateDirs.isOn = value;
			PlayerPrefs.SetInt ("NavigateDirs", uiNavigateDirs.isOn ? 1 : 0);
			g.remesh ();
		}
	}

	[HideInInspector]
	public float FileSize {
		//FileSize range: 1 (50%) - 2 (100%) - 4 (200%) 
		get {
			return uiFileSize.value;
		}
		set {
			if (uiFileSize.value != value)
				uiFileSize.value = value;
			PlayerPrefs.SetFloat ("FileSize", uiFileSize.value);

			int percent = Mathf.RoundToInt(uiFileSize.value * 50f);
			uiFileSize.transform.parent.FindChild ("Value").GetComponent<Text> ().text = percent + "%";
			g.remesh ();
		}
	}

	[HideInInspector]
	public float FolderSize {
		//FolderSize range: 1 (25%) - 2 (50%) - 4 (100%) 
		get {
			return uiFolderSize.value;
		}
		set {
			if (uiFolderSize.value != value)
				uiFolderSize.value = value;
			PlayerPrefs.SetFloat ("FolderSize", uiFolderSize.value);

			int percent = Mathf.RoundToInt(uiFolderSize.value * 25f);
			uiFolderSize.transform.parent.FindChild ("Value").GetComponent<Text> ().text = percent + "%";

			g.remesh ();
		}
	}

	[HideInInspector]
	public float MaxLevel {
		// 1 - 20, 21 = infinate
		get {
			int m = Mathf.RoundToInt(uiMaxLevel.value);
			if (m == 21)
				m = 65000; //effectivly infinity
			return (float)m;
		}
		set {
			if (uiMaxLevel.value != value)
				uiMaxLevel.value = value;
			PlayerPrefs.SetFloat ("MaxLevel", uiMaxLevel.value);

			if (Mathf.RoundToInt (uiMaxLevel.value) == 21)
				uiMaxLevel.transform.parent.FindChild ("Value").GetComponent<Text> ().text = "\u221E";
			else
				uiMaxLevel.transform.parent.FindChild ("Value").GetComponent<Text> ().text = Mathf.RoundToInt(uiMaxLevel.value).ToString();

			g.remesh ();
		}
	}

	[HideInInspector]
	public float Degrees {
		get {
			return Mathf.Round(uiDegrees.value);
		}
		set {
			if (uiDegrees.value != value)
				uiDegrees.value = value;
			PlayerPrefs.SetFloat ("Degrees", uiDegrees.value);

			uiDegrees.transform.parent.FindChild ("Value").GetComponent<Text> ().text = Mathf.RoundToInt(uiDegrees.value).ToString() + "°";

			g.remesh ();
		}
	}

	[HideInInspector]
	public float MinHeight {
		//file will not shorter than MinHeight KB
		get {
			return uiMinHeight.value;
		}
		set {
			if (uiMinHeight.value != value)
				uiMinHeight.value = value;
			PlayerPrefs.SetFloat ("MinHeight", uiMinHeight.value);

			uiMinHeight.transform.parent.FindChild ("Value").GetComponent<Text> ().text = Mathf.RoundToInt(uiMinHeight.value) + " KB";

			g.remesh ();
		}
	}

	[HideInInspector]
	public bool DirSubHeight {
		get {
			return uiDirSubHeight.isOn;
		}
		set {
			if (uiDirSubHeight.isOn != value)
				uiDirSubHeight.isOn = value;
			PlayerPrefs.SetInt ("DirSubHeight", uiDirSubHeight.isOn ? 1 : 0);
			g.remesh ();
		}
	}

	public float fileHeight(FileVR f) {
		float height = f.fi.Length / 1024;
		if (height < MinHeight)
			height = MinHeight;
		height /= 1024;
		return height;
	}

	public float dirHeight(DirectoryVR d) {
		float height;
		if(DirSubHeight || d.level == Mathf.RoundToInt(MaxLevel))
			height = d.totalLength / 1024;
		else
			height = d.length / 1024;
		if (height < MinHeight)
			height = MinHeight;
		height /= 1024 * -1;
		return height;
	}


	//##############################################################################
	// COLORS
	//##############################################################################

	//createFolderToggles(FolderColorsPath, folderTextureSlices, uiDefaultFolderColor, uiFolderColorPrefab) 

	private Color[] createColorToggles(string path, int slices, GameObject layoutGroup, GameObject prefab, UnityEngine.Events.UnityAction<bool> callback, ref int textureHeight) {
		Texture2D t = Resources.Load( path ) as Texture2D;
		textureHeight = t.height;

		Color[] colors = new Color[slices];

		ToggleGroup toggleGroup = layoutGroup.GetComponent<ToggleGroup> ();

		for (int i = 0; i < slices; i++) {
			colors [i] = t.GetPixel (0, (slices-i) * t.height / slices - t.height / slices / 2); //getpixel coords are from bottom left

			GameObject o = Instantiate(prefab, Vector3.zero, Quaternion.identity) as GameObject;
			o.transform.SetParent(layoutGroup.transform, false);
			o.name = i.ToString ();

			o.transform.Find ("Background").gameObject.GetComponent<Image>().color = colors [i];

			Toggle toggle = o.GetComponent<Toggle> ();
			toggle.isOn = false;
			toggle.group = toggleGroup;
			toggle.onValueChanged.AddListener (callback);
		}
		return colors;
	}

	//Default Dir Color

	public void setFolderColorCallBack(bool b) {
		if(b) {
			for (int i = 0; i < uiDefaultFolderColor.transform.childCount; i++) {
				if (uiDefaultFolderColor.transform.GetChild (i).gameObject.GetComponent<Toggle> ().isOn) {
					defaultFolderColor = i;
					PlayerPrefs.SetInt ("defaultFolderColor", defaultFolderColor);

					Color c = FolderColors [defaultFolderColor];
					g.mDir.color = c;
					c.r = c.r * 0.8f;
					c.g = c.g * 0.8f;
					c.b = c.b * 0.8f;
					g.mSelectDir.color = c;
					break;
				}
			}
		}
	}

	public void setFolderColor(int color) {
		uiDefaultFolderColor.transform.GetChild (color).gameObject.GetComponent<Toggle> ().isOn = true;
	}

	//Default File Color

	public void setDefaultFileColorCallBack(bool b) {
		if(b) {
			for (int i = 0; i < uiDefaultFileColor.transform.childCount; i++) {
				if (uiDefaultFileColor.transform.GetChild (i).gameObject.GetComponent<Toggle> ().isOn) {
					defaultFileColor = i;
					PlayerPrefs.SetInt ("defaultFileColor", defaultFileColor);
					g.remesh ();
					break;
				}
			}
		}
	}

	public void setDefaultFileColor(int color) {
		uiDefaultFileColor.transform.GetChild (color).gameObject.GetComponent<Toggle> ().isOn = true;
	}

	//Assinged File Color

	public void updateAssingedColorLabel(int i) {
		string types = "";

		List<string> keys = new List<string>();
		foreach (KeyValuePair<string,int> p in assignedColor) {
			if (p.Value == i)
				keys.Add(p.Key);
		}
		keys.Sort();

		foreach (string k in keys) {
			types += k + " | ";
		}

		if (types != "") {
			types = types.Trim().TrimEnd ('|');
			PlayerPrefs.SetString ("assingedColor" + i, types);
		} else {
			PlayerPrefs.DeleteKey ("assingedColor" + i);
		}
		uiAssingedFileColor.transform.GetChild (i).gameObject.transform.FindChild ("Label").gameObject.GetComponent<Text> ().text = types;
	}

	public void initAssingedColorLabels() {
		for (int i = 0; i < uiAssingedFileColor.transform.childCount; i++) {
			string types = PlayerPrefs.GetString ("assingedColor" + i,"");

			string[] split = types.Split ('|');

			foreach(string k in split) {
				assignedColor[k.Trim ()] = i;
			}
			
			uiAssingedFileColor.transform.GetChild (i).gameObject.transform.FindChild ("Label").gameObject.GetComponent<Text> ().text = types;
		}
	}

	public void resetAssingedColorLabels() {
		setFolderColor(2);
		setDefaultFileColor(6);

		assignedColor.Clear ();
		ignoreAssignedColorToggleChange = true;
		for (int i = 0; i < uiAssingedFileColor.transform.childCount; i++) {
			PlayerPrefs.DeleteKey ("assingedColor" + i);
			uiAssingedFileColor.transform.GetChild (i).gameObject.transform.FindChild ("Label").gameObject.GetComponent<Text> ().text = "";
			uiAssingedFileColor.transform.GetChild (i).gameObject.GetComponent<Toggle> ().isOn = false;
		}
		ignoreAssignedColorToggleChange = false;
		g.remesh ();
	}

	bool ignoreAssignedColorToggleChange = false;

	public void setAssingedFileColorCallBack(bool b) {
		if (!ignoreAssignedColorToggleChange && g.selectedFile != null) {
			string type = g.selectedFile.fi.Extension.ToLower().Trim().TrimStart('.');
			if (type == "")
				type = "none";
			if (!b) {
				int i;
				if (assignedColor.TryGetValue (type, out i)) {
					assignedColor.Remove (type);
					updateAssingedColorLabel (i);
				}
			} else {
				for (int i = 0; i < uiAssingedFileColor.transform.childCount; i++) {
					if (uiAssingedFileColor.transform.GetChild (i).gameObject.GetComponent<Toggle> ().isOn) {
						assignedColor[type] = i;
						updateAssingedColorLabel (i);
						break;
					}
				}
			}
			g.remesh ();
		}
	}

	private void clearAssignedColorToggles(bool enable) {
		ignoreAssignedColorToggleChange = true;
		for (int i = 0; i < uiAssingedFileColor.transform.childCount; i++) {
			uiAssingedFileColor.transform.GetChild (i).gameObject.GetComponent<Toggle> ().isOn = false;
			uiAssingedFileColor.transform.GetChild (i).gameObject.GetComponent<Toggle> ().interactable = enable;
		}
		ignoreAssignedColorToggleChange = false;
	}

	public void setSelectedFileType(string type) {
		if(type != null) {
			type = type.ToLower ().Trim().TrimStart('.');
			if (type == "")
				type = "none";
			assingedInstructions.text = "Set color for type: " + type;
			clearAssignedColorToggles (true);

			ignoreAssignedColorToggleChange = true;
			int i;
			if (assignedColor.TryGetValue (type, out i))
				uiAssingedFileColor.transform.GetChild (i).gameObject.GetComponent<Toggle> ().isOn = true;
			ignoreAssignedColorToggleChange = false;
		} else {
			assingedInstructions.text = "Select a file to set type color.";
			clearAssignedColorToggles (false);
		}
	}

	public int getFileColorIndex(string type) {
		int i;

		type = type.ToLower ().Trim().TrimStart('.');
		if (type == "")
			type = "none";

		if (!assignedColor.TryGetValue (type, out i))
			i = defaultFileColor;

		return i;
	}

	public Color getFileHLColor(string type) {
		return FileColors [getFileColorIndex (type)];
	}

}
