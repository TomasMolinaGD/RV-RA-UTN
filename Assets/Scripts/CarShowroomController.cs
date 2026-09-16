using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CarShowroomController : MonoBehaviour
{
    private const float ShowroomWorldY = -1000f;
    private const float TargetModelSize = 3.25f;
    private const float MinimumCameraDistance = 3.8f;
    private const float MaximumCameraDistance = 7.2f;

    private Action _openRequested;
    private Action _nextRequested;
    private Action _clearRequested;
    private Action _toggleInfoRequested;

    private Canvas _canvas;
    private GameObject _scanPrompt;
    private Text _scanPromptText;
    private GameObject _previewControls;
    private GameObject _showroomPanel;
    private RawImage _showroomImage;
    private Text _showroomTitle;
    private Text _infoToggleLabel;
    private RenderTexture _renderTexture;
    private int _renderScreenWidth;
    private int _renderScreenHeight;

    private GameObject _showroomWorld;
    private Transform _turntablePivot;
    private Camera _showroomCamera;
    private GameObject _currentModel;
    private float _cameraDistance = 5.6f;
    private Vector2 _lastPointerPosition;
    private bool _dragging;

    public bool IsOpen { get; private set; }
    public GameObject CurrentModel => _currentModel;

    public void Initialize(
        Action openRequested,
        Action nextRequested,
        Action clearRequested,
        Action toggleInfoRequested)
    {
        _openRequested = openRequested;
        _nextRequested = nextRequested;
        _clearRequested = clearRequested;
        _toggleInfoRequested = toggleInfoRequested;

        EnsureEventSystem();
        BuildShowroomWorld();
        BuildInterface();
        ShowScanningPrompt();
    }

    public void ShowMarkerPreview()
    {
        if (IsOpen)
            return;

        _scanPrompt.SetActive(false);
        _previewControls.SetActive(true);
    }

    public void ShowScanningPrompt(bool waitingForMarkerRelease = false)
    {
        if (_scanPrompt == null)
            return;

        IsOpen = false;
        _showroomPanel.SetActive(false);
        _previewControls.SetActive(false);
        _showroomWorld.SetActive(false);
        _scanPrompt.SetActive(true);
        _scanPromptText.text = waitingForMarkerRelease
            ? "Alejá la cámara del marcador y volvé a enfocarlo"
            : "Escaneá uno de los marcadores para comenzar";
    }

    public GameObject Open(string brandName, GameObject prefab)
    {
        IsOpen = true;
        _scanPrompt.SetActive(false);
        _previewControls.SetActive(false);
        _showroomWorld.SetActive(true);
        _showroomPanel.SetActive(true);
        _showroomTitle.text = brandName.ToUpperInvariant();
        ResetView();
        return DisplayModel(prefab);
    }

    public void SetInfoVisible(bool visible)
    {
        if (_infoToggleLabel != null)
            _infoToggleLabel.text = visible ? "OCULTAR FICHA" : "MOSTRAR FICHA";
    }

    public GameObject DisplayModel(GameObject prefab)
    {
        if (_currentModel != null)
            Destroy(_currentModel);

        if (prefab == null)
            return null;

        _currentModel = Instantiate(prefab, _turntablePivot);
        _currentModel.name = prefab.name;
        _currentModel.transform.localPosition = Vector3.zero;
        _currentModel.transform.localRotation = Quaternion.identity;
        NormalizeAndCenter(_currentModel);
        return _currentModel;
    }

    public void Close(bool waitingForMarkerRelease)
    {
        if (_currentModel != null)
        {
            Destroy(_currentModel);
            _currentModel = null;
        }

        ShowScanningPrompt(waitingForMarkerRelease);
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        EnsureRenderTextureMatchesScreen();
        HandleTouchInput();

#if UNITY_EDITOR
        HandleMouseInput();
#endif
    }

    private void HandleTouchInput()
    {
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                _dragging = !IsPointerOverUi(touch.fingerId);
                _lastPointerPosition = touch.position;
            }
            else if (_dragging && touch.phase == TouchPhase.Moved)
            {
                Vector2 delta = touch.position - _lastPointerPosition;
                RotateModel(delta);
                _lastPointerPosition = touch.position;
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                _dragging = false;
            }
        }
        else if (Input.touchCount >= 2)
        {
            Touch first = Input.GetTouch(0);
            Touch second = Input.GetTouch(1);
            Vector2 previousFirst = first.position - first.deltaPosition;
            Vector2 previousSecond = second.position - second.deltaPosition;
            float previousDistance = Vector2.Distance(previousFirst, previousSecond);
            float currentDistance = Vector2.Distance(first.position, second.position);
            ApplyZoom((currentDistance - previousDistance) * 0.006f);
            _dragging = false;
        }
    }

