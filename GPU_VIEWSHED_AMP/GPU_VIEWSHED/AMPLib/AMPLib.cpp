#include "stdafx.h"
#include "amp.h"
#include <amp_math.h>
#include <algorithm> 
#include <iostream>
#include <cstdlib>


#define RING_COUNTER 1
#define NORTH_NORTH_EAST_COUNTER 1
#define NORTH_NORTH_WEST_COUNTER 1
#define EAST_NORTH_EAST_COUNTER 1
#define WEST_NORTH_WEST_COUNTER 1
#define SOUTH_SOUTH_EAST_COUNTER 1
#define SOUTH_SOUTH_WEST_COUNTER 1
#define EAST_SOUTH_EAST_COUNTER 1
#define WEST_SOUTH_WEST_COUNTER 1

#define XDRAW 1
#define SDRAW 2
#define DDA 3
#define R3 4
#define R2 5



using namespace concurrency;


void calcDDA(float* zArray, int zArrayLengthX, int zArrayLengthY,
	int* visibleArray, int visibleArrayX, int visibleArrayY, int currX, int currY, int currZ,
	int rasterWidth, int rasterHeight)
{
	accelerator device(accelerator::default_accelerator);
	accelerator_view av = device.default_view;

	extent<1> eY(zArrayLengthY);
	extent<1> eX(zArrayLengthX);
	const array_view<const float, 2> dataViewZ(zArrayLengthY, zArrayLengthX, &zArray[0, 0]);
	array_view<int, 2> dataViewVisible(visibleArrayY, visibleArrayX, &visibleArray[0, 0]);
	dataViewVisible.discard_data();
	// Run code on the GPU

	dataViewVisible(currX, currY) = 1;

	parallel_for_each(av, eY, [=](index<1> idx) restrict(amp)
	{

		int destX;
		int destY;
		for (int i = 0; i < 2; i++)
		{
			if (i == 0)
			{
				destX = 0;
				destY = idx[0];
			}
			else
			{
				destX = rasterWidth;
				destY = rasterHeight - idx[0];
			}


			//Values for stepping through the line
			int dx = destX - currX;
			int dy = destY - currY;
			int steps;
			float xIncrement, yIncrement;
			float x = (int) currX;
			float y = (int) currY;
			float prevX = x;
			float prevY = y;

			//previously highest LOS
			float highest = -999.0;


			//Determine whether steps should be in the x or y axis
			if (fast_math::fabs(dx) > fast_math::fabs(dy))
			{
				steps = fast_math::fabs(dx);
			}
			else
			{
				steps = fast_math::fabs(dy);
			}


			xIncrement = dx / (float) steps;
			yIncrement = dy / (float) steps;



			//traverse through the line step by step
			for (int k = 0; k < steps; k++)
			{
				//move the current check point
				x += xIncrement;
				y += yIncrement;

				//distance to the check point, snapped to whole values 
				float dist = fast_math::sqrt(((int) x - currX) * ((int) x - currX) +
					((int) y - currY) * ((int) y - currY));

				//Elevation to check point
				float elev = (dataViewZ[(int) y][(int) x] - currZ) / dist;

				//elevation check
				if (elev >= highest)
				{
					dataViewVisible[(int) fast_math::round(y)][(int) fast_math::round(x)] = 1;
					highest = elev;
				}


			}
		}

	});


	parallel_for_each(av, eX, [=](index<1> idx) restrict(amp)
	{

		int destX;
		int destY;
		for (int i = 0; i < 2; i++)
		{
			if (i == 0)
			{
				destX = idx[0];
				destY = 0;
			}
			else
			{
				destX = rasterWidth - idx[0];
				destY = rasterHeight;
			}


			//Values for stepping through the line
			int dx = destX - currX;
			int dy = destY - currY;
			int steps;
			float xIncrement, yIncrement;
			float x = (int) currX;
			float y = (int) currY;
			float prevX = x;
			float prevY = y;

			//previously highest LOS
			float highest = -999.0;


			//Determine whether steps should be in the x or y axis
			if (fast_math::fabs(dx) > fast_math::fabs(dy))
			{
				steps = fast_math::fabs(dx);
			}

			else
			{
				steps = fast_math::fabs(dy);
			}


			xIncrement = dx / (float) steps;
			yIncrement = dy / (float) steps;



			//traverse through the line step by step
			for (int k = 0; k < steps; k++)
			{
				//move the current check point
				x += xIncrement;
				y += yIncrement;

				//distance to the check point, snapped to whole values 
				float dist = fast_math::sqrt(((int) x - currX) * ((int) x - currX) +
					((int) y - currY) * ((int) y - currY));

				//Elevation to check point
				float elev = (dataViewZ[(int) y][(int) x] - currZ) / dist;

				//elevation check
				if (elev >= highest)
				{
					dataViewVisible[(int) fast_math::round(y)][(int) fast_math::round(x)] = 1;
					highest = elev;
				}


			}
		}

	});



}

