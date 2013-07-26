using System;
using System.Diagnostics;
using Myriax.Eonfusion.API;
using Myriax.Eonfusion.API.Binding;
using Myriax.Eonfusion.API.Data;
using Myriax.Eonfusion.API.Properties;
using Myriax.Eonfusion.API.UI.Descriptors;
using Myriax.Eonfusion.API.Helpers;
using System.Collections.Generic;
using System.Threading;
//AMP
using System.Runtime.InteropServices;
using System.Linq;
using System.Text;
using Add_in1;

namespace GPU_VIEWSHED
{
    public class ViewshedAddIn : IAddIn
    {

        /*
         * Our .DLL imports
         * Staging is called so we can use C++ AMP restricted keywords!!!!
         */
       // [DllImport("AMPLib", CallingConvention = CallingConvention.StdCall)]
        //extern unsafe static void calcGPU(float* zaArray, int zArrayLengthX, int zArrayLengthY, int* visibleArray, 
       //     int visibleArrayX, int visibleArrayY, int currX, int currY, int currZ, int rasterWidth, int rasterHeight);
        
        [DllImport("AMPLib", CallingConvention = CallingConvention.StdCall)]
        extern unsafe static void staging(float* zaArray, int zArrayLengthX, int zArrayLengthY, int* visibleArray,
            int visibleArrayX, int visibleArrayY, int currX, int currY, int currZ,
            int rasterWidth, int rasterHeight, float* losArrayPt);



        //Array of heights for each pixel
        double[,] zArray;
        float[,] zArrayFloat;

        //Array for the XDraw LOS values for each pixel
        float[,] losArray;

        //Array of visible pixels to be parsed out
        bool[,] visibleArray;
        int[,] visibleArrayInt;

        //Array of vertices which have been visited
        bool[,] visitedArray;

        //Array of previous Lines of Sights for the Octants calculation
        double[] prevLOS = { -999, -999, -999, -999, -999, -999, -999, -999 };

        int[] octants = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

        int rasterWidth, rasterHeight;

        Stopwatch stopwatch;

