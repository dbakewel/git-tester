using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;
using UnityEngine.UI;

public class GameController : MonoBehaviour {
	[Header("Objects")]
	[Tooltip("Root of all file system objects.")]
	
	public GameObject fileSystemRoot;

	[Tooltip("Text object of the sceeen from the left controller.")]
	public Text screenTitle;
	public Text screenLeft;
	public Text screenRight;
	public Text screenAlert;
	[Tooltip("Object used to highlight a file.")]
	public GameObject fileHL;
	[Tooltip("Object used to highlight a dir.")]
	public GameObject dirHL;
	[Tooltip("Text object from the file dialog.")]
	public Text folderPath;
	public Text screenTitle;
	public Text screenLeft;
	public Text screenRight;
	public Text screenAlert;

	[Header("Controller Objects")]
	[Tooltip("Plane Pointer from the right controller")]
	public GameObject planePointer;
	[Tooltip("VRTK UIPointer from the right controller")]
	public GameObject UIPointer;
	[Tooltip("Radial Menu from left controller.")]
	public GameObject displayMenu;
	[Tooltip("Radial Menu from right controller.")]
	public GameObject selectedMenu;
	public ControllerEventListener leftController;
	public ControllerEventListener rightController;
	public GameObject leftControllerToolTips;
	public GameObject rightControllerToolTips;

	[Header("UI Objects")]
	[Tooltip("Root of all UI objects.")]
	public GameObject UIRoot;
	public GameObject working;

	[Header("Prefabs")]
	public GameObject file;
	public GameObject dir;

	[Header("Materials")]
	public Material mDir;
	public Material mFile;
	public Material mSelectFile;
	public Material mSelectDir;
	public Material mFade;

	private FileSystemCollector fsc;
	private Thread collectThread;
	private Coroutine layoutCoroutine;
	private Coroutine startThreadCoroutine;

	private bool scaling = false; //are we currently scaling the file system root.
	private bool scaleVertical = false; //are we currently scaling the file system root on the vertical only.
	private float scaleStartDistance; //distance between controllers at start of scalling.
	private Vector3 scaleStartVector; //vector beentween center of controllers and center of fileSystemRoot as start of scaling.
	private Vector3 scaleStart; //localScale of filesystemroot at start of scaling.

	private enum Mode {Home, View};
	private Mode mode;

	private DirectoryVR pointedDir;
	private FileVR pointedFile;
	private DirectoryVR selectedDir;
	[HideInInspector]
	public FileVR selectedFile;

	private Vector3 moveSpeedi;
	private Vector3 moveSpeed {
		get { 
			return moveSpeedi;
		}
		set {
			moveSpeedi = value;
			if (moveSpeedi.magnitude > 0.2f) // make sure we dont' throw it to fast. This can happen when we lose tracking for a few frames.
				moveSpeedi = moveSpeedi * (0.2f/moveSpeedi.magnitude);
		}
	}

	private float rotateSpeedi;
	private float rotateSpeed {
		get { 
			return rotateSpeedi;
		}
		set {
			rotateSpeedi = value;
			if (rotateSpeedi > 180)
				rotateSpeedi -= 360;
			if (rotateSpeedi < -180)
				rotateSpeedi += 360;
			if (Mathf.Abs (rotateSpeedi) > 10) { // make sure we dont' throw it to fast. This can happen when we lose tracking for a few frames.
				if(rotateSpeedi > 0)
					rotateSpeedi = 10;
				else
					rotateSpeedi = -10;
			}
		}
	}

	private bool releasingScale; //true if the last thing to happen was a release of one of the grip buttons while the other one is still down.
	private float scaleSpeedi;
	private float scaleSpeed{
		get { 
			return scaleSpeedi;
		}
		set {
			scaleSpeedi = value;
			if (Mathf.Abs (scaleSpeedi) > 0.1f) // make sure we dont' throw it to fast. This can happen when we lose tracking for a few frames.
			if (scaleSpeedi > 0)
				scaleSpeedi = 0.1f;
			else
				scaleSpeedi = -0.1f;
		}
	}
	private Vector3	lastScaleMid;

	private Settings s;

	void Start () {
		s = this.GetComponent<Settings> ();
		resetSpeed ();
		setMode(Mode.Home);
		displayMenu.SetActive (false); //turn this off until the first filesystem is loaded.
	}

	void resetSpeed() {
		moveSpeed = new Vector3();
		rotateSpeed = 0;
		scaleSpeed = 0;
	}