void calcR3(float* zArray, int zArrayLengthX, int zArrayLengthY,
	int* visibleArray, int visibleArrayX, int visibleArrayY, int currX, int currY, int currZ,
	int rasterWidth, int rasterHeight)
{
	accelerator device(accelerator::default_accelerator);
	accelerator_view av = device.default_view;

	extent<1> eY(zArrayLengthY);
	extent<1> eX(zArrayLengthX);
	const array_view<const float, 2> dataViewZ(zArrayLengthY, zArrayLengthX, &zArray[0, 0]);
	array_view<int, 2> dataViewVisible(visibleArrayY, visibleArrayX, &visibleArray[0, 0]);
	dataViewVisible.discard_data();
	// Run code on the GPU
	dataViewVisible(currX, currY) = 1;


	parallel_for_each(av, eY, [=](index<1> idx) restrict(amp)
	{

		int destX;
		int destY;
		for (int i = 0; i < 2; i++)
		{
			if (i == 0)
			{
				destX = 0;
				destY = idx[0];
			}
			else
			{
				destX = rasterWidth;
				destY = rasterHeight - idx[0];
			}


			//Values for stepping through the line
			int dx = destX - currX;
			int dy = destY - currY;
			int steps;
			float xIncrement, yIncrement;
			float x = (int) currX;
			float y = (int) currY;
			float prevX = x;
			float prevY = y;

			//previously highest LOS
			float highest = -999.0;


			//Determine whether steps should be in the x or y axis
			if (fast_math::fabs(dx) > fast_math::fabs(dy))
			{
				steps = fast_math::fabs(dx);
			}
			else
			{
				steps = fast_math::fabs(dy);
			}


			xIncrement = dx / (float) steps;
			yIncrement = dy / (float) steps;



			//traverse through the line step by step
			for (int k = 0; k < steps; k++)
			{
				//move the current check point
				x += xIncrement;
				y += yIncrement;

				//Delta between the two points surrounding the ray
				float diffX = x - (float) fast_math::round(x);
				float diffY = y - (float) fast_math::round(y);

				//grab the snapped height closest to the ray
				float lerpHeight = dataViewZ((int) fast_math::round(y), (int) fast_math::round(x));

				//used to store the height of the closest neighbour
				float nextHeight;

				//Check to see if any of the values will exceed the boundaries of the array
				//If so, just use the snapped lerpHeight instead
				if (x > 1 && x < rasterWidth && y > 1 && y < rasterHeight - 1)
				{
					//if the deltaX is negative, check x + 1
					if (diffX < 0)
					{
						//grab the nextHeight
						nextHeight = dataViewZ((int) y, (int) x + 1);
						//interpolated height is original heights + difference in heights * delta 
						lerpHeight = lerpHeight + ((nextHeight - lerpHeight) * diffX);
					}
					//if the deltaX is positive, check x -1
					if (diffX > 0)
					{
						//grab the nextHeight
						nextHeight = dataViewZ((int) y, (int) x - 1);
						//interpolated height is original heights + difference in heights * delta 
						lerpHeight = lerpHeight + ((nextHeight - lerpHeight) * diffX);
					}
					//if the deltaY is negative, check y + 1
					if (diffY < 0)
					{
						//grab the nextHeight
						nextHeight = dataViewZ((int) y + 1, (int) x);
						//interpolated height is original heights + difference in heights * delta 
						lerpHeight = lerpHeight + ((nextHeight - lerpHeight) * diffY);
					}
					//if the deltaY is positive, check y - 1
					if (diffY > 0)
					{
						//grab the nextHeight
						nextHeight = dataViewZ((int) y - 1, (int) x);
						//interpolated height is original heights + difference in heights * delta 
						lerpHeight = lerpHeight + ((nextHeight - lerpHeight) * diffY);
					}
				}

				//distance to the check point, snapped to whole values 
				float dist = fast_math::sqrt(((int) x - currX) * ((int) x - currX) +
					((int) y - currY) * ((int) y - currY));



				//Elevation to check point
				float elev = (dataViewZ[(int) y][(int) x] - currZ) / dist;

				//elevation check
				if (elev > highest)
				{
					dataViewVisible[(int) fast_math::round(y)][(int) fast_math::round(x)] = 1;
					highest = elev;
				}


			}
		}

	});


	parallel_for_each(av, eX, [=](index<1> idx) restrict(amp)
	{

		int destX;
		int destY;
		for (int i = 0; i < 2; i++)
		{
			if (i == 0)
			{
				destX = idx[0];
				destY = 0;
			}
			else
			{
				destX = rasterWidth - idx[0];
				destY = rasterHeight;
			}
			//Values for stepping through the line
			int dx = destX - currX;
			int dy = destY - currY;
			int steps;
			float xIncrement, yIncrement;
			float x = (int) currX;
			float y = (int) currY;
			float prevX = x;
			float prevY = y;

			//previously highest LOS
			float highest = -999.0;


			//Determine whether steps should be in the x or y axis
			if (fast_math::fabs(dx) > fast_math::fabs(dy))
			{
				steps = fast_math::fabs(dx);
			}
			else
			{
				steps = fast_math::fabs(dy);
			}


			xIncrement = dx / (float) steps;
			yIncrement = dy / (float) steps;



			//traverse through the line step by step
			for (int k = 0; k < steps; k++)
			{
				//move the current check point
				x += xIncrement;
				y += yIncrement;

				//Delta between the two points surrounding the ray
				float diffX = x - (float) fast_math::round(x);
				float diffY = y - (float) fast_math::round(y);

				//grab the snapped height closest to the ray
				float lerpHeight = dataViewZ((int) fast_math::round(y), (int) fast_math::round(x));

				//used to store the height of the closest neighbour
				float nextHeight;

				//Check to see if any of the values will exceed the boundaries of the array
				//If so, just use the snapped lerpHeight instead
				if (x > 1 && x < rasterWidth && y > 1 && y < rasterHeight - 1)
				{
					//if the deltaX is negative, check x + 1
					if (diffX < 0)
					{
						//grab the nextHeight
						nextHeight = dataViewZ((int) y, (int) x + 1);
						//interpolated height is original heights + difference in heights * delta 
						lerpHeight = lerpHeight + ((nextHeight - lerpHeight) * diffX);
					}
					//if the deltaX is positive, check x -1
					if (diffX > 0)
					{
						//grab the nextHeight
						nextHeight = dataViewZ((int) y, (int) x - 1);
						//interpolated height is original heights + difference in heights * delta 
						lerpHeight = lerpHeight + ((nextHeight - lerpHeight) * diffX);
					}
					//if the deltaY is negative, check y + 1
					if (diffY < 0)
					{
						//grab the nextHeight
						nextHeight = dataViewZ((int) y + 1, (int) x);
						//interpolated height is original heights + difference in heights * delta 
						lerpHeight = lerpHeight + ((nextHeight - lerpHeight) * diffY);
					}
					//if the deltaY is positive, check y - 1
					if (diffY > 0)
					{
						//grab the nextHeight
						nextHeight = dataViewZ((int) y - 1, (int) x);
						//interpolated height is original heights + difference in heights * delta 
						lerpHeight = lerpHeight + ((nextHeight - lerpHeight) * diffY);
					}
				}

				//distance to the check point, snapped to whole values 
				float dist = fast_math::sqrt(((int) x - currX) * ((int) x - currX) +
					((int) y - currY) * ((int) y - currY));



				//Elevation to check point
				float elev = (dataViewZ[(int) y][(int) x] - currZ) / dist;

				//elevation check
				if (elev > highest)
				{
					dataViewVisible[(int) fast_math::round(y)][(int) fast_math::round(x)] = 1;
					highest = elev;
				}


			}
		}

	});



}

