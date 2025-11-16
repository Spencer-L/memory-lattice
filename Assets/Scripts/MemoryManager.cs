using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MemoryManager : MonoBehaviour
{
    [Header("Splat Game Objects")]
    [SerializeField] private List<GameObject> splatObjects;
    
    [Header("Animation Settings")]
    [SerializeField] private float transitionDelay = 0.5f; // Time to wait for close animation before opening next
    
    private GameObject currentlyActiveSplat = null;
    private static readonly string ANIMATOR_PARAM_IS_CLOSED = "IsClosed";
    
    private void Start()
    {
        // Initialize all splats as closed
        InitializeSplats();
    }
    
    /// <summary>
    /// Initialize all splat objects to closed state
    /// </summary>
    private void InitializeSplats()
    {
        foreach (GameObject splat in splatObjects)
        {
            if (splat != null)
            {
                Animator animator = splat.GetComponent<Animator>();
                if (animator != null)
                {
                    animator.SetBool(ANIMATOR_PARAM_IS_CLOSED, true);
                }
                splat.SetActive(false);
            }
        }
    }
    
    /// <summary>
    /// Opens a splat by index
    /// </summary>
    /// <param name="index">Index of the splat in the splatObjects list</param>
    /// <param name="closeCurrentFirst">If true, closes the currently active splat before opening</param>
    public void OpenSplat(int index, bool closeCurrentFirst = true)
    {
        if (index < 0 || index >= splatObjects.Count)
        {
            Debug.LogWarning($"MemoryManager: Invalid splat index {index}");
            return;
        }
        
        OpenSplat(splatObjects[index], closeCurrentFirst);
    }
    
    /// <summary>
    /// Opens a specific splat game object
    /// </summary>
    /// <param name="splat">The splat GameObject to open</param>
    /// <param name="closeCurrentFirst">If true, closes the currently active splat before opening</param>
    public void OpenSplat(GameObject splat, bool closeCurrentFirst = true)
    {
        if (splat == null)
        {
            Debug.LogWarning("MemoryManager: Attempted to open null splat");
            return;
        }
        
        if (!splatObjects.Contains(splat))
        {
            Debug.LogWarning($"MemoryManager: Splat {splat.name} is not in the managed list");
            return;
        }
        
        // If this splat is already active, do nothing
        if (currentlyActiveSplat == splat)
        {
            Debug.Log($"MemoryManager: Splat {splat.name} is already open");
            return;
        }
        
        // Close current splat if requested and exists
        if (closeCurrentFirst && currentlyActiveSplat != null)
        {
            StartCoroutine(TransitionSplats(currentlyActiveSplat, splat));
        }
        else
        {
            OpenSplatImmediate(splat);
        }
    }
    
    /// <summary>
    /// Immediately opens a splat without waiting for transitions
    /// </summary>
    private void OpenSplatImmediate(GameObject splat)
    {
        if (splat == null) return;
        
        splat.SetActive(true);
        Animator animator = splat.GetComponent<Animator>();
        
        if (animator != null)
        {
            animator.SetBool(ANIMATOR_PARAM_IS_CLOSED, false);
            Debug.Log($"MemoryManager: Opened splat {splat.name}");
        }
        else
        {
            Debug.LogWarning($"MemoryManager: No Animator found on {splat.name}");
        }
        
        currentlyActiveSplat = splat;
    }
    
    /// <summary>
    /// Closes a splat by index
    /// </summary>
    /// <param name="index">Index of the splat in the splatObjects list</param>
    public void CloseSplat(int index)
    {
        if (index < 0 || index >= splatObjects.Count)
        {
            Debug.LogWarning($"MemoryManager: Invalid splat index {index}");
            return;
        }
        
        CloseSplat(splatObjects[index]);
    }
    
    /// <summary>
    /// Closes a specific splat game object
    /// </summary>
    /// <param name="splat">The splat GameObject to close</param>
    public void CloseSplat(GameObject splat)
    {
        if (splat == null)
        {
            Debug.LogWarning("MemoryManager: Attempted to close null splat");
            return;
        }
        
        Animator animator = splat.GetComponent<Animator>();
        
        if (animator != null)
        {
            animator.SetBool(ANIMATOR_PARAM_IS_CLOSED, true);
            Debug.Log($"MemoryManager: Closed splat {splat.name}");
        }
        else
        {
            Debug.LogWarning($"MemoryManager: No Animator found on {splat.name}");
        }
        
        // If this was the currently active splat, clear the reference
        if (currentlyActiveSplat == splat)
        {
            currentlyActiveSplat = null;
        }
        
        // Optionally deactivate after animation completes
        StartCoroutine(DeactivateSplatAfterDelay(splat, transitionDelay));
    }
    
    /// <summary>
    /// Closes the currently active splat
    /// </summary>
    public void CloseCurrentSplat()
    {
        if (currentlyActiveSplat != null)
        {
            CloseSplat(currentlyActiveSplat);
        }
        else
        {
            Debug.Log("MemoryManager: No currently active splat to close");
        }
    }
    
    /// <summary>
    /// Transitions from one splat to another with proper timing
    /// </summary>
    private IEnumerator TransitionSplats(GameObject fromSplat, GameObject toSplat)
    {
        // Close the current splat
        CloseSplat(fromSplat);
        
        // Wait for close animation to complete
        yield return new WaitForSeconds(transitionDelay);
        
        // Open the new splat
        OpenSplatImmediate(toSplat);
    }
    
    /// <summary>
    /// Deactivates a splat GameObject after a delay (for after close animation)
    /// </summary>
    private IEnumerator DeactivateSplatAfterDelay(GameObject splat, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (splat != null)
        {
            splat.SetActive(false);
        }
    }
    
    /// <summary>
    /// Gets the currently active splat
    /// </summary>
    public GameObject GetCurrentSplat()
    {
        return currentlyActiveSplat;
    }
    
    /// <summary>
    /// Gets the index of the currently active splat, or -1 if none
    /// </summary>
    public int GetCurrentSplatIndex()
    {
        if (currentlyActiveSplat == null) return -1;
        return splatObjects.IndexOf(currentlyActiveSplat);
    }
    
    /// <summary>
    /// Opens the next splat in the list (wraps around)
    /// </summary>
    public void OpenNextSplat()
    {
        if (splatObjects.Count == 0) return;
        
        int currentIndex = GetCurrentSplatIndex();
        int nextIndex = (currentIndex + 1) % splatObjects.Count;
        OpenSplat(nextIndex);
    }
    
    /// <summary>
    /// Opens the previous splat in the list (wraps around)
    /// </summary>
    public void OpenPreviousSplat()
    {
        if (splatObjects.Count == 0) return;
        
        int currentIndex = GetCurrentSplatIndex();
        int prevIndex = (currentIndex - 1 + splatObjects.Count) % splatObjects.Count;
        OpenSplat(prevIndex);
    }
}
