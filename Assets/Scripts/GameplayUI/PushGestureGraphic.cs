using UnityEngine;
using UnityEngine.UI;

// A layered, outlined boxing glove / shove emblem. All curves remain crisp at HUD size.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class PushGestureGraphic : MaskableGraphic
{
    void Oval(VertexHelper vh,Vector2 p,Vector2 radii,Color tint){int n=vh.currentVertCount;vh.AddVert(p,tint,Vector2.zero);for(int i=0;i<=40;i++){float a=i*Mathf.PI/20;vh.AddVert(p+new Vector2(Mathf.Cos(a)*radii.x,Mathf.Sin(a)*radii.y),tint,Vector2.zero);}for(int i=0;i<40;i++)vh.AddTriangle(n,n+i+1,n+i+2);}
    void Line(VertexHelper vh,Vector2 a,Vector2 b,float w,Color tint){Vector2 side=new Vector2(a.y-b.y,b.x-a.x).normalized*w*.5f;int n=vh.currentVertCount;vh.AddVert(a-side,tint,Vector2.zero);vh.AddVert(a+side,tint,Vector2.zero);vh.AddVert(b+side,tint,Vector2.zero);vh.AddVert(b-side,tint,Vector2.zero);vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);Oval(vh,a,Vector2.one*w*.5f,tint);Oval(vh,b,Vector2.one*w*.5f,tint);}
    Color Tint(float r,float g,float b,float a=1)=>new Color(r,g,b,a)*color;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();var outline=Tint(.06f,.14f,.20f);var shade=Tint(.80f,.29f,.12f);var glove=Tint(1,.64f,.22f);var highlight=Tint(1,.88f,.56f);var cyan=Tint(.59f,.94f,.96f);
        // Knuckle silhouette, thumb and cuff are separate readable overlapping volumes.
        Oval(vh,new Vector2(4,1),new Vector2(18,17),outline);
        Oval(vh,new Vector2(5,3),new Vector2(15.5f,14.5f),shade);
        Oval(vh,new Vector2(6,6),new Vector2(14.5f,12),glove);
        Oval(vh,new Vector2(10,11),new Vector2(8,4.8f),highlight);
        Line(vh,new Vector2(-12,-3),new Vector2(-3,5),11,outline);
        Line(vh,new Vector2(-11,-2),new Vector2(-3,5),7.3f,glove);
        Line(vh,new Vector2(-18,-15),new Vector2(-6,-15),14,outline);
        Line(vh,new Vector2(-18,-15),new Vector2(-6,-15),9.5f,cyan);
        Line(vh,new Vector2(-18,-13),new Vector2(-7,-13),2.2f,Tint(.87f,1,1));
        Line(vh,new Vector2(3,-7),new Vector2(12,-6),1.6f,shade);
        Line(vh,new Vector2(-7,15),new Vector2(-3,18),2,highlight);
        // Compact impact sparkle and speed accents replace the tiny stick figure.
        Line(vh,new Vector2(23,17),new Vector2(27,23),2.5f,cyan);
        Line(vh,new Vector2(25,8),new Vector2(31,9),2.5f,cyan);
        Line(vh,new Vector2(14,24),new Vector2(15,29),2.5f,highlight);
        Line(vh,new Vector2(-29,5),new Vector2(-20,5),2.5f,cyan);
        Line(vh,new Vector2(-27,12),new Vector2(-20,12),2.5f,Tint(.59f,.94f,.96f,.65f));
        var v=new UIVertex();float scale=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)/68;
        for(int i=0;i<vh.currentVertCount;i++){vh.PopulateUIVertex(ref v,i);v.position=(Vector3)rectTransform.rect.center+v.position*scale;vh.SetUIVertex(v,i);}
    }
}
