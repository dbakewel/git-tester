//////////////////////////////////////////////////////
// GLOBALS
//////////////////////////////////////////////////////

var layout = false; //image layout downloaded from server. [{x,y,width,height,mag},...]
var bounds; //min and max of coords in tile coords. Determined from layout

//current tile X,Y coords of center of screen
var centerX; 
var centerY;

var currentMag; //current display magnification of screen
var powerMag; //closet power of 2 to currentMag;

var canvas; //html canvas element
var context; //2d context element of canvas

//Used by Mouse and touch events
var cntrlIsPressed = false;
var mouseIsDown = false;
var zoomIsDown = false;
var mouseDownX;
var mouseDownY;
var pinchStartDistance = false;
var pinchStartMag;
var pinchCenterX;
var pinchCenterY;

//Selected image index
var selected = 0;
var multiselected = []; //other selected images.
var removeOnUp = -1; //multielect element to remove on mouse up.
//x,y of clicke point inside image while mouse is down.
var selectedIndex = -1;
var selectedX;
var selectedY;

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

function findImage(x,y,l) {
	l = typeof l !== 'undefined' ? l : layout;
	selectedIndex = -1;
	for(var i=l.length-1; i > -1 ; i--) {
		if(l[i].x <= x && x < l[i].x + l[i].width &&
		   l[i].y <= y && y < l[i].y + l[i].height) {
		   	selectedIndex = i;
		   	selectedX = x - l[i].x;
		   	selectedY = y - l[i].y;
			return i;
		}
	}
	return -1;
}

function sortLayout(l) {
	l = typeof l !== 'undefined' ? l : layout;

	//indexes are all about to change so reset selection
	selected = 0;
	multiselected = [];
	selectedIndex = 0;

	l.sort(function(a,b) {
		if(a.mag == b.mag) {
			if(a.x < b.x)
				return -1;
			else if(a.x > b.x)
				return 1;
			else if(a.y < b.y)
				return -1;
			else if(a.y > b.y)
				return 1;
			return 0;
		} else {
			return b.mag - a.mag;
		}
	});
}

function alignImage(i,l) {
	l = typeof l !== 'undefined' ? l : layout;
	for(var j=0; j < l.length; j++) {
		if(i != j) {
			//if i and j overlap
			if( ((l[j].x <= l[i].x && l[i].x < l[j].x + l[j].width) || (l[i].x < l[j].x && l[j].x < l[i].x + l[i].width)) &&
			    ((l[j].y <= l[i].y && l[i].y < l[j].y + l[j].height) || (l[i].y < l[j].y && l[j].y < l[i].y + l[i].height)) ) {
			   	//move i to closest border of j

			   var moveX = l[j].x+l[j].width - l[i].x;
			   if(Math.abs(l[i].x+l[i].width - l[j].x) < Math.abs(moveX))
			   		moveX = (l[i].x+l[i].width - l[j].x) * -1;

			   var moveY = (l[j].y+l[j].height - l[i].y) * -1;
			   if(Math.abs(l[i].y+l[i].height - l[j].y) < Math.abs(moveX))
			   		moveY = l[i].y+l[i].height - l[j].y;

			    if(Math.abs(moveX) < Math.abs(moveY))
			    	l[i].x = l[i].x + moveX;
			    else
			    	l[i].y = l[i].y - moveY;

				//aligned = true;
			}
		}
	}
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
		tWidth/(canvas.width*0.9),
		tHeight/(canvas.height*0.9)
		);
	return checkMag(mag);
}

