using MathSupport;
using OpenTK;
using Rendering;
using System;
using Utilities;

namespace MichalIvicic
{
  public static class Lightning
  {
    public static ISceneNode CreateLightning(Vector3d[] points, double r, double innerR = 0.03)
    {
      CSGInnerNode ltn = new CSGInnerNode( SetOperation.Union );

      RecursionFunction del = (Intersection intersection, Vector3d dir, double importance, out RayRecursion rr) =>
      {
        double direct = 1.0 - intersection.TextureCoord.X;
        if (direct >= (1 - innerR))
          direct = 1;
        else
        {
          direct /= 1 - innerR;
          direct *= 0.9;
        }
        direct = Math.Pow(direct, 8.0);

        rr = new RayRecursion(
          new double[] {direct, direct, direct},
          new RayRecursion.RayContribution(intersection, dir, importance));

        return 144L;
      };

      foreach (Vector3d point in points)
      {
        SphereFront sphere = new SphereFront();

        sphere.SetAttribute(PropertyName.RECURSION, del);
        sphere.SetAttribute(PropertyName.NO_SHADOW, true);
        ltn.InsertChild(sphere, Matrix4d.Scale(r) * Matrix4d.CreateTranslation(point));
      }

      for (int i = 0; i <= points.Length - 2; i++)
      {
        double dist = Vector3d.Distance(points[i], points[i + 1]);
        ISolid cilinder = new CylinderFront(0, dist);

        cilinder.SetAttribute(PropertyName.RECURSION, del);
        cilinder.SetAttribute(PropertyName.NO_SHADOW, true);

        ltn.InsertChild(cilinder,  Matrix4d.Scale(r, r, 1) * Matrix4d.Rotate(GetRotationBetween(Vector3d.UnitZ, Vector3d.Subtract(points[i+1], points[i]))) *  Matrix4d.CreateTranslation(points[i]));
      }

      return ltn;
    }

    static Quaterniond GetRotationBetween (Vector3d u, Vector3d v)
    {
      //https://stackoverflow.com/a/11741520

      double k_cos_theta = Vector3d.Dot(u, v);
      double k = Math.Sqrt(v.LengthSquared + u.LengthSquared);

      if (k_cos_theta / k == -1)
      {
        // 180 degree rotation around any orthogonal vector
        return new Quaterniond(new Vector3d(-u.Y, u.X, u.Z), 0);
      }

      Vector3d cross = Vector3d.Cross(u, v);
      Quaterniond q = new Quaterniond(cross, k_cos_theta + k);
      return q.Normalized();
    }
  }
}
