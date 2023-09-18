using System;
using System.Collections.Generic;
using Godot;

public struct Edge
{
    public Vertex From { get; init; }
    public Vertex To { get; init; }
}

/**
 * Vertex located along any intersection or point along the simplified road polylines 
 */
public class Vertex
{
    public List<Edge> Edges { get; } = new();
    public Vector2 Point { get; }

    public Vertex(Vector2 point, List<Edge> edges = null)
    {
        Point = point;
        if (edges != null)
        {
            Edges = edges;
        }
    }
}

public class Graph
{
    public List<Vertex> Vertices { get; }
    public List<Edge> Edges { get; }

    public Graph()
    {
        Vertices = new();
        Edges = new();
    }

    public static Graph CreateFromStreamlines(List<List<Vector2>> streamlines, bool deleteDangling = false)
    {
        Graph graph = new();
        List<List<Edge>> unprocessedEdges = new();
        var (minPoints, maxPoints) = GetStreamlinesBounding(streamlines);
        
        foreach (var streamline in streamlines)
        {
            List<Edge> streamlineEdges = new();
            unprocessedEdges.Add(streamlineEdges);
            Vertex lastVertex = null;
            foreach (var point in streamline)
            {
                Vertex vertex = new(point);
                graph.Vertices.Add(vertex);
                if (lastVertex != null)
                {
                    streamlineEdges.Add(new() { From = lastVertex, To = vertex });
                }
                lastVertex = vertex;
            }
        }
        
        foreach (var streamlineEdges in unprocessedEdges)
        {
            while (streamlineEdges.Count > 0)
            {
                var edge = streamlineEdges[0];
                graph.ProcessEdge(unprocessedEdges, edge, minPoints, maxPoints, streamlineEdges);
            }
        }

        return graph;
    }

    private static (List<Vector2> minPoints, List<Vector2> maxPoints) GetStreamlinesBounding(List<List<Vector2>> streamlines)
    {
        List<Vector2> minPoints = new(), maxPoints = new();
        foreach (var streamline in streamlines)
        {
            Vector2 minPoint = streamline[0], maxPoint = streamline[0];
            foreach (var point in streamline)
            {
                minPoint.X = Math.Min(minPoint.X, point.X);
                minPoint.Y = Math.Min(minPoint.Y, point.Y);
                maxPoint.X = Math.Max(maxPoint.X, point.X);
                maxPoint.Y = Math.Max(maxPoint.Y, point.Y);
            }
            minPoints.Add(minPoint);
            maxPoints.Add(maxPoint);
        }
        
        return (minPoints, maxPoints);
    }

    private void ProcessEdge(List<List<Edge>> unprocessedEdges, Edge edge,
        List<Vector2> minPoints, List<Vector2> maxPoints, List<Edge> streamlineEdges)
    {
        if (!streamlineEdges.Contains(edge)) return;
        Vector2 from = edge.From.Point, to = edge.To.Point;

        for (var i = 0; i < unprocessedEdges.Count; i++)
        {
            var streamlineEdges2 = unprocessedEdges[i];
            Vector2 minPoint1 = new(Math.Min(from.X, to.X), Math.Min(from.Y, to.Y)),
                    maxPoint1 = new(Math.Max(from.X, to.X), Math.Max(from.Y, to.Y)),
                    minPoint2 = minPoints[i],
                    maxPoint2 = maxPoints[i];
            if (maxPoint1.X < minPoint2.X || maxPoint1.Y < minPoint2.Y ||
                maxPoint2.X < minPoint1.X || maxPoint2.Y < minPoint1.Y || streamlineEdges == streamlineEdges2)
            {
                // Do not check intersections if the streamlines are obviously in different regions or if it is the same streamline
                continue;
            }
            
            foreach (var edge2 in streamlineEdges2)
            {
                Vector2 from2 = edge2.From.Point, to2 = edge2.To.Point;
                var intersectionPoint = Geometry2D.SegmentIntersectsSegment(from, to,from2, to2);
                if (intersectionPoint.VariantType == Variant.Type.Vector2)
                {
                    var point = (Vector2)intersectionPoint;
                    if ((!from.IsEqualApprox(point) && !to.IsEqualApprox(point)) || 
                        (!from2.IsEqualApprox(point) && !to2.IsEqualApprox(point))) {
                        DivideEdges(edge, edge2, point, unprocessedEdges,
                            minPoints, maxPoints, streamlineEdges, streamlineEdges2);
                        return;
                    }
                }
            }
        }

        streamlineEdges.Remove(edge);
        Edges.Add(edge);
        edge.From.Edges.Add(edge);
        edge.To.Edges.Add(edge);
    }

    private void DivideEdges(Edge edge1, Edge edge2, Vector2 intersectionPoint, List<List<Edge>> unprocessedEdges,
        List<Vector2> minPoints, List<Vector2> maxPoints,
        List<Edge> streamlineEdges1, List<Edge> streamlineEdges2)
    {
        Vertex intersection;
        if (edge1.From.Point.IsEqualApprox(intersectionPoint)) intersection = edge1.From;
        else if (edge1.To.Point.IsEqualApprox(intersectionPoint)) intersection = edge1.To;
        else if (edge2.From.Point.IsEqualApprox(intersectionPoint)) intersection = edge2.From;
        else if (edge2.To.Point.IsEqualApprox(intersectionPoint)) intersection = edge2.To;
        else
        {
            intersection = new(intersectionPoint);
            Vertices.Add(intersection);
        }

        var dividedEdges1 = DivideEdge(edge1, intersection, streamlineEdges1);
        var dividedEdges2 = DivideEdge(edge2, intersection, streamlineEdges2);

        foreach (var edge in dividedEdges1) ProcessEdge(unprocessedEdges, edge, minPoints, maxPoints, streamlineEdges1);
        foreach (var edge in dividedEdges2) ProcessEdge(unprocessedEdges, edge, minPoints, maxPoints, streamlineEdges2);
    }

    private static List<Edge> DivideEdge(Edge edge, Vertex intersection, List<Edge> streamlineEdges)
    {
        List<Edge> dividedEdges = new();
        
        if (intersection != edge.From && intersection != edge.To)
        {
            var half1 = edge with { From = intersection };
            var half2 = edge with { To = intersection };
            dividedEdges.Add(half1);
            dividedEdges.Add(half2);
            streamlineEdges.Remove(edge);
            streamlineEdges.Add(half1);
            streamlineEdges.Add(half2);
        } else
        {
            dividedEdges.Add(edge);
        }

        return dividedEdges;
    }
}