        public IDataset Execute(IApplication application)
        {
            stopwatch = new Stopwatch();

            DEMRaster demRaster = application.InputDatasets.BindData(DEMBindProperty);
            FocalPointVectors focalPointVectors = application.InputDatasets.BindData(FocalPointBindProperty);

            //  Parse vectors to get focal point.

            if (focalPointVectors.VertexTable.Count == 0)
            {
                throw new AddInException("No focal point found.");
            }

            FocalPointVectorsVertex focalVertex = focalPointVectors.VertexTable.SelectAll()[0];
            double focalX = focalVertex.X;
            double focalY = focalVertex.Y;
            double focalZ = focalVertex.Z;

            application.EventGroup.InsertInfoEvent(string.Format("Focal point is ({0}, {1}).", focalX, focalY));



            ContinuousRasterHelper<DEMRasterVertex> demHelper = new ContinuousRasterHelper<DEMRasterVertex>(demRaster);
            ITable<DEMRasterVertex> demVertexTable = demRaster.VertexTable;

            rasterWidth = demRaster.GetVertexIndexCount(0);
            rasterHeight = demRaster.GetVertexIndexCount(1);

            zArray = new double[rasterHeight, rasterWidth];
            zArrayFloat = new float[rasterHeight, rasterWidth];
            visibleArray = new bool[rasterHeight, rasterWidth];
            visibleArrayInt = new int[rasterHeight, rasterWidth];
            losArray = new float[rasterHeight, rasterWidth];

            visitedArray = new bool[rasterHeight, rasterWidth];



            //  Make sure that our X and Y values are regular (i.e. that there
            //  is a linear transformation between (X, Y) values and raster
            //  index values.

            IValueList xAttributeList = demVertexTable.GetBoundValueList(vertex => vertex.X);
            IValueList yAttributeList = demVertexTable.GetBoundValueList(vertex => vertex.Y);

            application.EventGroup.InsertInfoEvent(string.Format("X attribute name is {0}.", xAttributeList.Name));
            application.EventGroup.InsertInfoEvent(string.Format("X transform is {0}.", demRaster.RegularListCoefficients(xAttributeList) == null ? "null" : "something"));

            //if (!demRaster.ListIsRegular(xAttributeList))
            //{
            //    throw new AddInException("X attribute needs to be regular.");
            //}
            //if (!demRaster.ListIsRegular(yAttributeList))
            //{
            //    throw new AddInException("Y attribute needs to be regular.");
            //}

            //  xTransform and yTransform each contain three values (Ax, Bx, Cx)
            //  and (Ay, By, Cy) such that:
            //    X = Ax * XIndex + Bx * YIndex + Cx
            //    Y = Ay * XIndex + By * YIndex + Cy

            //  We can use these two equations to solve for XIndex and YIndex when
            //  we know X and Y, thereby giving us a way of transforming from
            //  world (X, Y) coordinates into raster (XIndex, YIndex) values.  The
            //  XIndex, YIndex values then enable us to look up into the local
            //  arrays (e.g. zArray[YIndex, XIndex]).


            double[] xTransform = demRaster.RegularListCoefficients(xAttributeList);
            double[] yTransform = demRaster.RegularListCoefficients(yAttributeList);

            double x0 = xAttributeList.AsValueList<double>()[0];

            double[] xInvTransform = new double[3];
            double[] yInvTransform = new double[3];

            double denom = xTransform[0] * yTransform[1] - yTransform[0] * xTransform[1];

            xInvTransform[0] = yTransform[1] / denom;
            xInvTransform[1] = -xTransform[1] / denom;
            xInvTransform[2] = (xTransform[1] * yTransform[2] - yTransform[1] * xTransform[2]) / denom;

            yInvTransform[0] = -yTransform[0] / denom;
            yInvTransform[1] = xTransform[0] / denom;
            yInvTransform[2] = -(xTransform[0] * yTransform[2] - yTransform[0] * xTransform[2]) / denom;

            //  xInvTransform and yInvTransform now give us a way of transforming
            //  world (X, Y) coordinates back into raster (XIndex, YIndex) values
            //  as follows:
            //    XIndex = InvAx * X + InvBx * Y + InvCx
            //    YIndex = InvAy * Y + InvBy * Y + InvCy
            //  where xInvTransform has values (InvAx, InvBx, InvCx) and
            //  yInvTransform has values (InvAy, InvBy, InvCy).





            //  Copy Z values from Eon structures to local array.
            Trace.WriteLine("Grabbing raster");
            demHelper.ProcessVertexWindow2D(0, 0, rasterWidth, rasterHeight, delegate(int rasterIndex, int[] rasterTileOfs, int windowIndexOfs, int[] windowOfs, int spanSize)
            {
                int windowOfsX = windowOfs[0];
                int windowOfsY = windowOfs[1];

                for (int i = 0; i < spanSize; ++i)
                {
                    zArray[windowOfsY, windowOfsX + i] = demVertexTable[rasterIndex + i].Z;
                    zArrayFloat[windowOfsY, windowOfsX + i] = (float)demVertexTable[rasterIndex + i].Z;
                }
            });



            Trace.WriteLine("Finished grabbing raster");


            //Transform world coordinates to local coordinates
            focalX = xInvTransform[0] * focalX + xInvTransform[1] * focalY + xInvTransform[2];
            focalY = yInvTransform[0] * focalY + yInvTransform[1] * focalY + yInvTransform[2];

            //Duplication pretty much
            int currX = (int)Math.Round(focalX);
            int currY = (int)Math.Round(focalY);
            int currZ = (int)Math.Round(focalZ);


            //callXdraw(400, currX, currY, currZ);
            //callDDA(currX, currY, currZ);
            //callR3(currX, currY, currZ);
            //callR2(currX, currY, currZ);


            callDDA_GPU(currX, currY, currZ);
            
            
            //  Copy Visible values from local array to Eon structures.
            Trace.WriteLine("Sending raster");
            demHelper.ProcessVertexWindow2D(0, 0, rasterWidth, rasterHeight, delegate(int rasterIndex, int[] rasterTileOfs, int windowIndexOfs, int[] windowOfs, int spanSize)
            {
                int windowOfsX = windowOfs[0];
                int windowOfsY = windowOfs[1];

                for (int i = 0; i < spanSize; ++i)
                {
                    
                    //if (visibleArrayInt[windowOfsY, windowOfsX + i] >= 1)
                    //{
                    //    demVertexTable[rasterIndex + i].Visible = true;
                    //}
                    //else
                    //{
                     //   demVertexTable[rasterIndex + i].Visible = false;
                    //}
                    demVertexTable[rasterIndex + i].VisibleInt = visibleArrayInt[windowOfsY, windowOfsX + i];
                }
            });
            Trace.WriteLine("Finished sending raster");
            System.GC.Collect();
            return application.InputDatasets[0];
        }