	void setMode(Mode newMode) {
		switch (newMode) {
		case Mode.Home:
			fsFader (true);
			selectedMenu.SetActive (false);
			planePointer.SetActive (false);
		
			UIRoot.SetActive (true);
			UIPointer.SetActive (true);

			mode = newMode;
			break;
		case Mode.View:
			if(fsc != null) {
				fsFader (false);
				if (selectedDir == null) {
					selectedMenu.SetActive (false);
					planePointer.SetActive (true);
				} else {
					selectedMenu.SetActive (true);
					planePointer.SetActive (false);
				}

				UIRoot.SetActive (false);
				UIPointer.SetActive (false);

				mode = newMode;
			} else {
				screenAlert.text = "Please select\na folder.";
				screenAlert.GetComponent<ClearTextTimer> ().clearInSec (3f);
			}
			break;
		}
	}

	//##################################################
	// START - Thead Management
	//##################################################

	IEnumerator startThread(bool collect, string path, bool rescan) {
		if (layoutCoroutine != null) {
			StopCoroutine (layoutCoroutine);
			working.SetActive (false);
		}
		
		if (fsc != null && collectThread != null && collectThread.IsAlive) {
			fsc.abortThread = true;
			while(collectThread.IsAlive)
				yield return new WaitForEndOfFrame();
			fsc.abortThread = false;
		}

		if (collect) {
			collectThread = new Thread (() => fsc.collect (path, rescan));
			collectThread.Start ();
			layoutCoroutine = StartCoroutine (Layout (!rescan));
		} if (fsc != null && fsc.doneCollecting == true) {
				collectThread = new Thread (() => fsc.buildMesh ());
				collectThread.Start ();
				layoutCoroutine = StartCoroutine (Layout (false));
		}
	}

	private void collectRoot (string path, bool rescan) {
		folderPath.text = path; //note, the folder dialog will update it's display the next time we enable it. No need to do it now since we assume it is not enable now or will quicly be disabled.

		if(fsc == null)
			fsc = new FileSystemCollector (s);

		deselect ();

		if (startThreadCoroutine != null)
			StopCoroutine (startThreadCoroutine);
		startThreadCoroutine = StartCoroutine(startThread (true, path, rescan));
	}

	public void remesh() {
		if (fsc != null && fsc.doneCollecting == true) { //we can only remesh after collecting is done.
			if (startThreadCoroutine != null)
				StopCoroutine (startThreadCoroutine);
			startThreadCoroutine = StartCoroutine (startThread (false, "", false));
		}
	}

	//##################################################
	// END - Thead Management
	//##################################################

	void scale(float s, bool followControllers) {

		if(!scaleVertical && (0.25f/fsc.maxDepth > scaleStart.x * s || scaleStart.x * s > 5000f/fsc.maxDepth))
			return;

		if(scaleVertical && s < 1 && Mathf.Abs(scaleStart.y * s) < 100000f/fsc.maxDirHeight)
			return;

		if (scaleVertical) {
			fileSystemRoot.transform.localScale = Vector3.Scale (scaleStart, new Vector3 (1, s, 1));
		} else {
			fileSystemRoot.transform.localScale = Vector3.Scale (scaleStart, new Vector3 (s, s, s));

			if (followControllers)
				lastScaleMid = Vector3.Lerp (leftController.transform.position, rightController.transform.position, 0.5f);
			fileSystemRoot.transform.position = scaleStartVector * s + lastScaleMid;
		}
	}

