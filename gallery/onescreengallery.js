//////////////////////////////////////////////////////
// GLOBALS
//////////////////////////////////////////////////////

var tileSize = 256; //tile image size in pixels
var layout; //image layout downloaded from server. [{x,y,width,height,mag},...]
var bounds; //min and max of coords in tile coords. Determined from layout
var maxMag;

//current tile X,Y coords of center of screen
var centerX; 
var centerY;

//no drawing will be done until rootTile and bg are true;
var rootTile = false; //top most tile
var bg = false; //background Image

var currentMag; //current display magnification of screen
var powerMag; //closet power of 2 to currentMag;
var currentTileSize; //current tile display size in pixels. Determined by on tileSize and currentMag

var canvas; //html canvas element
var context; //2d context element of canvas

var play = false; //true is slidshow is playing.

//Used by Mouse and touch events
var mouseIsDown = false;
var zoomIsDown = false;
var mouseDownX;
var mouseDownY;
var pinchStartDistance = false;
var pinchStartMag;
var pinchCenterX;
var pinchCenterY;

//////////////////////////////////////////////////////
// UTILS
//////////////////////////////////////////////////////
function boundingBox(layout) {
	bounds = {
		minx: 0,
		miny: 0,
		maxx: 0,
		maxy:0,
		minmag: 999999
	};

	for(var i=0; i < layout.length; i++) {
		if(layout[i].x < bounds.minx)
			bounds.minx = layout[i].x;

		if(layout[i].y < bounds.miny)
			bounds.miny = layout[i].y;

		if(layout[i].x + layout[i].width > bounds.maxx)
			bounds.maxx = layout[i].x + layout[i].width;

		if(layout[i].y + layout[i].height > bounds.maxy)
			bounds.maxy = layout[i].y + layout[i].height;

		if(layout[i].mag < bounds.minmag)
			bounds.minmag = layout[i].mag;
	}
	return bounds;
}

function fitMag(tWidth,tHeight) {
	//determine min mag so width and height bounds will fit on screen.
	/*
	var mag = 1;
	while(canvas.width*mag  < tWidth ||
	      canvas.height*mag < tHeight )
		mag *= 2;
	*/
	var mag = Math.max(
		tWidth/(canvas.width*0.95),
		tHeight/(canvas.height*0.95)
		);
	return checkMag(mag);
}

function checkMag(mag) {
	if(mag < 1) 
		mag = 1;

	if(mag > maxMag) 
		mag = maxMag;

	return mag;
}

function checkCenter() {
	if(centerX < bounds.minx) 
		centerX = bounds.minx;

	if(centerX > bounds.maxx) 
		centerX = bounds.maxx;

	if(centerY < bounds.miny) 
		centerY = bounds.miny;

	if(centerY > bounds.maxy) 
		centerY = bounds.maxy;
}

function alignTile(v,mag) {
	mag = typeof mag !== 'undefined' ? mag : powerMag;
	//although layout should be positive values, background tiles will be in negative positions.
	if(v >= 0)
		v = v - v % (tileSize*mag);
	else
		v = v - tileSize*mag - v % (tileSize*mag); //note, -10 % 5 will give a negative number
	return(v);
}

function scrToTileX(x,mag,cx) {
	mag = typeof mag !== 'undefined' ? mag : currentMag;
	cx = typeof cx !== 'undefined' ? cx : centerX;
	return(Math.round((x-canvas.width/2)*mag + cx));
}

function scrToTileY(y,mag,cy) {
	mag = typeof mag !== 'undefined' ? mag : currentMag;
	cy = typeof cy !== 'undefined' ? cy : centerY;
	return(Math.round((y-canvas.height/2)*mag + cy));
}

function tileToScrX(x,mag) {
	mag = typeof mag !== 'undefined' ? mag : currentMag;
	return(Math.round((x - centerX)/mag + canvas.width/2));
}

function tileToScrY(y,mag) {
	mag = typeof mag !== 'undefined' ? mag : currentMag;
	return(Math.round((y - centerY)/mag + canvas.height/2));
}

function resizeCanvas() {
	context.canvas.width  = window.innerWidth;
  	context.canvas.height = window.innerHeight;
  	drawCanvas();
}

