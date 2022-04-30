using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

public class MeshDesign {
	public Vector3[] vertices;
	public Vector2[] uv;
	public int[] triangles;
}

public class FileSystemCollector {
	public bool doneCollecting = false;
	public bool doneMeshing = false;
	public DirectoryVR rootDir; //root that we are currently displaying. Either equal to collectedRootDir or a subDir of collectedRootDir.
	public DirectoryVR collectedRootDir; //root of all dirs collected.
	public List<MeshDesign> fileDesigns;
	public List<MeshDesign> dirDesigns;
	public float maxDepth = 0;
	public float maxDirHeight = 0;
	public float maxFileHeight = 0;
	public float maxLevel = 0;
	public long dirCollected = 0;
	public long fileCollected = 0;
	public long lengthCollected = 0;
	public bool abortThread = false;

	private Settings s;

	public FileSystemCollector(Settings s) {
		this.s = s;
	}

	public void collect(string root, bool rescan) { //rescan will collect from rootDir down, leaving the rest of collectedRootDir alone.
		doneCollecting = false;
		doneMeshing = false;
		fileDesigns = null;
		dirDesigns = null;
		dirCollected = 0;
		fileCollected = 0;
		lengthCollected = 0;

		Stack<DirectoryVR> dirs = new Stack<DirectoryVR>();

		try {
			if (rescan && rootDir != null) {
				rootDir.restoreDefaults(rootDir.di.FullName);
			} else {
				rootDir = new DirectoryVR (root, null);
				collectedRootDir = rootDir;
			}
			dirCollected++;
		} catch (UnauthorizedAccessException e) {                    
			Debug.Log (e.Message);
			return;
		} catch (System.IO.DirectoryNotFoundException e) {
			Debug.Log (e.Message);
			return;
		}

		dirs.Push(rootDir);

		while (dirs.Count > 0)
		{
			if (abortThread)
				return;
			
			DirectoryVR currentDir = dirs.Pop();
			string[] subDirs;
			try {
				subDirs = System.IO.Directory.GetDirectories(currentDir.di.FullName);
			} catch (UnauthorizedAccessException e) {                    
				Debug.Log(e.Message);
				continue;
			} catch (System.IO.DirectoryNotFoundException e) {
				Debug.Log(e.Message);
				continue;
			}
			Array.Sort (subDirs);

			foreach (string str in subDirs) {
				if (abortThread)
					return;
				
				DirectoryVR d;
				try {
					d = new DirectoryVR(str,currentDir);
					dirCollected++;
				} catch (UnauthorizedAccessException e) {                    
					Debug.Log(e.Message);
					continue;
				} catch (System.IO.DirectoryNotFoundException e) {
					Debug.Log(e.Message);
					continue;
				}
				currentDir.subDirs.Add(d);
				dirs.Push(d);
			}

			string[] files = null;
			try {
				files = System.IO.Directory.GetFiles(currentDir.di.FullName);
			} catch (UnauthorizedAccessException e) {
				Debug.Log(e.Message);
				continue;
			} catch (System.IO.DirectoryNotFoundException e) {
				Debug.Log(e.Message);
				continue;
			}
			Array.Sort (files);

			foreach (string f in files) {
				if (abortThread)
					return;
				
				FileVR file;
				try {
					file = new FileVR(f,currentDir);
					fileCollected++;
					lengthCollected += file.fi.Length;
				} catch (System.IO.FileNotFoundException e) {
					Debug.Log(e.Message);
					continue;
				}
				currentDir.files.Add(file);
			}
		}
		doneCollecting = true;

		if (abortThread)
			return;
		
		buildMesh ();
	}

	//////////////////////////////////
	/// Meshing 
	/////////////////////////////////

	private static int vertPerFile = 6;
	private static int triPerFile = 2;

	private static int vertPerDirSeg = 8;
	private static int triPerDirSeg = 8;
	private static int vertPerDirEnd = 4;
	private static int triPerDirEnd = 2;
	private static float maxDegreePerDirSegment = 5;

