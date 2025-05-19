using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterHubAnimation : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    protected readonly int _rotateWeaponHash = Animator.StringToHash("RotateWeapon");
    private const float WeaponRotateDelay = 4f;
    private Coroutine _rotateWeapon;

    private void PlayAnimation()
    {
        _rotateWeapon = StartCoroutine(PlayRotateWeaponAnimation());
    }

    private IEnumerator PlayRotateWeaponAnimation()
    {
        yield return new WaitForSecondsRealtime(WeaponRotateDelay);
        _animator.SetTrigger(_rotateWeaponHash);
        PlayAnimation();
    }

    private void StopAnimation()
    {
        if (_rotateWeapon != null)
        {
            StopCoroutine(_rotateWeapon);
        }
    }

    private void OnEnable()
    {
        PlayAnimation();
    }

    private void OnDisable()
    {
        StopAnimation();
    }
}