//////////////////////////////////////////////////////
// Tile Object
//////////////////////////////////////////////////////

function Tile(x, y, mag, layout, parent) {
	align = typeof align !== 'undefined' ? align : true;
	//top left corner of tile and it's mag
	this.x = alignTile(x,mag);
	this.y  = alignTile(y,mag);
	this.mag = mag;
	this.parent = parent;
	this.minmag = 999999;

	//image data for this tile
	this.imageURL = "tiles/" + this.mag + "/" + this.x%10  + this.y%10 + "/" + this.x + "_" + this.y + ".jpg";
	this.imageData = false;
	this.imageLoaded = false; //true once this.imageData contains fully downloaded Image.

	//stored data for rendering
	this.drawMag = false;
	this.drawCenterX = false;
	this.drawCenterY = false;
	drawQ = []; //note the Q is required for when we are waiting to download an image and we need to Q up multiple zoomed in parts of the tile that need rendering.

	//states for each subTile: 1=not evaluated, 2=evaluated and no data, else Tile()
	this.subTile = [
		1, //topLeft
		1, //topRight
		1, //bottomLeft
		1 //bottomRight
	];

	this.layout = [];				
	//copy images in layout that overlap this tile into this.layout[]
	for(var i=0; i < layout.length; i++) {
		if(layout[i].mag <= this.mag) {
			if( (
					(this.x <= layout[i].x && layout[i].x < this.x+(tileSize*this.mag)) || 
					(layout[i].x < this.x && this.x < layout[i].x+layout[i].width )
				) && (
					(this.y <= layout[i].y && layout[i].y < this.y+(tileSize*this.mag)) || 
					(layout[i].y < this.y && this.y < layout[i].y+layout[i].height)) 
				) {
				this.layout.push(layout[i]);
				if(layout[i].mag < this.minmag)
					this.minmag = layout[i].mag;
			}
		}
	}

	//=== Public Methods ==========================================

	this.bestTile = function(x, y, searchMag) {
		searchMag = typeof searchMag !== 'undefined' ? searchMag : powerMag;

		if( this.layout.length  == 0 || //possible rootTile has this.layout.length == 0
			!(this.x <= x && x < this.x+this.mag*tileSize) || 
			!(this.y <= y && y < this.y+this.mag*tileSize) ) {
			return false;
		}
		
		//tile is best match possible if mag == this.mag
		//tile is already better (more detail) than requested if mag > this.mag
		if(searchMag >= this.mag)
			return this;
		
		//use subTile if available
		var s = this.getSubTile(this.getSubTileIndex(x,y));
		if(s instanceof Tile) {
			return s.bestTile(x, y, searchMag);
		}

		//this tile does not have the mag (detail) requested but is the best one in existence.
		return this;
	}

	this.draw = function (x, y, download) {
		//x,y are assumed to be aligned with currentMag.
		download = typeof download !== 'undefined' ? download : true;

		if(this.drawMag != currentMag || this.drawCenterX != centerX || this.drawCenterY != centerY) {
			//the current drawing has moved so get rid of all previous draw requests that we have not drawn yet.
			this.drawMag = currentMag;
			this.drawCenterX = centerX;
			this.drawCenterY = centerY;
			this.drawQ = []; //display have moved so empty the drawQ
		}
		
		if(download) //if we are willing to download tile then we will draw it at some point.
			this.drawQ.push({x: x, y: y});

		if(!(this.imageData instanceof Image)) { //if we don't have image data for this tile.
			if(download)
				this.downloadImage(); //download and then do a delayed render of it.
	    	if(this.parent)
	    		this.parent.draw(x,y,false); //until this image downloads try to render available parent data.
	    	else
	    		drawBG(x,y); //otherwise all we can do is render the background until image loads.
		} else if(this.imageLoaded) {//else if image is fully loaded then render it now
			if(!download)
				this.drawQ.push({x: x, y: y});
			this.render();
		} else if(this.parent)
	    	this.parent.draw(x,y,false); //until this image downloads try to render available parent data.
	    else
	    	drawBG(x,y); //otherwise all we can do is render the background until image loads.
	}

	this.preload = function(depth) {
		if(!(this.imageData instanceof Image)) {
				this.downloadImage();
		}
		if(depth) {
			for(var i = 0; i < 4; i++) {
				var s = this.getSubTile(i);
				if(s instanceof Tile) {
					return s.preload(--depth);
				}
			}
		}
	}

	//=== Private Methods ==========================================

	this.getSubTile = function(subIndex) {
		//if we have never evaluated this tile
		if(this.subTile[subIndex] == 1) {
			var x = this.x;
			var y = this.y;

			if(subIndex == 1 || subIndex == 3)
				x += tileSize*this.mag/2;
			if(subIndex == 2 || subIndex == 3)
				y += tileSize*this.mag/2;

			this.subTile[subIndex] = new Tile(x,y,this.mag/2,this.layout,this);
			
			//if tile has no data then mark it as such
			if(this.subTile[subIndex].layout.length == 0)
				this.subTile[subIndex] = 2;
		}
		return(this.subTile[subIndex]);
	}

	this.getSubTileIndex = function(x, y) {
		//return index 0,1,2,3 of which subTile x,y falls in at this.mag/2
		//assume x,y is inside this tile
		x = alignTile(x,this.mag/2);
		y  = alignTile(y,this.mag/2);
		if(x == this.x && y == this.y)
			return 0; //top left
		else if(x != this.x && y == this.y)
			return 1; //top right
		else if(x == this.x && y != this.y)
			return 2; //bottom left
		else
			return 3; //bottom right
	}

	this.downloadImage = function() {
		this.imageData = new Image();
		var t = this;
	    this.imageData.onload = function () {
	    	t.imageLoaded = true;
	    	t.render();
	    	drawToolBar(); //delayed render so refresh the toolbar in case we just drew over it.
	    };
	    this.imageData.onerror = function() {
	    	t.imageData = false;
	    	console.log("Failed to download " + t.imageURL);
	    };
	    this.imageData.src = this.imageURL;
	}

	this.render = function() {
		//if we have not moved or zoomed
		if(this.drawMag == currentMag && this.drawCenterX == centerX && this.drawCenterY == centerY) {
			for(var i=0; i < this.drawQ.length; i++) {
				drawImage(this.imageData,
				    Math.round((this.drawQ[i].x - this.x)/this.mag), //image x
					Math.round((this.drawQ[i].y - this.y)/this.mag), //image y
					tileSize*powerMag/this.mag, //image width
					tileSize*powerMag/this.mag, //image height
					this.drawQ[i].x, //dst_tile_x
					this.drawQ[i].y //dst_tile_y
				);
			}
			this.drawQ = [];
		}
	}
}