	public void buildMesh() {
		doneMeshing = false;

		rootDir.setCounts(1);

		fileDesigns = new List<MeshDesign>();
		dirDesigns = new List<MeshDesign>();

		float startRadius = 0;
		if (rootDir != collectedRootDir)
			startRadius = 1;
		
		rootDir.setSunBurstCoord( 0 - s.Degrees/2, s.Degrees/2, 0 - s.Degrees/2, s.Degrees/2, startRadius, s.FileSize, s.FolderSize);
		
		maxDepth = 0;
		maxDirHeight = 0;
		maxFileHeight = 0;
		maxLevel = 0;
		rootDir.getMax(ref maxDepth, ref maxDirHeight, ref maxFileHeight, ref maxLevel);

		MeshDesign dd = null;
		int dvindex = 0;
		int dtindex = 0;

		MeshDesign fd = null;
		int fvindex = 0;
		int ftindex = 0;

		Stack<DirectoryVR> dirs = new Stack<DirectoryVR>();
		dirs.Push (rootDir);

		while (dirs.Count > 0) {
			if (abortThread)
				return;
			
			DirectoryVR currentDir = dirs.Pop ();

			if (currentDir.level != Mathf.RoundToInt(s.MaxLevel)) {
				foreach (DirectoryVR d in currentDir.subDirs) {
					dirs.Push (d);
				}
			}

			int segmentTotal = Mathf.CeilToInt ((currentDir.endBackDegree - currentDir.startBackDegree) / maxDegreePerDirSegment); //total segments for this dirs arc when we build it.

			int verTotal = segmentTotal * vertPerDirSeg + vertPerDirSeg + vertPerDirEnd * 2;
			int triTotal = segmentTotal * triPerDirSeg * 3 + triPerDirEnd * 3 * 2;
			if (dd != null && (dvindex + verTotal > 65534 || dtindex + triTotal > 65534)) { //if this dirs mesh will not fit in dd;
				//new dir will not fit so store this dd as is so we can start a new one.
				Array.Resize (ref dd.vertices, dvindex);
				Array.Resize (ref dd.triangles, dtindex);
				dirDesigns.Add (dd);
				dd = null;
			}

			if (dd == null) {
				dd = new MeshDesign ();
				dd.vertices = new Vector3[65534];
				dd.triangles = new int[65534];
				dvindex = 0;
				dtindex = 0;
			}

			dirMesh (currentDir, dd, segmentTotal, ref dvindex, ref dtindex);

			// Add mesh for all files
			foreach (FileVR f in currentDir.files) {
				if (abortThread)
					return;

				if (fd != null && (fvindex + vertPerFile > 65534 || ftindex + triPerFile > 65534)) { //if this file mesh will not fit in fd;
					Array.Resize(ref fd.vertices, fvindex);
					Array.Resize(ref fd.triangles, ftindex);
					Array.Resize(ref fd.uv, fvindex);
					fileDesigns.Add (fd);
					fd = null;
				}

				//if we don't have an fd then create one.
				if (fd == null) {
					fd = new MeshDesign ();
					fd.vertices = new Vector3[65534];
					fd.triangles = new int[65534];
					fd.uv = new Vector2[65534];
					fvindex = 0;
					ftindex = 0;
				}

				fileMesh(f, fd, ref fvindex, ref ftindex, currentDir);
			}
		}

		//reduce size of last unfilled dd and add store it.
		Array.Resize(ref dd.vertices, dvindex);
		Array.Resize(ref dd.triangles, dtindex);
		dirDesigns.Add (dd);
		dd = null;

		//reduce size of last unfilled fd and add store it.
		Array.Resize(ref fd.vertices, fvindex);
		Array.Resize(ref fd.triangles, ftindex);
		Array.Resize(ref fd.uv, fvindex);
		fileDesigns.Add (fd);
		fd = null;

		doneMeshing = true;
	}

