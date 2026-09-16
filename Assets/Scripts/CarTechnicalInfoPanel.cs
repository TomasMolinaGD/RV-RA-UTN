using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CarTechnicalInfoPanel : MonoBehaviour
{
    private readonly struct CarSpec
    {
        public readonly string Model;
        public readonly string Year;
        public readonly string Category;
        public readonly string Engine;
        public readonly string Power;
        public readonly string Drive;
        public readonly string TankCapacity;
        public readonly string EstimatedRange;

        public CarSpec(
            string model,
            string year,
            string category,
            string engine,
            string power,
            string drive,
            string tankCapacity,
            string estimatedRange)
        {
            Model = model;
            Year = year;
            Category = category;
            Engine = engine;
            Power = power;
            Drive = drive;
            TankCapacity = tankCapacity;
            EstimatedRange = estimatedRange;
        }
    }

    private static readonly Dictionary<string, CarSpec> SpecsByPrefabName =
        new Dictionary<string, CarSpec>(StringComparer.OrdinalIgnoreCase)
        {
            ["VW-Virtus"] = new CarSpec("Virtus TSI", "2024", "Sedán", "Motor 1.0 TSI", "116 CV", "Tracción delantera", "52 L", "750 km"),
            ["VW-Taos"] = new CarSpec("Taos", "2021", "SUV", "Motor 1.4 TSI", "150 CV", "Tracción delantera", "50 L", "650 km"),
            ["VW-Polo"] = new CarSpec("Polo 170 TSI", "2024", "Hatchback", "Motor 1.0 TSI", "101 CV", "Tracción delantera", "52 L", "750 km"),
            ["Ford Focus"] = new CarSpec("Focus RS", "2016", "Hatchback", "Motor 2.3 EcoBoost", "350 CV", "Tracción integral", "53 L", "480 km"),
            ["Ford Fiesta"] = new CarSpec("Fiesta ST", "2014", "Hatchback", "Motor 1.6 EcoBoost", "197 CV", "Tracción delantera", "47 L", "600 km"),
            ["Ford Shelby Mustang"] = new CarSpec("Mustang Shelby GT500", "2020", "Coupé", "Motor V8 5.2 supercargado", "760 CV", "Tracción trasera", "61 L", "360 km"),
            ["Audi-Q5"] = new CarSpec("Q5 45 TFSI", "2023", "SUV", "Motor 2.0 TFSI", "261 CV", "Tracción quattro", "70 L", "700 km"),
            ["Audi-Q3"] = new CarSpec("Q3 40 TFSI", "2023", "SUV", "Motor 2.0 TFSI", "184 CV", "Tracción quattro", "60 L", "600 km"),
            ["Audi-A3"] = new CarSpec("A3 2.0T", "2009", "Hatchback", "Motor 2.0 TFSI", "200 CV", "Tracción delantera", "55 L", "700 km")
        };

    private GameObject _panel;
    private Canvas _canvas;
    private Image _accent;
    private Text _title;
    private Text _subtitle;
    private Text _details;
    private bool _showroomMode;

    private void Awake()
    {
        BuildInterface();
        Hide();
    }

    public void ShowFor(GameObject carInstance, string brandName)
    {
        if (carInstance == null)
        {
            Hide();
            return;
        }

        string prefabName = carInstance.name.Replace("(Clone)", string.Empty).Trim();
        if (!SpecsByPrefabName.TryGetValue(prefabName, out CarSpec spec))
        {
            _title.text = $"{brandName} {prefabName}";
            _subtitle.text = "Ficha técnica";
            _details.text = $"Datos técnicos no disponibles\n{GetInteractionHint()}";
        }
        else
        {
            _title.text = $"{brandName} {spec.Model}";
            _subtitle.text = $"Año: {spec.Year}   •   Tipo: {spec.Category}";
            _details.text =
                $"{spec.Engine}   •   {spec.Power}   •   {spec.Drive}\n" +
                $"Tanque: {spec.TankCapacity}   •   Autonomía estimada: ~{spec.EstimatedRange}\n" +
                GetInteractionHint();
        }

        _accent.color = GetBrandColor(brandName);
        _panel.SetActive(true);
    }

    public void SetShowroomMode(bool enabled)
    {
        _showroomMode = enabled;
        if (_canvas != null)
            _canvas.sortingOrder = enabled ? 300 : 100;
    }

    public void Hide()
    {
        if (_panel != null)
            _panel.SetActive(false);
    }

    private void BuildInterface()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject canvasObject = new GameObject("CarTechnicalInfoCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        _canvas = canvasObject.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        _panel = new GameObject("TechnicalInfoPanel", typeof(RectTransform), typeof(Image));
        _panel.transform.SetParent(canvasObject.transform, false);

        RectTransform panelRect = _panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 32f);
        panelRect.sizeDelta = new Vector2(-64f, 320f);

        Image background = _panel.GetComponent<Image>();
        background.color = new Color(0.025f, 0.055f, 0.095f, 0.94f);

        GameObject accentObject = new GameObject("BrandAccent", typeof(RectTransform), typeof(Image));
        accentObject.transform.SetParent(_panel.transform, false);
        RectTransform accentRect = accentObject.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.anchoredPosition = Vector2.zero;
        accentRect.sizeDelta = new Vector2(12f, 0f);
        _accent = accentObject.GetComponent<Image>();

        _title = CreateText(
            "ModelName", font, 42, FontStyle.Bold, Color.white,
            new Vector2(0f, 0.74f), new Vector2(1f, 1f),
            new Vector2(42f, 0f), new Vector2(-28f, -10f));

        _subtitle = CreateText(
            "YearAndCategory", font, 27, FontStyle.Bold, new Color(0.68f, 0.80f, 0.94f),
            new Vector2(0f, 0.57f), new Vector2(1f, 0.75f),
            new Vector2(42f, 0f), new Vector2(-28f, 0f));

        _details = CreateText(
            "TechnicalDetails", font, 23, FontStyle.Normal, new Color(0.91f, 0.94f, 0.97f),
            new Vector2(0f, 0.04f), new Vector2(1f, 0.58f),
            new Vector2(42f, 6f), new Vector2(-28f, 0f));
    }

    private Text CreateText(
        string objectName,
        Font font,
        int fontSize,
        FontStyle fontStyle,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(_panel.transform, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = color;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static Color GetBrandColor(string brandName)
    {
        if (brandName.Equals("Audi", StringComparison.OrdinalIgnoreCase))
            return new Color(0.85f, 0.08f, 0.12f, 1f);

        if (brandName.Equals("Ford", StringComparison.OrdinalIgnoreCase))
            return new Color(0.05f, 0.34f, 0.72f, 1f);

        return new Color(0.15f, 0.55f, 0.92f, 1f);
    }

    private string GetInteractionHint()
    {
        return _showroomMode
            ? "Arrastrá para girar · Pellizcá para acercar"
            : "Tocá la pantalla para cambiar de modelo";
    }
}
