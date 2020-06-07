using MathSupport;
using OpenTK;
using Rendering;
using System;
using System.Collections.Generic;
using Utilities;
using System.Linq;

namespace MichalIvicic
{
  public static class Lightning
  {
    public static ISceneNode CreateLightning(
      Vector3d begin,
      Vector3d end,
      double r, // radius of the central lightning branch
      int points = 20, // number of points in the central lightning branch
      double nonStraightness = 0.3,
      double glowExp = 4,
      double branches = 7, // number of branches in the central lightning branch
      Random rand = null)
    {
      if (rand == null)
        rand = new Random();

      // generating vertices of the central lightning branch
      List<Vector3d> vertices = new List<Vector3d>();
      vertices.Add(end);
      vertices.AddRange(CreateRecursively(end,begin,points - 2, nonStraightness, rand, (begin - end).Normalized(), out Vector3d outvector));
      vertices.Add(begin);
      CSGInnerNode mainBranch = CreateLightningFromPoints(vertices.ToArray(), r, glowExp);

      if (points <= 2)
        return mainBranch;
      double height = (end - begin).Length;

      // generating other branches
      for (int i = 0; i < branches; i++)
      {
        int index = (int)(points/(branches*1.3)*(i + 1));
        index = points - 1 - index;
        double toEnd = (end - vertices[index]).Length;
        // deciding on the direction of the branch
        Vector3d direction = (-(vertices[index-1] - vertices[index]) - (vertices[index+1] - vertices[index])).Normalized();
        direction = (direction*0.8 + (end - vertices[index]).Normalized()).Normalized();
        direction *= (rand.NextDouble()*0.7 + 0.2) * toEnd;
        // recursive calling
        double sizeQuotient = direction.Length / height;
        mainBranch.InsertChild(CreateLightning(vertices[index], vertices[index] + direction, r * sizeQuotient, (int)(points * sizeQuotient),
          nonStraightness, glowExp, (int)(branches * sizeQuotient / 2), rand), Matrix4d.Identity);
      }

      return mainBranch;
    }

    static List<Vector3d> CreateRecursively(Vector3d begin, Vector3d end, int points, double nonStraightness, Random rand, Vector3d direction, out Vector3d outDirection)
    {
      // generates points between begin and end

      List<Vector3d> vertices = new List<Vector3d>();
      if (points <= 0)
      {
        outDirection = direction;
        return vertices;
      }
      else
      {
        Vector3d midpoint = RandomBetween(begin, end, nonStraightness, rand, direction, out outDirection);
        int inFirst = (points-1)/2;
        int inSecond = (points-1) - inFirst;
        if (rand.NextDouble() > 0.5)
        {
          inSecond = (points - 1) / 2;
          inFirst = (points - 1) - inSecond;
        }


        vertices.AddRange(CreateRecursively(begin, midpoint, inFirst, nonStraightness, rand, outDirection, out outDirection));
        vertices.Add(midpoint);
        vertices.AddRange(CreateRecursively(midpoint, end, inSecond, nonStraightness, rand, outDirection, out outDirection));

        return vertices;
      }
    }

    static Vector3d RandomBetween(Vector3d begin, Vector3d end, double nonStraightness, Random rand, Vector3d direction ,out Vector3d outDirection)
    {
      // direction is the direction vector from point before begin to begin
      // we take this into account so that the generated point won't create a too sharp angle

      double midLength = ((end-begin)/2).Length;
      Vector3d result =  begin + (direction + (end - begin).Normalized()*1.2).Normalized() * midLength * 1.2;

      result = result + new Vector3d(rand.NextDouble(), rand.NextDouble(), rand.NextDouble()).Normalized() * midLength * nonStraightness * rand.NextDouble();

      outDirection = (end - result).Normalized();
      return result;
    }