	void Update () {
		if (mode == Mode.View) { //trigger is used by UI pointer if we are not in View Mode.
			if (rightController.triggerDown) {
				if (selectedDir == null && pointedDir != null) { //then select the dir and optionally the file.
					selectedDir = pointedDir;
					selectedFile = pointedFile;
					planePointer.SetActive (false);
					selectedMenu.SetActive (true);
					if(selectedFile != null)
						s.setSelectedFileType (selectedFile.fi.Extension);
				} else {
					deselect ();
					planePointer.SetActive (true);
				}
				rightController.triggerDown = false;
			}

			if (leftController.triggerDown) {
				if (selectedDir != null && selectedFile != null) { //then unselect the file. This is an easy way to select only a dir.
					selectedFile = null;
					highlightDirFile (selectedDir, selectedFile);
				}
				leftController.triggerDown = false;
			}
		}

		if (fsc != null && fsc.doneMeshing) {
			//scale
			if (rightController.gridDown && leftController.gridDown) {
				if (scaling) {
					float sTmp = fileSystemRoot.transform.localScale.y;
					if (scaleVertical) {
						scale (Mathf.Abs (leftController.transform.position.y - rightController.transform.position.y) / scaleStartDistance, true);
					} else {
						scale (
							Mathf.Sqrt (
								Mathf.Pow (leftController.transform.position.x - rightController.transform.position.x, 2) +
								Mathf.Pow (leftController.transform.position.z - rightController.transform.position.z, 2)
							) / scaleStartDistance,
							true);
					}
					scaleSpeed = (fileSystemRoot.transform.localScale.y - sTmp) / fileSystemRoot.transform.localScale.y;
				} else {
					scaling = true;
					scaleStart = fileSystemRoot.transform.localScale;
					resetSpeed ();

					float verticalDistance = Mathf.Abs (leftController.transform.position.y - rightController.transform.position.y);

					float horizontalDistance = Mathf.Sqrt (
						                          Mathf.Pow (leftController.transform.position.x - rightController.transform.position.x, 2) +
						                          Mathf.Pow (leftController.transform.position.z - rightController.transform.position.z, 2)
					                          );

					if (verticalDistance > horizontalDistance) {
						scaleStartDistance = verticalDistance;
						scaleVertical = true;
					} else {
						scaleStartDistance = horizontalDistance;
						scaleVertical = false;
						lastScaleMid = Vector3.Lerp (leftController.transform.position, rightController.transform.position, 0.5f);
						scaleStartVector = fileSystemRoot.transform.position - lastScaleMid;
					}
				}
			} else { //!scale
				//finish scaling
				if (scaling) {
					scaling = false;
					releasingScale = true; //we have started realsing the buttons after scaling.
					rightController.setControllerValues ();
					leftController.setControllerValues ();
				}

				if (!rightController.gridDown && !leftController.gridDown)
					releasingScale = false; //we are done relaseing the buttons after scaling.

				//contineue any scaling speed
				if (Mathf.Abs(scaleSpeed) > 0.0001f) {
					scale ((scaleSpeed+1) * fileSystemRoot.transform.localScale.y  / scaleStart.y, false);
					scaleSpeed *= 0.95f;
				}

				//move
				if (rightController.gridDown) {
					if (!releasingScale) {
						moveSpeed = (rightController.transform.position - rightController.gripVector) - fileSystemRoot.transform.position;
						scaleSpeed = 0;
					}

					fileSystemRoot.transform.position = rightController.transform.position - rightController.gripVector;
				} else {
					//contineue any movement speed
					if (moveSpeed.magnitude > 0.001) {
						fileSystemRoot.transform.position += moveSpeed;
						moveSpeed *= 0.97f;
					}
				}

				//rotate
				if (leftController.gridDown) {
					float yTmp = fileSystemRoot.transform.eulerAngles.y;

					float angle = Mathf.Atan2 (
						              leftController.transform.position.z - fileSystemRoot.transform.position.z, 
						              leftController.transform.position.x - fileSystemRoot.transform.position.x
					              ) * Mathf.Rad2Deg;

					fileSystemRoot.transform.rotation = leftController.fileSystemRootRotation * Quaternion.Euler (0, leftController.gripAngle - angle, 0);

					if(!releasingScale) {
						rotateSpeed = yTmp - fileSystemRoot.transform.eulerAngles.y;
						scaleSpeed = 0;
					}
				} else {
					//contineue any rotation speed
					if(Mathf.Abs(rotateSpeed) > 0.001) {
						fileSystemRoot.transform.rotation = Quaternion.Euler(0, fileSystemRoot.transform.eulerAngles.y - rotateSpeed, 0);
						rotateSpeed *= 0.98f;
					}
				}
			}
		}

		if (leftController.menuDown) {
			if (mode == Mode.Home)
				setMode (Mode.View);
			else
				setMode (Mode.Home);
			
			leftController.menuDown = false;
		}

		if (rightController.menuDown) {
			leftControllerToolTips.SetActive ( ! leftControllerToolTips.activeSelf);
			rightControllerToolTips.SetActive ( ! rightControllerToolTips.activeSelf);
			rightController.menuDown = false;
		}
	}

	public void invert() {
		Vector3	s = new Vector3 (fileSystemRoot.transform.localScale.x,fileSystemRoot.transform.localScale.y * -1, fileSystemRoot.transform.localScale.z);
		fileSystemRoot.transform.localScale = s;
		if(scaling) //in case we pressed flip while in the middle of scaling then we need to update scale as well.
			scaleStart.y *= -1;
	}

