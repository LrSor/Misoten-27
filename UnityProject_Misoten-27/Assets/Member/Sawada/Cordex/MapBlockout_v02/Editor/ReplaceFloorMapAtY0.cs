using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;
using Unity.Mathematics;

[InitializeOnLoad]
public static class ReplaceFloorMapAtY0
{
    const string Work = @"D:\制作\Blender\Project Dara\Misoten27_3DMap";
    const string Target = "Assets/Member/Sawada/Scenes/testMapPlayerMove.unity";
    const string Request = Work + "/ReplaceFloorMapY0.request";
    const string Report = Work + "/Unity_Replacement_QA.json";
    static ReplaceFloorMapAtY0() { EditorApplication.delayCall += AutoRun; }
    static void AutoRun()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Move(Request, Work + "/ReplaceFloorMapY0.running");
        try { Run(); }
        catch (Exception e) { File.WriteAllText(Work + "/Unity_Replacement_Error.txt", e.ToString()); Debug.LogException(e); }
    }
    [MenuItem("Tools/Misoten27/Replace Floor Map at Y0")]
    public static void Run()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != Target || EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Open target scene in Edit Mode first.");
        var roots = scene.GetRootGameObjects();
        var root = roots.Single(g => g.name == "Misoten27_FloorMap_Y50");
        var old = roots.Single(g => g.name == "Maps");
        string backup = Work + "/Unity_Replacement_Backup/testMapPlayerMove.before_y0.unity";
        Directory.CreateDirectory(Path.GetDirectoryName(backup));
        if(!File.Exists(backup) && !EditorSceneManager.SaveScene(scene, backup, true)) throw new Exception("Scene backup failed.");
        if(!File.Exists(backup + ".meta")) File.Copy(Target + ".meta", backup + ".meta");
        var untouched = roots.Where(g => g != old && g != root).SelectMany(g => g.GetComponentsInChildren<Transform>(true))
            .Select(t => (t, t.position, t.rotation, t.localScale, t.gameObject.activeSelf)).ToArray();
        Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Replace map at Y0 and road center splines");
        Undo.RecordObjects(new UnityEngine.Object[]{root.transform,old}, "Replace floor map");
        root.transform.position = new Vector3(root.transform.position.x, 0, root.transform.position.z);
        // Keep the original object name so existing name-based tooling can still find it.
        old.SetActive(false);
        var existingSplines=root.transform.Find("Splines");
        var splines = existingSplines != null ? existingSplines.gameObject : new GameObject("Splines");
        if(existingSplines == null) Undo.RegisterCreatedObjectUndo(splines,"Road center splines");
        splines.transform.SetParent(root.transform, true);
        splines.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
        BuildRoutes(splines.transform);
        var changed = new List<string>();
        foreach(var mb in roots.SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).Where(m => m != null))
        {
            if(mb.GetType().Name != "TestPlayerCameraLookController") continue;
            var so = new SerializedObject(mb); var prop = so.FindProperty("m_splinesParent");
            if(prop == null) continue;
            Undo.RecordObject(mb,"Use new road center splines"); prop.objectReferenceValue=splines.transform;so.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(mb); changed.Add(mb.name);
        }
        Physics.SyncTransforms();
        if(!untouched.All(a=>a.t.position==a.position && a.t.rotation==a.rotation && a.t.localScale==a.localScale && a.t.gameObject.activeSelf==a.activeSelf))
            throw new Exception("An unrelated transform or active state changed.");
        Validate(scene,root,old,splines.transform,changed.Count);
        EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene,Target)) throw new Exception("Could not save target scene.");
        Undo.CollapseUndoOperations(undo);
        Selection.activeGameObject = splines;
        if(SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.LookAt(new Vector3(0,1.6f,18),Quaternion.Euler(90,0,0),76,true);
            SceneView.lastActiveSceneView.Repaint();
        }
        Debug.Log("MAP_Y0_REPLACEMENT_COMPLETE " + File.ReadAllText(Report));
    }
    static float Height(float z) => 1.6f * Mathf.Clamp01((z-10)/8) + 1.6f*Mathf.Clamp01((z-44)/12);
    static void AddRoute(Transform parent,string name,Vector2[] points)
    {
        var existing=parent.Find(name);
        var go = existing != null ? existing.gameObject : new GameObject(name);
        if(existing == null) Undo.RegisterCreatedObjectUndo(go,"Road center spline");
        go.transform.SetParent(parent,false);
        var container = go.GetComponent<SplineContainer>();
        if(container == null) container=go.AddComponent<SplineContainer>();
        var positions = new List<float3>();
        for(int i=1;i<points.Length;i++)
        {
            Vector2 a=points[i-1],b=points[i];
            // Keep slope breaks as explicit knots; interpolate each straight road section.
            var ts=new List<float>{0,1};
            if(Mathf.Abs(b.y-a.y)>.0001f) foreach(float z in new[]{10f,18f,44f,56f})
            { float t=(z-a.y)/(b.y-a.y); if(t>0 && t<1) ts.Add(t); }
            ts.Sort();
            foreach(var t in ts)
            {
                var p=Vector2.Lerp(a,b,t); var pos=new float3(p.x,Height(p.y)+.49f,p.y);
                if(positions.Count==0 || math.distance(positions[positions.Count-1],pos)>.001f) positions.Add(pos);
            }
        }
        container.Spline=new Spline(positions,TangentMode.Linear,false);
        EditorUtility.SetDirty(container);
    }
    static void BuildRoutes(Transform splines)
    {
        AddRoute(splines, "Spline - South West Approach", new Vector2[]{new Vector2(22f,-22f),new Vector2(6f,-18f),new Vector2(-12f,-18f),new Vector2(-21f,-12f)});
        AddRoute(splines, "Spline - West Long", new Vector2[]{new Vector2(-21f,-12f),new Vector2(-24f,-16f),new Vector2(-41f,2f),new Vector2(-51f,18f),new Vector2(-46f,25f),new Vector2(-39f,36f),new Vector2(-32f,36f)});
        AddRoute(splines, "Spline - Original South Bend", new Vector2[]{new Vector2(-3f,0f),new Vector2(-9f,-2f),new Vector2(-20f,-12f)});
        AddRoute(splines, "Spline - Entry Turn", new Vector2[]{new Vector2(-5f,0f),new Vector2(-9f,2f),new Vector2(-9f,6f)});
        AddRoute(splines, "Spline - South Mid Approach", new Vector2[]{new Vector2(20f,-23f),new Vector2(12f,-12f),new Vector2(10f,-4f),new Vector2(6f,0f)});
        AddRoute(splines, "Spline - Original Mid Rise", new Vector2[]{new Vector2(-9f,18f),new Vector2(-1f,31f),new Vector2(-5f,39f),new Vector2(-19f,40f)});
        AddRoute(splines, "Spline - Room South Door", new Vector2[]{new Vector2(-24f,-4f),new Vector2(-22f,-12f)});
        AddRoute(splines, "Spline - Room Mid Door", new Vector2[]{new Vector2(-15f,11f),new Vector2(-9f,11f)});
        AddRoute(splines, "Spline - Room West Door", new Vector2[]{new Vector2(-39f,17f),new Vector2(-46f,22f)});
        AddRoute(splines, "Spline - A Mid Link", new Vector2[]{new Vector2(-24.5f,39f),new Vector2(-15f,40f),new Vector2(-5f,39f)});
        AddRoute(splines, "Spline - A Defender Link", new Vector2[]{new Vector2(-28f,44f),new Vector2(-18f,51f),new Vector2(-1f,57f),new Vector2(4f,58f)});
        AddRoute(splines, "Spline - Original Upper Gallery", new Vector2[]{new Vector2(-5.5f,50f),new Vector2(10f,49f),new Vector2(11f,51f),new Vector2(17f,51f),new Vector2(19f,47.7143f),new Vector2(31f,46f),new Vector2(40f,43f)});
        AddRoute(splines, "Spline - Gallery Mid Door", new Vector2[]{new Vector2(-5f,39f),new Vector2(-6f,48f),new Vector2(-5.5f,50f)});
        AddRoute(splines, "Spline - South East Approach", new Vector2[]{new Vector2(24f,-24f),new Vector2(36f,-22f),new Vector2(49f,-12f),new Vector2(49f,0f),new Vector2(47f,8f),new Vector2(47f,22f),new Vector2(47f,28f),new Vector2(41.8f,28f),new Vector2(41.8f,37f),new Vector2(38f,37f)});
        AddRoute(splines, "Spline - B Defender Link", new Vector2[]{new Vector2(38f,37f),new Vector2(30f,47f),new Vector2(20f,56f),new Vector2(15f,58f)});
        AddRoute(splines, "Spline - Gallery B Door", new Vector2[]{new Vector2(31f,46f),new Vector2(38f,43f),new Vector2(38f,37f)});
        AddRoute(splines, "Spline - Mid East Link", new Vector2[]{new Vector2(-1f,29f),new Vector2(7f,28.5f),new Vector2(13f,28.5f),new Vector2(19f,30f),new Vector2(31f,31f)});
        AddRoute(splines, "Spline - Lower Rotate", new Vector2[]{new Vector2(12f,-12f),new Vector2(24f,-7f),new Vector2(35f,-9f),new Vector2(47f,-7f)});
        AddRoute(splines, "Spline - West Service Recess", new Vector2[]{new Vector2(-12f,-18f),new Vector2(-22f,-22f),new Vector2(-28f,-22f)});
        AddRoute(splines, "Spline - North Overlook", new Vector2[]{new Vector2(-28f,44f),new Vector2(-36f,50f),new Vector2(-41f,50f)});
        AddRoute(splines, "Spline - Entry Center", new Vector2[]{new Vector2(-5f,0f),new Vector2(6f,0f)});
        AddRoute(splines, "Spline - Narrow Center", new Vector2[]{new Vector2(-9f,6f),new Vector2(-9f,18f)});
        AddRoute(splines, "Spline - West Room Center", new Vector2[]{new Vector2(-24f,-4f),new Vector2(-20f,0f),new Vector2(-20f,11f),new Vector2(-15f,11f)});
        AddRoute(splines, "Spline - West Room Cross", new Vector2[]{new Vector2(-39f,17f),new Vector2(-33f,14f),new Vector2(-20f,14f),new Vector2(-20f,11f)});
        AddRoute(splines, "Spline - North Assembly Center", new Vector2[]{new Vector2(4f,58f),new Vector2(9f,58f),new Vector2(15f,58f)});
        AddRoute(splines, "Spline - South Assembly Center", new Vector2[]{new Vector2(6f,-18f),new Vector2(16f,-22f),new Vector2(24f,-24f)});
        AddRoute(splines, "Spline - A Court Center", new Vector2[]{new Vector2(-32f,36f),new Vector2(-24.5f,36f),new Vector2(-24.5f,39f)});
        AddRoute(splines, "Spline - B Court Center", new Vector2[]{new Vector2(31f,31f),new Vector2(33.5f,31f),new Vector2(33.5f,37f),new Vector2(38f,37f)});
    }
    [Serializable] class QA
    {
        public string scene,root,backup; public Vector3 position;
        public bool oldMapInactive,nonConvexColliders,collisionRendererHidden,unrelatedTransformsPreserved;
        public int splineContainers,knots,validatedSamples,cameraReferences,missingScripts;
        public float maxFloorOffsetError,minWallClearanceTestRadius,southFloorY,rampLowerY,rampMiddleY,rampUpperY,northFloorY;
        public string[] routes;
    }
    static float FloorY(GameObject root,float x,float z)
    {
        var c=root.transform.Find("Misoten27_Floor_Collision").GetComponentsInChildren<MeshCollider>();
        var ray=new Ray(new Vector3(x,20,z),Vector3.down);
        float highest=float.NegativeInfinity;
        foreach(var col in c) if(col.Raycast(ray,out var hit,40)) highest=Mathf.Max(highest,hit.point.y);
        if(float.IsNegativeInfinity(highest)) throw new Exception("No map floor at "+x+", "+z);
        return highest;
    }
    static void Validate(Scene scene,GameObject root,GameObject old,Transform splines,int refs)
    {
        var containers=splines.GetComponentsInChildren<SplineContainer>();
        var q=new QA{scene=Target,root=root.name,backup="Unity_Replacement_Backup/testMapPlayerMove.before_y0.unity",position=root.transform.position,
            oldMapInactive=!old.activeInHierarchy,splineContainers=containers.Length,cameraReferences=refs,
            nonConvexColliders=root.GetComponentsInChildren<MeshCollider>().All(c=>!c.convex && c.sharedMesh!=null),
            collisionRendererHidden=root.transform.Find("Misoten27_Floor_Collision").GetComponentsInChildren<Renderer>().All(r=>!r.enabled),
            minWallClearanceTestRadius=.55f,unrelatedTransformsPreserved=true,routes=containers.Select(c=>c.name).ToArray()};
        foreach(var t in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)))
            q.missingScripts+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
        foreach(var c in containers)
        {
            q.knots+=c.Spline.Count;
            // Dense parameter samples check every road section and slope break against real colliders.
            int steps=Mathf.CeilToInt(c.CalculateLength()*8);
            for(int j=0;j<=steps;j++)
            {
                Vector3 p=c.EvaluatePosition(j/(float)steps);
                float y=FloorY(root,p.x,p.z);
                q.maxFloorOffsetError=Mathf.Max(q.maxFloorOffsetError,Mathf.Abs(p.y-y-.49f));
                var blocked=Physics.OverlapSphere(new Vector3(p.x,y+.7f,p.z),.55f,~0,QueryTriggerInteraction.Ignore)
                    .Where(col=>col.transform.IsChildOf(root.transform) && col.transform.IsChildOf(root.transform.Find("Misoten27_LayoutWalls"))).ToArray();
                if(blocked.Length>0) throw new Exception("Spline approaches wall/cover: "+c.name+" at "+p);
                q.validatedSamples++;
            }
        }
        q.southFloorY=FloorY(root,16,-22);q.rampLowerY=FloorY(root,49,10);q.rampMiddleY=FloorY(root,49,14);q.rampUpperY=FloorY(root,49,18);q.northFloorY=FloorY(root,9,58);
        if(q.position.y!=0 || !q.oldMapInactive || q.maxFloorOffsetError>.015f || refs<1 || !q.nonConvexColliders || !q.collisionRendererHidden || q.missingScripts!=0)
            throw new Exception("Y0 replacement validation failed: "+JsonUtility.ToJson(q,true));
        File.WriteAllText(Report,JsonUtility.ToJson(q,true));
    }
}
