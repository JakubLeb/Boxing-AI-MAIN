using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BoxerManualController : MonoBehaviour
{
    [Header("Sterowanie rêczne")]
    public bool manualControl = false;   // W³¹cz tylko u jednego agenta
    public float moveSpeed = 2f;

    [Header("Korekta kierunku modelu")]
    [Tooltip("Jeœli model jest odwrócony, ustaw na -1")]
    public float directionMultiplier = -1f;  // Zmieñ na -1 jeœli model idzie ty³em

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
            // Korekta kierunku - pomnó¿ przez directionMultiplier
            Vector3 correctedDir = moveDir * directionMultiplier;

            // Obrót w kierunku ruchu z korekt¹
            transform.forward = correctedDir;

            // Ruch (u¿ywamy oryginalnego moveDir, ¿eby ruch by³ w dobr¹ stronê)
            rb.MovePosition(transform.position + moveDir * moveSpeed * Time.deltaTime);
        }
    }
}