	private void highlightDirFile(DirectoryVR dir, FileVR file) {
		if (!fsc.doneMeshing) //no point highlight nothing since we are in the middle of remeshing.
			return;

		setDirFileText (dir, file);
		
		if (file != null) {
			float height = s.fileHeight(file);

			mSelectFile.color = s.getFileHLColor (file.fi.Extension);
			fileHL.SetActive (true);
			fileHL.transform.localPosition = new Vector3 (file.x, height / 2, file.z);
			fileHL.transform.localScale = new Vector3 (1, height / 2, 1);

		} else {
			fileHL.SetActive (false);
		}

		if (dir != null) {
			Mesh m = new Mesh ();
			MeshDesign dd = fsc.selectDirMesh (dir);
			m.vertices = dd.vertices;
			m.triangles = dd.triangles;
			;
			m.RecalculateNormals ();
			MeshFilter mf = dirHL.GetComponent<MeshFilter> ();
			mf.mesh = m;
			dirHL.SetActive (true);
		} else {
			dirHL.SetActive (false);
		}
	}

	private void setDirFileText(DirectoryVR d, FileVR f) {
		if (f != null)
			screenTitle.text = f.fi.FullName;
		else if (d != null)
			screenTitle.text = d.di.FullName;
		else {
			screenTitle.text = "FileVR";
			screenLeft.text = "";
			screenRight.text = "";
		}

		if (f != null) {
			screenLeft.text = 
				"<b>File:</b>\n" +
			"Size:\n" +
			"Created:\n" +
			"Last Modified:\n" +
			"Last Accessed:\n" +
			"\n";
			
			screenRight.text = 
				"<b>" + f.fi.Name + "</b>\n" +
			prettyLength (f.fi.Length) + "\n" +
			f.fi.CreationTime.ToString () + "\n" +
			f.fi.LastWriteTime.ToString () + "\n" +
			f.fi.LastAccessTime.ToString () + "\n" +
			"\n";

		} else if (d != null) {
			screenLeft.text = 
				"\n" +
				"\n" +
				"\n" +
				"\n" +
				"\n" +
				"\n";

			screenRight.text = 
				"\n" +
				"\n" +
				"\n" +
				"\n" +
				"\n" +
				"\n";
		}

		if (d != null) {
			screenLeft.text += 
			"<b>Folder:</b>\n" +
			"File Count:\n" +
			"Size (Avg | Total):\n" +
			"Subfolder Count:\n" +
			"Created:\n" +
			"Last Modified:\n" +
			"Last Accessed:\n" +
			"\n" +
			"<b>Folder+Subfolders</b>\n" +
			"File Count:\n" +
			"Folder Count:\n" +
			"Size (Avg | Total):\n";
		
			screenRight.text += 
			"<b>" + d.di.Name + "</b>\n" +
			d.files.Count + "\n" +
			prettyLength (d.files.Count == 0 ? 0 : d.length/d.files.Count) + " | " + prettyLength (d.length)  + "\n" +
			d.subDirs.Count + "\n" +
			d.di.CreationTime.ToString () + "\n" +
			d.di.LastWriteTime.ToString () + "\n" +
			d.di.LastAccessTime.ToString () + "\n" +
			"\n" +
			"\n" +
			d.fileCount + "\n" +
			d.dirCount + "\n" +
			prettyLength (d.fileCount == 0 ? 0 : d.totalLength/d.fileCount) + " | "+ prettyLength (d.totalLength) + "\n";
		}
	}

	private string prettyLength(long b) {
		if (b < 1024 || b / 1024 < 3)
			return (b + " B");
		
		b = (long)Mathf.Round ((float)b / 1024f);
		if (b < 1024 || b / 1024 < 3)
			return (b + " KB");
		
		b = (long)Mathf.Round ((float)b / 1024f);
		if (b < 1024 || b / 1024 < 3)
			return (b + " MB");
		
		b = (long)Mathf.Round ((float)b / 1024f);
		return (b + " GB");
	}

	public bool pointedAt(float x, float z, float angle, float radius, bool FilePointer) {
		//passed args are local space point on fileSystemRoot
		pointedDir = null;
		pointedFile = null;

		bool hit = fsc.rootDir.find (x, z, angle, radius, out pointedDir, out pointedFile, Mathf.RoundToInt(s.MaxLevel));

		if (!FilePointer) //if we are not trying to point at a file then make sure we are not.
			pointedFile = null;
			
		highlightDirFile (pointedDir, pointedFile);

		return hit;
	}