	public void dirMesh(DirectoryVR currentDir, MeshDesign dd, int segmentTotal, ref int dvindex, ref int dtindex) {
		float height = s.dirHeight(currentDir);

		float frontDegreeWidth = currentDir.endFrontDegree - currentDir.startFrontDegree;
		float backDegreeWidth = currentDir.endBackDegree - currentDir.startBackDegree;

		//add first dir side
		float frontDegree = currentDir.startFrontDegree * Mathf.Deg2Rad;
		float backDegree = currentDir.startBackDegree * Mathf.Deg2Rad;
		float sinFront = Mathf.Sin (frontDegree);
		float cosFront = Mathf.Cos (frontDegree);
		float sinBack = Mathf.Sin (backDegree);
		float cosBack = Mathf.Cos (backDegree);

		if (currentDir.endFrontDegree - currentDir.startFrontDegree != 360) { //no sides if we are making a full circle, else when transparent you see an off inside.
			dd.vertices [dvindex++] = new Vector3 (currentDir.radius * cosFront, 0, currentDir.radius * sinFront); //top front
			dd.vertices [dvindex++] = new Vector3 ((currentDir.radius + currentDir.depth) * cosBack, 0, (currentDir.radius + currentDir.depth) * sinBack); //top back
			dd.vertices [dvindex++] = new Vector3 (currentDir.radius * cosFront, height, currentDir.radius * sinFront); //bottom front
			dd.vertices [dvindex++] = new Vector3 ((currentDir.radius + currentDir.depth) * cosBack, height, (currentDir.radius + currentDir.depth) * sinBack); //bottom back
			dd.triangles [dtindex++] = dvindex - 4;
			dd.triangles [dtindex++] = dvindex - 3;
			dd.triangles [dtindex++] = dvindex - 1;
			dd.triangles [dtindex++] = dvindex - 4; 
			dd.triangles [dtindex++] = dvindex - 1;
			dd.triangles [dtindex++] = dvindex - 2;
		}

		//add first eight dir vertices of first segment
		//top
		dd.vertices [dvindex++] = new Vector3 (currentDir.radius * cosFront, 0, currentDir.radius * sinFront); //top front
		dd.vertices [dvindex++] = new Vector3 ((currentDir.radius + currentDir.depth) * cosBack, 0, (currentDir.radius + currentDir.depth) * sinBack); //top back
		//bottom
		dd.vertices [dvindex++] = new Vector3 (currentDir.radius * cosFront, height, currentDir.radius * sinFront); //bottom front
		dd.vertices [dvindex++] = new Vector3 ((currentDir.radius + currentDir.depth) * cosBack, height, (currentDir.radius + currentDir.depth) * sinBack); //bottom back
		//front
		dd.vertices [dvindex++] = new Vector3 (currentDir.radius * cosFront, 0, currentDir.radius * sinFront); //front top
		dd.vertices [dvindex++] = new Vector3 (currentDir.radius * cosFront, height, currentDir.radius * sinFront); //front bottom
		//back
		dd.vertices [dvindex++] = new Vector3 ((currentDir.radius + currentDir.depth) * cosBack, 0, (currentDir.radius + currentDir.depth) * sinBack); //back top
		dd.vertices [dvindex++] = new Vector3 ((currentDir.radius + currentDir.depth) * cosBack, height, (currentDir.radius + currentDir.depth) * sinBack); //back bottom

		//add all dir segments and triangles
		for(int i = 1; i <= segmentTotal; i++) {
			frontDegree = (float)i/(float)segmentTotal * frontDegreeWidth + currentDir.startFrontDegree;
			backDegree = (float)i/(float)segmentTotal * backDegreeWidth + currentDir.startBackDegree;
			sinFront = Mathf.Sin (frontDegree * Mathf.Deg2Rad);
			cosFront = Mathf.Cos (frontDegree * Mathf.Deg2Rad);
			sinBack = Mathf.Sin (backDegree * Mathf.Deg2Rad);
			cosBack = Mathf.Cos (backDegree * Mathf.Deg2Rad);

			//top
			dd.vertices [dvindex++] = new Vector3 (currentDir.radius * cosFront, 0, currentDir.radius * sinFront); //top front
			dd.vertices [dvindex++] = new Vector3 ((currentDir.radius + currentDir.depth) * cosBack, 0, (currentDir.radius + currentDir.depth) * sinBack); //top back
			dd.triangles [dtindex++] = dvindex - vertPerDirSeg - 2; //top front old
			dd.triangles [dtindex++] = dvindex - 2; //top front new
			dd.triangles [dtindex++] = dvindex - vertPerDirSeg - 1; //top back old
			dd.triangles [dtindex++] = dvindex - 2; //top front new
			dd.triangles [dtindex++] = dvindex - 1; //top back new
			dd.triangles [dtindex++] = dvindex - vertPerDirSeg - 1; //top back old
			//bottom
			dd.vertices [dvindex++] = new Vector3 (currentDir.radius * cosFront, height, currentDir.radius * sinFront); //bottom front
			dd.vertices [dvindex++] = new Vector3 ((currentDir.radius + currentDir.depth) * cosBack, height, (currentDir.radius + currentDir.depth) * sinBack); //bottom back
			dd.triangles [dtindex++] = dvindex - vertPerDirSeg - 1; 
			dd.triangles [dtindex++] = dvindex - 2;
			dd.triangles [dtindex++] = dvindex - vertPerDirSeg - 2;
			dd.triangles [dtindex++] = dvindex - vertPerDirSeg - 1; 
			dd.triangles [dtindex++] = dvindex - 1;
			dd.triangles [dtindex++] = dvindex - 2;
			//front
			dd.vertices [dvindex++] = new Vector3 (currentDir.radius * cosFront, 0, currentDir.radius * sinFront); //front top
			dd.vertices [dvindex++] = new Vector3 (currentDir.radius * cosFront, height, currentDir.radius * sinFront); //front bottom
			dd.triangles [dtindex++] = dvindex - 2; 
			dd.triangles [dtindex++] = dvindex - vertPerDirSeg - 2;
			dd.triangles [dtindex++] = dvindex - vertPerDirSeg - 1;
			dd.triangles [dtindex++] = dvindex - vertPerDirSeg - 1; 
			dd.triangles [dtindex++] = dvindex - 1;
			dd.triangles [dtindex++] = dvindex - 2;
			//back
			dd.vertices [dvindex++] = new Vector3 ((currentDir.radius + currentDir.depth) * cosBack, 0, (currentDir.radius + currentDir.depth) * sinBack); //back top
			dd.vertices [dvindex++] = new Vector3 ((currentDir.radius + currentDir.depth) * cosBack, height, (currentDir.radius + currentDir.depth) * sinBack); //back bottom
			dd.triangles [dtindex++] = dvindex - vertPerDirSeg - 2; 
			dd.triangles [dtindex++] = dvindex - 2;
			dd.triangles [dtindex++] = dvindex - vertPerDirSeg - 1;
			dd.triangles [dtindex++] = dvindex - vertPerDirSeg - 1; 
			dd.triangles [dtindex++] = dvindex - 2;
			dd.triangles [dtindex++] = dvindex - 1;
		}

		//add second dir side
		if (currentDir.endFrontDegree - currentDir.startFrontDegree != 360) { //no sides if we are making a full circle, else when transparent you see an off inside.
			dd.vertices [dvindex++] = new Vector3 (currentDir.radius * cosFront, 0, currentDir.radius * sinFront); //top front
			dd.vertices [dvindex++] = new Vector3 ((currentDir.radius + currentDir.depth) * cosBack, 0, (currentDir.radius + currentDir.depth) * sinBack); //top back
			dd.vertices [dvindex++] = new Vector3 (currentDir.radius * cosFront, height, currentDir.radius * sinFront); //bottom front
			dd.vertices [dvindex++] = new Vector3 ((currentDir.radius + currentDir.depth) * cosBack, height, (currentDir.radius + currentDir.depth) * sinBack); //bottom back
			dd.triangles [dtindex++] = dvindex - 4;
			dd.triangles [dtindex++] = dvindex - 2;
			dd.triangles [dtindex++] = dvindex - 3;
			dd.triangles [dtindex++] = dvindex - 3; 
			dd.triangles [dtindex++] = dvindex - 2;
			dd.triangles [dtindex++] = dvindex - 1;
		}
	}

