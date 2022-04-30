using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public class FolderDialog : MonoBehaviour
{
    [Header("References")]
    public Text selectedPath;
    public GameObject filesScrollRectContent;
    public GameObject drivesScrollRectContent;

    [Header("Lists Prefabs")]
    public GameObject filesScrollRectElement;
    public GameObject drivesScrollRectElement;

    [Header("Lists Icons")]
    public Sprite folderIcon;

	public void Start()
    {
		selectedPath.text = string.IsNullOrEmpty(PlayerPrefs.GetString("savedPath", null)) ? Application.dataPath + "/../" : PlayerPrefs.GetString("savedPath", null);
		if (!new DirectoryInfo (selectedPath.text).Exists)
			selectedPath.text = Application.dataPath + "/../";
		
		GoTo(selectedPath.text);
    }

	public void OnEnable()
	{
		GoTo(selectedPath.text);
	}

	public void OnDisable()
	{
		PlayerPrefs.SetString("savedPath", selectedPath.text);
	}

    public void GoUp()
    {
		OpenDir(selectedPath.text + "/../");
    }

    public void GoTo(string newPath)
    {
         OpenDir(newPath + "/");
    }

    public void OpenDir(string path)
    {
		selectedPath.text = Path.GetFullPath(path);
        UpdateDrivesList();
        UpdateFilesList();
    }

    private void UpdateDrivesList()
    {
        GameObject target = drivesScrollRectContent;
        for (int i = 0; i < target.transform.childCount; i++)
        {
            Destroy(target.transform.GetChild(i).gameObject);
        }

        string[] info = Directory.GetLogicalDrives();

        for (int i = 0; i < info.Length; i++)
        {
            GameObject obj = Instantiate(drivesScrollRectElement, Vector3.zero, Quaternion.identity) as GameObject;
            obj.transform.SetParent(target.transform, false);
            obj.transform.localScale = new Vector3(1, 1, 1);

            FolderListElement element = obj.GetComponent<FolderListElement>();
            element.instance = this;
            element.data = info[i];
            element.elementName.text = info[i];
        }
    }

    private void UpdateFilesList()
    {
        GameObject target = filesScrollRectContent;
        for (int i = 0; i < target.transform.childCount; i++)
        {
            Destroy(target.transform.GetChild(i).gameObject);
        }

		DirectoryInfo dir = new DirectoryInfo(selectedPath.text);
        try
        {

            DirectoryInfo[] info = dir.GetDirectories();

            for (int i = 0; i < info.Length; i++)
            {
                GameObject obj = Instantiate(filesScrollRectElement, Vector3.zero, Quaternion.identity) as GameObject;
                obj.transform.SetParent(target.transform, false);
                obj.transform.localScale = new Vector3(1, 1, 1);

                FolderListElement element = obj.GetComponent<FolderListElement>();
                element.instance = this;
                element.data = info[i].FullName + "/";
                element.elementName.text = info[i].Name;
                element.icon.sprite = folderIcon;
            }
        }
        catch (Exception) { }
    }
}