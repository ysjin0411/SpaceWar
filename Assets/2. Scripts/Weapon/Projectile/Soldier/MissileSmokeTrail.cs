using System;
using UnityEngine;

public class MissileSmokeTrail : MonoBehaviour
{
    // 이동한 거리마다 독립적인 연기를 남긴다. 미사일이 사라져도 연기는 재생된다.
    [SerializeField] SpriteOneShotEffect _smokePrefab;
    [SerializeField, Min(0.01f)] float _spacing = 0.18f;
    Vector3 _lastPosition;

    private void OnEnable()
    {
        if (_smokePrefab == null || _spacing <= 0f)
            throw new InvalidOperationException("Missile smoke settings are missing.");
        _lastPosition = transform.position;
    }

    private void LateUpdate()
    {
        if (Time.deltaTime <= 0f) return;
        Vector3 delta = transform.position - _lastPosition;
        float distance = delta.magnitude;
        if (distance < _spacing) return;
        Vector3 direction = delta / distance;
        while (distance >= _spacing)
        {
            _lastPosition += direction * _spacing;
            Instantiate(_smokePrefab, _lastPosition, Quaternion.identity);
            distance -= _spacing;
        }
    }
}