//////////////////////////////////////////////////////
// TOOL BAR
//////////////////////////////////////////////////////
function drawToolBar() {
	var tbHeight = toolBarHeight();

	context.strokeStyle="rgb(0, 0, 0)";

	if(!play) {
		//minus button
		context.beginPath();
		context.moveTo(tbHeight*2+tbHeight/4+4,canvas.height-tbHeight/2);
		context.lineTo(tbHeight*2+tbHeight/4+tbHeight/2-4,canvas.height-tbHeight/2);
		context.stroke();
		context.closePath();

		context.beginPath();
		context.arc(tbHeight*2+tbHeight/2,canvas.height-tbHeight/2,tbHeight/4,0,2*Math.PI);
		context.stroke();
		context.closePath();

		//plus button
		context.beginPath();
		context.moveTo(canvas.width-tbHeight*2-tbHeight+tbHeight/4+4,canvas.height-tbHeight/2);
		context.lineTo(canvas.width-tbHeight*2-tbHeight/4-4,canvas.height-tbHeight/2);
		context.stroke();
		context.closePath();

		context.beginPath();
		context.moveTo(canvas.width-tbHeight*2-tbHeight/2,canvas.height-tbHeight/2-tbHeight/4+4);
		context.lineTo(canvas.width-tbHeight*2-tbHeight/2,canvas.height-tbHeight/2+tbHeight/4-4);
		context.stroke();
		context.closePath();

		context.beginPath();
		context.arc(canvas.width-tbHeight*2-tbHeight/2,canvas.height-tbHeight/2,tbHeight/4,0,2*Math.PI);
		context.stroke();
		context.closePath();

		if(canvas.width > tbHeight*10) {
			//slider bar
			var zoomTickX = getSliderXFromMag(currentMag);

			if(tbHeight*4 < zoomTickX-tbHeight/4) {
				context.beginPath();
				context.moveTo(tbHeight*4,canvas.height-tbHeight/2);
				context.lineTo(zoomTickX-tbHeight/4,canvas.height-tbHeight/2);
				context.stroke();
				context.closePath();
			}

			if(zoomTickX+tbHeight/4 < canvas.width-tbHeight*4) {
				context.beginPath();
				context.moveTo(canvas.width-tbHeight*4,canvas.height-tbHeight/2);
				context.lineTo(zoomTickX+tbHeight/4,canvas.height-tbHeight/2);
				context.stroke();
				context.closePath();
			}

			context.beginPath();
			context.arc(zoomTickX,canvas.height-tbHeight/2,tbHeight/4,0,2*Math.PI);
			context.stroke();
			context.closePath();
		}

		//play button
		context.beginPath();
		context.moveTo(tbHeight/4,canvas.height-tbHeight + tbHeight/4);
		context.lineTo(tbHeight/4,canvas.height-tbHeight/4);
		context.lineTo(tbHeight-tbHeight/4,canvas.height-tbHeight/2);
		context.lineTo(tbHeight/4,canvas.height-tbHeight + tbHeight/4);
		context.stroke();
		context.closePath();

		//info button
		context.beginPath();
		context.moveTo(canvas.width-tbHeight/2,canvas.height-tbHeight + tbHeight/2);
		context.lineTo(canvas.width-tbHeight/2,canvas.height-tbHeight/4);
		context.stroke();
		context.closePath();

		context.beginPath();
		context.arc(canvas.width-tbHeight/2,canvas.height-tbHeight+tbHeight/3,tbHeight/12,0,2*Math.PI);
		context.stroke();
		context.closePath();

	} else {
		//pause button
		context.beginPath();
		context.moveTo(tbHeight/4,canvas.height-tbHeight + tbHeight/4);
		context.lineTo(tbHeight/4,canvas.height-tbHeight/4);
		context.stroke();
		context.closePath();

		context.beginPath();
		context.moveTo(tbHeight/4+8,canvas.height-tbHeight + tbHeight/4);
		context.lineTo(tbHeight/4+8,canvas.height-tbHeight/4);
		context.stroke();
		context.closePath();
	}

}

