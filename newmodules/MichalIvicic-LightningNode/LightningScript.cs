using MichalIvicic;

//////////////////////////////////////////////////
//// Rendering params.

Debug.Assert(scene != null);
Debug.Assert(context != null);

// Override image resolution and supersampling.
context[PropertyName.CTX_WIDTH]  = 1200;
context[PropertyName.CTX_HEIGHT] =  800;

////////////////////////////////////////////////////
//// CSG scene.

CSGInnerNode root = new CSGInnerNode(SetOperation.Union);

root.SetAttribute(PropertyName.REFLECTANCE_MODEL, new PhongModel());
root.SetAttribute(PropertyName.MATERIAL, new PhongMaterial(new double[] {0.6, 0.0, 0.0}, 0.15, 0.8, 0.15, 16));
scene.Intersectable = root;

// Background color.
scene.BackgroundColor = new double[] {0.0, 0.05, 0.07};

// Camera.
scene.Camera = new StaticCamera(new Vector3d(0.7, 3.0, -10.0),
                                new Vector3d(0.0, -0.2, 1.0),
                                50.0);

// Light sources.
scene.Sources = new System.Collections.Generic.LinkedList<ILightSource>();
scene.Sources.Add(new AmbientLightSource(1.0));
scene.Sources.Add(new PointLightSource(new Vector3d(-5.0, 3.0, -3.0), 1.6));

// --- NODE DEFINITIONS ----------------------------------------------------

// Lightning

Vector3d begin = new Vector3d(0, 3, 3);
Vector3d end = new Vector3d(0, -2, 3);

root.InsertChild(
    Lightning.CreateLightning(begin, end, 0.05), Matrix4d.RotateX(0)
);
