using System;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class PlayerSpotController : MonoBehaviour
{
  private Rigidbody target;
  private Vector3 momentum = Vector3.zero;

  // Start is called once before the first execution of Update after the MonoBehaviour is created
  // void Start()
  // {
  // }

  // Update is called once per frame
  void Update()
  {
    PickTarget();
    ReleaseTarget();

    HandleTarget();
  }

  // ongui
  void OnGUI()
  {
    if (target == null) return;

    // Draw Momentum Vector
    Vector3 screenMomentum = Camera.main.WorldToScreenPoint(target.position + momentum * 2.0f);
    Vector3 screenPosition = Camera.main.WorldToScreenPoint(target.position);
    Debug.DrawLine(screenPosition, screenMomentum, Color.red);
  }

  void OnDrawGizmos()
  {
    if (target == null) return;

    // Draw Target Position
    Gizmos.color = Color.green;
    Gizmos.DrawWireSphere(target.position, 1.0f);

    // Draw Target Momentum
    Gizmos.color = Color.red;
    Gizmos.DrawLine(target.position, target.position + momentum * 2.0f);
  }

  void HandleTarget()
  {
    if (target == null) return;

    Camera cam = Camera.main;

    Plane plane = new(-cam.transform.forward, transform.position);

    Vector2 mouse = Mouse.current.position.ReadValue();
    Ray ray = cam.ScreenPointToRay(mouse);

    Debug.Log($"Mouse: {mouse.x}, {mouse.y}");

    if (plane.Raycast(ray, out float distance))
    {
      Debug.Log($"Distance: {distance}");
      Vector3 worldPosition = ray.GetPoint(distance);

      momentum = Vector3.Lerp(momentum, (worldPosition - target.position) * 50f, Time.deltaTime * 5000f);

      target.MovePosition(worldPosition);
    }
  }

  void ReleaseTarget()
  {
    if (!Mouse.current.leftButton.wasReleasedThisFrame) return;

    if (target != null)
    {
      Debug.Log($"RELEASE {target.name}");
      target.useGravity = true;
      target.linearVelocity = momentum;
      target = null;
    }
  }

  void PickTarget()
  {
    if (!Mouse.current.leftButton.wasPressedThisFrame) return;

    Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());

    if (Physics.Raycast(ray, out RaycastHit hit))
    {
      Debug.Log($"Hit {hit.collider.name}");
      Debug.Log($"Hit {hit.collider.tag}");
      // Tag "Item"
      if (hit.collider.CompareTag("Item"))
      {
        Debug.Log($"PICK {hit.collider.name}");
        target = hit.collider.GetComponent<Rigidbody>();
        target.useGravity = false;
        momentum = target.linearVelocity;

      }
    }
  }
}
