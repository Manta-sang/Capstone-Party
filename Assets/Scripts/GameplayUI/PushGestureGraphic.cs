using UnityEngine;
using UnityEngine.UI;

// Font-independent push emoji: a reaching hand, a knocked-back character and
// motion strokes. Vector UI stays sharp without adding another font asset.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class PushGestureGraphic : MaskableGraphic
{
    void Disc(VertexHelper vh,Vector2 center,float radius,Color tint){
        int first=vh.currentVertCount;vh.AddVert(center,tint,Vector2.zero);
        const int segments=24;
        for(int i=0;i<=segments;i++){float a=i*Mathf.PI*2/segments;vh.AddVert(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,tint,Vector2.zero);}
        for(int i=0;i<segments;i++)vh.AddTriangle(first,first+i+1,first+i+2);
    }
    void Stroke(VertexHelper vh,Vector2 a,Vector2 b,float width,Color tint){
        Vector2 normal=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;int first=vh.currentVertCount;
        vh.AddVert(a-normal,tint,Vector2.zero);vh.AddVert(a+normal,tint,Vector2.zero);vh.AddVert(b+normal,tint,Vector2.zero);vh.AddVert(b-normal,tint,Vector2.zero);vh.AddTriangle(first,first+1,first+2);vh.AddTriangle(first,first+2,first+3);Disc(vh,a,width*.5f,tint);Disc(vh,b,width*.5f,tint);
    }
    protected override void OnPopulateMesh(VertexHelper vh){
        vh.Clear();var blue=new Color(.36f,.80f,.92f)*color;var glove=new Color(1,.88f,.62f)*color;var dark=new Color(.13f,.22f,.29f)*color;
        // Coordinates use a 64px square around the center of the icon.
        var scale=rectTransform.rect.width/64;var center=rectTransform.rect.center;
        int start=vh.currentVertCount;
        Disc(vh,new Vector2(17,17),7,blue);Stroke(vh,new Vector2(12,4),new Vector2(18,-8),10,blue);
        Stroke(vh,new Vector2(12,1),new Vector2(24,3),5,blue);Stroke(vh,new Vector2(17,-8),new Vector2(9,-19),5,blue);Stroke(vh,new Vector2(18,-8),new Vector2(29,-14),5,blue);
        Disc(vh,new Vector2(19,19),1.1f,dark);
        Stroke(vh,new Vector2(-26,-5),new Vector2(-9,-5),10,glove);Stroke(vh,new Vector2(-9,-5),new Vector2(-5,6),10,glove);
        for(int i=0;i<3;i++)Stroke(vh,new Vector2(-8+i*4,4),new Vector2(-8+i*4,13-i),3,glove);
        Stroke(vh,new Vector2(-18,20),new Vector2(-7,20),2.3f,glove);Stroke(vh,new Vector2(-26,15),new Vector2(-19,15),2.3f,glove);
        UIVertex vertex=new UIVertex();for(int i=start;i<vh.currentVertCount;i++){vh.PopulateUIVertex(ref vertex,i);vertex.position=(Vector3)center+vertex.position*scale;vh.SetUIVertex(vertex,i);}
    }
}
