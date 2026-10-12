using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IncreaseAmmoScript : MonoBehaviour
{
    public int ammoIncrease = 6;
    // walk up to the crate to grab it
    public float collectDistance = 0.8f;
    public float spinSpeed = 45f;

    Camera mainCamera;
    bool collected;

    void Start()
    {
        mainCamera = Camera.main;
    }

    void Update()
    {
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);

        if (mainCamera == null) return;
        Vector3 offset = mainCamera.transform.position - transform.position;
        offset.y = 0f;
        if (offset.magnitude <= collectDistance) Collect();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Bullet"))
        {
            Destroy(other.gameObject);
            Collect();
        }
    }

    public void Collect()
    {
        GameControllerScript game = GameControllerScript.Instance;
        if (collected || game == null || !game.IsPlaying) return;

        collected = true;
        game.AddAmmo(ammoIncrease);
        Destroy(gameObject);
    }
}
