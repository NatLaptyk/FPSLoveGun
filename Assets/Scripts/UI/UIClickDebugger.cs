using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

/// <summary>
/// Temporary debug tool — attach to any active GameObject, press F8 at runtime
/// while hovering over a button to see every UI element under the cursor.
/// The TOP entry in the log is what's stealing the click.
/// REMOVE this script once the blocking element is identified.
/// </summary>
public class UIClickDebugger : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F8))
            LogElementsUnderPointer();
    }

    void LogElementsUnderPointer()
    {
        var eventData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        if (results.Count == 0)
        {
            Debug.Log("[UIClickDebugger] Nothing found under pointer — " +
                      "EventSystem may be missing or Canvas has no GraphicRaycaster.");
            return;
        }

        Debug.Log($"[UIClickDebugger] {results.Count} element(s) under pointer " +
                  $"(topmost first — this one receives the click):");

        for (int i = 0; i < results.Count; i++)
        {
            var r = results[i];
            var graphic = r.gameObject.GetComponent<UnityEngine.UI.Graphic>();
            bool raycastTarget = graphic != null && graphic.raycastTarget;

            Debug.Log($"  [{i}] {r.gameObject.name}  " +
                      $"(parent: {(r.gameObject.transform.parent != null ? r.gameObject.transform.parent.name : "none")})  " +
                      $"RaycastTarget={raycastTarget}  " +
                      $"depth={r.depth}  active={r.gameObject.activeInHierarchy}",
                      r.gameObject);   // clicking the log line highlights the object in the scene
        }
    }
}
