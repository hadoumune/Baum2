using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;
#if !NO_TEXTMESHPRO
using TMPro;
#endif

namespace Baum2.Editor
{
    public static class ElementFactory
    {
        public static readonly Dictionary<string, Func<Dictionary<string, object>, Element, Element>> Generator = new Dictionary<string, Func<Dictionary<string, object>, Element, Element>>()
        {
            { "Root", (d, p) => new RootElement(d, p) },
            { "Image", (d, p) => new ImageElement(d, p) },
            { "Mask", (d, p) => new MaskElement(d, p) },
            { "Group", (d, p) => new GroupElement(d, p) },
            { "Text", (d, p) => new TextElement(d, p) },
            { "Button", (d, p) => new ButtonElement(d, p) },
            { "List", (d, p) => new ListElement(d, p) },
            { "ScrollRect", (d, p) => new ScrollRectElement(d, p) },
            { "Slider", (d, p) => new SliderElement(d, p) },
            { "Scrollbar", (d, p) => new ScrollbarElement(d, p) },
            { "Toggle", (d, p) => new ToggleElement(d, p) },
        };

        public static Element Generate(Dictionary<string, object> json, Element parent)
        {
            var type = json.Get("type");
            Assert.IsTrue(Generator.ContainsKey(type), "[Baum2] Unknown type: " + type);
            return Generator[type](json, parent);
        }
    }

    public interface IRootType{
        bool isRoot();
    }

    public abstract class Element : IRootType
    {
        public string name;
        protected string pivot;
        protected bool stretchX = false;
        protected bool stretchY = false;
        protected Element parent;
        public float angle{ get; private set;} = 0;
        public bool useTouch{ get; private set;} = true;
        public bool useArea{ get; set; } = true;
        public virtual bool isRoot(){ return false; }

        public abstract GameObject Render(Renderer renderer,Transform parent=null);
        public abstract Area CalcArea();
        public virtual void PostRender(GameObject go, Element element,Transform parent){

        }

        public static bool CompareName(Element element, string stateName){
            return element.name.Equals(stateName, StringComparison.OrdinalIgnoreCase);
        }
        public static bool ContainsName(Element element, string stateName){
            return element.name.IndexOf(stateName, StringComparison.OrdinalIgnoreCase)>=0;
        }

        bool parentStretchX{
            get{
                return ((parent != null && (!parent.isRoot())) ? parent.stretchX : false);
            }
        }
        bool parentStretchY{
            get{
                return ((parent != null && (!parent.isRoot())) ? parent.stretchY : false);
            }
        }

        protected Element(Dictionary<string, object> json, Element parent)
        {
            this.parent = parent;
            name = json.Get("name");
            if (json.ContainsKey("pivot")) pivot = json.Get("pivot");
            if (json.ContainsKey("stretchxy") || json.ContainsKey("stretchx") ) stretchX = true;
            if (json.ContainsKey("stretchxy") || json.ContainsKey("stretchy") ) stretchY = true;
            angle = 0;
            if ( json.ContainsKey("rot") ) angle = -json.GetFloat("rot");
            useTouch = true;
            if ( json.ContainsKey("touch") ) useTouch = json.GetBool("touch");
        }

        protected GameObject CreateUIGameObject(Renderer renderer)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            if ( Mathf.Abs(angle) > 0.0001f ){
                Debug.Log($"{name}:SetAngle {angle}");
                rt.rotation = Quaternion.Euler(0,0,angle);
            }
            return go;
        }

        protected void SetPivot(GameObject root, Renderer renderer)
        {
            if (string.IsNullOrEmpty(pivot)) pivot = "none";

            var rect = root.GetComponent<RectTransform>();
            var pivotMin = rect.anchorMin;
            var pivotMax = rect.anchorMax;
            var sizeDelta = rect.sizeDelta;

            if (pivot.Contains("bottom"))
            {
                pivotMin.y = 0.0f;
                pivotMax.y = 0.0f;
                sizeDelta.y = CalcArea().Height;
            }
            else if (pivot.Contains("top"))
            {
                pivotMin.y = 1.0f;
                pivotMax.y = 1.0f;
                sizeDelta.y = CalcArea().Height;
            }
            else if (pivot.Contains("middle"))
            {
                pivotMin.y = 0.5f;
                pivotMax.y = 0.5f;
                sizeDelta.y = CalcArea().Height;
            }
            if (pivot.Contains("left"))
            {
                pivotMin.x = 0.0f;
                pivotMax.x = 0.0f;
                sizeDelta.x = CalcArea().Width;
            }
            else if (pivot.Contains("right"))
            {
                pivotMin.x = 1.0f;
                pivotMax.x = 1.0f;
                sizeDelta.x = CalcArea().Width;
            }
            else if (pivot.Contains("center"))
            {
                pivotMin.x = 0.5f;
                pivotMax.x = 0.5f;
                sizeDelta.x = CalcArea().Width;
            }

            rect.anchorMin = pivotMin;
            rect.anchorMax = pivotMax;
            rect.sizeDelta = sizeDelta;
        }

