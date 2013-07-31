
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
            int visibleArrayX, int visibleArrayY, int currX, int currY, int currZ, int rasterWidth, int rasterHeight, float* losArrayPt, int g);



        //Array of heights for each pixel
        double[,] zArray;
        float[,] zArrayFloat;

        //Array for the XDraw LOS values for each pixel
        float[,] losArray;

        //Array of visible pixels to be parsed out
        bool[,] visibleArray;
        int[,] visibleArrayInt;
        int[,] visibleArrayCPU;

        //Array of vertices which have been visited
        bool[,] visitedArray;

        //Array of previous Lines of Sights for the Octants calculation
        double[] prevLOS = { -999, -999, -999, -999, -999, -999, -999, -999 };

        int[] octants = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

        int rasterWidth, rasterHeight;
        int globalCurrX, globalCurrY, globalCurrZ;

        Stopwatch stopwatch;

        //String holding the viewshed type used during trace events
        String viewshedType;


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
            //Thread t = new Thread(callDDA);          // Kick off a new thread




            ContinuousRasterHelper<DEMRasterVertex> demHelper = new ContinuousRasterHelper<DEMRasterVertex>(demRaster);
            ITable<DEMRasterVertex> demVertexTable = demRaster.VertexTable;

            rasterWidth = demRaster.GetVertexIndexCount(0);
            rasterHeight = demRaster.GetVertexIndexCount(1);

            zArray = new double[rasterHeight, rasterWidth];
            zArrayFloat = new float[rasterHeight, rasterWidth];
            visibleArray = new bool[rasterHeight, rasterWidth];
            visibleArrayInt = new int[rasterHeight, rasterWidth];
            visibleArrayCPU = new int[rasterHeight, rasterWidth];
            losArray = new float[rasterHeight, rasterWidth];

            visitedArray = new bool[rasterHeight, rasterWidth];



            //  Make sure that our X and Y values are regular (i.e. that there
            //  is a linear transformation between (X, Y) values and raster
            //  index values.

            IValueList xAttributeList = demVertexTable.GetBoundValueList(vertex => vertex.X);
            IValueList yAttributeList = demVertexTable.GetBoundValueList(vertex => vertex.Y);

            //application.EventGroup.InsertInfoEvent(string.Format("X attribute name is {0}.", xAttributeList.Name));
            //application.EventGroup.InsertInfoEvent(string.Format("X transform is {0}.", demRaster.RegularListCoefficients(xAttributeList) == null ? "null" : "something"));

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
            TraceEvent("Grabbing Raster", application);

            demHelper.ProcessVertexWindow2D(0, 0, rasterWidth, rasterHeight, delegate(int rasterIndex, int[] rasterTileOfs, int windowIndexOfs, int[] windowOfs, int spanSize)
            {
                int windowOfsX = windowOfs[0];
                int windowOfsY = windowOfs[1];

                for (int i = 0; i < spanSize; ++i)
                {
                    //  zArray[windowOfsY, windowOfsX + i] = demVertexTable[rasterIndex + i].Z;
                    zArrayFloat[windowOfsY, windowOfsX + i] = (float)demVertexTable[rasterIndex + i].Z;
                }
            });


            TraceEvent("Finished Grabbing raster", application);
            Trace.WriteLine("Finished Grabbing Raster");


            //Transform world coordinates to local coordinates
            focalX = xInvTransform[0] * focalX + xInvTransform[1] * focalY + xInvTransform[2];
            focalY = yInvTransform[0] * focalY + yInvTransform[1] * focalY + yInvTransform[2];

            //Duplication pretty much
            int currX = (int)Math.Round(focalX);
            globalCurrX = currX;

            int currY = (int)Math.Round(focalY);
            globalCurrY = currY;

            int currZ = (int)Math.Round(focalZ);
            globalCurrZ = currZ;

            stopwatch.Start();


            //t.Start();

           // callDDA();
            //callR3();
            //callR2();
            
            callGPU(currX, currY, currZ, "R3");

            //t.Join();//needs join as the code will send back results without it
            stopwatch.Stop();
            TraceEvent("Total Time elapsed using" + viewshedType + " : " + stopwatch.Elapsed, application);
            Trace.WriteLine("Total Time elapsed using " + viewshedType + " : " + stopwatch.Elapsed);

            //  Copy Visible values from local array to Eon structures.
            TraceEvent("Sending raster", application);
            Trace.WriteLine("Sending raster");

            demHelper.ProcessVertexWindow2D(0, 0, rasterWidth, rasterHeight, delegate(int rasterIndex, int[] rasterTileOfs, int windowIndexOfs, int[] windowOfs, int spanSize)
            {
                int windowOfsX = windowOfs[0];
                int windowOfsY = windowOfs[1];

                for (int i = 0; i < spanSize; ++i)
                {

                    demVertexTable[rasterIndex + i].VisibleInt = visibleArrayCPU[windowOfsY, windowOfsX + i] + visibleArrayInt[windowOfsY, windowOfsX + i];

                }
            });
            TraceEvent("Finished Sending Raster", application);
            Trace.WriteLine("Finished Sending Raster");

            return application.InputDatasets[0];
        }

        #region Private methods.





        private unsafe void callGPU(int currX, int currY, int currZ, string gpuType)
        {
            //which gpu option to choose
            int g = 1;
            //set start point as visible
            visibleArrayInt[currY, currX] = 1;

            //Determine which GPU method to run
            if (gpuType == "XDRAW")
            {
                int destX, destY;
                g = 1;
                viewshedType = " GPU - XDRAW ";

                // Precaculate the 8 compass points for use in losArray
                preCalculateDDA(currX, currY, currZ, currX, rasterHeight - 1);
                preCalculateDDA(currX, currY, currZ, currX, 0);

                preCalculateDDA(currX, currY, currZ, 0, currY);
                preCalculateDDA(currX, currY, currZ, rasterWidth - 1, currY);


                //NW
                destX = currX - (rasterHeight - currY);
                destY = rasterHeight - 1;
                if (destX <= 0)
                {
                    destY = rasterHeight + destX - 1;
                    destX = 0;
                }
                preCalculateDDA(currX, currY, currZ, destX, destY);



                //SE
                destX = currX + (rasterHeight - (rasterHeight - currY));
                destY = 0;
                if (destX >= rasterWidth - 1)
                {
                    destY = (destX - rasterWidth - 1);
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
                visibleArrayInt[currY, currX - 1] = 1;
                visibleArrayInt[currY + 1, currX - 1] = 1;
                visibleArrayInt[currY - 1, currX - 1] = 1;
            }
            else if (gpuType == "SDRAW")
            {
                int destX, destY;
                g = 2;
                viewshedType = " GPU - SDRAW";

                // Precaculate the 8 compass points for use in losArray
                preCalculateDDA(currX, currY, currZ, currX, rasterHeight - 1);
                preCalculateDDA(currX, currY, currZ, currX, 0);

                preCalculateDDA(currX, currY, currZ, 0, currY);
                preCalculateDDA(currX, currY, currZ, rasterWidth - 1, currY);


                //NW
                destX = currX - (rasterHeight - currY);
                destY = rasterHeight - 1;
                if (destX <= 0)
                {
                    destY = rasterHeight + destX - 1;
                    destX = 0;
                }
                preCalculateDDA(currX, currY, currZ, destX, destY);



                //SE
                destX = currX + (rasterHeight - (rasterHeight - currY));
                destY = 0;
                if (destX >= rasterWidth - 1)
                {
                    destY = (destX - rasterWidth - 1);
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
                visibleArrayInt[currY, currX - 1] = 1;
                visibleArrayInt[currY + 1, currX - 1] = 1;
                visibleArrayInt[currY - 1, currX - 1] = 1;


            }
            else if (gpuType == "DDA")
            {
                g = 3;
                viewshedType = " GPU - DDA";
            }
            else if (gpuType == "R3")
            {
                g = 4;
                viewshedType = " GPU - R3";
            }


            //Start Timing
            stopwatch.Start();
            
            fixed (int* visibleArrayPt = &visibleArrayInt[0, 0])
            fixed (float* zArrayPt = &zArrayFloat[0, 0])
            fixed (float* losArrayPt = &losArray[0, 0])
                staging(zArrayPt, zArray.GetLength(1), zArray.GetLength(0),
                    visibleArrayPt, visibleArrayInt.GetLength(1), visibleArrayInt.GetLength(0),
                    currX, currY, currZ, rasterWidth, rasterHeight, losArrayPt, g);

            //Stop Timing
            stopwatch.Stop();

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
                if (elev > highest)
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



        private void callDDA()
        {
            viewshedType = "CPU - DDA";
            stopwatch.Start();
            int x = 0;
            int y = 0;
            for (x = 0; x < rasterWidth; x++)
            {
                calculateDDA(globalCurrX, globalCurrY, globalCurrZ, x, y);
            }


            for (y = 0; y < rasterHeight; y++)
            {
                calculateDDA(globalCurrX, globalCurrY, globalCurrZ, x - 1, y);

            }

            x = 0;
            for (y = 0; y < rasterHeight; y++)
            {
                calculateDDA(globalCurrX, globalCurrY, globalCurrZ, x, y);
            }

            x = 0;
            y = rasterHeight - 1;
            for (x = 0; x < rasterWidth; x++)
            {
                calculateDDA(globalCurrX, globalCurrY, globalCurrZ, x, y);
            }
            stopwatch.Stop();
            Trace.WriteLine("Time elapsed DDA: " + stopwatch.Elapsed);

        }

        private void callR3()
        {
            viewshedType = "CPU - R3";
            stopwatch.Start();
            int x = 0;
            int y = 0;

            for (x = 0; x < rasterWidth; x++)
            {
                calculateR3(globalCurrX, globalCurrY, globalCurrZ, x, y);
            }


            for (y = 0; y < rasterHeight; y++)
            {
                calculateR3(globalCurrX, globalCurrY, globalCurrZ, x - 1, y);

            }

            x = 0;
            for (y = 0; y < rasterHeight; y++)
            {
                calculateR3(globalCurrX, globalCurrY, globalCurrZ, x, y);
            }

            x = 0;
            y = rasterHeight - 1;
            for (x = 0; x < rasterWidth; x++)
            {
                calculateR3(globalCurrX, globalCurrY, globalCurrZ, x, y);
            }
            stopwatch.Stop();
            Trace.WriteLine("Time elapsed R3: " + stopwatch.Elapsed);
        }

        private void callR2()
        {
            viewshedType = "CPU - R2";
            stopwatch.Start();
            int x = 0;
            int y = 0;
            for (x = 0; x < rasterWidth; x++)
            {
                calculateR2(globalCurrX, globalCurrY, globalCurrZ, x, y);
            }

            for (y = 0; y < rasterHeight; y++)
            {
                calculateR2(globalCurrX, globalCurrY, globalCurrZ, x - 1, y);

            }

            x = 0;
            for (y = 0; y < rasterHeight; y++)
            {
                calculateR2(globalCurrX, globalCurrY, globalCurrZ, x, y);
            }

            x = 0;
            y = rasterHeight - 1;
            for (x = 0; x < rasterWidth; x++)
            {
                calculateR2(globalCurrX, globalCurrY, globalCurrZ, x, y);
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

            //first point is visible
            visibleArrayInt[(int)y, (int)x] = 1;

            //traverse through the line step by step
            for (int k = 0; k < steps; k++)
            {

                //move the current check point
                x += xIncrement;
                y += yIncrement;

                //distance to the check point, snapped to whole values 
                float dist = (float)Math.Sqrt((x - focalX) * (x - focalX) + (y - focalY) * (y - focalY));

                //Elevation to check point
                float elev = (zArrayFloat[(int)y, (int)x] - focalZ) / dist;

                //elevation check
                if (elev > highest)
                {
                    visibleArrayCPU[(int)y, (int)x] = 1;
                    highest = elev;
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
            float highest = -999;



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
            visibleArrayInt[(int)y, (int)x] = 1;



            //Step through the line
            for (int k = 0; k < steps; k++)
            {
                prevX = x;
                prevY = y;
                x += xIncrement;
                y += yIncrement;

                //Delta between the two points surrounding the ray
                float diffX = x - (float)Math.Round(x);
                float diffY = y - (float)Math.Round(y);

                //grab the snapped height closest to the ray
                float lerpHeight = zArrayFloat[(int)Math.Round(y), (int)Math.Round(x)];

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
                        nextHeight = zArrayFloat[(int)y, (int)x + 1];
                        //interpolated height is original heights + difference in heights * delta 
                        lerpHeight = lerpHeight + ((nextHeight - lerpHeight) * diffX);
                    }
                    //if the deltaX is positive, check x -1
                    if (diffX > 0)
                    {
                        //grab the nextHeight
                        nextHeight = zArrayFloat[(int)y, (int)x - 1];
                        //interpolated height is original heights + difference in heights * delta 
                        lerpHeight = lerpHeight + ((nextHeight - lerpHeight) * diffX);
                    }
                    //if the deltaY is negative, check y + 1
                    if (diffY < 0)
                    {
                        //grab the nextHeight
                        nextHeight = zArrayFloat[(int)y + 1, (int)x];
                        //interpolated height is original heights + difference in heights * delta 
                        lerpHeight = lerpHeight + ((nextHeight - lerpHeight) * diffY);
                    }
                    //if the deltaY is positive, check y - 1
                    if (diffY > 0)
                    {
                        //grab the nextHeight
                        nextHeight = zArrayFloat[(int)y - 1, (int)x];
                        //interpolated height is original heights + difference in heights * delta 
                        lerpHeight = lerpHeight + ((nextHeight - lerpHeight) * diffY);
                    }
                }


                //calculate distance to the lerped x,y point
                float dist = (float)Math.Sqrt(((int)Math.Round(x) - focalX) * ((int)Math.Round(x) - focalX) + ((int)Math.Round(y) - focalY) * ((int)Math.Round(y) - focalY));

                //calculate the elevation at lerped point
                float elev = (lerpHeight - focalZ) / dist;

                //DO the sightline check
                if (elev > highest)
                {
                    visibleArrayCPU[(int)Math.Round(y), (int)Math.Round(x)] = 1;
                    highest = elev;
                }

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
            float highest = -999;



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
            visibleArrayInt[(int)Math.Round(y), (int)Math.Round(x)] = 1;

            //calculate distance to the final check point
            float finalDist = (float)Math.Sqrt((destX - focalX) * (destX - focalX) + (destY - focalY) * (destY - focalY));


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
                    float diffX = x - (float)Math.Round(x);
                    float diffY = y - (float)Math.Round(y);



                    //grab the snapped height closest to the ray
                    float lerpHeight = zArrayFloat[(int)y, (int)x];

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
                            nextHeight = zArrayFloat[guessedY, guessedX];
                            //interpolated height is original heights + difference in heights * delta 
                            lerpHeight = lerpHeight + ((nextHeight - lerpHeight) * diffX);
                        }
                        //if the deltaX is positive, check x -1
                        if (diffX > 0)
                        {
                            guessedX = (int)x - 1;
                            guessedY = (int)y;
                            //grab the nextHeight
                            nextHeight = zArrayFloat[guessedY, guessedX];
                            //interpolated height is original heights + difference in heights * delta 
                            lerpHeight = lerpHeight + ((nextHeight - lerpHeight) * diffX);
                        }
                        //if the deltaY is negative, check y + 1
                        if (diffY < 0)
                        {
                            guessedX = (int)x;
                            guessedY = (int)y + 1;
                            //grab the nextHeight
                            nextHeight = zArrayFloat[guessedY, guessedX];
                            //interpolated height is original heights + difference in heights * delta 
                            lerpHeight = lerpHeight + ((nextHeight - lerpHeight) * diffY);
                        }
                        //if the deltaY is positive, check y - 1
                        if (diffY > 0)
                        {
                            guessedX = (int)x;
                            guessedY = (int)y - 1;
                            //grab the nextHeight
                            nextHeight = zArrayFloat[guessedY, guessedX];
                            //interpolated height is original heights + difference in heights * delta 
                            lerpHeight = lerpHeight + ((nextHeight - lerpHeight) * diffY);
                        }
                    }

                    //calculate distance to the lerped x,y point
                    float dist = (float)Math.Sqrt((x - focalX) * (x - focalX) + (y - focalY) * (y - focalY));

                    //calculate the elevation at lerped point
                    float elev = (lerpHeight - focalZ) / dist;

                    //DO the sightline check
                    if (elev > highest)
                    {
                        visibleArrayCPU[(int)Math.Round(y), (int)Math.Round(x)] = 1;
                        //visibleArrayCPU[guessedY, guessedX] = 1;
                        highest = elev;
                    }
                    // visitedArray[guessedY, guessedX] = true;
                    visitedArray[(int)Math.Round(y), (int)Math.Round(x)] = true;



                }
            }
        }

        //Trace an Eonfusion event
        private void TraceEvent(String s, IApplication a)
        {
            a.EventGroup.InsertInfoEvent(string.Format(s));
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
            get { return "Viewshed GPU Repository"; }
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