function toolBarHeight() {
	return Math.min(canvas.height*0.9, 40);
}

function getMagFromMouseX(screenX) {
	var sliderwidth = canvas.width - toolBarHeight()*8;
	return Math.pow(2,Math.log2(maxMag) * ((sliderwidth - (screenX - toolBarHeight()*4))/sliderwidth));
}

function getSliderXFromMag(mag) {
	var sliderwidth = canvas.width - toolBarHeight()*8;
	return canvas.width - toolBarHeight()*4 - Math.log2(currentMag)/Math.log2(maxMag) * sliderwidth;
}

//////////////////////////////////////////////////////
// DRAWING
//////////////////////////////////////////////////////
function drawImage(image,ix,iy,iwidth,iheight,tx,ty) {
	context.drawImage(image, 
		ix, //src_x
		iy, //src_y
		iwidth, //src_w
		iheight, //src_h
		tileToScrX(tx), //dst_x
		tileToScrY(ty), //dst_y
		currentTileSize, //dst_w
		currentTileSize //dst_h
	);
}

function drawBG(tx,ty) {
	drawImage(bg,
	    0, //image x
		0, //image y
		tileSize, //image width
		tileSize, //image height
		tx, //dst_tile_x
		ty //dst_tile_y
	);
}

function drawImageBorders() {
	for(var i=0; i < layout.length; i++) {
		context.beginPath();
		context.rect(
			tileToScrX(layout[i].x),
			tileToScrY(layout[i].y),
			Math.ceil(layout[i].width/currentMag),
			Math.ceil(layout[i].height/currentMag));
		context.stroke();
		context.closePath();
	}
}