        #region Private methods.

        

        private unsafe void callDDA_GPU(int currX, int currY, int currZ)
        {
            
            stopwatch.Start();
            //set start point as visible
            visibleArrayInt[currY, currX] = 1;
            int destX, destY;



            // Precaculate the 8 compass points fro use in losArray
            preCalculateDDA(currX, currY, currZ, currX, rasterHeight - 1);
            preCalculateDDA(currX, currY, currZ, currX, 0);

            preCalculateDDA(currX, currY, currZ, 0, currY);
            preCalculateDDA(currX, currY, currZ, rasterWidth -1, currY);


            //NW
            destX = currX - (rasterHeight - currY);
            destY = rasterHeight - 1;
            if (destX <= 0)
            {
                destY = rasterHeight + destX - 1; 
                destX = 0;
            }
            preCalculateDDA(currX, currY, currZ, destX , destY);

            //NE
            destX = currX + (rasterHeight - currY);
            destY = rasterHeight - 1;
            if (destX >= rasterWidth - 1)
            {
                destY = rasterHeight - 1 - (destX - rasterWidth - 1);
                destX = rasterWidth - 1;
            }
            preCalculateDDA(currX, currY, currZ, destX, destY);


            //SE
            destX = currX + (rasterHeight - (rasterHeight - currY));
            destY = 0;
            if (destX >= rasterWidth - 1)
            {
                destY =  (destX - rasterWidth -1);
                destX = rasterWidth - 1;
            }
            preCalculateDDA(currX, currY, currZ, destX, destY);


            //SW
            destX = currX - (rasterHeight - (rasterHeight - currY));
            destY = 0;
            if (destX <= 0)
            {
                destY = rasterHeight - (rasterHeight + destX - 1);
                destX = 0;
            }
            preCalculateDDA(currX, currY, currZ, destX, destY);


            visibleArrayInt[currY - 1, currX] = 1;
            visibleArrayInt[currY + 1, currX] = 1;
            visibleArrayInt[currY + 1, currX + 1] = 1;
            visibleArrayInt[currY, currX + 1] = 1;
            visibleArrayInt[currY - 1, currX + 1] = 1;
            visibleArrayInt[currY , currX - 1] = 1;
            visibleArrayInt[currY + 1, currX - 1] = 1;
            visibleArrayInt[currY - 1, currX - 1] = 1;



            


            fixed (int* visibleArrayPt = &visibleArrayInt[0, 0])
            fixed (float* zArrayPt = &zArrayFloat[0, 0])
            fixed (float* losArrayPt = &losArray[0, 0])

            staging(zArrayPt, zArray.GetLength(1), zArray.GetLength(0), 
                visibleArrayPt, visibleArrayInt.GetLength(1), visibleArrayInt.GetLength(0),
                 currX, currY, currZ, rasterWidth, rasterHeight, losArrayPt);

            stopwatch.Stop();
            Trace.WriteLine("Time elapsed DDA_GPU: " + stopwatch.Elapsed);

            
        }

        private void preCalculateDDA(int focalX, int focalY, int focalZ, int destinationX, int destinationY)
        {

            int destX = destinationX;
            int destY = destinationY;

            //Values for stepping through the line
            int dx = destX - (int)focalX;
            int dy = destY - (int)focalY;
            int steps;
            float xIncrement, yIncrement;
            float x = (int)focalX;
            float y = (int)focalY;
            float prevX = x;
            float prevY = y;

            //previously highest LOS
            float highest = -999;


            //Determine whether steps should be in the x or y axis
            if (Math.Abs(dx) > Math.Abs(dy))
            {
                steps = Math.Abs(dx);
            }

            else
            {
                steps = Math.Abs(dy);
            }


            xIncrement = dx / (float)steps;
            yIncrement = dy / (float)steps;
            
            ///////////////////
            //first point is visible sshouldn't do this everytime!!!!! Move out of line code
            ////////////////////////////
            visibleArray[(int)Math.Round(y), (int)Math.Round(x)] = true;
            losArray[(int)Math.Round(y), (int)Math.Round(x)] = -999;

            //traverse through the line step by step
            for (int k = 0; k < steps; k++)
            {
                //move the current check point
                x += xIncrement;
                y += yIncrement;

                //distance to the check point, snapped to whole values 
                float dist = (float)Math.Sqrt(((int)Math.Round(x) - focalX) * ((int)Math.Round(x) - focalX) + ((int)Math.Round(y) - focalY) * ((int)Math.Round(y) - focalY));

                //Elevation to check point
                float elev = (float)(zArray[(int)Math.Round(y), (int)Math.Round(x)] - focalZ) / dist;

                
                //elevation check
                if (elev >= highest)
                {
                    visibleArrayInt[(int)Math.Round(y), (int)Math.Round(x)] = 2;
                    highest = elev;
                    losArray[(int)Math.Round(y), (int)Math.Round(x)] = elev;
                }
                else
                {
                    losArray[(int)Math.Round(y), (int)Math.Round(x)] = highest;
                }

            }

        }