	public void smallScale (){
		resetSpeed ();
		fileSystemRoot.transform.position = new Vector3(0f, 0.9f, 1f);
		fileSystemRoot.transform.rotation = Quaternion.identity;
		fileSystemRoot.transform.localScale = new Vector3(1.5f/fsc.maxDepth, 1.5f/fsc.maxDepth, 1.5f/fsc.maxDepth);
	}

	public void largeScale () {
		resetSpeed ();
		fileSystemRoot.transform.position = new Vector3(0f, 0f, 0f);
		fileSystemRoot.transform.rotation = Quaternion.identity;
		fileSystemRoot.transform.localScale = new Vector3(100f / fsc.maxDepth, 100f / fsc.maxDepth, 100f / fsc.maxDepth);
	}

	public void prevSelected() {
		if (selectedDir != null) {
			fsc.prev (ref selectedDir, ref selectedFile, s.NavigateDirs);
			highlightDirFile (selectedDir, selectedFile);
		}
	}

	public void nextSelected() {
		if (selectedDir != null) {
			fsc.next (ref selectedDir, ref selectedFile, s.NavigateDirs);
			highlightDirFile (selectedDir, selectedFile);
		}
	}

	private void deselect() {
		selectedDir = null;
		selectedFile = null;
		pointedDir = null;
		pointedFile = null;
		highlightDirFile (null, null);
		selectedMenu.SetActive (false);
		s.setSelectedFileType (null);
	}

	public void newPathSelected() {
		collectRoot (folderPath.text, false);
	}

	public void refreshRoot() {
		if (fsc.doneMeshing == true)
			collectRoot (fsc.rootDir.di.FullName, true);
	}

	public void selectedToRoot() {
		if (selectedDir != null && selectedDir != fsc.collectedRootDir) {
			if (selectedDir == fsc.rootDir && fsc.rootDir.parent != null) {
				selectedDir = fsc.rootDir.parent; //select the new center
				selectedFile = null;
				fsc.rootDir = fsc.rootDir.parent; //go up one level
				screenAlert.text = "Center Changed\nto\nParent\n\n(up one level)";
			} else {
				fsc.rootDir = selectedDir;
				screenAlert.text = "Center Changed\nto\nSelected Folder";

				//move file system root center to center of selectedDir
				float degreeMid = selectedDir.startDegree + (selectedDir.endDegree - selectedDir.startDegree)/2;
				float radiusMid = selectedDir.radius + selectedDir.depth/2;
				float x = radiusMid * Mathf.Cos (degreeMid * Mathf.Deg2Rad);
				float z = radiusMid * Mathf.Sin (degreeMid * Mathf.Deg2Rad);
				fileSystemRoot.transform.position = fileSystemRoot.transform.TransformPoint(new Vector3(x, 0, z));
			}
			screenAlert.GetComponent<ClearTextTimer> ().clearInSec (3f);
			folderPath.text = fsc.rootDir.di.FullName;
			remesh ();
		}
	}

	public void showInExplorer() {
		if (selectedDir != null) {
			if(selectedFile != null)
				Process.Start ("explorer.exe", "/select," + selectedFile.fi.FullName);
			else
				Process.Start ("explorer.exe", selectedDir.di.FullName);

			screenAlert.text = "Opened on\nthe Desktop";
			screenAlert.GetComponent<ClearTextTimer> ().clearInSec (3f);
		}
	}