	public MeshDesign selectDirMesh(DirectoryVR currentDir) {
		int segmentTotal = Mathf.CeilToInt ((currentDir.endBackDegree - currentDir.startBackDegree) / maxDegreePerDirSegment); //total segments for this dirs arc when we build it.
		int verTotal = segmentTotal * vertPerDirSeg + vertPerDirSeg + vertPerDirEnd * 2;
		int triTotal = segmentTotal * triPerDirSeg * 3 + triPerDirEnd * 3 * 2;

		if (currentDir.endFrontDegree - currentDir.startFrontDegree == 360) { //no sides if we are making a full circle, else when transparent you see an off inside.
			verTotal -= vertPerDirEnd * 2;
			triTotal -= triPerDirEnd * 3 * 2;
		}

		MeshDesign dd = new MeshDesign ();
		dd.vertices = new Vector3[verTotal];
		dd.triangles = new int[triTotal];

		int dvindex = 0;
		int dtindex = 0;

		float offsetHeight = -0.02f;
		float offsetRadius = 0.02f;
		float offsetDegree = 0.1f;

		float height = s.dirHeight(currentDir);

		float frontDegreeWidth = currentDir.endFrontDegree - currentDir.startFrontDegree;
		float backDegreeWidth = currentDir.endBackDegree - currentDir.startBackDegree;

		//add first dir side
		float frontDegree = (currentDir.startFrontDegree-offsetDegree) * Mathf.Deg2Rad;
		float backDegree = (currentDir.startBackDegree-offsetDegree) * Mathf.Deg2Rad;
		float sinFront = Mathf.Sin (frontDegree);
		float cosFront = Mathf.Cos (frontDegree);
		float sinBack = Mathf.Sin (backDegree);
		float cosBack = Mathf.Cos (backDegree);

		if (currentDir.endFrontDegree - currentDir.startFrontDegree != 360) { //no sides if we are making a full circle, else when transparent you see an off inside.
			dd.vertices [dvindex++] = new Vector3 ((currentDir.radius - offsetRadius) * cosFront, 0 - offsetHeight, (currentDir.radius - offsetRadius) * sinFront); //top front
			dd.vertices [dvindex++] = new Vector3 ((currentDir.radius + offsetRadius + currentDir.depth) * cosBack, 0 - offsetHeight, (currentDir.radius + offsetRadius + currentDir.depth) * sinBack); //top back
			dd.vertices [dvindex++] = new Vector3 ((currentDir.radius - offsetRadius) * cosFront, height + offsetHeight, (currentDir.radius - offsetRadius) * sinFront); //bottom front
			dd.vertices [dvindex++] = new Vector3 ((currentDir.radius + offsetRadius + currentDir.depth) * cosBack, height + offsetHeight, (currentDir.radius + offsetRadius + currentDir.depth) * sinBack); //bottom back
			dd.triangles [dtindex++] = dvindex - 4;
			dd.triangles [dtindex++] = dvindex - 3;
			dd.triangles [dtindex++] = dvindex - 1;
			dd.triangles [dtindex++] = dvindex - 4; 
			dd.triangles [dtindex++] = dvindex - 1;
			dd.triangles [dtindex++] = dvindex - 2;
		}

		//add first eight dir vertices of first segment
		//top
		dd.vertices [dvindex++] = new Vector3 ((currentDir.radius-offsetRadius) * cosFront, 0-offsetHeight, (currentDir.radius-offsetRadius) * sinFront); //top front
		dd.vertices [dvindex++] = new Vector3 ((currentDir.radius + offsetRadius + currentDir.depth) * cosBack, 0-offsetHeight, (currentDir.radius + offsetRadius + currentDir.depth) * sinBack); //top back
		//bottom
		dd.vertices [dvindex++] = new Vector3 ((currentDir.radius-offsetRadius) * cosFront, height+offsetHeight, (currentDir.radius-offsetRadius) * sinFront); //bottom front
		dd.vertices [dvindex++] = new Vector3 ((currentDir.radius + offsetRadius + currentDir.depth) * cosBack, height+offsetHeight, (currentDir.radius + offsetRadius + currentDir.depth) * sinBack); //bottom back
		//front
		dd.vertices [dvindex++] = new Vector3 ((currentDir.radius-offsetRadius) * cosFront, 0-offsetHeight, (currentDir.radius-offsetRadius) * sinFront); //front top
		dd.vertices [dvindex++] = new Vector3 ((currentDir.radius-offsetRadius) * cosFront, height+offsetHeight, (currentDir.radius-offsetRadius) * sinFront); //front bottom
		//back
		dd.vertices [dvindex++] = new Vector3 ((currentDir.radius + offsetRadius + currentDir.depth) * cosBack, 0-offsetHeight, (currentDir.radius + offsetRadius + currentDir.depth) * sinBack); //back top
		dd.vertices [dvindex++] = new Vector3 ((currentDir.radius + offsetRadius + currentDir.depth) * cosBack, height+offsetHeight, (currentDir.radius + offsetRadius + currentDir.depth) * sinBack); //back bottom

		//add all dir segments and triangles
		for(int i = 1; i <= segmentTotal; i++) {
			frontDegree = ((float)i/(float)segmentTotal * frontDegreeWidth + currentDir.startFrontDegree + offsetDegree) * Mathf.Deg2Rad;
			backDegree = ((float)i/(float)segmentTotal * backDegreeWidth + currentDir.startBackDegree + offsetDegree) * Mathf.Deg2Rad;
			sinFront = Mathf.Sin (frontDegree);
			cosFront = Mathf.Cos (frontDegree);
			sinBack = Mathf.Sin (backDegree);
			cosBack = Mathf.Cos (backDegree);

			//top
			dd.vertices [dvindex++] = new Vector3 ((currentDir.radius-offsetRadius) * cosFront, 0-offsetHeight, (currentDir.radius-offsetRadius) * sinFront); //top front
			dd.vertices [dvindex++] = new Vector3 ((currentDir.radius + offsetRadius + currentDir.depth) * cosBack, 0-offsetHeight, (currentDir.radius + offsetRadius + currentDir.depth) * sinBack); //top back
			dd.triangles [dtindex++] = dvindex - vertPerDirSeg - 2; //top front old
			dd.triangles [dtindex++] = dvindex - 2; //top front new
			dd.triangles [dtindex++] = dvindex - vertPerDirSeg - 1; //top back old
			dd.triangles [dtindex++] = dvindex - 2; //top front new
			dd.triangles [dtindex++] = dvindex - 1; //top back new
			dd.triangles [dtindex++] = dvindex - vertPerDirSeg - 1; //top back old
			//bottom
			dd.vertices [dvindex++] = new Vector3 ((currentDir.radius-offsetRadius) * cosFront, height+offsetHeight, (currentDir.radius-offsetRadius) * sinFront); //bottom front
			dd.vertices [dvindex++] = new Vector3 ((currentDir.radius + offsetRadius + currentDir.depth) * cosBack, height+offsetHeight, (currentDir.radius + offsetRadius + currentDir.depth) * sinBack); //bottom back
			dd.triangles [dtindex++] = dvindex - vertPerDirSeg - 1; 
			dd.triangles [dtindex++] = dvindex - 2;
			dd.triangles [dtindex++] = dvindex - vertPerDirSeg - 2;
			dd.triangles [dtindex++] = dvindex - vertPerDirSeg - 1; 
			dd.triangles [dtindex++] = dvindex - 1;
			dd.triangles [dtindex++] = dvindex - 2;
			//front
			dd.vertices [dvindex++] = new Vector3 ((currentDir.radius-offsetRadius) * cosFront, 0-offsetHeight, (currentDir.radius-offsetRadius) * sinFront); //front top
			dd.vertices [dvindex++] = new Vector3 ((currentDir.radius-offsetRadius) * cosFront, height+offsetHeight, (currentDir.radius-offsetRadius) * sinFront); //front bottom
			dd.triangles [dtindex++] = dvindex - 2; 
			dd.triangles [dtindex++] = dvindex - vertPerDirSeg - 2;
			dd.triangles [dtindex++] = dvindex - vertPerDirSeg - 1;
			dd.triangles [dtindex++] = dvindex - vertPerDirSeg - 1; 
			dd.triangles [dtindex++] = dvindex - 1;
			dd.triangles [dtindex++] = dvindex - 2;
			//back
			dd.vertices [dvindex++] = new Vector3 ((currentDir.radius + offsetRadius + currentDir.depth) * cosBack, 0-offsetHeight, (currentDir.radius + offsetRadius + currentDir.depth) * sinBack); //back top
			dd.vertices [dvindex++] = new Vector3 ((currentDir.radius + offsetRadius + currentDir.depth) * cosBack, height+offsetHeight, (currentDir.radius + offsetRadius + currentDir.depth) * sinBack); //back bottom
			dd.triangles [dtindex++] = dvindex - vertPerDirSeg - 2; 
			dd.triangles [dtindex++] = dvindex - 2;
			dd.triangles [dtindex++] = dvindex - vertPerDirSeg - 1;
			dd.triangles [dtindex++] = dvindex - vertPerDirSeg - 1; 
			dd.triangles [dtindex++] = dvindex - 2;
			dd.triangles [dtindex++] = dvindex - 1;
		}

		//add second dir side
		if (currentDir.endFrontDegree - currentDir.startFrontDegree != 360) { //no sides if we are making a full circle, else when transparent you see an off inside.
			dd.vertices [dvindex++] = new Vector3 ((currentDir.radius - offsetRadius) * cosFront, 0 - offsetHeight, (currentDir.radius - offsetRadius) * sinFront); //top front
			dd.vertices [dvindex++] = new Vector3 ((currentDir.radius + offsetRadius + currentDir.depth) * cosBack, 0 - offsetHeight, (currentDir.radius + offsetRadius + currentDir.depth) * sinBack); //top back
			dd.vertices [dvindex++] = new Vector3 ((currentDir.radius - offsetRadius) * cosFront, height + offsetHeight, (currentDir.radius - offsetRadius) * sinFront); //bottom front
			dd.vertices [dvindex++] = new Vector3 ((currentDir.radius + offsetRadius + currentDir.depth) * cosBack, height + offsetHeight, (currentDir.radius + offsetRadius + currentDir.depth) * sinBack); //bottom back
			dd.triangles [dtindex++] = dvindex - 4;
			dd.triangles [dtindex++] = dvindex - 2;
			dd.triangles [dtindex++] = dvindex - 3;
			dd.triangles [dtindex++] = dvindex - 3; 
			dd.triangles [dtindex++] = dvindex - 2;
			dd.triangles [dtindex++] = dvindex - 1;
		}

		return dd;
	}

