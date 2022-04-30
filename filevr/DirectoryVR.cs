using UnityEngine;
using System.Collections.Generic;

public class DirectoryVR {
	public DirectoryVR parent = null;
	public System.IO.DirectoryInfo di;
	public List<DirectoryVR> subDirs;
	public List<FileVR> files;

	//Counts
	public long length; //size in bytes of all files in this direcotory
	public long totalLength; //size in bytes of all files in and below this direcotory
	public int level;
	public long fileCount; //Count of all files in and below this directory.
	public long dirCount; //Count of all dirs, including this one and below.

	///////////////////////
	//layout
	///////////////////////
	// unbuffered degrees
	public float startDegree;
	public float endDegree;
	//buffered degress
	public float startFrontDegree;
	public float endFrontDegree;
	public float startBackDegree;
	public float endBackDegree;
	//distance from center
	public float radius;
	public float depth;
	// X/Z size, including buffer space, of one file in 3D space
	public float fileSize; //layout (mesh) size of files in this directory

	public DirectoryVR(string dir, DirectoryVR parent) {
		this.parent = parent;
		restoreDefaults (dir);
	}

	public void restoreDefaults(string dir) {
		di = new System.IO.DirectoryInfo (dir);
		subDirs = new List<DirectoryVR>();
		files = new List<FileVR>();
	}

	public void setCounts(int level) {
		length = 0;
		totalLength = 0;
		this.level = level;

		dirCount = 1;
		fileCount = files.Count;

		foreach (DirectoryVR d in subDirs) {
			d.setCounts (level + 1);
			totalLength += d.totalLength;
			dirCount += d.dirCount;
			fileCount += d.fileCount;
		}

		foreach (FileVR f in files) {
			length += f.fi.Length;
			totalLength += f.fi.Length;
		}
	}

	public void getMax(ref float maxDepth, ref float maxDirHeight, ref float maxFileHeight, ref float maxLevel) {
		if(radius + depth > maxDepth)
			maxDepth = radius+depth;
		if(length > maxDirHeight)
			maxDirHeight = length;
		if(level > maxLevel)
			maxLevel = level;

		foreach (FileVR f in files) {
			if(f.fi.Length > maxFileHeight)
				maxFileHeight = f.fi.Length;
		}

		foreach (DirectoryVR d in subDirs) {
			d.getMax(ref maxDepth, ref maxDirHeight, ref maxFileHeight, ref maxLevel);
		}
	}

