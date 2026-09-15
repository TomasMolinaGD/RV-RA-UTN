using System;
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

    private ARTrackedImageManager _trackedImageManager;

    private Dictionary<string, BrandGroup> _logoToBrandMap = new Dictionary<string, BrandGroup>();
    private Dictionary<string, List<GameObject>> _spawnedBrandCars = new Dictionary<string, List<GameObject>>();
    private Dictionary<string, int> _activeCarIndexPerBrand = new Dictionary<string, int>();
    private Dictionary<string, Transform> _brandWorldContainers = new Dictionary<string, Transform>();

    private void Awake()
    {
        _trackedImageManager = GetComponent<ARTrackedImageManager>();
    }

    private void Start()
    {
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
    }

    private void Update()
    {
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            SwitchCarModelForActiveBrands();
        }
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

        if (!_logoToBrandMap.ContainsKey(logoName))
        {
            Debug.LogWarning($"[AR DETECCIÓN] Se escaneó '{logoName}' pero no está registrado en el Inspector.");
            return;
        }

        BrandGroup brand = _logoToBrandMap[logoName];
        List<GameObject> brandCars = _spawnedBrandCars[brand.brandName];

        if (brandCars == null || brandCars.Count == 0) return;

        // Si la cámara detecta la imagen claramente
        if (trackedImage.trackingState == TrackingState.Tracking)
        {
            // Si la marca no tiene contenedor en la escena, se posiciona en la ubicación de la imagen
            if (!_brandWorldContainers.ContainsKey(brand.brandName))
            {
                GameObject container = new GameObject($"Anchor_{brand.brandName}");
                container.transform.position = trackedImage.transform.position;
                container.transform.rotation = trackedImage.transform.rotation;

                _brandWorldContainers.Add(brand.brandName, container.transform);

                foreach (var car in brandCars)
                {
                    car.transform.SetParent(container.transform);
                    car.transform.localPosition = Vector3.zero;
                    car.transform.localRotation = Quaternion.identity;
                }
                
                Debug.Log($"[AR DETECCIÓN] Autos de {brand.brandName} anclados en la posición escaneada.");
            }

            // Muestra el modelo activo
            int selectedIndex = _activeCarIndexPerBrand[brand.brandName];
            for (int i = 0; i < brandCars.Count; i++)
            {
                brandCars[i].SetActive(i == selectedIndex);
            }
        }
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
    }
}