	public void fileMesh(FileVR f, MeshDesign fd, ref int fvindex, ref int ftindex, DirectoryVR currentDir) {
		//compute height of this file
		float height = s.fileHeight(f);

		Vector3 postion = new Vector3 (f.x, 0, f.z);

		Quaternion rotation;
		float frontDirWidth = (currentDir.endFrontDegree - currentDir.startFrontDegree) * Mathf.Deg2Rad * currentDir.radius;
		float backDirWidth = (currentDir.endBackDegree - currentDir.startBackDegree) * Mathf.Deg2Rad * (currentDir.radius + currentDir.depth);
		float currentWidth = frontDirWidth + (backDirWidth-frontDirWidth) * (f.radius - currentDir.radius) / currentDir.depth;
		if(s.FileSize * 0.55 > currentWidth) //if directory width is less than s.FileSize then turn file sideways from normal
			rotation = Quaternion.Euler(0, Mathf.Atan2(postion.x, postion.z) * Mathf.Rad2Deg + 90, 0);
		else //face file towards 0,0
			rotation = Quaternion.Euler(0, Mathf.Atan2(postion.x, postion.z) * Mathf.Rad2Deg, 0);

		fd.vertices [fvindex + 0] = rotation * new Vector3 (-0.5f, height, 0) + postion; //top left
		fd.vertices [fvindex + 1] = rotation * new Vector3 (0, 0, 0) + postion; //bottom center
		fd.vertices [fvindex + 2] = rotation * new Vector3 (0.5f , height, 0) + postion; //top right

		fd.triangles [ftindex + 0] = fvindex + 0; 
		fd.triangles [ftindex + 1] = fvindex + 2;
		fd.triangles [ftindex + 2] = fvindex + 1;

		fd.vertices [fvindex + 3] = rotation * new Vector3 (-0.5f, height, 0) + postion; //top left
		fd.vertices [fvindex + 4] = rotation * new Vector3 (0, 0, 0) + postion; //bottom center
		fd.vertices [fvindex + 5] = rotation * new Vector3 (0.5f, height, 0) + postion; //top right

		fd.triangles [ftindex + 3] = fvindex + 3; 
		fd.triangles [ftindex + 4] = fvindex + 4;
		fd.triangles [ftindex + 5] = fvindex + 5;

		int color = s.getFileColorIndex(f.fi.Extension);
		float colorPixelY = (float)(s.fileTextureSlices - color) * (float)s.fileTextureHeight / (float)s.fileTextureSlices - (float)s.fileTextureHeight / (float)s.fileTextureSlices / 2; //y coord is from bottom, not top.
		float colorPercent = colorPixelY/(float)s.fileTextureHeight;
		fd.uv [fvindex + 0] = new Vector2(0.2f,colorPercent);
		fd.uv [fvindex + 1] = new Vector2(0.8f,colorPercent);
		fd.uv [fvindex + 2] = new Vector2(0.2f,colorPercent);
		fd.uv [fvindex + 3] = new Vector2(0.2f,colorPercent);
		fd.uv [fvindex + 4] = new Vector2(0.8f,colorPercent);
		fd.uv [fvindex + 5] = new Vector2(0.2f,colorPercent);

		fvindex += vertPerFile;
		ftindex += triPerFile * 3;
	}