void calcR2(float* zArray, int zArrayLengthX, int zArrayLengthY,
	int* visibleArray, int visibleArrayX, int visibleArrayY, int currX, int currY, int currZ,
	int rasterWidth, int rasterHeight)
{
	accelerator device(accelerator::default_accelerator);
	accelerator_view av = device.default_view;

	extent<1> eY(zArrayLengthY);
	extent<1> eX(zArrayLengthX);


	const array_view<const float, 2> dataViewZ(zArrayLengthY, zArrayLengthX, &zArray[0, 0]);
	array_view<int, 2> dataViewVisible(visibleArrayY, visibleArrayX, &visibleArray[0, 0]);
	array_view<int, 2> dataViewVisited(visibleArrayY, visibleArrayX, &visibleArray[0, 0]);// Doesn't work, need a new array perhaps or fudge :P
	dataViewVisible.discard_data();
	// Run code on the GPU
	dataViewVisible(currX, currY) = 1;

	/*
	parallel_for_each(av, eY, [=] (index<1> idx) restrict(amp)
	{

	int destX;
	int destY;
	for(int i = 0; i < 2; i ++)
	{
	if(i == 0)
	{
	destX = 0;
	destY = idx[0];
	}
	else
	{
	destX = rasterWidth;
	destY = rasterHeight - idx[0];
	}
	//Values for stepping through the line
	int destX = destX;
	int destY = destY;

	int guessedX = 0;
	int guessedY = 0;
	int dx = destX - (int)currX;
	int dy = destY - (int)currY;
	int steps;
	float xIncrement, yIncrement;
	float x = (int)currX;
	float y = (int)currY;
	float prevX = x;
	float prevY = y;
	float highest = -999;



	if (fast_math::fabs(dx) > fast_math::fabs(dy))
	{
	steps = fast_math::fabs(dx);
	}

	else
	{
	steps = fast_math::fabs(dy);
	}

	xIncrement = dx / (float)steps;
	yIncrement = dy / (float)steps;
	//All same as Bresenham


	//first point is visible
	dataViewVisible((int)(y), (int)(x)) = 1;

	//Step through the line
	for (int k = 0; k < steps; k++)
	{
	prevX = x;
	prevY = y;
	x += xIncrement;
	y += yIncrement;
	if (dataViewVisited((int)y, (int)x) != 1)
	{

	//Delta between the two points surrounding the ray
	float diffX = x - (float)(x);
	float diffY = y - (float)(y);



	//grab the snapped height closest to the ray
	float lerpHeight = dataViewZ((int)y, (int)x);

	//used to store the height of the closest neighbour
	float nextHeight;

	//Check to see if any of the values will exceed the boundaries of the array
	//If so, just use the snapped lerpHeight instead
	if (x > 1 && x < rasterWidth && y > 1 && y < rasterHeight - 1)
	{
	//if the deltaX is negative, check x + 1
	if (diffX < 0)
	{
	guessedX = (int)x + 1;
	guessedY = (int)y;
	//grab the nextHeight
	nextHeight = dataViewZ(guessedY, guessedX);
	//interpolated height is original heights + difference in heights * delta
	lerpHeight = lerpHeight + ((nextHeight - lerpHeight) * diffX);
	}
	//if the deltaX is positive, check x -1
	if (diffX > 0)
	{
	guessedX = (int)x - 1;
	guessedY = (int)y;
	//grab the nextHeight
	nextHeight = dataViewZ(guessedY, guessedX);
	//interpolated height is original heights + difference in heights * delta
	lerpHeight = lerpHeight + ((nextHeight - lerpHeight) * diffX);
	}
	//if the deltaY is negative, check y + 1
	if (diffY < 0)
	{
	guessedX = (int)x;
	guessedY = (int)y + 1;
	//grab the nextHeight
	nextHeight = dataViewZ(guessedY, guessedX);
	//interpolated height is original heights + difference in heights * delta
	lerpHeight = lerpHeight + ((nextHeight - lerpHeight) * diffY);
	}
	//if the deltaY is positive, check y - 1
	if (diffY > 0)
	{
	guessedX = (int)x;
	guessedY = (int)y - 1;
	//grab the nextHeight
	nextHeight = dataViewZ(guessedY, guessedX);
	//interpolated height is original heights + difference in heights * delta
	lerpHeight = lerpHeight + ((nextHeight - lerpHeight) * diffY);
	}
	}

	//calculate distance to the lerped x,y point
	float dist = (float)fast_math::sqrt((destX - currX) * (destX - currX) + (destY - currY) * (destY - currY));

	//calculate the elevation at lerped point
	float elev = (lerpHeight - currZ) / dist;

	//DO the sightline check
	if (elev > highest)
	{
	dataViewVisible((int)(y), (int)(x)) = 1;
	//visibleArrayCPU[guessedY, guessedX] = 1;
	highest = elev;
	}
	// visitedArray[guessedY, guessedX] = true;
	dataViewVisited((int)(y), (int)(x)) = true;



	}
	}
	}

	});

	dataViewVisited.discard_data();


	parallel_for_each(av, eX, [=] (index<1> idx) restrict(amp)
	{

	int destX;
	int destY;
	for(int i = 0; i < 2; i ++)
	{
	if(i == 0)
	{
	destX = idx[0];
	destY = 0;
	}
	else
	{
	destX = rasterWidth - idx[0];
	destY = rasterHeight;
	}
	//Values for stepping through the line
	int destX = destX;
	int destY = destY;

	int guessedX = 0;
	int guessedY = 0;
	int dx = destX - (int)currX;
	int dy = destY - (int)currY;
	int steps;
	float xIncrement, yIncrement;
	float x = (int)currX;
	float y = (int)currY;
	float prevX = x;
	float prevY = y;
	float highest = -999;



	if (fast_math::fabs(dx) > fast_math::fabs(dy))
	{
	steps = fast_math::fabs(dx);
	}

	else
	{
	steps = fast_math::fabs(dy);
	}

	xIncrement = dx / (float)steps;
	yIncrement = dy / (float)steps;
	//All same as Bresenham


	//first point is visible
	dataViewVisible((int)(y), (int)(x)) = 1;

	//Step through the line
	for (int k = 0; k < steps; k++)
	{
	prevX = x;
	prevY = y;
	x += xIncrement;
	y += yIncrement;
	if (dataViewVisited((int)y, (int)x) == 1)
	{

	//Delta between the two points surrounding the ray
	float diffX = x - (float)(x);
	float diffY = y - (float)(y);



	//grab the snapped height closest to the ray
	float lerpHeight = dataViewZ((int)y, (int)x);

	//used to store the height of the closest neighbour
	float nextHeight;

	//Check to see if any of the values will exceed the boundaries of the array
	//If so, just use the snapped lerpHeight instead
	if (x > 1 && x < rasterWidth && y > 1 && y < rasterHeight - 1)
	{
	//if the deltaX is negative, check x + 1
	if (diffX < 0)
	{
	guessedX = (int)x + 1;
	guessedY = (int)y;
	//grab the nextHeight
	nextHeight = dataViewZ(guessedY, guessedX);
	//interpolated height is original heights + difference in heights * delta
	lerpHeight = lerpHeight + ((nextHeight - lerpHeight) * diffX);
	}
	//if the deltaX is positive, check x -1
	if (diffX > 0)
	{
	guessedX = (int)x - 1;
	guessedY = (int)y;
	//grab the nextHeight
	nextHeight = dataViewZ(guessedY, guessedX);
	//interpolated height is original heights + difference in heights * delta
	lerpHeight = lerpHeight + ((nextHeight - lerpHeight) * diffX);
	}
	//if the deltaY is negative, check y + 1
	if (diffY < 0)
	{
	guessedX = (int)x;
	guessedY = (int)y + 1;
	//grab the nextHeight
	nextHeight = dataViewZ(guessedY, guessedX);
	//interpolated height is original heights + difference in heights * delta
	lerpHeight = lerpHeight + ((nextHeight - lerpHeight) * diffY);
	}
	//if the deltaY is positive, check y - 1
	if (diffY > 0)
	{
	guessedX = (int)x;
	guessedY = (int)y - 1;
	//grab the nextHeight
	nextHeight = dataViewZ(guessedY, guessedX);
	//interpolated height is original heights + difference in heights * delta
	lerpHeight = lerpHeight + ((nextHeight - lerpHeight) * diffY);
	}
	}

	//calculate distance to the lerped x,y point
	float dist = (float)fast_math::sqrt((destX - currX) * (destX - currX) + (destY - currY) * (destY - currY));

	//calculate the elevation at lerped point
	float elev = (lerpHeight - currZ) / dist;

	//DO the sightline check
	if (elev > highest)
	{
	dataViewVisible((int)(y), (int)(x)) = 1;
	//visibleArrayCPU[guessedY, guessedX] = 1;
	highest = elev;
	}
	// visitedArray[guessedY, guessedX] = true;
	dataViewVisited((int)(y), (int)(x)) = true;



	}
	}
	}

	});
	dataViewVisited.discard_data();

	*/
}



