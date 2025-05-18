using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PrimeTween;
using VInspector;
using UnityEngine.SceneManagement;

public class MenuPage : MonoBehaviour
{
    [Header("Settings")] 
    [SerializeField] private bool selectFirstSelectableOnEnable = true;
    [SerializeField] private bool forgetLastSelectableOnDisable = true;
    [SerializeField] protected List<Selectable> selectables = new List<Selectable>();
    
    [Foldout("Selectable Animation Scale")]
    [SerializeField] private bool scaleOnSelect = false;
    [ShowIf("scaleOnSelect")]
    [SerializeField] private float scaleMultiplier = 1.1f;
    [SerializeField] private float scaleDuration = 0.15f;
    [SerializeField] private Ease scaleEase = Ease.InOutBounce;
    [SerializeField] private List<Selectable> scaleExclusions = new List<Selectable>();
    [EndIf]
    [EndFoldout]
    
    
    [Foldout("Selectable Animation Rotation")]
    [SerializeField] private bool rotateOnSelect = false;
    [ShowIf("rotateOnSelect")]
    [SerializeField] private Vector3 rotationStrength = new Vector3(0, 0, 15);
    [SerializeField] private float rotationDuration = 0.15f;
    [SerializeField] private Ease rotationEase = Ease.InOutBounce;
    [SerializeField] private List<Selectable> rotateExclusions = new List<Selectable>();
    [EndIf]
    [EndFoldout]
    
    
    [Foldout("Selectable Animation Shake")]
    [SerializeField] private bool shakeOnSelect = false;
    [ShowIf("shakeOnSelect")]
    [SerializeField] private bool shakeOnDeselect = false;
    [SerializeField] private Vector3 shakeAxis = new Vector3(3, 3, 0);
    [SerializeField] private float shakeFrequency = 10f;
    [SerializeField] private float shakeDuration = 0.5f;
    [SerializeField] private Ease shakeEase = Ease.Default;
    [SerializeField] private List<Selectable> shakeExclusions = new List<Selectable>();
    [EndIf]
    [EndFoldout]
    
    
    [ReadOnly] public Selectable currentSelectable;
    [ReadOnly] public Selectable previousSelectable;
    protected MenuController menuController;
    protected MenuCategory menuCategory;
    protected  MenuCategoryMainMenu menuCategoryMain;
    protected MenuCategoryPause menuCategoryPause;
    private readonly Dictionary<Selectable, Vector3> _selectableOriginalScales = new Dictionary<Selectable, Vector3>();
    private readonly Dictionary<Selectable, Vector3> _selectableOriginalRotations = new Dictionary<Selectable, Vector3>();
    private readonly Dictionary<Selectable, Vector3> _selectableOriginalPositions = new Dictionary<Selectable, Vector3>();
    private readonly Dictionary<Selectable, bool> _selectableOriginalState = new Dictionary<Selectable, bool>();

  
    private void Awake()
    {
        menuController = GetComponentInParent<MenuController>();
        menuCategory = GetComponentInParent<MenuCategory>();
        menuCategoryMain = GetComponentInParent<MenuCategoryMainMenu>();
        menuCategoryPause = GetComponentInParent<MenuCategoryPause>();
        
        if (selectables.Count == 0) return;
        foreach (Selectable selectable in selectables)
        {
            SetupSelectable(selectable);
        }
        
    }

    protected virtual void Start()
    {
        if (menuCategory.currentPage != this || menuController.currentCategory != menuCategory)
        {
            DisableAllSelectables();
        }
    }
    
    
    public virtual void OnPageSelected()
    {
        ResetAllSelectables();
        StartCoroutine(SelectFirstAvailableSelectableCar()); 
    }
    
    public virtual void OnPageDeselected()
    {
        previousSelectable = forgetLastSelectableOnDisable ? null : currentSelectable;
        currentSelectable = null;
    }
    
    

    
    
    #region Page Management // ---------------------------------------------------------------------
    
    
    
    protected virtual void SetupSelectable(Selectable selectable)
    {
        // Store all original information
        _selectableOriginalScales[selectable] = selectable.transform.localScale;
        _selectableOriginalRotations[selectable] = selectable.transform.localRotation.eulerAngles;
        _selectableOriginalPositions[selectable] = selectable.transform.localPosition;
        _selectableOriginalState[selectable] = selectable.interactable;
        
        
        // Add events
        var eventTrigger = selectable.GetComponent<EventTrigger>() ?? selectable.gameObject.AddComponent<EventTrigger>();
        AddEventTriggerEntry(eventTrigger, EventTriggerType.Select, OnSelect);
        AddEventTriggerEntry(eventTrigger, EventTriggerType.Deselect, OnDeselect);
        AddEventTriggerEntry(eventTrigger, EventTriggerType.PointerEnter, OnPointerEnter);
        AddEventTriggerEntry(eventTrigger, EventTriggerType.PointerExit, OnPointerExit);
    }