	private DirectoryVR findPrevDir(DirectoryVR d, bool filesRequired) {
		if (d == rootDir) {
			//find right most child
			while (d.subDirs.Count > 0)
				d = d.subDirs [d.subDirs.Count - 1];
		} else if(d.parent.subDirs.IndexOf(d) > 0) { //d has a sibling to the left
			//find left siblings rigth most child
			d = d.parent.subDirs[d.parent.subDirs.IndexOf(d) - 1];
			while (d.subDirs.Count > 0)
				d = d.subDirs [d.subDirs.Count - 1];
		} else {
			d = d.parent;
		}

		if ((d.files.Count == 0 && filesRequired) || d.fileCount == 0)
			d = findPrevDir (d, filesRequired);
		return d;
	}

	public void prev (ref DirectoryVR selectedDir, ref FileVR selectedFile, bool crossDirs){
		if (selectedDir == null)
			return;
		if (selectedFile == null) { //goto prev dir
			if (crossDirs) { //find next dir of all dirs
				selectedDir = findPrevDir (selectedDir, false);
			} else if(selectedDir != rootDir && selectedDir.parent != null && selectedDir.parent.subDirs.Count > 1) { //find prev dir of currentDirs sidblings
				DirectoryVR old = selectedDir;
				do {
					if (selectedDir.parent.subDirs.IndexOf (selectedDir) == 0)
						selectedDir = selectedDir.parent.subDirs [selectedDir.parent.subDirs.Count - 1];
					else
						selectedDir = selectedDir.parent.subDirs [selectedDir.parent.subDirs.IndexOf (selectedDir) - 1];
					if (old == selectedDir) //there were siblings but they were all totally empty of files, including subdirs.
						break;
				} while(selectedDir.fileCount == 0); //new selectedDir is totally empty of files, including subdirs.
			}
		} else { // goto prev file
			int i = selectedDir.files.IndexOf (selectedFile);
			i--;
			if (i == -1) {
				if (crossDirs) {
					selectedDir = findPrevDir (selectedDir, true);
				}
				i = selectedDir.files.Count - 1;
			}
			selectedFile = selectedDir.files [i];
		}
	}