function checkMag(mag) {
	if(mag < 1) 
		mag = 1;

	if(mag > 32768) 
		mag = 32768;

	return mag;
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
// TOOL BAR
//////////////////////////////////////////////////////
function drawToolBar() {
	var tbHeight = toolBarHeight();

	context.lineWidth = 1;
	context.strokeStyle="rgb(0, 0, 0)";

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

}

function toolBarHeight() {
	return Math.min(canvas.height*0.9, 40);
}

function getMagFromMouseX(screenX) {
	var sliderwidth = canvas.width - toolBarHeight()*8;
	return Math.pow(2,15 * ((sliderwidth - (screenX - toolBarHeight()*4))/sliderwidth));
}

function getSliderXFromMag(mag) {
	var sliderwidth = canvas.width - toolBarHeight()*8;
	return canvas.width - toolBarHeight()*4 - Math.log2(currentMag)/15 * sliderwidth;
}

//////////////////////////////////////////////////////
// DRAWING
//////////////////////////////////////////////////////
function drawImage(i) {
	context.drawImage(layout[i].imageData, 
		0, //src_x
		0, //src_y
		layout[i].imageData.width, //src_w
		layout[i].imageData.height, //src_h
		tileToScrX(layout[i].x), //dst_x
		tileToScrY(layout[i].y), //dst_y
		layout[i].width/currentMag, //dst_w
		layout[i].height/currentMag//dst_h
	);

	context.lineWidth = 3;
	if(selected == i) {
		context.strokeStyle="rgb(255, 0, 0)";
		context.beginPath();
		context.rect(
			tileToScrX(layout[i].x),
			tileToScrY(layout[i].y),
			Math.round(layout[i].width/currentMag),
			Math.round(layout[i].height/currentMag));
		context.stroke();
		context.closePath();
	} else if(multiselected.indexOf(i) != -1) {
		context.strokeStyle="rgb(0, 0, 255)";
		context.beginPath();
		context.rect(
			tileToScrX(layout[i].x),
			tileToScrY(layout[i].y),
			Math.round(layout[i].width/currentMag),
			Math.round(layout[i].height/currentMag));
		context.stroke();
		context.closePath();
	}
}

function drawImageBorders() {
	context.lineWidth = 1;
	context.strokeStyle="rgb(0, 0, 0)";
	for(var i=0; i < layout.length; i++) {
		context.beginPath();
		context.rect(
			tileToScrX(layout[i].x),
			tileToScrY(layout[i].y),
			Math.round(layout[i].width/currentMag),
			Math.round(layout[i].height/currentMag));
		context.stroke();
		context.closePath();
	}
}

function drawCanvas() {
	if(loading || !layout)
		return;

	context.clearRect(0, 0, canvas.width, canvas.height);

	currentMag = checkMag(currentMag);
	powerMag = Math.pow(2,Math.round(Math.log2(currentMag)));

	for(var i=0; i < layout.length; i++) {
		drawImage(i);
	}

	//overlay image and tile borders for debugging.
	//drawImageBorders();

	drawToolBar();
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

	if($('#modal').is(':visible')) {
		//modal.close();
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
				alignImage(selected);
				sortLayout();
			} else if(toolBarHeight()*2 < mouseDownX && mouseDownX < toolBarHeight()*3) {
				if(selected != -1) {
					layout[selected].mag /= 2;
					if(layout[selected].mag < 1)
						layout[selected].mag = 1;
					layout[selected].width = layout[selected].fileWidth * layout[selected].mag;
					layout[selected].height = layout[selected].fileHeight * layout[selected].mag;
				} else {
					zoomOut();
				}
			} else if(toolBarHeight()*4 < mouseDownX && mouseDownX < canvas.width-toolBarHeight()*4 && canvas.width > toolBarHeight()*10) {
				zoomIsDown = true;
				currentMag = getMagFromMouseX(mouseDownX);
				drawCanvas();
			} else if(canvas.width-toolBarHeight()*3 < mouseDownX && mouseDownX < canvas.width-toolBarHeight()*2) {
				if(selected != -1) {
					layout[selected].mag *= 2;
					if(layout[selected].mag > 32768)
						layout[selected].mag = 32768;
					layout[selected].width = layout[selected].fileWidth * layout[selected].mag;
					layout[selected].height = layout[selected].fileHeight * layout[selected].mag;
				} else {
					zoomIn();
				}
			} else if(canvas.width-toolBarHeight() < mouseDownX) {
				//info button
				modal.open({content: "<pre>"+JSON.stringify(layout,null,4)+"</pre>"});
				//console.log(JSON.stringify(layout,null,4));
			}
		} else {
			mouseIsDown = true;
			removeOnUp = -1;
			mouseDownX = scrToTileX(mouseDownX);
			mouseDownY = scrToTileY(mouseDownY);
			if(e instanceof MouseEvent) { //if mouse then do selection stuff
				if(cntrlIsPressed && selected != -1) { //if ctrl down and we already have a primary image selected.
					var i = findImage(mouseDownX,mouseDownY);
					if(i != -1) { //if user clicked on an image
						if(multiselected.indexOf(i) != -1) { //if we clicked on an image that is already in multiselect
							//remove image from multiselect on mouse up
							removeOnUp = multiselected.indexOf(i);
						} else if(i != selected) { //if we didn't click on the already selected image.
							multiselected.push(i);
						}
					} else { //start drag select
						//selectedIndex == -1 from findImage
					}
				} else { //clear selection to one image if we are clicking on an image.
					var i = findImage(mouseDownX,mouseDownY);
					if(i != -1) {
						selected = i;
						multiselected = [];
					}
				}
			}
		}
	}
	drawCanvas();
}

