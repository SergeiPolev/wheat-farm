using System;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using Lean.Pool;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

public class CombatText : MonoBehaviour
{
    [SerializeField] protected TMP_Text _textHolder;
    [SerializeField] protected MeshRenderer _meshRenderer;
    [SerializeField] protected string _prefix;
    [SerializeField] protected string _postfix;
    [SerializeField] protected float _appearanceTime = 0.3f;
    [SerializeField] protected float _disappearanceTime = 0.2f;
    [SerializeField] protected float _delayTime = 0.2f;
    
    protected Vector3 _viewScale = new Vector3(1f, 1f, 1f);
    protected Sequence _viewSequence;
    protected Sequence _hideSequence;
    protected TweenerCore<Vector3, Vector3, VectorOptions> _moveTween;

    protected Transform _transform;
    
    public virtual void Setup(string value, Vector2 position, bool setValue = true)
    {
        _transform = transform;
        if (setValue)
        {
            SetText(value);
        }
        SetPosition(position);
        SetTime();
        Show();
    }

    protected virtual void SetTime()
    {
        _appearanceTime = 0.3f;
        _delayTime = 0.2f;
        _disappearanceTime = 0.2f;
    }

    protected virtual void SetText(string value)
    {
        _textHolder.SetText($"{_prefix}{value}{_postfix}");
    }

    public void SetColor(Color color)
    {
        _textHolder.color = color;
    }
    protected void SetPosition(Vector3 pos)
    {
        _transform.position = pos;
        _transform.localScale = Vector3.one * 0.2f;
    }

    protected void Show()
    {
        _meshRenderer.enabled = true;
        
        if (_viewSequence == null)
        {
            _moveTween = _transform.DOMove(GetMovePosition(), _appearanceTime).SetAutoKill(false); 
            _viewSequence = DOTween.Sequence();
            _viewSequence.Join(_transform.DOScale(Vector3.one, _appearanceTime));
            _viewSequence.AppendInterval(_delayTime);
            _viewSequence.OnComplete(Hide);
            _viewSequence.SetAutoKill(false);
        }
        else
        {
            _moveTween.ChangeStartValue(_transform.position);
            _moveTween.ChangeEndValue(GetMovePosition());
            _moveTween.Restart();
            _viewSequence.Restart();
        }
    }

    protected void Hide()
    {
        if (_hideSequence == null)
        {
            _hideSequence = DOTween.Sequence();
            _hideSequence.Append(_transform.DOScale(Vector3.one * 0.5f, _disappearanceTime));
            _hideSequence.OnComplete(Disable);
            _hideSequence.SetAutoKill(false);
        }
        else
        {
            _hideSequence.Restart();
        }
    }

    protected virtual Vector3 GetMovePosition()
    {
        float rPosX = Random.Range(-0.15f, 0.15f);
        float rPosY = Random.Range(0.25f, 0.5f);
        return new Vector3(rPosX, rPosY, 0) + _transform.position;
    }

    protected void Disable()
    {
        _meshRenderer.enabled = false;
        LeanPool.Despawn(gameObject);
    }

    public bool CanBeUsed()
    {
        return !_meshRenderer.enabled;
    }

    public void OnCreate()
    {
        _meshRenderer.enabled = false;
    }

    public void Cleanup()
    {
        _meshRenderer.enabled = false;
        LeanPool.Despawn(gameObject);
    }
}