	public void setSunBurstCoord(float startDegree, float endDegree, float startFrontDegree, float endFrontDegree, float radius, float fileSize, float folderSize) {
		this.startDegree = startDegree;
		this.endDegree = endDegree;
		this.startFrontDegree = startFrontDegree;
		this.endFrontDegree = endFrontDegree;
		this.radius = radius;
		this.fileSize = fileSize;

		///////////////////////////////////////////
		// Layout Files. Also set depth and startBackDegree/endBackDegree at the same time.
		///////////////////////////////////////////

		float nextRadius = radius + fileSize/4; //start first file fileSize/4 further from center.
		this.depth = nextRadius - radius + fileSize/4;

		float BufferDistance = (startFrontDegree - startDegree) * Mathf.Deg2Rad * radius;
		float BufferAngle = 1 / (radius + depth) * Mathf.Rad2Deg * BufferDistance;
		this.startBackDegree = startDegree + BufferAngle;

		BufferDistance = (endDegree - endFrontDegree) * Mathf.Deg2Rad * radius;
		BufferAngle = 1 / (radius + depth) * Mathf.Rad2Deg * BufferDistance;
		this.endBackDegree = endDegree - BufferAngle;

		if (radius == 0) //speial case for very first file in center of everything.
			nextRadius = 0;
		float angleBetweenFiles = getAngleBetweenFiles(nextRadius);
		int filesRemaining = files.Count;
		float nextDegree = getNextDegree (this.startBackDegree, this.endBackDegree, angleBetweenFiles, filesRemaining);

		if(2 * angleBetweenFiles > endDegree - startDegree) { //if even one file will not fit with buffer then put nextDegree in the exact center

		} else if ((filesRemaining + 1) * angleBetweenFiles <= endDegree - startDegree) { //if all remaining files will fit with buffer then increase the buffer size so they are centered in row
			
		}

		foreach (FileVR f in files) {
			if ( ( !(2 * angleBetweenFiles > this.endBackDegree - this.startBackDegree ) && nextDegree > this.endBackDegree - angleBetweenFiles/2 ) || // if width is greater than one file and nextdegree is greater than end - some buffer space
				( (2 * angleBetweenFiles > this.endBackDegree - this.startBackDegree ) && nextDegree > this.endBackDegree ) ) { // if width is less than one file and nextdegree is past end completely
				nextRadius += fileSize*0.7f;
				angleBetweenFiles = getAngleBetweenFiles(nextRadius);
				nextDegree = getNextDegree (this.startBackDegree, this.endBackDegree, angleBetweenFiles, filesRemaining);

				this.depth = nextRadius - radius + fileSize/4;

				BufferDistance = (startFrontDegree - startDegree) * Mathf.Deg2Rad * radius;
				BufferAngle = 1 / (radius + depth) * Mathf.Rad2Deg * BufferDistance;
				this.startBackDegree = startDegree + BufferAngle;

				BufferDistance = (endDegree - endFrontDegree) * Mathf.Deg2Rad * radius;
				BufferAngle = 1 / (radius + depth) * Mathf.Rad2Deg * BufferDistance;
				this.endBackDegree = endDegree - BufferAngle;
			}

			f.x = nextRadius * Mathf.Cos (nextDegree * Mathf.Deg2Rad);
			f.z = nextRadius * Mathf.Sin (nextDegree * Mathf.Deg2Rad);
			f.angle = nextDegree;
			f.radius = nextRadius;

			nextDegree += angleBetweenFiles;
			filesRemaining--;
		}

		///////////////////////////////////////////
		//compute sub dirs
		///////////////////////////////////////////

		int subDirCount = 0; //count of all subdirs, directly below this dir, that are not empty and subdirs are not emty
		for (int i = 0; i < subDirs.Count; i++) {
			if (subDirs [i].fileCount != 0) {
				subDirCount++;
			}
		}

		float endDirDegree;
		if (endDegree - startDegree == 360) { //if this dir is a full circle then we need to buffer the end so the first subdir and last subdir do not touch.
			BufferAngle = 1 / (radius + depth + folderSize / 4) * Mathf.Rad2Deg * folderSize / 8;
			nextDegree = startDegree += BufferAngle;
			endDirDegree = endDegree -= BufferAngle;
		} else {
			BufferDistance = (this.startBackDegree - startDegree) * Mathf.Deg2Rad * (radius + depth);
			BufferAngle = 1 / (radius + depth + folderSize / 4) * Mathf.Rad2Deg * BufferDistance;
			nextDegree = startDegree + BufferAngle;

			BufferDistance = (endDegree - this.endBackDegree) * Mathf.Deg2Rad * (radius + depth);
			BufferAngle = 1 / (radius + depth + folderSize / 4) * Mathf.Rad2Deg * BufferDistance;
			endDirDegree = endDegree - BufferAngle;
		}

		float totalDegreeWidth = endDirDegree - nextDegree;

		BufferAngle = totalDegreeWidth * 0.4f / (subDirCount - 1);
		if (BufferAngle * Mathf.Deg2Rad * (radius + depth + folderSize/4) > folderSize / 2)
			BufferAngle = 1 / (radius + depth + folderSize/4) * Mathf.Rad2Deg * folderSize / 2;
		totalDegreeWidth -= BufferAngle * (subDirCount - 1); //subtrack buffer space from availble space.

		for (int i = 0; i < subDirs.Count; i++) {
			if (subDirs [i].fileCount != 0) {
				float DegreeWidth = totalDegreeWidth * subDirs [i].fileCount / (fileCount - files.Count);

				subDirs [i].setSunBurstCoord (
					i == 0 ? startDegree : nextDegree - BufferAngle / 2,
					subDirCount-- == 1 ? endDegree : nextDegree + DegreeWidth + BufferAngle / 2,
					nextDegree, 
					nextDegree + DegreeWidth, 
					(radius + depth + folderSize/4),
					fileSize,
					folderSize
					);
				nextDegree += DegreeWidth + BufferAngle;
			}
		}
	}

	private float getAngleBetweenFiles(float radius) {
		float angleBetweenFiles = 360;
		if(radius > 0)
			angleBetweenFiles = 1 / radius * Mathf.Rad2Deg * fileSize*0.7f;
		return angleBetweenFiles;
	}

	private float getNextDegree(float startDegree, float endDegree, float angleBetweenFiles, int filesRemaining) {
		float nextDegree;

		if(2 * angleBetweenFiles > endDegree - startDegree) { //if even one file will not fit with buffer then put nextDegree in the exact center
			nextDegree = startDegree + (endDegree - startDegree) / 2; 
		} else if ((filesRemaining + 1) * angleBetweenFiles <= endDegree - startDegree) { //if all remaining files will fit with buffer then increase the buffer size so they are centered in row
			nextDegree = startDegree + (endDegree - startDegree)/2 - (filesRemaining-1)*angleBetweenFiles/2;
		} else { //start a new full row with a normal buffer size
			int filesInArc = Mathf.FloorToInt ((endDegree - startDegree) / angleBetweenFiles);
			nextDegree = startDegree + (endDegree - startDegree)/2 - (filesInArc-1)*angleBetweenFiles/2;
		}

		return nextDegree;
	}

	public bool find(float x, float z, float angle, float radius, out DirectoryVR dir, out FileVR file, int MaxLevel) {
		dir = null;
		file = null;

		if (level > MaxLevel)
			return false;

		if (startDegree <= angle && angle <= endDegree) { //if this is the correct direcotry subtree
			if (this.radius <= radius && radius <= this.radius + depth) { //if this is the correct directory
				dir = this;
				foreach (FileVR f in files) {
					float distance = Mathf.Sqrt (
						Mathf.Pow (x - f.x, 2) +
						Mathf.Pow (z - f.z, 2)
					);
					if (distance <= fileSize / 2) { //if this is the correct file
						file = f;
						break;
					}
				}
				return true;
			} else { //search subdirs
				foreach (DirectoryVR d in subDirs) {
					if (d.find (x, z, angle, radius, out dir, out file, MaxLevel)) {
						return true;
					}
				}
			}
		}
		return false;
	}
}
