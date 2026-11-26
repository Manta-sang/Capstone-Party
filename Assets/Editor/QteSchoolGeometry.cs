#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static class QteSchoolGeometry
{
 const string Folder="Assets/Materials/QteGenerated/";
 public static void Refine(Transform school,Transform details)
 {
  string[] soften={"School main building","School west classroom wing","School east classroom wing","School entrance porch","School entrance column","School front step upper landing","School front step middle","School front step lower","Main hall lower plinth"};
  foreach(var t in school.GetComponentsInChildren<Transform>().Where(t=>soften.Contains(t.name)).ToArray())
  {
   var filter=t.GetComponent<MeshFilter>();if(!filter)continue;
   string path=Folder+"School_soft_"+t.name.Replace(" ","_")+".asset";
   var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
   if(existing&&filter.sharedMesh==existing)continue;
   Vector3 size=t.localScale;float radius=t.name.Contains("building")?.38f:t.name.Contains("wing")?.28f:.10f;
   var mesh=RoundedBox(size,Mathf.Min(radius,Mathf.Min(size.x,Mathf.Min(size.y,size.z))*.2f));
   Save(mesh,path);filter.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);t.localScale=Vector3.one;
   var box=t.GetComponent<BoxCollider>();if(box)box.size=size;
  }
  var clay=AssetDatabase.LoadAssetAtPath<Material>(Folder+"School_Roof_tile.mat");var stone=AssetDatabase.LoadAssetAtPath<Material>(Folder+"School_Limestone.mat");
  foreach(float wing in new[]{-30f,0f,30f})
  {
   float width=wing==0?35:24,depth=wing==0?14:12.5f,eave=wing==0?20.15f:16.15f,rise=wing==0?3.1f:2.3f;
   foreach(float side in new[]{-1f,1f})
   {
    float x=wing+side*width*.5f;
    var vertices=new[]{new Vector3(x,eave,34-depth*.5f),new Vector3(x,eave+rise,34),new Vector3(x,eave,34+depth*.5f)};
    var mesh=new Mesh();mesh.vertices=vertices;mesh.triangles=side>0?new[]{0,1,2}:new[]{0,2,1};mesh.RecalculateNormals();
    MeshObject(details,"Finished gable "+wing+" "+side,mesh,stone);
   }
  }
  // The entrance silhouette is a shallow barrel vault rather than a stack of boxes.
  foreach(string name in new[]{"School entrance canopy","School entrance canopy soffit"}){var old=school.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==name);if(old)old.gameObject.SetActive(false);}
  var canopy=new Mesh();var v=new List<Vector3>();var triangles=new List<int>();const int count=40;
  for(int i=0;i<=count;i++)
  {
   float a=-Mathf.PI*.5f+Mathf.PI*i/count;float x=Mathf.Sin(a)*6.7f,y=5.8f+Mathf.Cos(a)*.95f;
   v.Add(new Vector3(x,y,23.75f));v.Add(new Vector3(x,y,26.8f));v.Add(new Vector3(x,y-.18f,23.75f));v.Add(new Vector3(x,y-.18f,26.8f));
   if(i==0)continue;int k=i*4,p=k-4;
   triangles.AddRange(new[]{p,p+1,k,p+1,k+1,k,p+2,k+2,p+3,p+3,k+2,k+3,p,p+2,k,p+2,k+2,k,p+1,k+1,p+3,p+3,k+1,k+3});
  }
  canopy.SetVertices(v);canopy.SetTriangles(triangles,0);canopy.RecalculateNormals();MeshObject(details,"Curved ceramic entrance canopy",canopy,clay);
  // Rounded columns and a stone curb frame the entrance without changing the race footprint.
  foreach(float x in new[]{-5.6f,5.6f})
  {
   var column=GameObject.CreatePrimitive(PrimitiveType.Cylinder);column.name="Entrance round stone column";column.transform.SetParent(details,false);column.transform.position=new Vector3(x,2.94f,24.7f);column.transform.localScale=new Vector3(.47f,2.94f,.47f);column.GetComponent<Renderer>().sharedMaterial=stone;Object.DestroyImmediate(column.GetComponent<Collider>());
  }
  foreach(var old in school.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="School entrance column"))old.gameObject.SetActive(false);
 }
 static Mesh RoundedBox(Vector3 size,float radius)
 {
  var v=new List<Vector3>();var n=new List<Vector3>();var uv=new List<Vector2>();var tr=new List<int>();Vector3 half=size*.5f,inner=half-Vector3.one*radius;const int segments=8;
  for(int axis=0;axis<3;axis++)foreach(float sign in new[]{-1f,1f})
  {
   int start=v.Count,u=(axis+1)%3,w=(axis+2)%3;
   for(int j=0;j<=segments;j++)for(int i=0;i<=segments;i++)
   {
    Vector3 p=Vector3.zero;p[axis]=half[axis]*sign;p[u]=Coordinate(half[u],radius,i);p[w]=Coordinate(half[w],radius,j);
    Vector3 q=new Vector3(Mathf.Clamp(p.x,-inner.x,inner.x),Mathf.Clamp(p.y,-inner.y,inner.y),Mathf.Clamp(p.z,-inner.z,inner.z));Vector3 normal=(p-q).normalized;v.Add(q+normal*radius);n.Add(normal);uv.Add(new Vector2(i/(float)segments,j/(float)segments));
   }
   for(int j=0;j<segments;j++)for(int i=0;i<segments;i++){int a=start+j*(segments+1)+i,b=a+1,c=a+segments+1,d=c+1;bool outward=Vector3.Dot(Vector3.Cross(v[b]-v[a],v[c]-v[a]),n[a])>0;if(outward)tr.AddRange(new[]{a,b,c,b,d,c});else tr.AddRange(new[]{a,c,b,b,c,d});}
  }
  var mesh=new Mesh();mesh.SetVertices(v);mesh.SetNormals(n);mesh.SetUVs(0,uv);mesh.SetTriangles(tr,0);mesh.RecalculateBounds();return mesh;
 }
 static float Coordinate(float h,float r,int i){switch(i){case 0:return -h;case 1:return -h+r*.2f;case 2:return -h+r*.55f;case 3:return -h+r;case 4:return 0;case 5:return h-r;case 6:return h-r*.55f;case 7:return h-r*.2f;default:return h;}}
 static void Save(Mesh mesh,string path){var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old){EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);}else AssetDatabase.CreateAsset(mesh,path);}
 static void MeshObject(Transform parent,string name,Mesh mesh,Material material){string path=Folder+"School_"+name.Replace(" ","_")+".asset";Save(mesh,path);var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(parent,false);g.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);g.GetComponent<MeshRenderer>().sharedMaterial=material;g.isStatic=true;}
}
#endif