function drawTileBorders() {
	var twidth = scrToTileX(canvas.width+currentTileSize);
	var theight = scrToTileY(canvas.height+currentTileSize);
	for(var tx=alignTile(scrToTileX(0)); tx<twidth; tx+=tileSize*powerMag) {
		for(var ty=alignTile(scrToTileY(0)); ty<theight; ty+=tileSize*powerMag) {
			context.beginPath();
			context.rect(
				tileToScrX(tx), //dst_x
				tileToScrY(ty), //dst_y
				currentTileSize,
				currentTileSize);
			context.stroke();
			context.closePath();
		}
	}
}

function drawCanvas(download) {
	download = typeof download !== 'undefined' ? download : true;

	if(!rootTile || !bg)
		return;

	//clear canvas for debugging
	//context.clearRect(0, 0, canvas.width, canvas.height);

	currentMag = checkMag(currentMag);
	powerMag = Math.pow(2,Math.round(Math.log2(currentMag)));
	currentTileSize = Math.ceil(powerMag/currentMag * tileSize);

	var twidth = scrToTileX(canvas.width+currentTileSize);
	var theight = scrToTileY(canvas.height+currentTileSize);
	for(var tx=alignTile(scrToTileX(0)); tx<twidth; tx+=tileSize*powerMag) {
		for(var ty=alignTile(scrToTileY(0)); ty<theight; ty+=tileSize*powerMag) {
			var t = rootTile.bestTile(tx,ty);
			if(t)
				t.draw(tx,ty,download);
			else
				drawBG(tx,ty);
		}
	}

	//overlay image and tile borders for debugging.
	//drawImageBorders();
	//drawTileBorders();

	drawToolBar();
}

//////////////////////////////////////////////////////
// SLIDE SHOW ANIMATION
//////////////////////////////////////////////////////
var startTime = false;
var startX;
var startY;
var startMag;
var endTime;
var endX;
var endY;
var endMag;
var nextImage = 0;
var fadeImage;
var moveTime = 4000; //4 sec
var fadeTime = 3000; //3 sec
var holdTime = 10000; //10 sec

function easeInOutExpo(currentIteration, startValue, changeInValue, totalIterations) {
	if ((currentIteration /= totalIterations / 2) < 1) {
		return changeInValue / 2 * Math.pow(2, 10 * (currentIteration - 1)) + startValue;
	}
	return changeInValue / 2 * (-Math.pow(2, -10 * --currentIteration) + 2) + startValue;
}

function animateStep(timestamp) {
	var easing = easeInOutExpo;

	if(!play)
		return;
	
	if(!startTime) {
		startTime = timestamp;
		endTime = timestamp + moveTime;
		window.requestAnimationFrame(animateStep);
	} else {
		var currentIteration=timestamp-startTime;
		var totalIterations=endTime-startTime;
		centerX = Math.round(easing(currentIteration, startX, endX-startX, totalIterations));
		centerY = Math.round(easing(currentIteration, startY, endY-startY, totalIterations));
		currentMag = easing(currentIteration, startMag, endMag-startMag, totalIterations);
		
		if(timestamp < endTime) {
			drawCanvas(false); //do not allow downloading
			window.requestAnimationFrame(animateStep);
		} else {
			drawCanvas(); //allow downloading

			//fade down border, wait, fade up border, go to next image.
			fadeImage=nextImage;
			startTime = false;
			window.requestAnimationFrame(fadeDown);

			//get ready for next Image
			nextImage = (nextImage + 1) % layout.length;
			setNextImage();
		}
	}
}

