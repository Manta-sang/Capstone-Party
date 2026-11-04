using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class PartyButtonFeedback : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,ISelectHandler,IDeselectHandler,IPointerDownHandler,IPointerUpHandler {
 Button button;Image image;Color baseColor,lastColor;bool hover,selected,pressed,initialized;
 public Color disabledColor=new Color(.80f,.81f,.82f);
 void OnEnable(){
  button=GetComponent<Button>();image=button.targetGraphic as Image;if(image==null)return;
  if(!initialized){baseColor=image.color*button.colors.normalColor;initialized=true;}else if(image.color!=lastColor)baseColor=image.color;
  var c=button.colors;c.normalColor=c.highlightedColor=c.selectedColor=c.pressedColor=c.disabledColor=Color.white;c.colorMultiplier=1;button.colors=c;button.transition=Selectable.Transition.ColorTint;lastColor=image.color;Apply();
 }
 void LateUpdate(){if(image==null)return;if(image.color!=lastColor)baseColor=image.color;Apply();}
 void Apply(){if(image==null)return;var target=!button.interactable?disabledColor:pressed?Color.Lerp(baseColor,Color.black,.08f):(hover||selected)?Color.Lerp(baseColor,Color.white,.09f):baseColor;target.a=baseColor.a;image.color=lastColor=target;}
 public void OnPointerEnter(PointerEventData e){hover=true;}
 public void OnPointerExit(PointerEventData e){hover=false;pressed=false;}
 public void OnSelect(BaseEventData e){selected=true;}
 public void OnDeselect(BaseEventData e){selected=false;}
 public void OnPointerDown(PointerEventData e){pressed=true;}
 public void OnPointerUp(PointerEventData e){pressed=false;}
 void OnDisable(){hover=selected=pressed=false;if(image!=null)image.color=lastColor=baseColor;}
}