void calcXdrawOptim(float* zArray, int zArrayLengthX, int zArrayLengthY,
	int* visibleArray, int visibleArrayX, int visibleArrayY, int currX, int currY, int currZ,
	int rasterWidth, int rasterHeight, float* losArray)
{



	accelerator device(accelerator::default_accelerator);
	accelerator_view av = device.default_view;
	array_view<float, 2> dataViewZ(zArrayLengthY, zArrayLengthX, &zArray[0, 0]);
	array_view<int, 2> dataViewVisible(visibleArrayY, visibleArrayX, &visibleArray[0, 0]);
	array_view<float, 2> losArrayView(visibleArrayY, visibleArrayX, &losArray[0, 0]);


	dataViewVisible(currX, currY) = 1;


	int ringCounter = RING_COUNTER;//start 2 rings out
	int northNorthEastCounter = NORTH_NORTH_EAST_COUNTER;
	int northNorthWestCounter = NORTH_NORTH_WEST_COUNTER;
	int southSouthEastCounter = SOUTH_SOUTH_EAST_COUNTER;
	int southSouthWestCounter = SOUTH_SOUTH_WEST_COUNTER;

	int eastNorthEastCounter = EAST_NORTH_EAST_COUNTER;
	int eastSouthEastCounter = EAST_SOUTH_EAST_COUNTER;
	int westNorthWestCounter = WEST_NORTH_WEST_COUNTER;
	int westSouthWestCounter = WEST_SOUTH_WEST_COUNTER;

	//Total size of the ring in X & Y
	int maxRingY = max(rasterHeight - currY - 1, currY);
	int maxRingX = max(rasterWidth - currX - 1, currX);

	while (ringCounter < maxRingY)
	{
		extent<1> yExtent((northNorthEastCounter + northNorthWestCounter + southSouthEastCounter + southSouthWestCounter + 1));

		//Get CPU to calculate DDA compass points then send LOSARRAY to GPU
		parallel_for_each(av, yExtent.tile<256>().pad(), [=](tiled_index<256>idx) restrict(amp)
		{

			if (idx.global[0] < northNorthEastCounter)//NNE
			{
				tile_static float los[256][2];
				int interX = currX + idx.global[0] + 1;
				int interY = currY + ringCounter;

				int x1 = interX - 1;
				int y1 = interY - 1;
				int x2 = interX;
				int y2 = interY - 1;



				los[idx.local[0]][0] = losArrayView(y1, x1);
				los[idx.local[0] + 1][0] = losArrayView(y2, x2);
				los[idx.local[0]][1] = dataViewZ(interY, interX);

				float leftLos = los[idx.local[0]][0];
				float rightLos = los[idx.local[0] + 1][0];

				float losMax = fast_math::fmaxf(los[idx.local[0]][0], los[idx.local[0] + 1][0]);
				float losMin = fast_math::fminf(los[idx.local[0]][0], los[idx.local[0] + 1][0]);

				//float losLerpX = (((x1 * y2) - (y1 * x2)) * (currX * interX)) - (x1 - x2) * ((currX * interY)  - (currY * interX)) /
				//	((x1 - x2)*(currY - interY)) - ((y1 - y2) * (currX - interX));

				//float lerpLOS =  (rightLos - leftLos) * fast_math::fabs(rightLos - losLerpX);

				float losLerp = rightLos + (leftLos - rightLos) * (interX / interY);//does not work!!!l!l!!

				float lerpLOS = (losMin + (los[idx.local[0]][0] + los[idx.local[0] + 1][0]) / 2) / 2;

				float d = fast_math::sqrt((interX - currX) * (interX - currX) + (interY - currY) * (interY - currY));
				float e = ((los[idx.local[0]][1] - currZ) / d);


				dataViewVisible(interY, interX) += fast_math::fmaxf(0.0f, 1.0f);

				losArrayView(interY, interX) = fast_math::fmaxf(e, lerpLOS);
			}

			else if (idx.global[0] > northNorthEastCounter && idx.global[0] <= northNorthEastCounter + northNorthWestCounter)//NNW
			{
				int interX = currX - (idx.global[0] - northNorthEastCounter);
				int interY = currY + ringCounter;

				int vert1X = interX + 1;
				int vert1Y = interY - 1;
				int vert2X = interX;
				int vert2Y = interY - 1;

				tile_static float los[256][2];

				los[idx.local[0]][0] = losArrayView(vert1Y, vert1X);
				los[idx.local[0] + 1][0] = losArrayView(vert2Y, vert2X);
				los[idx.local[0]][1] = dataViewZ(interY, interX);

				float leftLos = los[idx.local[0]][0];
				float rightLos = los[idx.local[0] + 1][0];

				float losMax = fast_math::fmaxf(los[idx.local[0]][0], los[idx.local[0] + 1][0]);
				float losMin = fast_math::fminf(los[idx.local[0]][0], los[idx.local[0] + 1][0]);

				//float losLerpX = (((x1 * y2) - (y1 * x2)) * (currX * interX)) - (x1 - x2) * ((currX * interY)  - (currY * interX)) /
				//	((x1 - x2)*(currY - interY)) - ((y1 - y2) * (currX - interX));

				//float lerpLOS =  (rightLos - leftLos) * fast_math::fabs(rightLos - losLerpX);

				float losLerp = rightLos + (leftLos - rightLos) * (interX / interY);//does not work!!!l!l!!

				float lerpLOS = (losMin + (los[idx.local[0]][0] + los[idx.local[0] + 1][0]) / 2) / 2;

				float d = fast_math::sqrt((interX - currX) * (interX - currX) + (interY - currY) * (interY - currY));
				float e = ((los[idx.local[0]][1] - currZ) / d);


				dataViewVisible(interY, interX) += fast_math::fmaxf(0.0f, 1.0f);

				losArrayView(interY, interX) = fast_math::fmaxf(e, lerpLOS);
			}
			else if (idx.global[0] >= northNorthEastCounter + northNorthWestCounter && idx.global[0] <= northNorthEastCounter + northNorthWestCounter + southSouthWestCounter)//SSW
			{
				int interX = currX - (idx.global[0] - (northNorthEastCounter + northNorthWestCounter));
				int interY = currY - ringCounter;

				int vert1X = interX + 1;
				int vert1Y = interY + 1;
				int vert2X = interX;
				int vert2Y = interY + 1;

				tile_static float los[256][2];

				los[idx.local[0]][0] = losArrayView(vert1Y, vert1X);
				los[idx.local[0] + 1][0] = losArrayView(vert2Y, vert2X);
				los[idx.local[0]][1] = dataViewZ(interY, interX);

				float leftLos = los[idx.local[0]][0];
				float rightLos = los[idx.local[0] + 1][0];

				float losMax = fast_math::fmaxf(los[idx.local[0]][0], los[idx.local[0] + 1][0]);
				float losMin = fast_math::fminf(los[idx.local[0]][0], los[idx.local[0] + 1][0]);

				//float losLerpX = (((x1 * y2) - (y1 * x2)) * (currX * interX)) - (x1 - x2) * ((currX * interY)  - (currY * interX)) /
				//	((x1 - x2)*(currY - interY)) - ((y1 - y2) * (currX - interX));

				//float lerpLOS =  (rightLos - leftLos) * fast_math::fabs(rightLos - losLerpX);

				float losLerp = rightLos + (leftLos - rightLos) * (interX / interY);//does not work!!!l!l!!

				float lerpLOS = (losMin + (los[idx.local[0]][0] + los[idx.local[0] + 1][0]) / 2) / 2;

				float d = fast_math::sqrt((interX - currX) * (interX - currX) + (interY - currY) * (interY - currY));
				float e = ((los[idx.local[0]][1] - currZ) / d);


				dataViewVisible(interY, interX) += fast_math::fmaxf(0.0f, 1.0f);

				losArrayView(interY, interX) = fast_math::fmaxf(e, lerpLOS);
			}

			else if (idx.global[0] >= northNorthEastCounter + northNorthWestCounter + southSouthWestCounter && idx.global[0] < northNorthEastCounter + northNorthWestCounter + southSouthWestCounter + southSouthEastCounter)//SSE
			{
				int interX = currX + (idx.global[0] - (northNorthEastCounter + northNorthWestCounter + southSouthWestCounter));
				int interY = currY - ringCounter;

				int vert1X = interX - 1;
				int vert1Y = interY + 1;
				int vert2X = interX;
				int vert2Y = interY + 1;

				tile_static float los[256][2];

				los[idx.local[0]][0] = losArrayView(vert1Y, vert1X);
				los[idx.local[0] + 1][0] = losArrayView(vert2Y, vert2X);
				los[idx.local[0]][1] = dataViewZ(interY, interX);

				float leftLos = los[idx.local[0]][0];
				float rightLos = los[idx.local[0] + 1][0];

				float losMax = fast_math::fmaxf(los[idx.local[0]][0], los[idx.local[0] + 1][0]);
				float losMin = fast_math::fminf(los[idx.local[0]][0], los[idx.local[0] + 1][0]);

				//float losLerpX = (((x1 * y2) - (y1 * x2)) * (currX * interX)) - (x1 - x2) * ((currX * interY)  - (currY * interX)) /
				//	((x1 - x2)*(currY - interY)) - ((y1 - y2) * (currX - interX));

				//float lerpLOS =  (rightLos - leftLos) * fast_math::fabs(rightLos - losLerpX);

				float losLerp = rightLos + (leftLos - rightLos) * (interX / interY);//does not work!!!l!l!!

				float lerpLOS = (losMin + (los[idx.local[0]][0] + los[idx.local[0] + 1][0]) / 2) / 2;

				float d = fast_math::sqrt((interX - currX) * (interX - currX) + (interY - currY) * (interY - currY));
				float e = ((los[idx.local[0]][1] - currZ) / d);


				dataViewVisible(interY, interX) += fast_math::fmaxf(0.0f, 1.0f);

				losArrayView(interY, interX) = fast_math::fmaxf(e, lerpLOS);
			}

		});

		extent<1> xExtent(eastNorthEastCounter + eastSouthEastCounter + westNorthWestCounter + westSouthWestCounter + 1);

		//Get CPU to calculate DDA compass points then send LOSARRAY to GPU
		parallel_for_each(av, xExtent.tile<256>().pad(), [=](tiled_index<256>idx) restrict(amp)
		{

			if (idx.global[0] < eastNorthEastCounter && currX + ringCounter < rasterWidth)//ENE
			{
				int interY = currY + idx.global[0] + 1;
				int interX = currX + ringCounter;

				int vert1X = interX - 1;
				int vert1Y = interY;
				int vert2X = interX - 1;
				int vert2Y = interY - 1;

				tile_static float los[256][2];

				los[idx.local[0]][0] = losArrayView(vert1Y, vert1X);
				los[idx.local[0] + 1][0] = losArrayView(vert2Y, vert2X);
				los[idx.local[0]][1] = dataViewZ(interY, interX);

				float leftLos = los[idx.local[0]][0];
				float rightLos = los[idx.local[0] + 1][0];

				float losMax = fast_math::fmaxf(los[idx.local[0]][0], los[idx.local[0] + 1][0]);
				float losMin = fast_math::fminf(los[idx.local[0]][0], los[idx.local[0] + 1][0]);

				//float losLerpX = (((x1 * y2) - (y1 * x2)) * (currX * interX)) - (x1 - x2) * ((currX * interY)  - (currY * interX)) /
				//	((x1 - x2)*(currY - interY)) - ((y1 - y2) * (currX - interX));

				//float lerpLOS =  (rightLos - leftLos) * fast_math::fabs(rightLos - losLerpX);

				float losLerp = rightLos + (leftLos - rightLos) * (interX / interY);//does not work!!!l!l!!

				float lerpLOS = (losMin + (los[idx.local[0]][0] + los[idx.local[0] + 1][0]) / 2) / 2;

				float d = fast_math::sqrt((interX - currX) * (interX - currX) + (interY - currY) * (interY - currY));
				float e = ((los[idx.local[0]][1] - currZ) / d);


				dataViewVisible(interY, interX) += fast_math::fmaxf(0.0f, 1.0f);

				losArrayView(interY, interX) = fast_math::fmaxf(e, lerpLOS);
			}

			else if (idx.global[0] > eastNorthEastCounter && idx.global[0] <= eastNorthEastCounter + eastSouthEastCounter && currX + ringCounter < rasterWidth)//ESE
			{
				int interY = currY - (idx.global[0] - eastNorthEastCounter);
				int interX = currX + ringCounter;

				int vert1X = interX - 1;
				int vert1Y = interY;
				int vert2X = interX - 1;
				int vert2Y = interY + 1;

				tile_static float los[256][2];

				los[idx.local[0]][0] = losArrayView(vert1Y, vert1X);
				los[idx.local[0] + 1][0] = losArrayView(vert2Y, vert2X);
				los[idx.local[0]][1] = dataViewZ(interY, interX);

				float leftLos = los[idx.local[0]][0];
				float rightLos = los[idx.local[0] + 1][0];

				float losMax = fast_math::fmaxf(los[idx.local[0]][0], los[idx.local[0] + 1][0]);
				float losMin = fast_math::fminf(los[idx.local[0]][0], los[idx.local[0] + 1][0]);

				//float losLerpX = (((x1 * y2) - (y1 * x2)) * (currX * interX)) - (x1 - x2) * ((currX * interY)  - (currY * interX)) /
				//	((x1 - x2)*(currY - interY)) - ((y1 - y2) * (currX - interX));

				//float lerpLOS =  (rightLos - leftLos) * fast_math::fabs(rightLos - losLerpX);

				float losLerp = rightLos + (leftLos - rightLos) * (interX / interY);//does not work!!!l!l!!

				float lerpLOS = (losMin + (los[idx.local[0]][0] + los[idx.local[0] + 1][0]) / 2) / 2;

				float d = fast_math::sqrt((interX - currX) * (interX - currX) + (interY - currY) * (interY - currY));
				float e = ((los[idx.local[0]][1] - currZ) / d);


				dataViewVisible(interY, interX) += fast_math::fmaxf(0.0f, 1.0f);

				losArrayView(interY, interX) = fast_math::fmaxf(e, lerpLOS);

			}
			else if (idx.global[0] >= eastNorthEastCounter + eastSouthEastCounter && idx.global[0] <= eastNorthEastCounter + eastSouthEastCounter + westSouthWestCounter
				&& currX - ringCounter > 0)//WSW
			{
				int interY = currY - (idx.global[0] - (eastNorthEastCounter + eastSouthEastCounter));
				int interX = currX - ringCounter;

				int vert1X = interX + 1;
				int vert1Y = interY + 1;
				int vert2X = interX + 1;
				int vert2Y = interY;

				tile_static float los[256][2];

				los[idx.local[0]][0] = losArrayView(vert1Y, vert1X);
				los[idx.local[0] + 1][0] = losArrayView(vert2Y, vert2X);
				los[idx.local[0]][1] = dataViewZ(interY, interX);

				float leftLos = los[idx.local[0]][0];
				float rightLos = los[idx.local[0] + 1][0];

				float losMax = fast_math::fmaxf(los[idx.local[0]][0], los[idx.local[0] + 1][0]);
				float losMin = fast_math::fminf(los[idx.local[0]][0], los[idx.local[0] + 1][0]);

				//float losLerpX = (((x1 * y2) - (y1 * x2)) * (currX * interX)) - (x1 - x2) * ((currX * interY)  - (currY * interX)) /
				//	((x1 - x2)*(currY - interY)) - ((y1 - y2) * (currX - interX));

				//float lerpLOS =  (rightLos - leftLos) * fast_math::fabs(rightLos - losLerpX);

				float losLerp = rightLos + (leftLos - rightLos) * (interX / interY);//does not work!!!l!l!!

				float lerpLOS = (losMin + (los[idx.local[0]][0] + los[idx.local[0] + 1][0]) / 2) / 2;

				float d = fast_math::sqrt((interX - currX) * (interX - currX) + (interY - currY) * (interY - currY));
				float e = ((los[idx.local[0]][1] - currZ) / d);


				dataViewVisible(interY, interX) += fast_math::fmaxf(0.0f, 1.0f);

				losArrayView(interY, interX) = fast_math::fmaxf(e, lerpLOS);
			}
			else if (idx.global[0] >= eastNorthEastCounter + eastSouthEastCounter + westSouthWestCounter
				&& idx.global[0] <= eastNorthEastCounter + eastSouthEastCounter + westSouthWestCounter + westNorthWestCounter && currX - ringCounter > 0)//WNW
			{
				int interY = currY + (idx.global[0] - (eastNorthEastCounter + eastSouthEastCounter + westSouthWestCounter));
				int interX = currX - ringCounter;

				int vert1X = interX + 1;
				int vert1Y = interY - 1;
				int vert2X = interX + 1;
				int vert2Y = interY;

				tile_static float los[256][2];

				los[idx.local[0]][0] = losArrayView(vert1Y, vert1X);
				los[idx.local[0] + 1][0] = losArrayView(vert2Y, vert2X);
				los[idx.local[0]][1] = dataViewZ(interY, interX);

				float leftLos = los[idx.local[0]][0];
				float rightLos = los[idx.local[0] + 1][0];

				float losMax = fast_math::fmaxf(los[idx.local[0]][0], los[idx.local[0] + 1][0]);
				float losMin = fast_math::fminf(los[idx.local[0]][0], los[idx.local[0] + 1][0]);

				//float losLerpX = (((x1 * y2) - (y1 * x2)) * (currX * interX)) - (x1 - x2) * ((currX * interY)  - (currY * interX)) /
				//	((x1 - x2)*(currY - interY)) - ((y1 - y2) * (currX - interX));

				//float lerpLOS =  (rightLos - leftLos) * fast_math::fabs(rightLos - losLerpX);

				float losLerp = rightLos + (leftLos - rightLos) * (interX / interY);//does not work!!!l!l!!

				float lerpLOS = (losMin + (los[idx.local[0]][0] + los[idx.local[0] + 1][0]) / 2) / 2;

				float d = fast_math::sqrt((interX - currX) * (interX - currX) + (interY - currY) * (interY - currY));
				float e = ((los[idx.local[0]][1] - currZ) / d);


				dataViewVisible(interY, interX) += fast_math::fmaxf(0.0f, 1.0f);

				losArrayView(interY, interX) = fast_math::fmaxf(e, lerpLOS);

			}



		});



		//ALL THIS IS KINDA FUDGED, figure out real values
		//If the northNorthEastCounter hasn't hit the Eastern boundary of the DEM
		if (currY + northNorthEastCounter < rasterHeight)
		{
			eastNorthEastCounter++;
			westNorthWestCounter++;
		}

		//If the northNorthWestCounter hasn't hit the Western boundary of the DEM
		if (currY - northNorthWestCounter > 0)
		{
			eastSouthEastCounter++;
			westSouthWestCounter++;
		}



		//If the northNorthEastCounter hasn't hit the Eastern boundary of the DEM
		if (currX + northNorthEastCounter < rasterWidth)
		{
			northNorthEastCounter++;
			southSouthEastCounter++;
		}

		//If the northNorthWestCounter hasn't hit the Western boundary of the DEM
		if (currX - northNorthWestCounter > 0)
		{
			northNorthWestCounter++;
			southSouthWestCounter++;
		}



		ringCounter++;

	}
	av.wait();

	losArrayView.discard_data();
	dataViewZ.discard_data();

}