function drawFade(timestamp) {
	drawCanvas();

	var easing = easeInOutExpo;
	var currentIteration=timestamp-startTime;
	var totalIterations=endTime-startTime;
	context.fillStyle = "rgba(50, 50, 50, "+
		easing(currentIteration, 0, 0.95, totalIterations)
		+")";

	context.fillRect(
		0,
		0,
		tileToScrX(centerX-layout[fadeImage].width/2)+1,
		canvas.height); //left

	context.fillRect(
		tileToScrX(centerX+layout[fadeImage].width/2)-1,
		0,
		canvas.width,
		canvas.height); //right

	context.fillRect(
		tileToScrX(centerX-layout[fadeImage].width/2)+1,
		0,
		tileToScrX(centerX+layout[fadeImage].width/2)-1,
		tileToScrY(centerY-layout[fadeImage].height/2)+1); //top

	context.fillRect(
		tileToScrX(centerX-layout[fadeImage].width/2)+1,
		tileToScrY(centerY+layout[fadeImage].height/2)-1,
		tileToScrX(centerX+layout[fadeImage].width/2)-1,
		canvas.height); //bottom

	context.fillStyle = "#000000";
}

function fadeDown(timestamp) {
	if(!play)
		return;

	if(!startTime) {
		startTime = timestamp;
		endTime = timestamp + fadeTime;
	}

	if(timestamp < endTime) {
		drawFade(timestamp);
		window.requestAnimationFrame(fadeDown);
	} else {
		startTime = false;
		setTimeout(function() {
			window.requestAnimationFrame(fadeUp);
		},holdTime);
	}
}

function fadeUp(timestamp) {
	if(!play)
		return;

	if(!startTime) {
		startTime = timestamp;
		endTime = timestamp + fadeTime;
	}

	if(timestamp < endTime) {
		drawFade(endTime-(timestamp-startTime));
		window.requestAnimationFrame(fadeUp);
	} else {
		startTime = false;
		gotoNextImage();
	}
}

function setNextImage() {
	endX = Math.round(layout[nextImage].x + layout[nextImage].width/2);
	endY = Math.round(layout[nextImage].y + layout[nextImage].height/2);
	endMag  = checkMag(fitMag(layout[nextImage].width,layout[nextImage].height));

	//override globals with local vars to keep readability.
	var powerMag = Math.pow(2,Math.round(Math.log2(endMag)));
	var currentTileSize = Math.ceil(powerMag/endMag * tileSize);

	var twidth = scrToTileX(canvas.width+currentTileSize,endMag,endX);
	var theight = scrToTileY(canvas.height+currentTileSize,endMag,endY);
	for(var tx=alignTile(scrToTileX(0,endMag,endX),powerMag); tx<twidth; tx+=tileSize*powerMag) {
		for(var ty=alignTile(scrToTileY(0,endMag,endY),powerMag); ty<theight; ty+=tileSize*powerMag) {
			var t = rootTile.bestTile(tx,ty,powerMag);
			if(t)
				t.preload(tx,ty,0);
		}
	}
}

function gotoNextImage() {
	if(!play)
		return;

	startX = centerX;
	startY = centerY;
	startMag = currentMag;

	startTime = false;
	window.requestAnimationFrame(animateStep);
}

//////////////////////////////////////////////////////
// EVENT HANDLING
//////////////////////////////////////////////////////

function zoomIn() {
	currentMag /= 2;
	//land on power of 2 mag
	currentMag = Math.pow(2,Math.round(Math.log2(currentMag)));
	drawCanvas();
}

function zoomOut() {
	currentMag *= 2;
	//land on power of 2 mag
	currentMag = Math.pow(2,Math.round(Math.log2(currentMag)));
	drawCanvas();
}

function mouseDown(e) { //mouse or touch down
	e.preventDefault();

	if(play) {
		play = false;
		drawCanvas();
		return;
	}

	if($('#modal').is(':visible')) {
		modal.close();
		return;
	}

	if(e.target = canvas) {
		if(e instanceof MouseEvent) {
			mouseDownX = e.offsetX;
			mouseDownY = e.offsetY;
		} else { //e instanceof TouchEvent
			mouseDownX = e.touches[0].clientX;
			mouseDownY = e.touches[0].clientY;
		}
		
		if(mouseDownY>canvas.height-toolBarHeight()) {
			if(mouseDownX < toolBarHeight()) {
				play = !play;
				if(play) {
					setNextImage();
					gotoNextImage();
				} else {
					drawCanvas();
				}
			} else if(toolBarHeight()*2 < mouseDownX && mouseDownX < toolBarHeight()*3 && !play) {
				zoomOut();
			} else if(toolBarHeight()*4 < mouseDownX && mouseDownX < canvas.width-toolBarHeight()*4 && canvas.width > toolBarHeight()*10 && !play) {
				zoomIsDown = true;
				currentMag = getMagFromMouseX(mouseDownX);
				drawCanvas();
			} else if(canvas.width-toolBarHeight()*3 < mouseDownX && mouseDownX < canvas.width-toolBarHeight()*2 && !play) {
				zoomIn();
			} else if(canvas.width-toolBarHeight() < mouseDownX && !play) {
				//info button
				modal.open({content: $('#infoContent').html()});
			}
		} else {
			mouseIsDown = true;
			mouseDownX = scrToTileX(mouseDownX);
			mouseDownY = scrToTileY(mouseDownY);
		}
	}
}

