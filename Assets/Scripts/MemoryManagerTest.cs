using System.Collections;
using UnityEngine;

/// <summary>
/// Test script for MemoryManager that cycles through all splats in sequence
/// </summary>
public class MemoryManagerTest : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MemoryManager memoryManager;
    
    [Header("Test Settings")]
    [SerializeField] private float displayDuration = 3f; // How long each splat stays open
    [SerializeField] private bool autoStart = false; // Start test automatically on scene load
    [SerializeField] private bool loopTest = false; // Loop the test continuously
    [SerializeField] private KeyCode startTestKey = KeyCode.Space; // Key to manually start test
    
    private bool isTestRunning = false;
    private Coroutine currentTestCoroutine = null;
    
    private void Start()
    {
        if (memoryManager == null)
        {
            memoryManager = GetComponent<MemoryManager>();
            if (memoryManager == null)
            {
                Debug.LogError("MemoryManagerTest: No MemoryManager found! Please assign one in the Inspector.");
                return;
            }
        }
        
        if (autoStart)
        {
            StartTest();
        }
    }
    
    private void Update()
    {
        // Manual start with keyboard input
        if (Input.GetKeyDown(startTestKey) && !isTestRunning)
        {
            StartTest();
        }
    }
    
    /// <summary>
    /// Starts the sequential test of all splats
    /// </summary>
    public void StartTest()
    {
        if (memoryManager == null)
        {
            Debug.LogError("MemoryManagerTest: Cannot start test - no MemoryManager assigned!");
            return;
        }
        
        if (isTestRunning)
        {
            Debug.LogWarning("MemoryManagerTest: Test is already running!");
            return;
        }
        
        if (currentTestCoroutine != null)
        {
            StopCoroutine(currentTestCoroutine);
        }
        
        currentTestCoroutine = StartCoroutine(RunSequentialTest());
    }
    
    /// <summary>
    /// Stops the currently running test
    /// </summary>
    public void StopTest()
    {
        if (currentTestCoroutine != null)
        {
            StopCoroutine(currentTestCoroutine);
            currentTestCoroutine = null;
        }
        
        isTestRunning = false;
        memoryManager.CloseCurrentSplat();
        Debug.Log("MemoryManagerTest: Test stopped");
    }
    
    /// <summary>
    /// Coroutine that cycles through all splats sequentially
    /// </summary>
    private IEnumerator RunSequentialTest()
    {
        isTestRunning = true;
        Debug.Log("MemoryManagerTest: Starting sequential splat test...");
        
        do
        {
            // Get the number of splats (assuming GetCurrentSplatIndex works with a full list)
            int splatCount = 0;
            
            // Try opening splats until we get an invalid index
            for (int i = 0; i < 100; i++) // Safety limit
            {
                GameObject testSplat = memoryManager.GetCurrentSplat();
                memoryManager.OpenSplat(i, false);
                
                // Check if we successfully opened this index
                if (memoryManager.GetCurrentSplatIndex() == i)
                {
                    splatCount = i + 1;
                    memoryManager.CloseCurrentSplat();
                    yield return new WaitForSeconds(3f);
                }
                else
                {
                    break;
                }
            }
            
            Debug.Log($"MemoryManagerTest: Found {splatCount} splats. Starting sequence...");
            
            if (splatCount == 0)
            {
                Debug.LogWarning("MemoryManagerTest: No splats found in MemoryManager!");
                break;
            }
            
            // Open and close each splat in sequence
            for (int i = 0; i < splatCount; i++)
            {
                Debug.Log($"MemoryManagerTest: Opening splat {i + 1}/{splatCount}");
                
                // Open the splat
                memoryManager.OpenSplat(i);
                
                // Wait for the display duration
                yield return new WaitForSeconds(displayDuration);
                
                // Close the splat
                Debug.Log($"MemoryManagerTest: Closing splat {i + 1}/{splatCount}");
                memoryManager.CloseSplat(i);
                
                // Small delay between transitions
                yield return new WaitForSeconds(3f);
            }
            
            Debug.Log("MemoryManagerTest: Completed one full cycle through all splats");
            
            if (loopTest)
            {
                Debug.Log("MemoryManagerTest: Loop enabled - restarting test...");
                yield return new WaitForSeconds(3f);
            }
            
        } while (loopTest);
        
        isTestRunning = false;
        Debug.Log("MemoryManagerTest: Test complete!");
    }
    
    /// <summary>
    /// Alternative test that uses the Next/Previous navigation methods
    /// </summary>
    public void StartNavigationTest()
    {
        if (currentTestCoroutine != null)
        {
            StopCoroutine(currentTestCoroutine);
        }
        
        currentTestCoroutine = StartCoroutine(RunNavigationTest());
    }
    
    /// <summary>
    /// Tests the OpenNextSplat functionality
    /// </summary>
    private IEnumerator RunNavigationTest()
    {
        isTestRunning = true;
        Debug.Log("MemoryManagerTest: Starting navigation test (using OpenNextSplat)...");
        
        do
        {
            // Count how many splats by trying to go through them
            int count = 0;
            int startIndex = memoryManager.GetCurrentSplatIndex();
            
            memoryManager.OpenNextSplat();
            yield return new WaitForSeconds(3f);
            
            while (memoryManager.GetCurrentSplatIndex() != startIndex && count < 100)
            {
                count++;
                memoryManager.OpenNextSplat();
                yield return new WaitForSeconds(3f);
            }
            
            count++; // Include the starting splat
            Debug.Log($"MemoryManagerTest: Found {count} splats. Starting navigation sequence...");
            
            // Now do the actual test with proper timing
            for (int i = 0; i < count; i++)
            {
                Debug.Log($"MemoryManagerTest: Displaying splat {i + 1}/{count}");
                
                // Wait for display duration
                yield return new WaitForSeconds(displayDuration);
                
                // Navigate to next
                memoryManager.OpenNextSplat();
                
                // Small transition delay
                yield return new WaitForSeconds(3f);
            }
            
            // Close the last one
            memoryManager.CloseCurrentSplat();
            
            Debug.Log("MemoryManagerTest: Completed navigation test cycle");
            
            if (loopTest)
            {
                Debug.Log("MemoryManagerTest: Loop enabled - restarting navigation test...");
                yield return new WaitForSeconds(3f);
            }
            
        } while (loopTest);
        
        isTestRunning = false;
        Debug.Log("MemoryManagerTest: Navigation test complete!");
    }
    
    private void OnDestroy()
    {
        StopTest();
    }
}

