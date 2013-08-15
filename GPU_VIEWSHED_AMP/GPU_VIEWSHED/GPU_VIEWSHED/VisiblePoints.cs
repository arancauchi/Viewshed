using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GPU_VIEWSHED
{
    class VisiblePoints
    {
        private int numPoints;

        //Set the number
        public void setVisiblePoints(int i)
        {
            numPoints += i;
        }

        //retrieve number of points
        public int getVisiblepoints()
        {
            return numPoints;
        }

    }
}