	private DirectoryVR findNextDir(DirectoryVR d, DirectoryVR lastChild, bool filesRequired) {
		if (lastChild == null) { //if we are not moving up the tree
			if (d.subDirs.Count > 0) { //if we are not moving back up the tree and there a subdir.
				d = d.subDirs [0]; //go to first subdir
			} else {
				if (d !=  rootDir)
					d = findNextDir (d.parent, d, filesRequired);
				else
					d = rootDir;
			}
		} else { 
			if (d.subDirs.IndexOf (lastChild) < d.subDirs.Count - 1) { //is there a subdir of the parent that is after lastChild
				d = d.subDirs [d.subDirs.IndexOf (lastChild) + 1];
			} else {
				if (d != rootDir)
					d = findNextDir (d.parent, d, filesRequired);
				else
					d = rootDir;
			}
		}
		if ((d.files.Count == 0 && filesRequired) || d.fileCount == 0)
			d = findNextDir (d, null, filesRequired);
		
		return d;
	}

	public void next (ref DirectoryVR selectedDir, ref FileVR selectedFile, bool crossDirs) {
		if (selectedDir == null)
			return;
		if (selectedFile == null) { //goto next dir
			if (crossDirs) { //find next dir of all dirs
				selectedDir = findNextDir (selectedDir, null, false);
			} else if(selectedDir != rootDir && selectedDir.parent != null && selectedDir.parent.subDirs.Count > 1) { //find next dir of currentDirs sidblings
				DirectoryVR old = selectedDir;
				do {
					if (selectedDir.parent.subDirs.IndexOf (selectedDir) == selectedDir.parent.subDirs.Count - 1)
						selectedDir = selectedDir.parent.subDirs [0];
					else
						selectedDir = selectedDir.parent.subDirs [selectedDir.parent.subDirs.IndexOf (selectedDir) + 1];
					if (old == selectedDir) //there were siblings but they were all totally empty of files, including subdirs.
						break;
				} while(selectedDir.fileCount == 0); //new selectedDir is totally empty of files, including subdirs.
			}
		} else { //goto next file
			int i = selectedDir.files.IndexOf (selectedFile);
			i++;
			if (i == selectedDir.files.Count) {
				if (crossDirs) {
					selectedDir = findNextDir (selectedDir, null, true);
				}
				i = 0; //go to first file in this dir.
			}
			selectedFile = selectedDir.files [i];
		}
	}
}