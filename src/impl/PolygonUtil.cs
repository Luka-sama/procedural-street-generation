using Godot;
using System.Collections.Generic;

public static class PolygonUtil
{
    public static bool InsidePolygon(Vector2 point, List<Vector2> polygon)
    {
        // Ray-casting algorithm based on
        // http://www.ecse.rpi.edu/Homepages/wrf/Research/Short_Notes/pnpoly.html
        
        if (polygon.Count == 0)
        {
            return false;
        }

        bool inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            double xi = polygon[i].X, yi = polygon[i].Y;
            double xj = polygon[j].X, yj = polygon[j].Y;

            bool intersect = ((yi > point.Y) != (yj > point.Y))
                             && (point.X < (xj - xi) * (point.Y - yi) / (yj - yi) + xi);
            if (intersect)
            {
                inside = !inside;
            }
        }

        return inside;
    }

}