	IEnumerator Layout(bool setView) {
		working.SetActive (true);
		screenTitle.text = "Working...";
		screenLeft.text = "";
		screenRight.text = "";

		resetSpeed ();

		if (setView) {
			//destroy meshes
			GameObject[] obs;
			obs = GameObject.FindGameObjectsWithTag("FileMesh");
			foreach (GameObject o in obs) {
				Destroy (o);
				yield return new WaitForEndOfFrame();
			}
			obs = GameObject.FindGameObjectsWithTag("DirMesh");
			foreach (GameObject o in obs) {
				Destroy (o);
				yield return new WaitForEndOfFrame();
			}

			setMode (Mode.View);
		} // else destroyMeshes later and we do not set the view.

		Stopwatch stopWatch = new Stopwatch();
		stopWatch.Start ();

		bool doFinalUpdate = !fsc.doneCollecting;
		while (fsc.doneCollecting == false) {
			screenLeft.text = "Scanning\n" + 
				stopWatch.ElapsedMilliseconds +"ms\n" + 
				fsc.dirCollected + " Directories\n" + 
				fsc.fileCollected + " Files\n" +
				prettyLength (fsc.lengthCollected) + "\n";
			yield return new WaitForEndOfFrame();
		}
		stopWatch.Stop ();
		if (doFinalUpdate) {
			screenLeft.text = "Scanning\n" +
			stopWatch.ElapsedMilliseconds + "ms\n" +
			fsc.dirCollected + " Directories\n" +
			fsc.fileCollected + " Files\n" +
			prettyLength (fsc.lengthCollected) + "\n";
		}

		string log = screenLeft.text;

		stopWatch.Reset ();
		stopWatch.Start ();

		while (fsc.doneMeshing == false) {
			screenLeft.text = log + 
				"\nComputing Mesh\n" + 
				stopWatch.ElapsedMilliseconds +"ms \n";
			yield return new WaitForEndOfFrame();
		}
		stopWatch.Stop ();

		screenLeft.text = log + 
			"\nComputing Mesh\n" + 
			stopWatch.ElapsedMilliseconds +"ms \n";
		
		screenLeft.text = screenLeft.text + "\nInstantiating\n";

		stopWatch.Reset ();
		stopWatch.Start ();

		if (setView) {
			smallScale (); //we had to wait until mesh was computed before we could scale based on the meshes new size.
		} else {
			//destroy meshes
			GameObject[] obs;
			obs = GameObject.FindGameObjectsWithTag("FileMesh");
			foreach (GameObject o in obs) {
				Destroy (o);
				yield return new WaitForEndOfFrame();
			}
			obs = GameObject.FindGameObjectsWithTag("DirMesh");
			foreach (GameObject o in obs) {
				Destroy (o);
				yield return new WaitForEndOfFrame();
			}
		}

		foreach (MeshDesign md in fsc.fileDesigns) {
			Mesh m = new Mesh ();
			m.vertices = md.vertices;
			m.triangles = md.triangles;
			m.uv = md.uv;
			;
			m.RecalculateNormals ();

			GameObject gm = Instantiate (file);
			fadeObject (fsFaded, gm, mFile);
			gm.tag = "FileMesh";
			MeshFilter mf = gm.GetComponent<MeshFilter> ();
			mf.mesh = m;
			gm.transform.position = fileSystemRoot.transform.position;
			gm.transform.rotation = fileSystemRoot.transform.rotation;
			gm.transform.localScale = fileSystemRoot.transform.localScale;
			gm.transform.parent = fileSystemRoot.transform;

			yield return new WaitForEndOfFrame();
		}

		foreach (MeshDesign md in fsc.dirDesigns) {
			Mesh m = new Mesh ();
			m.vertices = md.vertices;
			m.triangles = md.triangles;
			;
			m.RecalculateNormals ();

			GameObject gm = Instantiate (dir);
			fadeObject (fsFaded, gm, mDir);
			gm.tag = "DirMesh";
			MeshFilter mf = gm.GetComponent<MeshFilter> ();
			mf.mesh = m;
			gm.transform.position = fileSystemRoot.transform.position;
			gm.transform.rotation = fileSystemRoot.transform.rotation;
			gm.transform.localScale = fileSystemRoot.transform.localScale;
			gm.transform.parent = fileSystemRoot.transform;

			yield return new WaitForEndOfFrame();
		}

		stopWatch.Stop ();

		screenLeft.text = screenLeft.text + stopWatch.ElapsedMilliseconds +"ms\n\nDone.";

		screenTitle.text = "FileVR";

		highlightDirFile (selectedDir, selectedFile);
		working.SetActive (false);
		displayMenu.SetActive (true); //this is turned off when we first start up. Once we have a filesystem loaded we can leave it truned on.
	}

	private bool fsFaded;

	private void fsFader(bool fade) {
		this.fsFaded = fade;

		GameObject[] obs;
		obs = GameObject.FindGameObjectsWithTag("FileMesh");
		foreach (GameObject o in obs) {
			fadeObject (fade, o, mFile);
		}
		obs = GameObject.FindGameObjectsWithTag("DirMesh");
		foreach (GameObject o in obs) {
			fadeObject (fade, o, mDir);
		}

		fadeObject (fade, fileHL, mSelectFile);
		fadeObject (fade, dirHL, mSelectDir);
	}

	private void fadeObject(bool fade, GameObject o, Material normalMaterial) {
		if(fade)
			o.GetComponent<Renderer> ().material = mFade;
		else
			o.GetComponent<Renderer> ().material = normalMaterial;
			
	}
}