        protected void SetStretch(GameObject root, Renderer renderer)
        {
            if (!stretchX && !stretchY) return;

            var parentSize = parent != null ? parent.CalcArea().Size : renderer.CanvasSize;
            var rect = root.GetComponent<RectTransform>();
            var pivotPosMin = new Vector2(0.5f, 0.5f);
            var pivotPosMax = new Vector2(0.5f, 0.5f);
            var sizeDelta = rect.sizeDelta;

            if (stretchX)
            {
                pivotPosMin.x = 0.0f;
                pivotPosMax.x = 1.0f;
                sizeDelta.x = CalcArea().Width - parentSize.x;
            }

            if (stretchY)
            {
                pivotPosMin.y = 0.0f;
                pivotPosMax.y = 1.0f;
                sizeDelta.y = CalcArea().Height - parentSize.y;
            }

            rect.anchorMin = pivotPosMin;
            rect.anchorMax = pivotPosMax;
            rect.sizeDelta = sizeDelta;
        }
    }

    public class GroupElement : Element
    {
        protected readonly List<Element> elements;
        private Area areaCache;

        public GroupElement(Dictionary<string, object> json, Element parent, bool resetStretch = false) : base(json, parent)
        {
            elements = new List<Element>();
            var jsonElements = json.Get<List<object>>("elements");
            foreach (var jsonElement in jsonElements)
            {
                var x = stretchX;
                var y = stretchY;
                if (resetStretch)
                {
                    stretchX = false;
                    stretchY = false;
                }
                elements.Add(ElementFactory.Generate(jsonElement as Dictionary<string, object>, this));
                stretchX = x;
                stretchY = y;
            }
            elements.Reverse();
            areaCache = CalcAreaInternal();
        }

        public override GameObject Render(Renderer renderer,Transform parent)
        {
            var go = CreateSelf(renderer);

            RenderChildren(renderer, go);

            SetStretch(go, renderer);
            SetPivot(go, renderer);
            return go;
        }

        protected virtual GameObject CreateSelf(Renderer renderer)
        {
            var go = CreateUIGameObject(renderer);

            var rect = go.GetComponent<RectTransform>();
            var area = CalcArea();
            rect.sizeDelta = area.Size;
            rect.anchoredPosition = renderer.CalcPosition(area.Min, area.Size);

            SetMaskImage(renderer, go);
            return go;
        }

        protected void SetMaskImage(Renderer renderer, GameObject go)
        {
            var maskSource = elements.Find(x => x is MaskElement);
            if (maskSource == null) return;

            elements.Remove(maskSource);
            var maskImage = go.AddComponent<Image>();
            maskImage.raycastTarget = false;

            var dummyMaskImage = maskSource.Render(renderer);
            dummyMaskImage.transform.SetParent(go.transform);
            dummyMaskImage.GetComponent<Image>().CopyTo(maskImage);
            GameObject.DestroyImmediate(dummyMaskImage);

            var mask = go.AddComponent<Mask>();
            mask.showMaskGraphic = false;
        }

        protected void RenderChildren(Renderer renderer, GameObject root, Action<GameObject, Element> callback = null)
        {
            foreach (var element in elements)
            {
                var go = element.Render(renderer,root.transform);
                var rectTransform = go.GetComponent<RectTransform>();
                var sizeDelta = rectTransform.sizeDelta;
                go.transform.SetParent(root.transform, true);
                rectTransform.sizeDelta = sizeDelta;
                rectTransform.localScale = Vector3.one;
                if ( Mathf.Abs(angle) > 0.0001f && Mathf.Abs(element.angle) <= 0.0001f )
                {
                    rectTransform.localEulerAngles = Vector3.zero;
                }
                if (callback != null) callback(go, element);
                element.PostRender(go,element,root.transform);
            }
        }

        private Area CalcAreaInternal()
        {
            if ( !useArea ) return Area.None();
            var area = Area.None();
            foreach (var element in elements){
                if ( element.useArea ){
                    area.Merge(element.CalcArea());
                }
            }
            return area;
        }

        public Area RecalcArea(){
            areaCache = CalcAreaInternal();
            return CalcArea();
        }

        public override Area CalcArea()
        {
            return areaCache;
        }
    }

    public class RootElement : GroupElement
    {
        private Vector2 sizeDelta;
        public override bool isRoot(){ return true; }

        public RootElement(Dictionary<string, object> json, Element parent) : base(json, parent)
        {
        }

        protected override GameObject CreateSelf(Renderer renderer)
        {
            var go = CreateUIGameObject(renderer);

            var rect = go.GetComponent<RectTransform>();
            sizeDelta = renderer.CanvasSize;
            rect.sizeDelta = sizeDelta;
            rect.anchoredPosition = Vector2.zero;

            SetMaskImage(renderer, go);
            // Rootのストレッチは最後にやらないとレイアウトが崩れる.
            //SetStretch(go, renderer);

            SetPivot(go, renderer);
            return go;
        }

        public override Area CalcArea()
        {
            if ( !useArea ) return Area.None();
            return new Area(-sizeDelta / 2.0f, sizeDelta / 2.0f);
        }
    }

    public class ImageElement : Element
    {
        private string spriteName;
        private Vector2 canvasPosition;
        private Vector2 sizeDelta;
        private float opacity;

        public ImageElement(Dictionary<string, object> json, Element parent) : base(json, parent)
        {
            spriteName = json.Get("image");
            canvasPosition = json.GetVector2("x", "y");
            sizeDelta = json.GetVector2("w", "h");
            opacity = json.GetFloat("opacity");

        }

        public override GameObject Render(Renderer renderer,Transform parent)
        {
            var go = CreateUIGameObject(renderer);

            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = renderer.CalcPosition(canvasPosition, sizeDelta);
            rect.sizeDelta = sizeDelta;

            var image = go.AddComponent<Image>();
            image.sprite = renderer.GetSprite(spriteName);
            image.type = Image.Type.Sliced;
            image.color = new Color(1.0f, 1.0f, 1.0f, opacity / 100.0f);

            SetStretch(go, renderer);
            SetPivot(go, renderer);

            return go;
        }

        public override Area CalcArea()
        {
            if ( !useArea ) return Area.None();
            return Area.FromPositionAndSize(canvasPosition, sizeDelta);
        }
    }

    public sealed class MaskElement : ImageElement
    {
        public MaskElement(Dictionary<string, object> json, Element parent) : base(json, parent)
        {
        }
    }

    public sealed class TextElement : Element
    {
        private string message;
        private string font;
        private float fontSize;
        private string align;
        private float virtualHeight;
        private Color fontColor;
        private Vector2 canvasPosition;
        private Vector2 sizeDelta;
        private bool enableStroke;
        private int strokeSize;
        private Color strokeColor;
        private string type;
        private string style;
        private bool useAutoSize;

        public TextElement(Dictionary<string, object> json, Element parent) : base(json, parent)
        {
            message = json.Get("text");
            font = json.Get("font");
            fontSize = json.GetFloat("size");
            align = json.Get("align");
            type = json.Get("textType");
            if (json.ContainsKey("strokeSize"))
            {
                enableStroke = true;
                strokeSize = json.GetInt("strokeSize");
                strokeColor = EditorUtil.HexToColor(json.Get("strokeColor"));
            }
            fontColor = EditorUtil.HexToColor(json.Get("color"));
            sizeDelta = json.GetVector2("w", "h");
            canvasPosition = json.GetVector2("x", "y");
            virtualHeight = json.GetFloat("vh");
            if ( !json.ContainsKey("style") ){
                style = "normal";
            }
            else{
                style = json.Get("style");
            }

            useAutoSize = true;
            if ( json.ContainsKey("autosize") ){
                useAutoSize = json.GetBool("autosize");
            }
        }

        /// フォントスタイルの取得.
        private FontStyle GetStyle(){
            var fontStyle = FontStyle.Normal;
            switch( style ){
                case "bold": fontStyle = FontStyle.Bold; break;
                case "italic": fontStyle = FontStyle.Italic; break;
                case "bolditalic": fontStyle = FontStyle.BoldAndItalic; break;
            }
            return fontStyle;
        }
        /// アライメントの取得.
        private TextAnchor GetAlignment(bool middle){
            var alignment = TextAnchor.MiddleCenter;
            switch( align ){
                case "left": alignment = middle ? TextAnchor.MiddleLeft : TextAnchor.UpperLeft; break;
                case "center": alignment = middle ? TextAnchor.MiddleCenter : TextAnchor.UpperCenter; break;
                case "right": alignment = middle ? TextAnchor.MiddleRight : TextAnchor.UpperRight; break;
            }
            return alignment;
        }

        #if !NO_TEXTMESHPRO

        /// TMPフォントスタイルの取得.
        private TMPro.FontStyles GetTMPStyle(){
            int fontStyle = (int)TMPro.FontStyles.Normal;
            switch( style ){
                case "bold": fontStyle |= (int)TMPro.FontStyles.Bold; break;
                case "italic": fontStyle |= (int)TMPro.FontStyles.Italic; break;
                case "bolditalic": fontStyle |= (int)TMPro.FontStyles.Bold+(int)TMPro.FontStyles.Italic; break;
            }
            return (TMPro.FontStyles)fontStyle;
        }

        /// TMPアライメントの取得.
        private TextAlignmentOptions GetTMPAlignment(bool middle){
            var alignment = TextAlignmentOptions.Center;
            switch( align ){
                case "left": alignment = middle ? TextAlignmentOptions.Left : TextAlignmentOptions.TopLeft; break;
                case "center": alignment = middle ? TextAlignmentOptions.Center : TextAlignmentOptions.Top; break;
                case "right": alignment = middle ? TextAlignmentOptions.Right : TextAlignmentOptions.TopRight;break;
            }
            return alignment;
        }

        #endif

        public override GameObject Render(Renderer renderer,Transform parent)
        {
            var go = CreateUIGameObject(renderer);

            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = renderer.CalcPosition(canvasPosition, sizeDelta);
            rect.sizeDelta = sizeDelta;

            var raw = go.AddComponent<RawData>();
            raw.Info["font_size"] = fontSize;
            raw.Info["align"] = align;

            bool isNormalText = true;
            #if !NO_TEXTMESHPRO
            TMP_FontAsset tmpFont = renderer.GetTMPFont(font);
            if ( tmpFont != null ){
                isNormalText = false;
            }
            #endif
            if ( isNormalText ){
                raw.Info["isTMP"] = false;
                var text = go.AddComponent<Text>();
                text.text = message;
                text.font = renderer.GetFont(font);
                text.fontSize = Mathf.RoundToInt(fontSize);
                text.color = fontColor;
                text.fontStyle = GetStyle();
                bool middle = true;
                if (type == "point")
                {
                    text.horizontalOverflow = HorizontalWrapMode.Overflow;
                    text.verticalOverflow = VerticalWrapMode.Overflow;
                    middle = true;
                }
                else if (type == "paragraph")
                {
                    text.horizontalOverflow = HorizontalWrapMode.Wrap;
                    text.verticalOverflow = VerticalWrapMode.Overflow;
                    middle = !message.Contains("\n");
                }
                else
                {
                    Debug.LogError("unknown type " + type);
                }

                text.alignment = GetAlignment(middle);
                text.raycastTarget = false;

                if (enableStroke)
                {
                    var outline = go.AddComponent<Outline>();
                    outline.effectColor = strokeColor;
                    outline.effectDistance = new Vector2(strokeSize / 2.0f, -strokeSize / 2.0f);
                    outline.useGraphicAlpha = false;
                }
            }
            else{
            #if !NO_TEXTMESHPRO
                raw.Info["isTMP"] = true;
                var tmp = go.AddComponent<TextMeshProUGUI>();
                tmp.text = message;
                tmp.font = tmpFont;
                tmp.fontSize = Mathf.RoundToInt(fontSize);
                tmp.color = fontColor;
                tmp.fontStyle = GetTMPStyle();
                tmp.enableAutoSizing = useAutoSize;
                var text = go.GetComponent<TMP_Text>();
                bool middle = true;

                if (type == "point")
                {
                    text.enableWordWrapping = false;
                    text.overflowMode = TextOverflowModes.Overflow;
                    middle = true;
                }
                else if (type == "paragraph")
                {
                    text.enableWordWrapping = true;
                    text.overflowMode = TextOverflowModes.Overflow;
                    middle = !message.Contains("\n");
                }
                else
                {
                    Debug.LogError("unknown type " + type);
                }

                text.alignment = GetTMPAlignment(middle);
                text.raycastTarget = false;
            #endif
            }

            // 位置を補正する.
            var fixedPos = rect.anchoredPosition;
            switch (align)
            {
                case "left":
                    rect.pivot = new Vector2(0.0f, 0.5f);
                    fixedPos.x -= sizeDelta.x / 2.0f;
                    break;

                case "center":
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    break;

                case "right":
                    rect.pivot = new Vector2(1.0f, 0.5f);
                    fixedPos.x += sizeDelta.x / 2.0f;
                    break;
            }
            rect.anchoredPosition = fixedPos;

            var d = rect.sizeDelta;
            d.y = virtualHeight;
            rect.sizeDelta = d;

            SetStretch(go, renderer);
            SetPivot(go, renderer);
            return go;
        }

        public override Area CalcArea()
        {
            if ( !useArea ) return Area.None();
            return Area.FromPositionAndSize(canvasPosition, sizeDelta);
        }
    }

    public sealed class ButtonElement : GroupElement
    {
        private bool isExpandRaycastPadding = true;

        public ButtonElement(Dictionary<string, object> json, Element parent) : base(json, parent)
        {
            isExpandRaycastPadding = true;
            if ( json.ContainsKey("noexpand") ) isExpandRaycastPadding = !json.GetBool("noexpand");
        }

        public override GameObject Render(Renderer renderer,Transform parent)
        {
            var go = CreateSelf(renderer);

            Graphic bgImage = null;
            Graphic normalImage = null;
            Image pressImage = null;
            Image highlightImage = null;
            Image disableImage = null;
            Image selectImage = null;

            RenderChildren(renderer, go, (g, element) =>
            {
                if (bgImage == null && element is ImageElement) bgImage = g.GetComponent<Image>();

                if ( ContainsName(element,"normal") ) normalImage = g.GetComponent<Image>();
                else if ( ContainsName(element,"press") ) pressImage = g.GetComponent<Image>();
                else if ( ContainsName(element,"highlight") ) highlightImage = g.GetComponent<Image>();
                else if ( ContainsName(element,"disable") ) disableImage = g.GetComponent<Image>();
                else if ( ContainsName(element,"select") ) selectImage = g.GetComponent<Image>();
            });

            // イメージボタンが1個でもあってかつnormalが無ければ一番奥のイメージをnormalImageにする.
            if ( normalImage == null && (pressImage != null || highlightImage != null || disableImage != null) ){
                normalImage = bgImage;
            }

            // 当たり判定拡張をぶら下げる親のTransform
            Transform raycastParentTransform = go.transform;
            var button = go.AddComponent<Button>();
            // ColorTintボタン.
            if (bgImage != null && normalImage == null )
            {
                button.transition = Selectable.Transition.ColorTint;
                button.targetGraphic = bgImage;
                raycastParentTransform = bgImage.transform;
            }
            // SpriteSwapボタン.
            else{
                button.transition = Selectable.Transition.SpriteSwap;
                button.targetGraphic = normalImage;
                raycastParentTransform = normalImage.transform;
                var sprites = button.spriteState;
                sprites.pressedSprite = pressImage?.sprite;
                sprites.highlightedSprite = highlightImage?.sprite;
                sprites.disabledSprite = disableImage?.sprite;
                sprites.selectedSprite = selectImage?.sprite;
                button.spriteState = sprites;

                // 不要なImageを削除.
                GameObject.DestroyImmediate(pressImage?.gameObject);
                GameObject.DestroyImmediate(highlightImage?.gameObject);
                GameObject.DestroyImmediate(disableImage?.gameObject);
                GameObject.DestroyImmediate(selectImage?.gameObject);
            }

            SetStretch(go, renderer);
            SetPivot(go, renderer);

            // 当たり判定だけ大きくする.
            if ( isExpandRaycastPadding ){
                var hitbox = new GameObject("RaycastPadding");
                var rt = hitbox.AddComponent<RectTransform>();
                var rp = hitbox.AddComponent<UIRaycastPadding>();

                hitbox.transform.SetParent(raycastParentTransform,false);
                // 端から端迄ストレッチ.
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;

                /// todo: 設定ファイルかインポータで指定できるようにする.
                const float expandSize = 12.0f;
                rt.offsetMin = new Vector2(-expandSize,-expandSize);
                rt.offsetMax = new Vector2( expandSize, expandSize);
            }
            return go;
        }
    }

    public sealed class ListElement : GroupElement
    {
        private string scroll;
        private bool useImageMask = false;
        Scrollbar scrollbar = null;
        Scrollbar.Direction scrollDir = Scrollbar.Direction.BottomToTop;

        public ListElement(Dictionary<string, object> json, Element parent) : base(json, parent, true)
        {
            scroll = "horizontal";
            if (json.ContainsKey("scroll")) scroll = json.Get("scroll");
            useImageMask = false;
            if ( json.ContainsKey("imagemask") ) useImageMask = json.GetBool("imagemask");
        }

        public override GameObject Render(Renderer renderer,Transform parent)
        {
            var go = CreateSelf(renderer);
            var content = new GameObject("Content");
            content.AddComponent<RectTransform>();
            content.transform.SetParent(go.transform);

            SetupScroll(go, content);
            SetMaskImage(renderer, go, content);

            var items = CreateItems(renderer, go, content, parent);
            SetupList(go, items, content);

            // スクロールバーを上のレイヤーに移動させる.
            {
                var scrollRect = go.GetComponent<ScrollRect>();
                /// todo: scroll == "v" || scroll == "vertical" のような形に置き換える.
                if ( scroll.StartsWith("v") )
                {
                    scrollRect.verticalScrollbar = scrollbar;
                }
                else if ( scroll.StartsWith("h") ){
                    scrollRect.horizontalScrollbar = scrollbar;
                }
            }

            SetStretch(go, renderer);
            SetPivot(go, renderer);
            return go;
        }

        public override void PostRender(GameObject go, Element element, Transform parent)
        {
            // このオブジェクトより手前側に持ってくる.
            if ( scrollbar != null ){
                scrollbar.transform.SetAsLastSibling();
            }
        }

        private void SetupScroll(GameObject go, GameObject content)
        {
            var scrollRect = go.AddComponent<ScrollRect>();
            scrollRect.content = content.GetComponent<RectTransform>();

            ListLayoutGroup layoutGroup = null;
            layoutGroup = content.AddComponent<ListLayoutGroup>();
            /// todo: scroll == "v" || scroll == "vertical" のような形に置き換える.
            if ( scroll.StartsWith("v") )
            {
                scrollRect.vertical = true;
                scrollRect.horizontal = false;
                scrollDir = Scrollbar.Direction.BottomToTop;
                layoutGroup.Scroll = Scroll.Vertical;
            }
            /// todo: scroll == "h" || scroll == "horizontal" のような形に置き換える.
            else if ( scroll.StartsWith("h") )
            {
                scrollRect.vertical = false;
                scrollRect.horizontal = true;
                scrollDir = Scrollbar.Direction.LeftToRight;
                layoutGroup.Scroll = Scroll.Horizontal;
            }
        }

        private void SetMaskImage(Renderer renderer, GameObject go, GameObject content)
        {
            var maskImage = go.AddComponent<Image>();

            var dummyMaskImage = CreateDummyMaskImage(renderer);
            dummyMaskImage.transform.SetParent(go.transform);
            go.GetComponent<RectTransform>().CopyTo(content.GetComponent<RectTransform>());
            content.GetComponent<RectTransform>().localPosition = Vector3.zero;
            dummyMaskImage.GetComponent<Image>().CopyTo(maskImage);
            GameObject.DestroyImmediate(dummyMaskImage);

            maskImage.color = new Color(0.0f, 0.0f, 0.0f, 0.0f);
            if ( useImageMask ){
                var mask = go.AddComponent<Mask>();
                mask.showMaskGraphic = false;
            }
            else{
                go.AddComponent<RectMask2D>();
            }
        }

        private GameObject CreateDummyMaskImage(Renderer renderer)
        {
            var maskElement = elements.Find(x => (x is ImageElement && x.name.Equals("Area", StringComparison.OrdinalIgnoreCase)));
            if (maskElement == null) throw new Exception(string.Format("{0} Area not found", name));
            elements.Remove(maskElement);

            var maskImage = maskElement.Render(renderer);
            maskImage.SetActive(false);
            return maskImage;
        }

        private List<GameObject> CreateItems(Renderer renderer, GameObject go,GameObject content, Transform parent)
        {
            scrollbar = null;
            var items = new List<GameObject>();
            foreach (var element in elements)
            {
                // scrollがあったら上に移動させる。
                var scrollbarElem = element as ScrollbarElement;
                if ( scrollbarElem != null )
                {
                    scrollbar = scrollbarElem.Render(renderer,null)?.GetComponent<Scrollbar>();
                    if ( scrollbar != null ){
                        scrollbar.transform.SetParent(parent,true);
                    }
                    continue;
                }
                var item = element as GroupElement;
                if (item == null) throw new Exception(string.Format("{0}'s element {1} is not group", name, element.name));

                var itemObject = item.Render(renderer,go.transform);
                itemObject.transform.SetParent(go.transform);

                var rect = itemObject.GetComponent<RectTransform>();
                var originalPosition = rect.anchoredPosition;

            /// todo: scroll == "v" || scroll == "vertical" のような形に置き換える.
                if ( scroll.StartsWith("v") )
                {
                    rect.anchorMin = new Vector2(0.5f, 1.0f);
                    rect.anchorMax = new Vector2(0.5f, 1.0f);
                    rect.anchoredPosition = new Vector2(originalPosition.x, -rect.rect.height / 2f);
                }
            /// todo: scroll == "h" || scroll == "horizontal" のような形に置き換える.
                else if ( scroll.StartsWith("h") )
                {
                    rect.anchorMin = new Vector2(0.0f, 0.5f);
                    rect.anchorMax = new Vector2(0.0f, 0.5f);
                    rect.anchoredPosition = new Vector2(rect.rect.width / 2f, originalPosition.y);
                }
                items.Add(itemObject);
            }
            return items;
        }

        private void SetupList(GameObject go, List<GameObject> itemSources, GameObject content)
        {
            var list = go.AddComponent<List>();
            list.ItemSources = itemSources;
            list.LayoutGroup = content.GetComponent<ListLayoutGroup>();
        }
    }

    public sealed class ScrollRectElement : GroupElement
    {
        private string scroll;
        private bool useImageMask=false;
        private Scrollbar scrollbar;
        Scrollbar.Direction scrollDir = Scrollbar.Direction.BottomToTop;

        public ScrollRectElement(Dictionary<string, object> json, Element parent) : base(json, parent, true)
        {
            scroll = "horizontal";
            if (json.ContainsKey("scroll")) scroll = json.Get("scroll");
            useImageMask = false;
            if ( json.ContainsKey("imagemask") ) useImageMask = json.GetBool("imagemask");
        }

        public override GameObject Render(Renderer renderer,Transform parent)
        {
            // マスクイメージ以外のエリアを無視するようにする.
            foreach(var element in elements)
            {
                var scrollbarElem = element as ScrollbarElement;
                if ( element.name != "Area" && scrollbarElem == null )
                {
                    // マスクイメージ以外を無視するようにする.
                    element.useArea = false;
                }
            }
            var area = RecalcArea();

            var go = CreateSelf(renderer);
            var content = new GameObject("Content");
            content.AddComponent<RectTransform>();
            content.transform.SetParent(go.transform);

            SetupScroll(go, content);
            SetMaskImage(renderer, go, content);

            scrollbar = null;
            var scrollRect = go.GetComponent<ScrollRect>();
            RenderChildren(renderer, go,(g,element)=>{
                var scrollbarElem = element as ScrollbarElement;
                if ( scrollbarElem != null )
                {
                    scrollbar = g.GetComponent<Scrollbar>();
                    scrollbar.transform.SetParent(parent,true);
                }
                else{
                    g.transform.SetParent(content.transform,true);
                }
            });

            // スクロールバーが生成されてからアタッチする.
            if ( scroll == "v" || scroll == "vertical" )
            {
                scrollRect.verticalScrollbar = scrollbar;
            }
            else if ( scroll == "h" || scroll == "horizontal" )
            {
                scrollRect.horizontalScrollbar = scrollbar;
            }
            else if ( scroll == "free" )
            {
            }

            SetStretch(go, renderer);
            SetPivot(go, renderer);
            return go;
        }

        public override void PostRender(GameObject go, Element element, Transform parent)
        {
            // このオブジェクトより手前側に持ってくる.
            if ( scrollbar != null ){
                scrollbar.transform.SetAsLastSibling();
            }
        }

        private void SetupScroll(GameObject go, GameObject content)
        {
            var scrollRect = go.AddComponent<ScrollRect>();
            scrollRect.content = content.GetComponent<RectTransform>();

            if ( scroll == "v" || scroll == "vertical" )
            {
                scrollRect.vertical = true;
                scrollRect.horizontal = false;
                scrollRect.verticalScrollbar = scrollbar;
                scrollDir = Scrollbar.Direction.BottomToTop;
            }
            else if ( scroll == "h" || scroll == "horizontal" )
            {
                scrollRect.vertical = false;
                scrollRect.horizontal = true;
                scrollRect.horizontalScrollbar = scrollbar;
                scrollDir = Scrollbar.Direction.LeftToRight;
            }
            else if ( scroll == "free" )
            {
                scrollRect.vertical = true;
                scrollRect.horizontal = true;
                //scrollRect.horizontalScrollbar = scrollbar;
                scrollDir = Scrollbar.Direction.LeftToRight;
            }
        }

        private void SetMaskImage(Renderer renderer, GameObject go, GameObject content)
        {
            var maskImage = go.AddComponent<Image>();

            var dummyMaskImage = CreateDummyMaskImage(renderer);
            dummyMaskImage.transform.SetParent(go.transform);
            go.GetComponent<RectTransform>().CopyTo(content.GetComponent<RectTransform>());
            content.GetComponent<RectTransform>().localPosition = Vector3.zero;
            dummyMaskImage.GetComponent<Image>().CopyTo(maskImage);
            GameObject.DestroyImmediate(dummyMaskImage);

            maskImage.color = new Color(1.0f, 1.0f, 1.0f, 1.0f);
            if ( useImageMask ){
                var mask = go.AddComponent<Mask>();
                mask.showMaskGraphic = false;
            }
            else{
                go.AddComponent<RectMask2D>();
            }
        }

        private GameObject CreateDummyMaskImage(Renderer renderer)
        {
            var maskElement = elements.Find(x => (x is ImageElement && x.name.Equals("Area", StringComparison.OrdinalIgnoreCase)));
            if (maskElement == null) throw new Exception(string.Format("{0} Area not found", name));
            elements.Remove(maskElement);

            var maskImage = maskElement.Render(renderer);
            maskImage.SetActive(false);
            return maskImage;
        }
    }


    public sealed class SliderElement : GroupElement
    {
        private string scroll;
        private bool isHandleStretch = false;
        private Area handleArea = Area.None();
        private Area fillArea = Area.None();
        public SliderElement(Dictionary<string, object> json, Element parent) : base(json, parent)
        {
            scroll = "horizontal";
            isHandleStretch = false;
            if (json.ContainsKey("scroll")) scroll = json.Get("scroll");
            if (json.ContainsKey("hstretch")) isHandleStretch = json.GetBool("hstretch");
        }

        private Slider.Direction GetSliderDirection(){
            var direction = Slider.Direction.LeftToRight;

            /// todo: scroll == "v" || scroll == "vertical" のような形に置き換える.
            if ( scroll.StartsWith("v") || scroll.StartsWith("b") ){
                direction = Slider.Direction.BottomToTop;
            }
            else if ( scroll.StartsWith("t")){
                direction = Slider.Direction.TopToBottom;
            }
            else if ( scroll.StartsWith("h") || scroll.StartsWith("l") ) {
                direction = Slider.Direction.LeftToRight;
            }
            else if ( scroll.StartsWith("r")){
                direction = Slider.Direction.RightToLeft;
            }
            return direction;
        }

        // 塗りつぶしのセットアップ.
        private void SetupFill(Slider slider,RectTransform fillRect)
        {
            if (fillRect != null)
            {
                fillRect.localScale = Vector2.zero;
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = Vector2.one;
                fillRect.anchoredPosition = Vector2.zero;
                fillRect.sizeDelta = Vector2.zero;
                fillRect.localScale = Vector3.one;
                slider.fillRect = fillRect;
            }
        }

        // ハンドルのセットアップ
        private void SetupHandle(Slider slider,RectTransform handleRect)
        {
            var handleImage = handleRect == null ? null : handleRect.GetComponent<Image>();
            if (handleImage != null)
            {
                handleImage.raycastTarget = true;
                handleRect.anchoredPosition = Vector2.zero;
                handleRect.anchorMin = new Vector2(0.0f, 0.0f);
                handleRect.anchorMax = new Vector2(1.0f, 0.0f);

                GameObject noStretchHandle = null;
                // ハンドルをストレッチさせたくない場合は子供にぶら下げるしかない.
                if ( !isHandleStretch ){
                    noStretchHandle = GameObject.Instantiate(handleImage.gameObject);
                }

                slider.direction = GetSliderDirection();
                slider.value = 0.0f;
                slider.targetGraphic = handleImage;
                slider.handleRect = handleRect;

                // ハンドルをストレッチさせたくない場合は子供にぶら下げるしかない.
                if ( !isHandleStretch && noStretchHandle != null ){
                    var nshrect = noStretchHandle.GetComponent<RectTransform>();
                    noStretchHandle.transform.SetParent(handleRect.transform,true);
                    nshrect.name = handleImage.name;
                    nshrect.anchoredPosition = Vector2.zero;
                    nshrect.anchorMin = new Vector2(0.5f, 0.5f);
                    nshrect.anchorMax = new Vector2(0.5f, 0.5f);
                    nshrect.sizeDelta = handleArea.Size;
                    GameObject.DestroyImmediate(handleImage);
                }
            }
        }

        public override GameObject Render(Renderer renderer,Transform parent)
        {
            var go = CreateSelf(renderer);

            RectTransform fillRect = null;
            RectTransform handleRect = null;

            var useTouch = false;

            RenderChildren(renderer, go, (g, element) =>
            {
                var image = element as ImageElement;
                if ( image == null ) return;

                g.GetComponent<Image>().raycastTarget = false;
                if ( CompareName(element,"Fill") )
                {
                    fillRect = g.GetComponent<RectTransform>();
                    fillArea = element.CalcArea();
                }
                else if ( CompareName(element,"Handle") )
                {
                    handleRect = g.GetComponent<RectTransform>();
                    useTouch = element.useTouch;
                    handleArea = element.CalcArea();
                    // ハンドルを無視するようにする.
                    element.useArea = false;
                }
                else{
                    // Fill以外無視するようにする.
                    element.useArea = false;
                }
            });

            var slider = go.AddComponent<Slider>();
            slider.transition = Selectable.Transition.None;
            slider.interactable = useTouch;

            SetupFill(slider,fillRect);
            SetupHandle(slider,handleRect);

            // ハンドルが見つかった場合、ハンドルを無視して再計算が必要.
            if ( handleRect != null ){
                var area = RecalcArea();
                var sliderRect = slider.GetComponent<RectTransform>();
                sliderRect.sizeDelta = area.Size;
            }

            SetStretch(go, renderer);
            SetPivot(go, renderer);
            return go;
        }
    }

    public sealed class ScrollbarElement : GroupElement
    {
        public ScrollbarElement(Dictionary<string, object> json, Element parent) : base(json, parent)
        {
        }

        public override GameObject Render(Renderer renderer,Transform parent)
        {
            var go = CreateSelf(renderer);

            RectTransform handleRect = null;
            RenderChildren(renderer, go, (g, element) =>
            {
                var image = element as ImageElement;
                if (handleRect != null || image == null) return;
                if (element.name.Equals("Handle", StringComparison.OrdinalIgnoreCase)) handleRect = g.GetComponent<RectTransform>();
                g.GetComponent<Image>().raycastTarget = image.useTouch;
            });

            var scrollbar = go.AddComponent<Scrollbar>();
            var handleImage = handleRect == null ? null : handleRect.GetComponent<Image>();
            if (handleImage != null)
            {
                handleRect.anchoredPosition = Vector2.zero;
                handleRect.anchorMin = new Vector2(0.0f, 0.0f);
                handleRect.anchorMax = new Vector2(1.0f, 0.0f);

                scrollbar.direction = Scrollbar.Direction.BottomToTop;
                scrollbar.value = 1.0f;
                scrollbar.targetGraphic = handleImage;
                scrollbar.handleRect = handleRect;

                handleRect.sizeDelta = Vector2.zero;
            }

            SetStretch(go, renderer);
            SetPivot(go, renderer);
            return go;
        }
    }

    public sealed class ToggleElement : GroupElement
    {
        public ToggleElement(Dictionary<string, object> json, Element parent) : base(json, parent)
        {
        }

        public override GameObject Render(Renderer renderer,Transform parent)
        {
            var go = CreateSelf(renderer);

            Graphic lastImage = null;
            Graphic checkImage = null;
            RenderChildren(renderer, go, (g, element) =>
            {
                var image = element as ImageElement;
                if (image == null) return;
                if (lastImage == null) lastImage = g.GetComponent<Image>();
                if (element.name.Contains("Check") || element.name.Contains("check")) checkImage = g.GetComponent<Image>();
            });

            var toggle = go.AddComponent<Toggle>();
            toggle.targetGraphic = lastImage;
            toggle.graphic = checkImage;

            SetStretch(go, renderer);
            SetPivot(go, renderer);
            return go;
        }
    }

    public sealed class NullElement : Element
    {
        public NullElement(Dictionary<string, object> json, Element parent) : base(json, parent)
        {
        }

        public override GameObject Render(Renderer renderer,Transform parent)
        {
            var go = CreateUIGameObject(renderer);
            SetStretch(go, renderer);
            SetPivot(go, renderer);
            return go;
        }

        public override Area CalcArea()
        {
            return Area.None();
        }
    }
}
