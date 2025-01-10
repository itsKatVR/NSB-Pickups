using System;
using System.Collections.Generic;
using UnityEngine;

// TODO: normals are just wrong.
public class CapsuleMeshGenerator
{
    public static Mesh CreateCapsuleMesh(float radius, float height, int segments)
    {
        Mesh mesh = new Mesh();

        segments = segments % 2 != 0 ? segments + 1 : segments;


        // Ensure the height accommodates the hemispheres
        float cylinderHeight = Math.Max(height - 2 * radius, 0);

        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var normals = new List<Vector3>();

        int hemisphereSegments = segments / 2;
        // North
        GenerateHemisphere(vertices, triangles, normals, radius, segments, hemisphereSegments, true, cylinderHeight / 2);
        // South
        GenerateHemisphere(vertices, triangles, normals, radius, segments, hemisphereSegments, false, -(cylinderHeight / 2));
        // Cylinder
        GenerateCylinder(vertices, triangles, normals, radius, cylinderHeight, segments);


        // Assign vertices and triangles to mesh
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.normals = normals.ToArray();
        
        
        // mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.Optimize();

        return mesh;
    }

    private static void GenerateHemisphere(
        List<Vector3> vertices,
        List<int> triangles,
        List<Vector3> normals,
        float radius, int segments, int hemisphereSegments, bool isNorth, float yOffset)
    {
        int baseIndex = vertices.Count;

        for (int lat = 0; lat <= hemisphereSegments; lat++)
        {
            float latAngle = Mathf.PI * 0.5f * (lat / (float)hemisphereSegments) * (isNorth ? 1 : -1);
            float y = radius * Mathf.Sin(latAngle);
            float ringRadius = radius * Mathf.Cos(latAngle);

            for (int lon = 0; lon <= segments; lon++)
            {
                float lonAngle = 2 * Mathf.PI * lon / segments;
                float x = ringRadius * Mathf.Cos(lonAngle);
                float z = ringRadius * Mathf.Sin(lonAngle);

                vertices.Add(new Vector3(x, y + yOffset, z));
                normals.Add(new Vector3(0, isNorth ? 1f : -1f, 0));

                if (lat < hemisphereSegments && lon < segments)
                {
                    int current = baseIndex + lat * (segments + 1) + lon;
                    int next = baseIndex + lat * (segments + 1) + lon + 1;
                    int below = baseIndex + (lat + 1) * (segments + 1) + lon;
                    int belowNext = baseIndex + (lat + 1) * (segments + 1) + lon + 1;

                    if (!isNorth)
                    {
                        triangles.Add(current);
                        triangles.Add(next);
                        triangles.Add(below);
                        triangles.Add(next);
                        triangles.Add(belowNext);
                        triangles.Add(below);
                    }
                    else
                    {
                        triangles.Add(current);
                        triangles.Add(below);
                        triangles.Add(next);
                        triangles.Add(next);
                        triangles.Add(below);
                        triangles.Add(belowNext);
                    }
                }
            }
        }
    }

    private static void GenerateCylinder(
        List<Vector3> vertices,
        List<int> triangles,
        List<Vector3> normals,
        float radius, float height, int segments)
    {
        if (height <= 0) return;

        int baseIndex = vertices.Count;

        // Create top and bottom circles
        for (int i = 0; i <= 1; i++)
        {
            float y = (i == 0 ? 1 : -1) * height / 2;

            for (int lon = 0; lon <= segments; lon++)
            {
                float lonAngle = 2 * Mathf.PI * lon / segments;
                float x = radius * Mathf.Cos(lonAngle);
                float z = radius * Mathf.Sin(lonAngle);

                vertices.Add(new Vector3(x, y, z));
                normals.Add(new Vector3(x, 0, z));
            }
        }

        // Connect top and bottom circles
        for (int lon = 0; lon < segments; lon++)
        {
            int topCurrent = baseIndex + lon;
            int topNext = baseIndex + lon + 1;
            int bottomCurrent = baseIndex + segments + 1 + lon;
            int bottomNext = baseIndex + segments + 1 + lon + 1;

            triangles.Add(topCurrent);
            triangles.Add(topNext);
            triangles.Add(bottomCurrent);

            triangles.Add(bottomNext);
            triangles.Add(bottomCurrent);
            triangles.Add(topNext);
        }
    }
}