void calcXdraw(float* zArray, int zArrayLengthX, int zArrayLengthY,
	int* visibleArray, int visibleArrayX, int visibleArrayY, int currX, int currY, int currZ,
	int rasterWidth, int rasterHeight, float* losArray)
{



	accelerator device(accelerator::default_accelerator);
	accelerator_view av = device.default_view;
	array_view<float, 2> dataViewZ(zArrayLengthY, zArrayLengthX, &zArray[0, 0]);
	array_view<int, 2> dataViewVisible(visibleArrayY, visibleArrayX, &visibleArray[0, 0]);
	array_view<float, 2> losArrayView(visibleArrayY, visibleArrayX, &losArray[0, 0]);


	//((e - lerpLOS)*d) + fast_math::fabsf(e);


	int ringCounter = RING_COUNTER;//start 2 rings out
	int northNorthEastCounter = NORTH_NORTH_EAST_COUNTER;
	int northNorthWestCounter = NORTH_NORTH_WEST_COUNTER;
	int southSouthEastCounter = SOUTH_SOUTH_EAST_COUNTER;
	int southSouthWestCounter = SOUTH_SOUTH_WEST_COUNTER;

	int eastNorthEastCounter = EAST_NORTH_EAST_COUNTER;
	int eastSouthEastCounter = EAST_SOUTH_EAST_COUNTER;
	int westNorthWestCounter = WEST_NORTH_WEST_COUNTER;
	int westSouthWestCounter = WEST_SOUTH_WEST_COUNTER;

	//Total size of the ring in X & Y
	int maxRingY = max(rasterHeight - currY - 1, currY);
	int maxRingX = max(rasterWidth - currX - 1, currX);

	while (ringCounter < maxRingY)
	{
		extent<1> yExtent(northNorthEastCounter + northNorthWestCounter + southSouthEastCounter + southSouthWestCounter + 1);

		//Get CPU to calculate DDA compass points then send LOSARRAY to GPU
		parallel_for_each(av, yExtent, [=](index<1> idx) restrict(amp)
		{

			if (idx[0] < northNorthEastCounter)//NNE
			{
				int interX = currX + idx[0] + 1;
				int interY = currY + ringCounter;

				int x1 = interX - 1;
				int y1 = interY - 1;
				int x2 = interX;
				int y2 = interY - 1;

				float leftLos = losArrayView(y1, x1);
				float rightLos = losArrayView(y2, x2);

				float losMax = fast_math::fmaxf(leftLos, rightLos);
				float losMin = fast_math::fminf(leftLos, rightLos);

				//float losLerpX = (((x1 * y2) - (y1 * x2)) * (currX * interX)) - (x1 - x2) * ((currX * interY)  - (currY * interX)) /
				//	((x1 - x2)*(currY - interY)) - ((y1 - y2) * (currX - interX));

				//float lerpLOS =  (rightLos - leftLos) * fast_math::fabs(rightLos - losLerpX);

				//float losLerp = rightLos + (leftLos - rightLos) * (interX / interY);//does not work!!!l!l!!

				float lerpLOS = (losMin + losMax) / 2;

				float d = fast_math::sqrt((interX - currX) * (interX - currX) + (interY - currY) * (interY - currY));
				float e = ((dataViewZ(interY, interX) - currZ) / d);


				if (e > lerpLOS)
				{
					dataViewVisible(interY, interX) = 1;
					losArrayView(interY, interX) = e;
				}
				else
				{
					losArrayView(interY, interX) = lerpLOS;
				}
			}

			else if (idx[0] > northNorthEastCounter && idx[0] <= northNorthEastCounter + northNorthWestCounter)//NNW
			{
				int interX = currX - (idx[0] - northNorthEastCounter);
				int interY = currY + ringCounter;

				int vert1X = interX + 1;
				int vert1Y = interY - 1;
				int vert2X = interX;
				int vert2Y = interY - 1;

				float leftLos = losArrayView(vert1Y, vert1X);
				float rightLos = losArrayView(vert2Y, vert2X);

				float losMax = fast_math::fmaxf(leftLos, rightLos);
				float losMin = fast_math::fminf(leftLos, rightLos);

				//float losLerp = rightLos + (leftLos - rightLos) * (interX / interY);//does not work!!!l!l!!

				float lerpLOS = (losMin + losMax) / 2;

				float d = fast_math::sqrt((interX - currX) * (interX - currX) + (interY - currY) * (interY - currY));
				float e = ((dataViewZ(interY, interX) - currZ) / d);


				if (e > lerpLOS)
				{
					dataViewVisible(interY, interX) = 1;
					losArrayView(interY, interX) = e;
				}
				else
				{
					losArrayView(interY, interX) = lerpLOS;
				}
			}
			else if (idx[0] >= northNorthEastCounter + northNorthWestCounter && idx[0] <= northNorthEastCounter + northNorthWestCounter + southSouthWestCounter)//SSW
			{
				int interX = currX - (idx[0] - (northNorthEastCounter + northNorthWestCounter));
				int interY = currY - ringCounter;

				int vert1X = interX + 1;
				int vert1Y = interY + 1;
				int vert2X = interX;
				int vert2Y = interY + 1;

				float leftLos = losArrayView(vert1Y, vert1X);
				float rightLos = losArrayView(vert2Y, vert2X);

				float losMax = fast_math::fmaxf(leftLos, rightLos);
				float losMin = fast_math::fminf(leftLos, rightLos);

				//float losLerp = rightLos + (leftLos - rightLos) * (interX / interY);//does not work!!!l!l!!

				float lerpLOS = (losMin + losMax) / 2;

				float d = fast_math::sqrt((interX - currX) * (interX - currX) + (interY - currY) * (interY - currY));
				float e = ((dataViewZ(interY, interX) - currZ) / d);


				if (e > lerpLOS)
				{
					dataViewVisible(interY, interX) = 1;
					losArrayView(interY, interX) = e;
				}
				else
				{
					losArrayView(interY, interX) = lerpLOS;
				}
			}
			else if (idx[0] >= northNorthEastCounter + northNorthWestCounter + southSouthWestCounter && idx[0] <= northNorthEastCounter + northNorthWestCounter + southSouthWestCounter + southSouthEastCounter)//SSE
			{
				int interX = currX + (idx[0] - (northNorthEastCounter + northNorthWestCounter + southSouthWestCounter));
				int interY = currY - ringCounter;

				int vert1X = interX - 1;
				int vert1Y = interY + 1;
				int vert2X = interX;
				int vert2Y = interY + 1;

				float leftLos = losArrayView(vert1Y, vert1X);
				float rightLos = losArrayView(vert2Y, vert2X);

				float losMax = fast_math::fmaxf(leftLos, rightLos);
				float losMin = fast_math::fminf(leftLos, rightLos);

				//float losLerp = rightLos + (leftLos - rightLos) * (interX / interY);//does not work!!!l!l!!

				float lerpLOS = (losMin + losMax) / 2;

				float d = fast_math::sqrt((interX - currX) * (interX - currX) + (interY - currY) * (interY - currY));
				float e = ((dataViewZ(interY, interX) - currZ) / d);


				if (e > lerpLOS)
				{
					dataViewVisible(interY, interX) = 1;
					losArrayView(interY, interX) = e;
				}
				else
				{
					losArrayView(interY, interX) = lerpLOS;
				}

			}



		});



		extent<1> xExtent(eastNorthEastCounter + eastSouthEastCounter + westNorthWestCounter + westSouthWestCounter + 1);

		//Get CPU to calculate DDA compass points then send LOSARRAY to GPU
		parallel_for_each(av, xExtent, [=](index<1> idx) restrict(amp)
		{

			if (idx[0] < eastNorthEastCounter && currX + ringCounter < rasterWidth)//ENE
			{
				int interY = currY + idx[0] + 1;
				int interX = currX + ringCounter;

				int vert1X = interX - 1;
				int vert1Y = interY;
				int vert2X = interX - 1;
				int vert2Y = interY - 1;

				float leftLos = losArrayView(vert1Y, vert1X);
				float rightLos = losArrayView(vert2Y, vert2X);

				float losMax = fast_math::fmaxf(leftLos, rightLos);
				float losMin = fast_math::fminf(leftLos, rightLos);

				//float losLerp = rightLos + (leftLos - rightLos) * (interX / interY);//does not work!!!l!l!!

				float lerpLOS = (losMin + losMax) / 2;

				float d = fast_math::sqrt((interX - currX) * (interX - currX) + (interY - currY) * (interY - currY));
				float e = ((dataViewZ(interY, interX) - currZ) / d);


				if (e > lerpLOS)
				{
					dataViewVisible(interY, interX) = 1;
					losArrayView(interY, interX) = e;
				}
				else
				{
					losArrayView(interY, interX) = lerpLOS;
				}
			}

			else if (idx[0] > eastNorthEastCounter && idx[0] <= eastNorthEastCounter + eastSouthEastCounter && currX + ringCounter < rasterWidth)//ESE
			{
				int interY = currY - (idx[0] - eastNorthEastCounter);
				int interX = currX + ringCounter;

				int vert1X = interX - 1;
				int vert1Y = interY;
				int vert2X = interX - 1;
				int vert2Y = interY + 1;

				float leftLos = losArrayView(vert1Y, vert1X);
				float rightLos = losArrayView(vert2Y, vert2X);

				float losMax = fast_math::fmaxf(leftLos, rightLos);
				float losMin = fast_math::fminf(leftLos, rightLos);

				//float losLerp = rightLos + (leftLos - rightLos) * (interX / interY);//does not work!!!l!l!!

				float lerpLOS = (losMin + losMax) / 2;

				float d = fast_math::sqrt((interX - currX) * (interX - currX) + (interY - currY) * (interY - currY));
				float e = ((dataViewZ(interY, interX) - currZ) / d);


				if (e > lerpLOS)
				{
					dataViewVisible(interY, interX) = 1;
					losArrayView(interY, interX) = e;
				}
				else
				{
					losArrayView(interY, interX) = lerpLOS;
				}
			}
			else if (idx[0] >= eastNorthEastCounter + eastSouthEastCounter && idx[0] <= eastNorthEastCounter + eastSouthEastCounter + westSouthWestCounter
				&& currX - ringCounter > 0)//WSW
			{
				int interY = currY - (idx[0] - (eastNorthEastCounter + eastSouthEastCounter));
				int interX = currX - ringCounter;

				int vert1X = interX + 1;
				int vert1Y = interY + 1;
				int vert2X = interX + 1;
				int vert2Y = interY;

				float leftLos = losArrayView(vert1Y, vert1X);
				float rightLos = losArrayView(vert2Y, vert2X);

				float losMax = fast_math::fmaxf(leftLos, rightLos);
				float losMin = fast_math::fminf(leftLos, rightLos);

				//float losLerp = rightLos + (leftLos - rightLos) * (interX / interY);//does not work!!!l!l!!

				float lerpLOS = (losMin + losMax) / 2;

				float d = fast_math::sqrt((interX - currX) * (interX - currX) + (interY - currY) * (interY - currY));
				float e = ((dataViewZ(interY, interX) - currZ) / d);


				if (e > lerpLOS)
				{
					dataViewVisible(interY, interX) = 1;
					losArrayView(interY, interX) = e;
				}
				else
				{
					losArrayView(interY, interX) = lerpLOS;
				}
			}
			else if (idx[0] >= eastNorthEastCounter + eastSouthEastCounter + westSouthWestCounter
				&& idx[0] <= eastNorthEastCounter + eastSouthEastCounter + westSouthWestCounter + westNorthWestCounter && currX - ringCounter > 0)//WNW
			{
				int interY = currY + (idx[0] - (eastNorthEastCounter + eastSouthEastCounter + westSouthWestCounter));
				int interX = currX - ringCounter;

				int vert1X = interX + 1;
				int vert1Y = interY - 1;
				int vert2X = interX + 1;
				int vert2Y = interY;

				float leftLos = losArrayView(vert1Y, vert1X);
				float rightLos = losArrayView(vert2Y, vert2X);

				float losMax = fast_math::fmaxf(leftLos, rightLos);
				float losMin = fast_math::fminf(leftLos, rightLos);

				//float losLerp = rightLos + (leftLos - rightLos) * (interX / interY);//does not work!!!l!l!!

				float lerpLOS = (losMin + losMax) / 2;

				float d = fast_math::sqrt((interX - currX) * (interX - currX) + (interY - currY) * (interY - currY));
				float e = ((dataViewZ(interY, interX) - currZ) / d);


				if (e > lerpLOS)
				{
					dataViewVisible(interY, interX) = 1;
					losArrayView(interY, interX) = e;
				}
				else
				{
					losArrayView(interY, interX) = lerpLOS;
				}

			}



		});



		//If the westNorthWestCounter hasn't hit the Northern boundary of the DEM - CORRECT
		if (currY + westNorthWestCounter < rasterHeight - 1)
		{
			eastNorthEastCounter++;
			westNorthWestCounter++;
		}

		//If the westSouthWestCounter hasn't hit the Southern boundary of the DEM - CORRECT
		if (currY - westSouthWestCounter > 1)
		{
			westSouthWestCounter++;
			eastSouthEastCounter++;

		}

		//If the northNorthEastCounter hasn't hit the Eastern boundary of the DEM - CORRECT
		if (currX + northNorthEastCounter < rasterWidth - 1)
		{
			northNorthEastCounter++;
			southSouthEastCounter++;
		}

		//If the northNorthWestCounter hasn't hit the Western boundary of the DEM - CORRECT
		if (currX - northNorthWestCounter > 1)
		{
			northNorthWestCounter++;
			southSouthWestCounter++;
		}


		ringCounter++;
		//av.wait();

	}
	losArrayView.discard_data();
	dataViewZ.discard_data();

}