#if UNITY_EDITOR
    private void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            _dragging = !IsPointerOverUi();
            _lastPointerPosition = Input.mousePosition;
        }
        else if (_dragging && Input.GetMouseButton(0))
        {
            Vector2 currentPosition = Input.mousePosition;
            RotateModel(currentPosition - _lastPointerPosition);
            _lastPointerPosition = currentPosition;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            _dragging = false;
        }

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > Mathf.Epsilon)
            ApplyZoom(scroll * 0.35f);
    }
#endif

    private void RotateModel(Vector2 delta)
    {
        if (_turntablePivot == null)
            return;

        Vector3 angles = _turntablePivot.localEulerAngles;
        float pitch = NormalizeAngle(angles.x) - delta.y * 0.08f;
        pitch = Mathf.Clamp(pitch, -12f, 24f);
        float yaw = angles.y - delta.x * 0.18f;
        _turntablePivot.localRotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void ApplyZoom(float amount)
    {
        _cameraDistance = Mathf.Clamp(
            _cameraDistance - amount,
            MinimumCameraDistance,
            MaximumCameraDistance);
        UpdateCameraPosition();
    }

    private void ResetView()
    {
        _cameraDistance = 5.6f;
        _turntablePivot.localRotation = Quaternion.Euler(8f, -25f, 0f);
        UpdateCameraPosition();
    }

    private void UpdateCameraPosition()
    {
        if (_showroomCamera == null)
            return;

        _showroomCamera.transform.localPosition = new Vector3(0f, 0.75f, -_cameraDistance);
        _showroomCamera.transform.LookAt(_turntablePivot.position + Vector3.up * 0.35f);
    }

    private void NormalizeAndCenter(GameObject model)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return;

        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
            bounds.Encapsulate(renderers[index].bounds);

        float longestSide = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (longestSide <= Mathf.Epsilon)
            return;

        model.transform.localScale *= TargetModelSize / longestSide;

        bounds = model.GetComponentsInChildren<Renderer>(true)[0].bounds;
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            bounds.Encapsulate(renderer.bounds);

        Vector3 shift = _turntablePivot.position - new Vector3(bounds.center.x, _turntablePivot.position.y, bounds.center.z);
        shift.y = (_turntablePivot.position.y - 0.76f) - bounds.min.y;
        model.transform.position += shift;
    }

    private void BuildShowroomWorld()
    {
        _showroomWorld = new GameObject("ShowroomWorld");
        _showroomWorld.transform.SetParent(transform, false);
        _showroomWorld.transform.position = new Vector3(0f, ShowroomWorldY, 0f);

        GameObject pivotObject = new GameObject("TurntablePivot");
        pivotObject.transform.SetParent(_showroomWorld.transform, false);
        _turntablePivot = pivotObject.transform;

        GameObject cameraObject = new GameObject("ShowroomCamera", typeof(Camera));
        cameraObject.transform.SetParent(_showroomWorld.transform, false);
        _showroomCamera = cameraObject.GetComponent<Camera>();
        _showroomCamera.clearFlags = CameraClearFlags.SolidColor;
        _showroomCamera.backgroundColor = new Color(0.008f, 0.018f, 0.032f, 1f);
        _showroomCamera.fieldOfView = 38f;
        _showroomCamera.nearClipPlane = 0.05f;
        _showroomCamera.farClipPlane = 40f;

        EnsureRenderTextureMatchesScreen();

        CreateDirectionalLight("ShowroomKeyLight", new Vector3(35f, -35f, 0f), 2.2f, new Color(1f, 0.91f, 0.80f));
        CreateDirectionalLight("ShowroomFillLight", new Vector3(20f, 145f, 0f), 1.25f, new Color(0.45f, 0.68f, 1f));
        ResetView();
        _showroomWorld.SetActive(false);
    }

    private void EnsureRenderTextureMatchesScreen()
    {
        int screenWidth = Mathf.Max(1, Screen.width);
        int screenHeight = Mathf.Max(1, Screen.height);
        if (_renderTexture != null &&
            screenWidth == _renderScreenWidth &&
            screenHeight == _renderScreenHeight)
        {
            return;
        }

        _renderScreenWidth = screenWidth;
        _renderScreenHeight = screenHeight;

        int longestScreenSide = Mathf.Max(screenWidth, screenHeight);
        float scale = Mathf.Min(1f, 1280f / longestScreenSide);
        int width = Mathf.Max(480, Mathf.RoundToInt(screenWidth * scale));
        int height = Mathf.Max(480, Mathf.RoundToInt(screenHeight * scale));

        if (_showroomCamera != null)
            _showroomCamera.targetTexture = null;

        if (_renderTexture != null)
        {
            _renderTexture.Release();
            Destroy(_renderTexture);
        }

        _renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            name = "ShowroomRenderTexture",
            antiAliasing = 2
        };
        _renderTexture.Create();

        if (_showroomCamera != null)
            _showroomCamera.targetTexture = _renderTexture;

        if (_showroomImage != null)
            _showroomImage.texture = _renderTexture;
    }

    private void CreateDirectionalLight(string objectName, Vector3 rotation, float intensity, Color color)
    {
        GameObject lightObject = new GameObject(objectName, typeof(Light));
        lightObject.transform.SetParent(_showroomWorld.transform, false);
        lightObject.transform.localRotation = Quaternion.Euler(rotation);
        Light lightComponent = lightObject.GetComponent<Light>();
        lightComponent.type = LightType.Directional;
        lightComponent.intensity = intensity;
        lightComponent.color = color;
        lightComponent.shadows = LightShadows.Soft;
    }

    private void BuildInterface()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameObject canvasObject = new GameObject("ShowroomCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        _canvas = canvasObject.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 200;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        _showroomPanel = new GameObject("ShowroomPanel", typeof(RectTransform), typeof(RawImage));
        _showroomPanel.transform.SetParent(canvasObject.transform, false);
        RectTransform showroomRect = _showroomPanel.GetComponent<RectTransform>();
        showroomRect.anchorMin = Vector2.zero;
        showroomRect.anchorMax = Vector2.one;
        showroomRect.offsetMin = Vector2.zero;
        showroomRect.offsetMax = Vector2.zero;
        _showroomImage = _showroomPanel.GetComponent<RawImage>();
        _showroomImage.texture = _renderTexture;
        _showroomImage.raycastTarget = false;

        GameObject topShade = CreatePanel(
            "TopShade", _showroomPanel.transform,
            new Color(0.015f, 0.035f, 0.06f, 0.94f),
            new Vector2(0f, 0.84f), Vector2.one);

        _showroomTitle = CreateText(
            "ShowroomTitle", topShade.transform, font, 38, FontStyle.Bold, Color.white,
            new Vector2(0f, 0.50f), new Vector2(1f, 1f),
            new Vector2(42f, 0f), new Vector2(-42f, -4f), TextAnchor.MiddleCenter);

        CreateButton(
            "ClearButton", topShade.transform, font, "LIMPIAR",
            new Vector2(0.04f, 0.07f), new Vector2(0.30f, 0.48f),
            new Color(0.22f, 0.27f, 0.33f, 1f), () => _clearRequested?.Invoke());

        Button infoToggleButton = CreateButton(
            "InfoToggleButton", topShade.transform, font, "OCULTAR FICHA",
            new Vector2(0.33f, 0.07f), new Vector2(0.64f, 0.48f),
            new Color(0.12f, 0.24f, 0.36f, 1f), () => _toggleInfoRequested?.Invoke());
        _infoToggleLabel = infoToggleButton.GetComponentInChildren<Text>();

        CreateButton(
            "NextButton", topShade.transform, font, "SIGUIENTE MODELO",
            new Vector2(0.67f, 0.07f), new Vector2(0.96f, 0.48f),
            new Color(0.08f, 0.36f, 0.72f, 1f), () => _nextRequested?.Invoke());

        Text gestureHint = CreateText(
            "GestureHint", _showroomPanel.transform, font, 25, FontStyle.Normal,
            new Color(0.76f, 0.84f, 0.93f, 1f),
            new Vector2(0.05f, 0.69f), new Vector2(0.95f, 0.76f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
        gestureHint.text = "Arrastrá para girar  ·  Pellizcá para acercar";

        _previewControls = CreatePanel(
            "PreviewControls", canvasObject.transform, Color.clear,
            new Vector2(0.52f, 0.83f), new Vector2(0.96f, 0.94f));
        CreateButton(
            "OpenShowroomButton", _previewControls.transform, font, "VER EN DETALLE",
            Vector2.zero, Vector2.one,
            new Color(0.08f, 0.36f, 0.72f, 0.96f), () => _openRequested?.Invoke());

        _scanPrompt = CreatePanel(
            "ScanPrompt", canvasObject.transform,
            new Color(0.015f, 0.035f, 0.06f, 0.88f),
            new Vector2(0.06f, 0.84f), new Vector2(0.94f, 0.94f));
        _scanPromptText = CreateText(
            "ScanPromptText", _scanPrompt.transform, font, 28, FontStyle.Bold, Color.white,
            Vector2.zero, Vector2.one, new Vector2(30f, 8f), new Vector2(-30f, -8f),
            TextAnchor.MiddleCenter);

        _showroomPanel.SetActive(false);
        _previewControls.SetActive(false);
    }

    private static GameObject CreatePanel(string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = color;
        return panel;
    }

    private static Text CreateText(
        string name,
        Transform parent,
        Font font,
        int size,
        FontStyle style,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax,
        TextAnchor alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static Button CreateButton(
        string name,
        Transform parent,
        Font font,
        string label,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Color color,
        UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = CreatePanel(name, parent, color, anchorMin, anchorMax);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonObject.GetComponent<Image>();
        button.onClick.AddListener(action);

        Text text = CreateText(
            "Label", buttonObject.transform, font, 25, FontStyle.Bold, Color.white,
            Vector2.zero, Vector2.one, new Vector2(12f, 6f), new Vector2(-12f, -6f),
            TextAnchor.MiddleCenter);
        text.text = label;
        return button;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static bool IsPointerOverUi(int pointerId = -1)
    {
        if (EventSystem.current == null)
            return false;

        return pointerId >= 0
            ? EventSystem.current.IsPointerOverGameObject(pointerId)
            : EventSystem.current.IsPointerOverGameObject();
    }

    private static float NormalizeAngle(float angle)
    {
        return angle > 180f ? angle - 360f : angle;
    }

    private void OnDestroy()
    {
        if (_renderTexture == null)
            return;

        _renderTexture.Release();
        Destroy(_renderTexture);
    }
}
