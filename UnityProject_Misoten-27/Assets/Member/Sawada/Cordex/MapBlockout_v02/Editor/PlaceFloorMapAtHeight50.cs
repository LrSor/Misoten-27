using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PlaceFloorMapAtHeight50
{
    const string Base="Assets/Member/Sawada/Cordex/MapBlockout_v02";
    const string Target="Assets/Member/Sawada/Scenes/testMapPlayerMove.unity";
    const string RootName="Misoten27_FloorMap_Y50";
    const string Report=@"D:\制作\Blender\Project Dara\Misoten27_3DMap\Unity_Placement_QA.json";
    [Serializable] class Result
    {
        public string scene; public string root; public Vector3 position,rotation,scale,min,max;
        public int meshColliders;public bool rampRendererMatchesCollision,originalRootsPreserved,nonConvexColliders,collisionRendererHidden,noOriginalColliderOverlap;
        public float southFloorY,eastRampLowerY,eastRampMiddleY,eastRampUpperY,northFloorY;
    }
    [MenuItem("Tools/Misoten27/Place Floor Map at Y50")]
    public static void Run()
    {
        var scene=EditorSceneManager.OpenScene(Target,OpenSceneMode.Single);
        var roots=scene.GetRootGameObjects();
        var existing=roots.FirstOrDefault(g=>g.name==RootName);
        if(existing!=null){Validate(scene,existing,true);return;}
        var originals=roots.SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Select(t=>(t,t.position,t.rotation,t.localScale)).ToArray();
        var oldColliders=roots.SelectMany(g=>g.GetComponentsInChildren<Collider>(true)).Where(c=>c.enabled&&!c.isTrigger&&c.gameObject.activeInHierarchy).ToArray();
        var root=new GameObject(RootName);SceneManager.MoveGameObjectToScene(root,scene);
        root.transform.SetPositionAndRotation(new Vector3(0,50,0),Quaternion.Euler(0,180,0));root.transform.localScale=Vector3.one;
        var visual=Instantiate("Misoten27_Floor",root.transform);
        var collision=Instantiate("Misoten27_Floor_Collision",root.transform);
        var walls=Instantiate("Misoten27_LayoutWalls",root.transform);
        foreach(var mf in collision.GetComponentsInChildren<MeshFilter>())AddCollider(mf);
        foreach(var r in collision.GetComponentsInChildren<Renderer>())r.enabled=false;
        foreach(var mf in walls.GetComponentsInChildren<MeshFilter>())AddCollider(mf);
        MakeMaterials(new[]{visual,walls});
        foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.isStatic=true;
        Physics.SyncTransforms();
        bool preserved=originals.All(a=>a.t!=null&&a.t.position==a.position&&a.t.rotation==a.rotation&&a.t.localScale==a.localScale);
        if(!preserved)throw new Exception("An original transform changed");
        foreach(var fresh in root.GetComponentsInChildren<Collider>())
        foreach(var old in oldColliders)
            if(fresh.bounds.Intersects(old.bounds))throw new Exception("Map bounds overlap existing collider: "+old.name);
        Validate(scene,root,preserved);
        EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene,Target))throw new Exception("Could not save target scene");
        AssetDatabase.SaveAssets();
        scene=EditorSceneManager.OpenScene(Target,OpenSceneMode.Single);
        root=scene.GetRootGameObjects().Single(g=>g.name==RootName);
        Validate(scene,root,preserved);
        Debug.Log("MAP_Y50_PLACEMENT_COMPLETE "+File.ReadAllText(Report));
    }
    static GameObject Instantiate(string name,Transform parent)
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Base+"/Models/"+name+".fbx");
        if(asset==null)throw new Exception("Missing model "+name);
        var ob=(GameObject)PrefabUtility.InstantiatePrefab(asset,parent);ob.name=name;
        return ob;
    }
    static void AddCollider(MeshFilter mf)
    {
        var c=mf.gameObject.AddComponent<MeshCollider>();c.sharedMesh=mf.sharedMesh;c.convex=false;c.isTrigger=false;
    }
    static void MakeMaterials(GameObject[] groups)
    {
        var shader=Shader.Find("Universal Render Pipeline/Lit");
        if(shader==null)throw new Exception("Expected URP/Lit shader is unavailable");
        if(!AssetDatabase.IsValidFolder(Base+"/Materials"))AssetDatabase.CreateFolder(Base,"Materials");
        var cache=new Dictionary<string,Material>();
        foreach(var group in groups)
        foreach(var r in group.GetComponentsInChildren<Renderer>())
        {
            var mats=r.sharedMaterials;
            for(int i=0;i<mats.Length;i++)
            {
                var source=mats[i];if(source==null)throw new Exception("Missing source material");
                string name=string.Join("_",source.name.Split(Path.GetInvalidFileNameChars()));
                if(!cache.TryGetValue(name,out var m))
                {
                    string path=Base+"/Materials/"+name+".mat";
                    m=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(m==null)
                    {
                        m=new Material(shader){name=name};
                        var color=source.HasProperty("_BaseColor")?source.GetColor("_BaseColor"):(source.HasProperty("_Color")?source.GetColor("_Color"):Color.gray);
                        m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.15f);AssetDatabase.CreateAsset(m,path);
                    }
                    cache.Add(name,m);
                }
                mats[i]=m;
            }
            r.sharedMaterials=mats;
        }
    }
    static float FloorHeight(GameObject root,float x,float z)
    {
        var hits=Physics.RaycastAll(new Vector3(x,65,z),Vector3.down,30,~0,QueryTriggerInteraction.Ignore);
        var own=hits.Where(h=>h.collider.transform.IsChildOf(root.transform)).OrderBy(h=>h.distance).ToArray();
        if(own.Length==0)throw new Exception("No floor collider at "+x+", "+z);
        return own[0].point.y;
    }
    static void Validate(Scene scene,GameObject root,bool preserved)
    {
        Physics.SyncTransforms();
        var colliders=root.GetComponentsInChildren<MeshCollider>();
        var bounds=new Bounds();bool first=true;
        foreach(var r in root.GetComponentsInChildren<Renderer>().Where(r=>r.enabled)){if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
        var result=new Result{scene=Target,root=RootName,position=root.transform.position,rotation=root.transform.eulerAngles,scale=root.transform.localScale,min=bounds.min,max=bounds.max,meshColliders=colliders.Length,originalRootsPreserved=preserved,nonConvexColliders=colliders.All(c=>!c.convex&&c.sharedMesh!=null&&!c.isTrigger),collisionRendererHidden=root.transform.Find("Misoten27_Floor_Collision").GetComponentsInChildren<Renderer>().All(r=>!r.enabled),noOriginalColliderOverlap=true};
        result.southFloorY=FloorHeight(root,16,-22);result.eastRampLowerY=FloorHeight(root,49,10);result.eastRampMiddleY=FloorHeight(root,49,14);result.eastRampUpperY=FloorHeight(root,49,18);result.northFloorY=FloorHeight(root,9,58);
        result.rampRendererMatchesCollision=Mathf.Abs(result.eastRampLowerY-50)<.01f&&Mathf.Abs(result.eastRampMiddleY-50.8f)<.01f&&Mathf.Abs(result.eastRampUpperY-51.6f)<.01f;
        if(root.transform.position!=new Vector3(0,50,0)||colliders.Length!=2||!result.nonConvexColliders||!result.collisionRendererHidden||!result.rampRendererMatchesCollision||Mathf.Abs(result.southFloorY-50)>.01f||Mathf.Abs(result.northFloorY-53.2f)>.01f)throw new Exception("Y50 placement validation failed: "+JsonUtility.ToJson(result));
        File.WriteAllText(Report,JsonUtility.ToJson(result,true));
    }
}
