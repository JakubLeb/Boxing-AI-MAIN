using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BoxerManualController : MonoBehaviour
{
    [Header("Sterowanie r�czne")]
    public bool manualControl = false;   // W��cz tylko u jednego agenta
    public float moveSpeed = 2f;

    [Header("Korekta kierunku modelu")]
    [Tooltip("Je�li model jest odwr�cony, ustaw na -1")]
    public float directionMultiplier = -1f;  // Zmie� na -1 je�li model idzie ty�em

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (!manualControl) return;

        float h = Input.GetAxisRaw("Horizontal"); // A/D
        float v = Input.GetAxisRaw("Vertical");   // W/S

        Vector3 moveDir = new Vector3(h, 0f, v).normalized;

        if (moveDir != Vector3.zero)
        {
            // Korekta kierunku - pomn� przez directionMultiplier
            Vector3 correctedDir = moveDir * directionMultiplier;

            // Obr�t w kierunku ruchu z korekt�
            transform.forward = correctedDir;

            // Ruch (u�ywamy oryginalnego moveDir, �eby ruch by� w dobr� stron�)
            rb.MovePosition(transform.position + moveDir * moveSpeed * Time.deltaTime);
        }
    }
}