        #region CPU ALGORITHMS DON'T USE - XDRAW IS OLD AND INCOMPLETE

        private void callXdraw(int ring, int currX, int currY, int currZ)
        {
            stopwatch.Start();
            //Begin calculating each ring
            for (int r = 0; r < ring; r += 1)
            {

                //calculate one ring on the octant plane
                 calculateOctants(r, currX, currY, currZ);

            }
            stopwatch.Stop();
            Trace.WriteLine("Time elapsed XDRAW: " + stopwatch.Elapsed);
        }

        private void callDDA(int currX, int currY, int currZ)
        {
            
            stopwatch.Start();
            int x = 0;
            int y = 0;
            for (x = 0; x < rasterWidth; x++)
            {
                calculateDDA(currX, currY, currZ, x, y);
            }


            for (y = 0; y < rasterHeight; y++)
            {
                calculateDDA(currX, currY, currZ, x - 1, y);

            }

            x = 0;
            for (y = 0; y < rasterHeight; y++)
            {
                calculateDDA(currX, currY, currZ, x, y);
            }

            x = 0;
            y = rasterHeight - 1;
            for (x = 0; x < rasterWidth; x++)
            {
                calculateDDA(currX, currY, currZ, x, y);
            }
            stopwatch.Stop();
            Trace.WriteLine("Time elapsed DDA: " + stopwatch.Elapsed);

        }

        private void callR3(int currX, int currY, int currZ)
        {

            stopwatch.Start();
            int x = 0;
            int y = 0;
            for (x = 0; x < rasterWidth; x++)
            {
                calculateR3(currX, currY, currZ, x, y);
            }


            for (y = 0; y < rasterHeight; y++)
            {
                calculateR3(currX, currY, currZ, x - 1, y);

            }

            x = 0;
            for (y = 0; y < rasterHeight; y++)
            {
                calculateR3(currX, currY, currZ, x, y);
            }

            x = 0;
            y = rasterHeight - 1;
            for (x = 0; x < rasterWidth; x++)
            {
                calculateR3(currX, currY, currZ, x, y);
            }
            stopwatch.Stop();
            Trace.WriteLine("Time elapsed R3: " + stopwatch.Elapsed);
        }

        private void callR2(int currX, int currY, int currZ)
        {

            stopwatch.Start();
            int x = 0;
            int y = 0;
            for (x = 0; x < rasterWidth; x++)
            {
                calculateR2(currX, currY, currZ, x, y);
            }


            for (y = 0; y < rasterHeight; y++)
            {
                calculateR2(currX, currY, currZ, x - 1, y);

            }

            x = 0;
            for (y = 0; y < rasterHeight; y++)
            {
                calculateR2(currX, currY, currZ, x, y);
            }

            x = 0;
            y = rasterHeight - 1;
            for (x = 0; x < rasterWidth; x++)
            {
                calculateR2(currX, currY, currZ, x, y);
            }
            stopwatch.Stop();
            Trace.WriteLine("Time elapsed R2: " + stopwatch.Elapsed);
        }