    static CSGInnerNode CreateLightningFromPoints (Vector3d[] points, double r, double glowExp)
    {
      // all points will be spheres and will be connected by cylinders - those objects will be returned in node
      // all objects are in their centre only white without any other colors from rays affecting them
      // cylinders have a small glow area arround them that can be controlled by glowExp

      CSGInnerNode ltn = new CSGInnerNode( SetOperation.Union );
      double innerR = 0.5;

      RecursionFunction del = (Intersection intersection, Vector3d dir, double importance, out RayRecursion rr) =>
      {
        double direct = 1.0 - intersection.TextureCoord.X;
        if (direct >= (1 - innerR))
          direct = 1;
        else
        {
          direct = 0;
        }

        rr = new RayRecursion(
          new double[] {direct, direct, direct},
          new RayRecursion.RayContribution(intersection, dir, importance));

        return 144L;
      };

      for (int i = 1; i < points.Length - 2; i++)
      {
        SphereFront sphere = new SphereFront();

        sphere.SetAttribute(PropertyName.RECURSION, del);
        sphere.SetAttribute(PropertyName.NO_SHADOW, true);

        ltn.InsertChild(sphere, Matrix4d.Scale(r) * Matrix4d.CreateTranslation(points[i]));
      }

      del = (Intersection intersection, Vector3d dir, double importance, out RayRecursion rr) =>
      {
        double direct = 1.0 - intersection.TextureCoord.X;
        if (direct >= (1 - innerR))
          direct = 1;
        else
        {
          direct /= 1 - innerR;
          direct = Math.Pow(direct, glowExp) /2;
        }

        rr = new RayRecursion(
          new double[] {direct, direct, direct},
          new RayRecursion.RayContribution(intersection, dir, importance));

        return 144L;
      };

      r += 2 * Intersection.RAY_EPSILON;
      for (int i = 0; i <= points.Length - 2; i++)
      {
        double dist = Vector3d.Distance(points[i], points[i + 1]);
        ISolid cilinder = new AdjustedCylinder(0, 1);

        cilinder.SetAttribute(PropertyName.RECURSION, del);
        cilinder.SetAttribute(PropertyName.NO_SHADOW, true);


        ltn.InsertChild(cilinder, Matrix4d.Scale(r, r, dist) * Matrix4d.Rotate(Vector3d.UnitZ + (points[i + 1] - points[i]).Normalized(), MathHelper.Pi) * Matrix4d.CreateTranslation(points[i]));
      }

      return ltn;
    }
  }

  class AdjustedCylinder : Cylinder {

    // CylinderFront wasn't suitable for this, so this is a kind of mix between
    // regular cylinder and CylinderFront

    // Texture coordinates return distance from center line segment on coordinate X
    // Note that CylinderFront returns distance from center axis

    public AdjustedCylinder (in double zMi, in double zMa) : base(zMi, zMa) { }

    struct CylinderData {
      public Vector3d Direction { get; set; }
      public Vector3d OtherIntersection { get; set; }
    }

    public override LinkedList<Intersection> Intersect (Vector3d p0, Vector3d p1)
    {
      var intersections = base.Intersect(p0, p1);
      if(intersections != null && intersections.Count == 2)
      {
        intersections.First.Value.SolidData = new CylinderData { Direction = p1, OtherIntersection = intersections.Last.Value.CoordLocal };
        intersections.Last.Value.SolidData = new CylinderData { Direction = p1, OtherIntersection = intersections.First.Value.CoordLocal };
        if (intersections.First.Value.Enter)
        {
          intersections.Last.Value.T = intersections.First.Value.T + Intersection.SHELL_THICKNESS;
          intersections.Last.Value.CoordLocal = intersections.First.Value.CoordLocal + Intersection.SHELL_THICKNESS * p1;
        }
        else
        {
          intersections.First.Value.T = intersections.Last.Value.T + Intersection.SHELL_THICKNESS;
          intersections.First.Value.CoordLocal = intersections.Last.Value.CoordLocal + Intersection.SHELL_THICKNESS * p1;
        }
      }
      return intersections;
    }

    public override void CompleteIntersection (Intersection inter)
    {
      // Normal vector (not used)
      inter.NormalLocal.Z = 0.0;
      inter.NormalLocal.X = inter.CoordLocal.X;
      inter.NormalLocal.Y = inter.CoordLocal.Y;

      // Texture coordinates encoding distance from the axis.
      if (inter.SolidData is CylinderData data)
      {
        Vector2d dirxy = data.Direction.Xy.Normalized();
        double cosa = Vector2d.Dot(dirxy, -inter.CoordLocal.Xy);
        Vector3d dirxyz = data.Direction.Normalized();
        Vector3d D = inter.CoordLocal + cosa * dirxyz;

        if(D.Z > ZMax || D.Z < ZMin)
        {
          double xyLength = inter.CoordLocal.Xy.Length;
          double xyOuterLength = data.OtherIntersection.Xy.Length;
          inter.TextureCoord.X = Math.Min(xyLength, xyOuterLength);
          inter.TextureCoord.Y = xyLength < xyOuterLength ? data.OtherIntersection.Z : inter.CoordLocal.Z;
        } else
        {
          inter.TextureCoord.X = D.Xy.Length;
          inter.TextureCoord.Y = inter.CoordLocal.Z;
        }
      }
      else
        inter.TextureCoord = Vector2d.Zero;
    }
  }
}