function mouseUp(e) { //mouse or touch up
	e.preventDefault();

	if(mouseIsDown && e instanceof TouchEvent && e.touches.length > 0) { //if we just lifted one finger but another finger is still down
		mouseDownX = scrToTileX(e.touches[0].clientX);
		mouseDownY = scrToTileY(e.touches[0].clientY);
	} else {
		mouseIsDown = false;
		zoomIsDown = false;
	}
	pinchStartDistance = false;
}

function mouseMove(e) { //mouse move
	e.preventDefault();

	if(mouseIsDown) {
        centerX = centerX - (scrToTileX(e.offsetX) - mouseDownX);
		centerY = centerY - (scrToTileY(e.offsetY) - mouseDownY);
		checkCenter();

		mouseDownX = scrToTileX(e.offsetX);
		mouseDownY = scrToTileY(e.offsetY);
		drawCanvas();
    } else if(zoomIsDown) {
    	currentMag = getMagFromMouseX(e.offsetX);
		drawCanvas();
    }
}

function touchMove(e) { //touch move
	e.preventDefault();

	if(zoomIsDown) {
		currentMag = getMagFromMouseX(e.touches[0].clientX);
	} else if(mouseIsDown) {
		if(e.targetTouches.length == 1) {
	    	centerX = centerX - (scrToTileX(e.touches[0].clientX) - mouseDownX);
			centerY = centerY - (scrToTileY(e.touches[0].clientY) - mouseDownY);
			checkCenter();

			mouseDownX = scrToTileX(e.touches[0].clientX);
			mouseDownY = scrToTileY(e.touches[0].clientY);
		} else if(e.targetTouches.length == 2) { //pinch
			if(!pinchStartDistance) {
				pinchStartDistance = Math.sqrt(
										Math.pow(e.touches[0].clientX - e.touches[1].clientX,2) +
										Math.pow(e.touches[0].clientY - e.touches[1].clientY,2) ); 
				pinchStartMag = currentMag;
				pinchCenterX = scrToTileX(e.touches[0].clientX + (e.touches[1].clientX-e.touches[0].clientX)/2);
				pinchCenterY = scrToTileY(e.touches[0].clientY + (e.touches[1].clientY-e.touches[0].clientY)/2);
			} else {
				var pinchDistance = Math.sqrt(
										Math.pow(e.touches[0].clientX - e.touches[1].clientX,2) +
										Math.pow(e.touches[0].clientY - e.touches[1].clientY,2) );
				currentMag = checkMag(pinchStartMag * pinchStartDistance/pinchDistance);

				centerX = centerX + (pinchCenterX - scrToTileX(e.touches[0].clientX + (e.touches[1].clientX-e.touches[0].clientX)/2));
				centerY = centerY + (pinchCenterY - scrToTileY(e.touches[0].clientY + (e.touches[1].clientY-e.touches[0].clientY)/2));
				checkCenter();
			}
	    }
	}
    drawCanvas();
}