        private void calculateDDA(int focalX, int focalY, int focalZ, int destinationX, int destinationY)
        {

            int destX = destinationX;
            int destY = destinationY;

            //Values for stepping through the line
            int dx = destX - (int)focalX;
            int dy = destY - (int)focalY;
            int steps;
            float xIncrement, yIncrement;
            float x = (int)focalX;
            float y = (int)focalY;
            float prevX = x;
            float prevY = y;

            //previously highest LOS
            double highest = -999.0;


            //Determine whether steps should be in the x or y axis
            if (Math.Abs(dx) > Math.Abs(dy))
            {
                steps = Math.Abs(dx);
            }

            else
            {
                steps = Math.Abs(dy);
            }


            xIncrement = dx / (float)steps;
            yIncrement = dy / (float)steps;

            //first point is visible
            visibleArray[(int)Math.Round(y), (int)Math.Round(x)] = true;

            //Check distance to destiantion point
            double finalDist = Math.Sqrt((destX - focalX) * (destX - focalX) + (destY - focalY) * (destY - focalY));

            //Check elevation to destination point
            double finalElev = (zArray[(int)destY, (int)destX] - focalZ) / finalDist;
            finalElev += 90;//rotate the angle of the elevation calculations away fro -0, 0 to 90 degrees

            //traverse through the line step by step
            for (int k = 0; k < steps; k++)
            {
                //move the current check point
                x += xIncrement;
                y += yIncrement;

                    //distance to the check point, snapped to whole values 
                    double dist = Math.Sqrt(((int)Math.Round(x) - focalX) * ((int)Math.Round(x) - focalX) + ((int)Math.Round(y) - focalY) * ((int)Math.Round(y) - focalY));

                    //Elevation to check point
                    double elev = (zArray[(int)y, (int)x] - focalZ) / dist;

                    //elevation check
                    if (elev <= finalElev && elev >= highest)
                    {
                        visibleArray[(int)Math.Round(y), (int)Math.Round(x)] = true;
                        highest = elev;
                        visitedArray[(int)Math.Round(y), (int)Math.Round(x)] = true;
                    }

                
            }

        }



        //N.B. GET RID OF ALL THE MATH.ROUNDS --HELLA SLOW
        private void calculateR3(int focalX, int focalY, int focalZ, int destinationX, int destinationY)
        {

            int destX = destinationX;
            int destY = destinationY;


            int dx = destX - (int)focalX;
            int dy = destY - (int)focalY;
            int steps;
            float xIncrement, yIncrement;
            float x = (int)focalX;
            float y = (int)focalY;
            float prevX = x;
            float prevY = y;
            double highest = -999.0;



            if (Math.Abs(dx) > Math.Abs(dy))
            {
                steps = Math.Abs(dx);
            }

            else
            {
                steps = Math.Abs(dy);
            }

            xIncrement = dx / (float)steps;
            yIncrement = dy / (float)steps;
            //All same as Bresenham


            //first point is visible
            visibleArray[(int)Math.Round(y), (int)Math.Round(x)] = true;

            //calculate distance to the final check point
            double finalDist = Math.Sqrt((destX - focalX) * (destX - focalX) + (destY - focalY) * (destY - focalY));

            //calculate elevation to the final check point
            double finalElev = (zArray[(int)destY, (int)destX] - focalZ) / finalDist;
            finalElev += 90;//rotate the angle of the elevation calculations away fro -0, 0 to 90 degrees

            //Step through the line
            for (int k = 0; k < steps; k++)
            {
                prevX = x;
                prevY = y;
                x += xIncrement;
                y += yIncrement;

                //Delta between the two points surrounding the ray
                double diffX = x - Math.Round(x);
                double diffY = y - Math.Round(y);

                //grab the snapped height closest to the ray
                double lerpHeight = zArray[(int)Math.Round(y), (int)Math.Round(x)];

                //used to store the height of the closest neighbour
                double nextHeight;

                //Check to see if any of the values will exceed the boundaries of the array
                //If so, just use the snapped lerpHeight instead
                if (x > 1 && x < rasterWidth && y > 1 && y < rasterHeight - 1)
                {
                    //if the deltaX is negative, check x + 1
                    if (diffX < 0)
                    {
                        //grab the nextHeight
                        nextHeight = zArray[(int)y, (int)x + 1];
                        //interpolated height is original heights + difference in heights * delta 
                        lerpHeight = lerpHeight + ((nextHeight - lerpHeight) * diffX);
                    }
                    //if the deltaX is positive, check x -1
                    if (diffX > 0)
                    {
                        //grab the nextHeight
                        nextHeight = zArray[(int)y, (int)x - 1];
                        //interpolated height is original heights + difference in heights * delta 
                        lerpHeight = lerpHeight + ((nextHeight - lerpHeight) * diffX);
                    }
                    //if the deltaY is negative, check y + 1
                    if (diffY < 0)
                    {
                        //grab the nextHeight
                        nextHeight = zArray[(int)y + 1, (int)x];
                        //interpolated height is original heights + difference in heights * delta 
                        lerpHeight = lerpHeight + ((nextHeight - lerpHeight) * diffY);
                    }
                    //if the deltaY is positive, check y - 1
                    if (diffY > 0)
                    {
                        //grab the nextHeight
                        nextHeight = zArray[(int)y - 1, (int)x];
                        //interpolated height is original heights + difference in heights * delta 
                        lerpHeight = lerpHeight + ((nextHeight - lerpHeight) * diffY);
                    }
                }


                //calculate distance to the lerped x,y point
                double dist = Math.Sqrt(((int)Math.Round(x) - focalX) * ((int)Math.Round(x) - focalX) + ((int)Math.Round(y) - focalY) * ((int)Math.Round(y) - focalY));

                //calculate the elevation at lerped point
                double elev = (lerpHeight - focalZ) / dist;

                //DO the sightline check
                if (elev <= finalElev && elev >= highest)
                {
                    visibleArray[(int)Math.Round(y), (int)Math.Round(x)] = true;
                    highest = elev;
                }

                visitedArray[(int)Math.Round(y), (int)Math.Round(x)] = true;


            }
        }

