using UnityEngine;
using TMPro;

public class DynamicAdjustment : MonoBehaviour
{
    private TMP_InputField inputField;
    private RoundedQuadMesh roundedQuadMesh;
    private BoxCollider boxCollider;

    private const float TMP_SCALE_FACTOR = 0.0007142857f;
    private const float TMP_PREFERRED_HEIGHT_REFERENCE = 1036.44f;
    private const float ROUNDED_QUAD_MESH_Y_REFERENCE = -0.06f;
    private const float ROUNDED_QUAD_MESH_H_REFERENCE = 0.095f;
    private const float BOX_COLLIDER_CENTER_Y_REFERENCE = -0.0125f;
    private const float BOX_COLLIDER_SIZE_Y_REFERENCE = 0.1f;
    
    private void Start()
    {
        // Find the RoundedQuadMesh script on the same GameObject
        roundedQuadMesh = GetComponent<RoundedQuadMesh>();

        // Find the BoxCollider on the same GameObject
        boxCollider = GetComponent<BoxCollider>();

        // Find the TMP_InputField in the children of the current GameObject
        inputField = GetComponentInChildren<TMP_InputField>();

        // Add a listener to the input field's OnValueChanged event
        if (inputField != null)
        {
            inputField.onValueChanged.AddListener(OnInputFieldValueChanged);
            AdjustHeightsAndPositions();
        }
        else
        {
            Debug.LogError("TMP_InputField not found!");
        }
    }

    private void OnInputFieldValueChanged(string value)
    {
        AdjustHeightsAndPositions();
    }

    private void AdjustHeightsAndPositions()
    {
        // Get the scaled preferred height of the TMP input field
        float scaledPreferredHeight = inputField.textComponent.preferredHeight * TMP_SCALE_FACTOR;

        // Calculate the difference between the current scaled preferred height and the reference height
        float heightDifference = scaledPreferredHeight - (TMP_PREFERRED_HEIGHT_REFERENCE * TMP_SCALE_FACTOR);

        // Adjust the RoundedQuadMesh Y and height values
        if (roundedQuadMesh != null)
        {
            roundedQuadMesh.rect.y = ROUNDED_QUAD_MESH_Y_REFERENCE - heightDifference;
            roundedQuadMesh.rect.height = ROUNDED_QUAD_MESH_H_REFERENCE + heightDifference;
            roundedQuadMesh.UpdateMesh();
        }

        // Adjust the BoxCollider center Y and size Y values
        if (boxCollider != null)
        {
            Vector3 boxColliderCenter = boxCollider.center;
            boxColliderCenter.y = BOX_COLLIDER_CENTER_Y_REFERENCE - (heightDifference / 2f);
            boxCollider.center = boxColliderCenter;

            boxCollider.size = new Vector3(boxCollider.size.x, BOX_COLLIDER_SIZE_Y_REFERENCE + heightDifference, boxCollider.size.z);
        }
    }
}