function mouseUp(e) { //mouse or touch up
	e.preventDefault();

	if(mouseIsDown && e instanceof TouchEvent && e.touches.length > 0) { //if we just lifted one finger but another finger is still down
		mouseDownX = scrToTileX(e.touches[0].clientX);
		mouseDownY = scrToTileY(e.touches[0].clientY);
	} else {
		mouseIsDown = false;
		zoomIsDown = false;
		if(removeOnUp != -1) {
			multiselected.splice(removeOnUp,1);
			removeOnUp = -1;
		}
	}
	pinchStartDistance = false;
	drawCanvas();
}

function mouseMove(e) { //mouse move
	e.preventDefault();

	if(mouseIsDown) {
		if(cntrlIsPressed && selectedIndex == -1) { //we are doing multiselect drag
			var i = findImage(scrToTileX(e.offsetX),scrToTileY(e.offsetY));
			selectedIndex = -1; //reset to see keep doing multiselect drag
			if(i != -1 && multiselected.indexOf(i) == -1) {
				multiselected.push(i);
			}
		} else if(selectedIndex != -1) {
			var dx = scrToTileX(e.offsetX) - selectedX - layout[selectedIndex].x;
			var dy = scrToTileY(e.offsetY) - selectedY - layout[selectedIndex].y;
			layout[selected].x += dx;
			layout[selected].y += dy;
			for(var i=0; i < multiselected.length; i++) {
				layout[multiselected[i]].x += dx;
				layout[multiselected[i]].y += dy;
			}
			removeOnUp = -1;
		}
    } else if(zoomIsDown) {
    	currentMag = getMagFromMouseX(e.offsetX);
    }
    drawCanvas();
}

function touchMove(e) { //touch move
	e.preventDefault();

	if(zoomIsDown) {
		currentMag = getMagFromMouseX(e.touches[0].clientX);
	} else if(mouseIsDown) {
		if(e.targetTouches.length == 1) {
		    	centerX = centerX - (scrToTileX(e.touches[0].clientX) - mouseDownX);
				centerY = centerY - (scrToTileY(e.touches[0].clientY) - mouseDownY);
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
				currentMag = pinchStartMag * pinchStartDistance/pinchDistance;

				centerX = centerX + (pinchCenterX - scrToTileX(e.touches[0].clientX + (e.touches[1].clientX-e.touches[0].clientX)/2));
				centerY = centerY + (pinchCenterY - scrToTileY(e.touches[0].clientY + (e.touches[1].clientY-e.touches[0].clientY)/2));
			}
		}
	}
    drawCanvas();
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
	$close = $('<a id="close" href="#"></a>');

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
var loading=0;

$( document ).ready(function() {
	canvas = $('#canvas').get(0);
	context = canvas.getContext("2d");
	resizeCanvas();

	$.ajax({
		dataType: "json",
		url: "images/layout_tb.json",
		success: function (data) {
	    	layout = data;

	    	//load thumbs
	    	for(var i=0; i < layout.length; i++) {
	    		loading++;
				layout[i].imageData = new Image();
				layout[i].imageData.onload = function () {
					loading--;
			    	drawCanvas();
			    };
			    layout[i].imageData.onerror = function() {
			    	console.log("failed to load " + layout[i].tbPath);
			    };
			    layout[i].imageData.src = layout[i].tbPath;
			}

			//get bounding box
			bounds = boundingBox(layout);

			//assume entire layout is pushed up agaist 0,0 and all x,y values are positive.
			centerX = Math.round(bounds.maxx/2);
			centerY = Math.round(bounds.maxy/2);

			//determine start currentMag so entire layout will fit on screen.
			currentMag = fitMag(bounds.maxx - bounds.minx,bounds.maxy - bounds.miny);
	    }
	});

	window.addEventListener('mousedown',mouseDown);
	window.addEventListener('mouseup',mouseUp);
	window.addEventListener('mousemove',mouseMove);
	window.addEventListener('touchstart',mouseDown);
	window.addEventListener('touchend',mouseUp);
	window.addEventListener('touchmove',touchMove);
	window.addEventListener('resize',resizeCanvas);
	$(document).keydown(function(event){
	    if(event.which=="17")
	        cntrlIsPressed = true;
	});
	cz = document.createEvent('KeyboardEvents'); // ctrl z event
	cz.initKeyboardEvent(
           'keydown', 
           true,     // key down events bubble 
           true,     // and they can be cancelled 
           document.defaultView,  // Use the default view 
           true,        // ctrl 
           false,       // alt
           false,        //shift
           false,       //meta key 
           90,          // keycode
           0
          );  

	$(document).keyup(function(){
	    cntrlIsPressed = false;
	});
});