        //N.B. does not acutally use Bresenham, uses DDA instead
        private void calculateR2(int focalX, int focalY, int focalZ, int destinationX, int destinationY)
        {

            int destX = destinationX;
            int destY = destinationY;

            int guessedX = 0;
            int guessedY = 0;
            int dx = destX - (int)focalX;
            int dy = destY - (int)focalY;
            int steps;
            float xIncrement, yIncrement;
            float x = (int)focalX;
            float y = (int)focalY;
            float prevX = x;
            float prevY = y;
            double highest = -999.0;



            if (Math.Abs(dx) > Math.Abs(dy))
            {
                steps = Math.Abs(dx);
            }

            else
            {
                steps = Math.Abs(dy);
            }

            xIncrement = dx / (float)steps;
            yIncrement = dy / (float)steps;
            //All same as Bresenham


            //first point is visible
            visibleArray[(int)Math.Round(y), (int)Math.Round(x)] = true;

            //calculate distance to the final check point
            double finalDist = Math.Sqrt((destX - focalX) * (destX - focalX) + (destY - focalY) * (destY - focalY));

            //calculate elevation to the final check point
            double finalElev = (zArray[(int)destY, (int)destX] - focalZ) / finalDist;
            finalElev += 90;//rotate the angle of the elevation calculations away fro -0, 0 to 90 degrees


            //Step through the line
            for (int k = 0; k < steps; k++)
            {
                prevX = x;
                prevY = y;
                x += xIncrement;
                y += yIncrement;
                if (visitedArray[(int)y, (int)x] == false)
                {

                    //Delta between the two points surrounding the ray
                    double diffX = x - Math.Round(x);
                    double diffY = y - Math.Round(y);



                    //grab the snapped height closest to the ray
                    double lerpHeight = zArray[(int)y, (int)x];

                    //used to store the height of the closest neighbour
                    double nextHeight;

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
                            nextHeight = zArray[guessedY, guessedX];
                            //interpolated height is original heights + difference in heights * delta 
                            lerpHeight = lerpHeight + ((nextHeight - lerpHeight) * diffX);
                        }
                        //if the deltaX is positive, check x -1
                        if (diffX > 0)
                        {
                            guessedX = (int)x - 1;
                            guessedY = (int)y;
                            //grab the nextHeight
                            nextHeight = zArray[guessedY, guessedX];
                            //interpolated height is original heights + difference in heights * delta 
                            lerpHeight = lerpHeight + ((nextHeight - lerpHeight) * diffX);
                        }
                        //if the deltaY is negative, check y + 1
                        if (diffY < 0)
                        {
                            guessedX = (int)x;
                            guessedY = (int)y + 1;
                            //grab the nextHeight
                            nextHeight = zArray[guessedY, guessedX];
                            //interpolated height is original heights + difference in heights * delta 
                            lerpHeight = lerpHeight + ((nextHeight - lerpHeight) * diffY);
                        }
                        //if the deltaY is positive, check y - 1
                        if (diffY > 0)
                        {
                            guessedX = (int)x;
                            guessedY = (int)y - 1;
                            //grab the nextHeight
                            nextHeight = zArray[guessedY, guessedX];
                            //interpolated height is original heights + difference in heights * delta 
                            lerpHeight = lerpHeight + ((nextHeight - lerpHeight) * diffY);
                        }
                    }

                        //calculate distance to the lerped x,y point
                        double dist = Math.Sqrt((x - focalX) * (x - focalX) + (y - focalY) * (y - focalY));

                        //calculate the elevation at lerped point
                        double elev = (lerpHeight - focalZ) / dist;

                        //DO the sightline check
                        if (elev <= finalElev && elev >= highest)
                        {
                            visibleArray[(int)Math.Round(y), (int)Math.Round(x)] = true;
                            visibleArray[guessedY, guessedX] = true;
                            highest = elev;
                        }
                        visitedArray[guessedY, guessedX] = true;
                        visitedArray[(int)Math.Round(y), (int)Math.Round(x)] = true;
                    


                }
            }
        }

        //Method to calculate the 8 points of one ring in the compass directions
        private void calculateOctants(int ring, double currX, double currY, double currZ)
        {

            //The octant offsets in X, Y format :p
            int[] offsets = { 0 , 1 , //S
                                1 , 1 , //SE 
                                1 , 0 , //E
                                1 , -1 , // NE
                                0 , -1 , //N
                                -1 , -1 , //NW
                                -1 , 0 , //W
                                -1 , 1}; //SW


            //Loop through the 8 compass points
            for (int i = 0; i < 16; i += 2)
            {


                //The next point to be tested
                int testX = (int)Math.Round(currX + offsets[i] * ring);
                int testY = (int)Math.Round(currY + offsets[i + 1] * ring);

                //Calculate distance from focal to test point and the determine its elevation
                double dist = Math.Sqrt((testX - currX) * (testX - currX) + (testY - currY) * (testY - currY));

                //Uses the A parallel computing approach to viewshed analysis of large terrain data using graphics processing units elevation formula
                double elev = ((zArray[(int)testY, (int)testX] - currZ) / dist) + 90;

                //Set the losArray plane with the calculated elevations
                losArray[testY, testX] = (float)elev;

                //if the calculated elevation is greater than the inner row of that octant set as visible
                if (elev > prevLOS[i / 2])
                {
                    visibleArray[testY, testX] = true;
                    prevLOS[i / 2] = elev;// update the highestLOS to this point

                    // Trace.WriteLine("   Pixel is visible \n");
                }



                //Update the Octants array with the 8 compass points
                octants[i] = (int)Math.Round(currX + offsets[i] * ring);
                octants[i + 1] = (int)Math.Round(currY + offsets[i + 1] * ring);

            }

            //Begin calculating the points intervening the octant LOSs
            calculateIntervals(ring, currX, currY, currZ);


        }

        //Calculates the points between compass points
        private void calculateIntervals(int j, double currX, double currY, double currZ)
        {

            //Array of positions that move outward as the rings grow larger
            int[] interPositions = new int[16];

            //The two vertices(X1,Y1,X2,Y2 format) through which the LOS passes through
            int[] interOffsets = { -1 , -1 , 0 , -1 , //NNE
                                   -1 , 0 , -1 , -1 , //ENE
                                   -1 , 1 , -1 , 0 , //ESE
                                   0 , 1 , - 1 , 1 , //SSE
                                   1 , 1 , 0 , 1 , // SSW
                                   1 , 0 , 1 , 1 , //WSW
                                   1 , -1 , 1 , 0 , // WNW
                                   0 , -1 , 1 , -1 };//NNW

            //Calculates the points between the octants for every ring
            for (int ring = 0; ring < j; ring += 1)
            {

                //counter for looping through the interOffsets array
                int interOffsetsCounter = 0;

                //Loop through the 8 intervening octants whilst updating their positions as per their respective ring
                for (int iter = 0; iter < 16; iter += 2)
                {
                    //NNE
                    interPositions[0] = (int)Math.Round(currX + ring);
                    interPositions[1] = octants[1];

                    //ENE
                    interPositions[2] = octants[6];
                    interPositions[3] = (int)Math.Round(currY + ring);

                    //ESE
                    interPositions[4] = octants[6];
                    interPositions[5] = (int)Math.Round(currY - ring);

                    //SSE
                    interPositions[6] = (int)Math.Round(currX + ring);
                    interPositions[7] = octants[9];

                    //SSW
                    interPositions[8] = (int)Math.Round(currX - ring);
                    interPositions[9] = octants[9];

                    //WSW
                    interPositions[10] = octants[12];
                    interPositions[11] = (int)Math.Round(currY - ring);

                    //WNW
                    interPositions[12] = octants[12];
                    interPositions[13] = (int)Math.Round(currY + ring);

                    //NNW
                    interPositions[14] = (int)Math.Round(currX + ring);
                    interPositions[15] = octants[1];

                    //begin calculating the pixel based on it's position and the two previous positions
                    calcInter(currX, currY, currZ, interPositions[iter], interPositions[iter + 1],
                            interOffsets[interOffsetsCounter], interOffsets[interOffsetsCounter + 1],
                            interOffsets[interOffsetsCounter + 2], interOffsets[interOffsetsCounter + 3]);
                    interOffsetsCounter += 4;
                }

                //visibleArray[(int)currY + k - 1, octants[6]] = true;//ENE plane

                //visibleArray[(int)currY - k, octants[6]] = true;//ESE plane

                //visibleArray[octants[9]-2, (int)currX + k] = true;//SSE plane

                //visibleArray[octants[9]-1, (int)currX - k] = true;//SSW plane

                //visibleArray[(int)currY - k, octants[12]] = true;//WSW plane

                //visibleArray[(int)currY + k, octants[12]] = true;//WNW plane

                //visibleArray[octants[1], (int)currX - k + 1] = true;//NNW plane
            }
        }

        private void calcInter(double currX, double currY, double currZ, int interX, int interY, int offsetX1, int offsetY1, int offsetX2, int offsetY2)
        {

            //Update the new offset value based on current position and offset modifier
            offsetX1 += interX;
            offsetY1 += interY;
            offsetX2 += interX;
            offsetY2 += interY;


            //Three phase Max, Min and lerp needed for good accuracy
            double lerpLOS1 = Math.Max(losArray[offsetY1, offsetX1], losArray[offsetY2, offsetX2]);
            double lerpLOS2 = Math.Min(losArray[offsetY1, offsetX1], losArray[offsetY2, offsetX2]);
            double lerpLOS = ((lerpLOS1 + lerpLOS2) / 2);//Insert special fudge sauce [-1,1]


            //Distance and elevations to the test point
            double d = Math.Sqrt((interX - currX) * (interX - currX) + (interY - currY) * (interY - currY));
            double e = ((zArray[(int)interY, (int)interX] - currZ) / d) + 90;
            double q = interX / interY;
            //double lerpLOS = (lerpLOS1 + lerpLOS2 + ((lerpLOS2 + lerpLOS1 * q)/2))/3;

            losArray[interY, interX] = (float)e;

            //Trace.WriteLine("elev = " + e);
            //Trace.WriteLine("Lerp = " + lerpLOS);

            //If the elevation is greater than the interpolated value on the next inner row
            if (e > lerpLOS)
            {
                // Trace.WriteLine("Lerped");
                visibleArray[interY, interX] = true;//NNE plane

            }

        }

        #endregion

        #endregion

        #region AddIn properties

        BindingProperty<DEMRaster> DEMBindProperty = new BindingProperty<DEMRaster> { PropertyName = "DEM Raster", PropertyDescription = "Raster to use for the DEM.", DefaultValue = "", InputSocketIndex = 0 };
        BindingProperty<FocalPointVectors> FocalPointBindProperty = new BindingProperty<FocalPointVectors> { PropertyName = "Focal point vector set group", PropertyDescription = "Vector set group to use for focal point.", DefaultValue = "", InputSocketIndex = 1 };

        #endregion

        #region AddIn description

        public int InputSocketCount
        {
            get { return 2; }
        }

        public string Name
        {
            get { return "Viewshed GPU - New"; }
        }

        public string Category
        {
            get { return "#Aran's Add-Ins"; }
        }

        public string Description
        {
            get { return "Viewshed Analysis"; }
        }

        public string Author
        {
            get { return "Aran Cauchi"; }
        }

        public System.Collections.Generic.IEnumerable<Descriptor> Descriptors
        {
            get
            {
                // To add additional, optional authoring information, remove the "return null" statement and uncomment the
                // required fields.  The descriptors can be reordered and/or repeated as desired, and formatted using RTF markup.
                return null;

                //yield return new DateCreatedDescriptor("(insert date created here)");
                //yield return new DateModifiedDescriptor("(insert date modified here)");
                //yield return new LineBreakDescriptor();

                //yield return new OrganizationDescriptor("(insert organization here)");
                //yield return new URLDescriptor("(insert URL here)");
                //yield return new EmailDescriptor("(insert email address here)");
                //yield return new LineBreakDescriptor();

                //yield return new LicenseDescriptor("(insert license details here)");
                //yield return new CopyrightDescriptor("(insert copyright notice here)");
                //yield return new ReferencesDescriptor("(insert reference here)");
                //yield return new LineBreakDescriptor();

                //yield return new UserDescriptor("(insert additional heading here)", "(insert additional descriptor text here)");
            }
        }

        #endregion

    }

}
