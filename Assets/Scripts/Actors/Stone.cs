using UnityEngine;
using System.Collections;

public class Stone : MonoBehaviour
{
    public float deleteDistance = 10f; // Distance from parent to delete
    private Transform parentTransform;
    private bool canDelete = false;

    void Start()
    {
        // Get the parent's transform
        parentTransform = transform.parent;
        if (parentTransform != null)
        {
            canDelete = true;
            StartCoroutine(CheckDistanceToDelete());
        }
        else
        {
            Debug.LogWarning("Stone object has no parent.  Distance check will not run.", gameObject);
        }
    }

    IEnumerator CheckDistanceToDelete()
    {
        while (canDelete)
        {
            // Wait for a random number of frames (optimize performance)
            yield return new WaitForSeconds(Random.Range(1.5f, 5f)); // Check every 0.5 to 2 seconds

            if (parentTransform != null)
            {
                float distance = Vector3.Distance(transform.position, parentTransform.position);
                if (distance > deleteDistance)
                {
                    Destroy(gameObject); // Destroy the Stone object
                    canDelete = false; // Stop the coroutine
                }
            }
            else
            {
                // Parent was destroyed, stop checking
                canDelete = false;
            }
        }
    }

    void OnDestroy()
    {
        //handle to prevent errors.
        canDelete = false;
    }
}