    private void AddEventTriggerEntry(EventTrigger eventTrigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> callback)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = type;
        entry.callback = new EventTrigger.TriggerEvent();
        entry.callback.AddListener(callback);
        eventTrigger.triggers.Add(entry);
    }

    protected virtual void ResetAllSelectables() 
    {
        if (selectables.Count == 0) return;
        
        foreach (Selectable selectable in selectables)
        {
            if (!selectable.gameObject.activeSelf || !selectable) continue;
            

            // Reset all original information
            
            if (_selectableOriginalState.TryGetValue(selectable, out bool originalState))
                selectable.interactable = originalState;
            if (_selectableOriginalScales.TryGetValue(selectable, out Vector3 originalScale))
                selectable.transform.localScale = originalScale;
            if (_selectableOriginalRotations.TryGetValue(selectable, out Vector3 originalRotation))
                selectable.transform.localRotation = Quaternion.Euler(originalRotation);
            if (_selectableOriginalPositions.TryGetValue(selectable, out Vector3 originalPosition))
                selectable.transform.localPosition = originalPosition;
            
            selectable.interactable = _selectableOriginalState[selectable];

            // Just resetting the position bugs the selectables that are in a layout group
            // so find the layout group if it exists and restart it to fix the positions
            LayoutGroup group = selectable.gameObject.GetComponentInParent<LayoutGroup>();
            if (group)
            {
                group.enabled = false;
                group.enabled = true;
            }
        }
    }
    
    public void DisableAllSelectables()
    {
        foreach (Selectable selectable in selectables)
        {
            selectable.interactable = false;
        }
    }

    private IEnumerator SelectFirstAvailableSelectableCar()
    {
        yield return new WaitForSeconds(0.3f); // small delay so the selectables will have time to initialize
        SelectFirstAvailableSelectable();
    }
    
    protected void SelectFirstAvailableSelectable()
    {
        if (selectables.Count == 0 || !selectFirstSelectableOnEnable) return;
        
        // Try to select previous selectable if it's valid
        if (previousSelectable && previousSelectable.isActiveAndEnabled)
        {
            EventSystem.current.SetSelectedGameObject(previousSelectable.gameObject);
            return;
        }
    
        // Look for first active selectable starting from index 0
        for (var i = 0; i < selectables.Count; i++)
        {
            if (!selectables[i].gameObject.activeSelf) continue;
            EventSystem.current.SetSelectedGameObject(selectables[i].gameObject);
            return;
        }
    }
    
    #endregion Page Management // ---------------------------------------------------------------------
    
    
    #region Events // ---------------------------------------------------------------------
    
    protected virtual void OnSelect(BaseEventData eventData)
    {
        
        if (!eventData.selectedObject.activeSelf) return;
        
        currentSelectable = eventData.selectedObject.GetComponent<Selectable>();
        if (!currentSelectable || !currentSelectable.interactable) return;

        if (scaleOnSelect && !scaleExclusions.Contains(currentSelectable))
        {
            PlayScaleAnimation(eventData.selectedObject.transform, true);
        }
        
        if (rotateOnSelect && !rotateExclusions.Contains(currentSelectable))
        {
            PlayRotateAnimation(eventData.selectedObject.transform, true);
        }
        
        if (shakeOnSelect && !shakeExclusions.Contains(currentSelectable))
        {
            PlayShakeAnimation(eventData.selectedObject.transform);
        }
        

        SoundManager.Instance?.PlaySoundFX("ButtonSelect");
    }

    protected virtual void OnDeselect(BaseEventData eventData)
    {
        
        if (!eventData.selectedObject.activeSelf || currentSelectable == null || !currentSelectable.interactable) return;
        
        if (scaleOnSelect && !scaleExclusions.Contains(currentSelectable))
        {
            PlayScaleAnimation(eventData.selectedObject.transform, false);
        }

        if (rotateOnSelect && !rotateExclusions.Contains(currentSelectable))
        {
            PlayRotateAnimation(eventData.selectedObject.transform, false);
        }
        
        if (shakeOnSelect && shakeOnDeselect && !shakeExclusions.Contains(currentSelectable))
        {
            PlayShakeAnimation(eventData.selectedObject.transform);
        }
        
        previousSelectable = currentSelectable;
        currentSelectable = null;
    }

    private void OnPointerEnter(BaseEventData eventData)
    {
        
        if (eventData is PointerEventData pointerEventData)
        {
            pointerEventData.selectedObject = pointerEventData.pointerEnter;
        }
    }
    
    private void OnPointerExit(BaseEventData eventData)
    {
        
        if (eventData is PointerEventData pointerEventData)
        {
            pointerEventData.selectedObject = null;
        }
    }
    
    public void OnNavigate(Vector2 input)
    {

        if (EventSystem.current.currentSelectedGameObject) return;
        SelectFirstAvailableSelectable();
    }
    
    public virtual void OnActiveSceneChanged(Scene currentScene, Scene nextScene)
    {


    }



    
    #endregion Events // ---------------------------------------------------------------------
    
    
    #region Animations // ---------------------------------------------------------------------
    
    private void PlayScaleAnimation(Transform target, bool scaleUp)
    {
        Vector3 endScale = scaleUp ? _selectableOriginalScales[currentSelectable] * scaleMultiplier : _selectableOriginalScales[currentSelectable];
        Tween.Scale(target, endScale, scaleDuration, scaleEase, useUnscaledTime: true);
    }

    private void PlayRotateAnimation(Transform target, bool rotateToTarget)
    {
        Vector3 endRotation = rotateToTarget ? rotationStrength : _selectableOriginalRotations[currentSelectable];
        Tween.LocalRotation(target, endRotation, rotationDuration, rotationEase, useUnscaledTime: true);
    }

    private void PlayShakeAnimation(Transform target)
    {
        Tween.ShakeLocalPosition(target, shakeAxis, shakeDuration, shakeFrequency, easeBetweenShakes: shakeEase, useUnscaledTime: true);
    }
    
    #endregion Animations // ---------------------------------------------------------------------

    
    
    
#if UNITY_EDITOR
    [Button] private void AddAllSelectablesInPage()
    {
        foreach (Transform child in transform)
        {
            if (!child.TryGetComponent(out Selectable selectable)) continue;
            if (!selectables.Contains(selectable))
            {
                selectables.Add(selectable);
            }
        }
        selectables.RemoveAll(menu => menu == null);
    }
    
#endif
}