function mouseWheel(e) {
<<<<<<< HEAD
	//get tile pos under mouse
	var mouseX = scrToTileX(e.clientX);
	var mouseY = scrToTileY(e.clientY);

	var delta = Math.max(-1, Math.min(1, (e.wheelDelta)));
	currentMag = checkMag(currentMag + currentMag*delta*0.1);

	//put same tile position back under mouse
	centerX += (mouseX - scrToTileX(e.clientX));
	centerY += (mouseY - scrToTileY(e.clientY));
	checkCenter();

=======
	var delta = Math.max(-1, Math.min(1, (e.wheelDelta)));
	currentMag = checkMag(currentMag + currentMag*delta*0.1);
>>>>>>> 2373864fbbd3773c66efa300f9df23895892d4d7
	drawCanvas();
	return false;
}

//////////////////////////////////////////////////////
// MODAL
//////////////////////////////////////////////////////
var modal = (function(){
	var 
	method = {},
	$overlay,
	$modal,
	$content,
	$close;

	// Center the modal in the viewport
	method.center = function () {
		var top, left;

		top = Math.max($(window).height() - $modal.outerHeight(), 0) / 2;
		left = Math.max($(window).width() - $modal.outerWidth(), 0) / 2;

		$modal.css({
			top:top + $(window).scrollTop(), 
			left:left + $(window).scrollLeft()
		});
	};

	// Open the modal
	method.open = function (settings) {
		$content.empty().append(settings.content);

		$modal.css({
			width: settings.width || 'auto', 
			height: settings.height || 'auto'
		});

		method.center();
		$(window).bind('resize.modal', method.center);
		$modal.show();
		$overlay.show();
	};

	// Close the modal
	method.close = function () {
		$modal.hide();
		$overlay.hide();
		$content.empty();
		$(window).unbind('resize.modal');
	};

	// Generate the HTML and add it to the document
	$overlay = $('<div id="overlay"></div>');
	$modal = $('<div id="modal"></div>');
	$content = $('<div id="content"></div>');
	$close = $('<a id="close" href="#">close</a>');

	$modal.hide();
	$overlay.hide();
	$modal.append($content, $close);

	$(document).ready(function(){
		$('body').append($overlay, $modal);						
	});

	$close.click(function(e){
		e.preventDefault();
		method.close();
	});

	return method;
}());

//////////////////////////////////////////////////////
// START UP
//////////////////////////////////////////////////////
$( document ).ready(function() {
	canvas = $('#canvas').get(0);
	context = canvas.getContext("2d");
	resizeCanvas();

	$.ajax({
		dataType: "json",
		url: "tiles/layout.json",
		success: function (data) {
	    	layout = data;

			//get bounding box
			bounds = boundingBox(layout);

			//find maxMag the same way tiler does so we know what the root tile is
			//find minimum mag where entire layout fits in one tile with top left corner at 0,0
			maxMag = 1;
			while( maxMag*tileSize < bounds.maxx ||
				   maxMag*tileSize < bounds.maxy )
				maxMag *= 2;
			//now make mag 4 times bigger in case we really want to zoom way way out.
			maxMag *= 4;

			//assume entire layout will fit in the tile at 0,0 with mag maxMag
			rootTile = new Tile(0, 0, maxMag, layout, null);

			//assume entire layout is pushed up agaist 0,0 and all x,y values are positive.
			centerX = Math.round(bounds.maxx/2);
			centerY = Math.round(bounds.maxy/2);

			//determine start currentMag so entire layout will fit on screen.
			currentMag = fitMag(bounds.maxx - bounds.minx,bounds.maxy - bounds.miny);

			//now set maxMag to get fit for user interface
			maxMag = currentMag*1.5;

			drawCanvas();
	    }
	});

	var bgTmp = new Image();
	bgTmp.onload = function () {
    	bg = this;
    	drawCanvas();
    };
    bgTmp.onerror = function() {
    	console.log("background failed to load");
    };
    bgTmp.src = "tiles/bg.jpg";

    window.addEventListener('resize',drawCanvas);
	window.addEventListener('mousedown',mouseDown);
	window.addEventListener('mouseup',mouseUp);
	window.addEventListener('mousemove',mouseMove);
	window.addEventListener('touchstart',mouseDown);
	window.addEventListener('touchend',mouseUp);
	window.addEventListener('touchmove',touchMove);
	window.addEventListener('resize',resizeCanvas);
	canvas.addEventListener("mousewheel", mouseWheel, false);

	modal.open({content: $('#infoContent').html()});
});