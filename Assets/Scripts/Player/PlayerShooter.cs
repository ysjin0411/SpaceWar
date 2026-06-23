using UnityEngine;

public class PlayerShooter : MonoBehaviour
{
    [Header("총알")]
    [SerializeField] GameObject _bulletPrefab;

    [Header("발사 위치")]
    [SerializeField] Transform _firePoint;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Z))
        {
            Fire();
        }
    }

    void Fire()
    {
        Instantiate(
            _bulletPrefab,
            _firePoint.position,
            Quaternion.identity);
    }
}