extern "C" __declspec (dllexport)
	void _stdcall staging(float* zArray, int zArrayLengthX,
	int zArrayLengthY, int* visibleArray, int visibleArrayX, int visibleArrayY, int currX, int currY, int currZ,
	int rasterWidth, int rasterHeight, float* losArray, int gpuType)
{


	if (gpuType == XDRAW)
	{
		calcXdraw(zArray, zArrayLengthX, zArrayLengthY, visibleArray, visibleArrayX,
			visibleArrayY, currX, currY, currZ, rasterWidth, rasterHeight, losArray);
	}
	else if (gpuType == DDA)
	{
		calcDDA(zArray, zArrayLengthX, zArrayLengthY, visibleArray,
			visibleArrayX, visibleArrayY, currX, currY, currZ, rasterWidth, rasterHeight);
	}
	else if (gpuType == R3)
	{
		calcR3(zArray, zArrayLengthX, zArrayLengthY, visibleArray,
			visibleArrayX, visibleArrayY, currX, currY, currZ, rasterWidth, rasterHeight);
	}
	else if (gpuType == R2)
	{
		calcR2(zArray, zArrayLengthX, zArrayLengthY, visibleArray,
			visibleArrayX, visibleArrayY, currX, currY, currZ, rasterWidth, rasterHeight);
	}


}
