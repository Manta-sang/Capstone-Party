using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways,RequireComponent(typeof(Graphic))]
public sealed class SoftTicketGradient : BaseMeshEffect {
 public Color top=Color.white;
 public Color bottom=new Color(.94f,.91f,.85f,1);
 public override void ModifyMesh(VertexHelper mesh){
  if(!IsActive())return;
  var rect=(transform as RectTransform).rect;var vertex=new UIVertex();
  for(int i=0;i<mesh.currentVertCount;i++){mesh.PopulateUIVertex(ref vertex,i);float t=Mathf.InverseLerp(rect.yMin,rect.yMax,vertex.position.y);vertex.color=(Color)vertex.color*Color.Lerp(bottom,top,t);mesh.SetUIVertex(vertex,i);}
 }
}
