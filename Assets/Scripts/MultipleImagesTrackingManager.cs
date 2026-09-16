using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class MultipleImagesTrackingManager : MonoBehaviour
{
    [System.Serializable]
    public struct BrandGroup
    {
        public string brandName;
        
        [Tooltip("Lista de logos pertenecientes a esta marca")]
        public List<string> logoNames; 
        
        [Tooltip("Los prefabs de autos de esta marca")]
        public List<GameObject> carPrefabs; 
    }

    [Header("Configuración de Marcas")]
    [SerializeField] private List<BrandGroup> brandGroups = new List<BrandGroup>();

    [Header("Presentación de modelos")]
    [SerializeField, Min(0.05f)] private float targetCarLengthMeters = 0.25f;

    private ARTrackedImageManager _trackedImageManager;

    private Dictionary<string, BrandGroup> _logoToBrandMap = new Dictionary<string, BrandGroup>();
    private Dictionary<string, List<GameObject>> _spawnedBrandCars = new Dictionary<string, List<GameObject>>();
    private Dictionary<string, int> _activeCarIndexPerBrand = new Dictionary<string, int>();
    private Dictionary<string, Transform> _brandWorldContainers = new Dictionary<string, Transform>();
    private CarTechnicalInfoPanel _technicalInfoPanel;
    private string _displayedBrandName;

    private void Awake()
    {
        _trackedImageManager = GetComponent<ARTrackedImageManager>();
        _technicalInfoPanel = GetComponent<CarTechnicalInfoPanel>();
        if (_technicalInfoPanel == null)
            _technicalInfoPanel = gameObject.AddComponent<CarTechnicalInfoPanel>();

        SetupSceneElements();
    }

    private void OnEnable()
    {
        if (_trackedImageManager != null)
            _trackedImageManager.trackablesChanged.AddListener(OnImagesTrackedChanged);
    }

    private void OnDisable()
    {
        if (_trackedImageManager != null)
            _trackedImageManager.trackablesChanged.RemoveListener(OnImagesTrackedChanged);

        HideAllCars();
    }

    private void Update()
    {
        bool switchRequested = Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began;

#if UNITY_EDITOR
        // Permite probar el cambio de modelo con clic izquierdo en XR Simulation.
        switchRequested |= Input.GetMouseButtonDown(0);
#endif

        if (switchRequested)
            SwitchCarModelForActiveBrands();
    }

    private void SetupSceneElements()
    {
        _logoToBrandMap.Clear();
        _spawnedBrandCars.Clear();
        _activeCarIndexPerBrand.Clear();
        _brandWorldContainers.Clear();

        foreach (var brand in brandGroups)
        {
            if (brand.carPrefabs == null || brand.carPrefabs.Count == 0) continue;

            List<GameObject> spawnedCarsForThisBrand = new List<GameObject>();

            foreach (var prefab in brand.carPrefabs)
            {
                if (prefab == null) continue;

                GameObject carInstance = Instantiate(prefab, Vector3.zero, Quaternion.identity);
                NormalizeCarScale(carInstance);
                carInstance.SetActive(false);
                spawnedCarsForThisBrand.Add(carInstance);
            }

            if (!_spawnedBrandCars.ContainsKey(brand.brandName))
            {
                _spawnedBrandCars.Add(brand.brandName, spawnedCarsForThisBrand);
                _activeCarIndexPerBrand.Add(brand.brandName, 0);
            }

            foreach (var logoName in brand.logoNames)
            {
                if (!string.IsNullOrEmpty(logoName) && !_logoToBrandMap.ContainsKey(logoName))
                {
                    _logoToBrandMap.Add(logoName, brand);
                }
            }
        }
    }

    private void NormalizeCarScale(GameObject carInstance)
    {
        Renderer[] renderers = carInstance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            Debug.LogWarning($"[AR ESCALA] '{carInstance.name}' no contiene renderers para calcular su tamaño.");
            return;
        }

        Bounds combinedBounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
            combinedBounds.Encapsulate(renderers[index].bounds);

        float longestSide = Mathf.Max(
            combinedBounds.size.x,
            combinedBounds.size.y,
            combinedBounds.size.z);

        if (longestSide <= Mathf.Epsilon)
        {
            Debug.LogWarning($"[AR ESCALA] No se pudo calcular el tamaño de '{carInstance.name}'.");
            return;
        }

        float scaleFactor = targetCarLengthMeters / longestSide;
        carInstance.transform.localScale *= scaleFactor;
    }

    private void OnImagesTrackedChanged(ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs)
    {
        foreach (var trackedImage in eventArgs.added)
        {
            Debug.Log($"[AR DETECCIÓN] ¡Imagen NUEVA detectada!: {trackedImage.referenceImage.name}");
            UpdateBrandPositionAndVisibility(trackedImage);
        }

        foreach (var trackedImage in eventArgs.updated)
        {
            UpdateBrandPositionAndVisibility(trackedImage);
        }
    }

    private void UpdateBrandPositionAndVisibility(ARTrackedImage trackedImage)
    {
        if (trackedImage == null) return;
        
        string logoName = trackedImage.referenceImage.name;

        if (!_logoToBrandMap.TryGetValue(logoName, out BrandGroup brand))
        {
            Debug.LogWarning($"[AR DETECCIÓN] Se escaneó '{logoName}' pero no está registrado en el Inspector.");
            return;
        }

        if (!_spawnedBrandCars.TryGetValue(brand.brandName, out List<GameObject> brandCars))
            return;

        if (brandCars == null || brandCars.Count == 0) return;

        // Si la cámara detecta la imagen claramente
        if (trackedImage.trackingState == TrackingState.Tracking)
        {
            // Si la marca no tiene contenedor en la escena, se crea uno para agrupar sus modelos.
            if (!_brandWorldContainers.ContainsKey(brand.brandName))
            {
                GameObject container = new GameObject($"Anchor_{brand.brandName}");
                _brandWorldContainers.Add(brand.brandName, container.transform);

                foreach (var car in brandCars)
                {
                    car.transform.SetParent(container.transform);
                    car.transform.localPosition = Vector3.zero;
                    car.transform.localRotation = Quaternion.identity;
                }
                
                Debug.Log($"[AR DETECCIÓN] Autos de {brand.brandName} anclados en la posición escaneada.");
            }

            // El contenido debe acompañar al marcador mientras AR Foundation actualiza su pose.
            Transform brandContainer = _brandWorldContainers[brand.brandName];
            brandContainer.SetPositionAndRotation(
                trackedImage.transform.position,
                trackedImage.transform.rotation);

            // Muestra el modelo activo
            int selectedIndex = _activeCarIndexPerBrand[brand.brandName];
            for (int i = 0; i < brandCars.Count; i++)
            {
                brandCars[i].SetActive(i == selectedIndex);
            }

            _displayedBrandName = brand.brandName;
            RefreshTechnicalInfoPanel(brand.brandName);
        }
        else
        {
            SetBrandCarsActive(brand.brandName, false);
            RefreshTechnicalInfoPanel();
        }
    }

    private void SetBrandCarsActive(string brandName, bool isActive)
    {
        if (!_spawnedBrandCars.TryGetValue(brandName, out List<GameObject> brandCars))
            return;

        foreach (var car in brandCars)
        {
            if (car != null)
                car.SetActive(isActive);
        }
    }

    private void HideAllCars()
    {
        foreach (string brandName in _spawnedBrandCars.Keys)
            SetBrandCarsActive(brandName, false);

        _displayedBrandName = null;
        _technicalInfoPanel?.Hide();
    }

    private void SwitchCarModelForActiveBrands()
    {
        foreach (var brandPair in _spawnedBrandCars)
        {
            string brandName = brandPair.Key;
            List<GameObject> brandCars = brandPair.Value;

            bool isVisible = brandCars.Exists(car => car.activeSelf);

            if (isVisible)
            {
                int currentIndex = _activeCarIndexPerBrand[brandName];
                brandCars[currentIndex].SetActive(false);

                int nextIndex = (currentIndex + 1) % brandCars.Count;
                _activeCarIndexPerBrand[brandName] = nextIndex;

                brandCars[nextIndex].SetActive(true);
            }
        }

        RefreshTechnicalInfoPanel(_displayedBrandName);
    }

    private void RefreshTechnicalInfoPanel(string preferredBrandName = null)
    {
        if (_technicalInfoPanel == null)
            return;

        if (TryGetVisibleCar(preferredBrandName, out GameObject preferredCar))
        {
            _displayedBrandName = preferredBrandName;
            _technicalInfoPanel.ShowFor(preferredCar, preferredBrandName);
            return;
        }

        foreach (var brandPair in _spawnedBrandCars)
        {
            if (TryGetVisibleCar(brandPair.Key, out GameObject visibleCar))
            {
                _displayedBrandName = brandPair.Key;
                _technicalInfoPanel.ShowFor(visibleCar, brandPair.Key);
                return;
            }
        }

        _displayedBrandName = null;
        _technicalInfoPanel.Hide();
    }

    private bool TryGetVisibleCar(string brandName, out GameObject visibleCar)
    {
        visibleCar = null;
        if (string.IsNullOrEmpty(brandName))
            return false;

        if (!_spawnedBrandCars.TryGetValue(brandName, out List<GameObject> brandCars))
            return false;

        visibleCar = brandCars.Find(car => car != null && car.activeSelf);
        return visibleCar != null;
    }
}
