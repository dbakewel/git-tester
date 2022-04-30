using UnityEngine;

public class FileVR {
	public DirectoryVR parent = null;
	public System.IO.FileInfo fi;
	public float angle;
	public float radius;
	public float x;
	public float z;

	public FileVR(string file, DirectoryVR parent) {
		this.parent = parent;
		fi = new System.IO.FileInfo (file);
	}
}
