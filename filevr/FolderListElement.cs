using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class FolderListElement : MonoBehaviour
{
    public Image icon;
    public Text elementName;

    public FolderDialog instance;
    public string data;

    public void OnClick()
    {
            instance.OpenDir(data);
    }
}
