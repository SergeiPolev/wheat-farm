using Lean.Pool;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Services
{
    public class CombatTextFactory : IService
    {
        private CombatText _prefab;
        private CombatText _critPrefab;
        private CombatText _dodgePrefab;

        private const string CombatTextPrefabPath = "CombatText/CombatText";
        private const string CritPrefabPath = "CombatText/CritDamageText";
        private const string DodgePrefabPath = "CombatText/DodgeText";
        public enum CombatTextType
        {
            Crit,
            Damage,
            Dodge
        }

        public void Initialize()
        {
            _prefab = Resources.Load<CombatText>(CombatTextPrefabPath);
            _critPrefab = Resources.Load<CombatText>(CritPrefabPath);
            _dodgePrefab = Resources.Load<CombatText>(DodgePrefabPath);
        }

        public CombatText Create(float value, Vector2 worldPosition, CombatTextType textType = CombatTextType.Damage)
        {
            string str = "";
            if(value <= 10)
                str = (Mathf.Ceil(value*10f)/10f).ToString(CultureInfo.InvariantCulture);
            else
                str = Mathf.Round(value).ToString(CultureInfo.InvariantCulture);
            return Create(str, worldPosition, textType);
        }

        public CombatText Create(string text, Vector2 worldPosition, CombatTextType textType = CombatTextType.Damage, float fontSize = 70f)
        {
            CombatText prefab = textType switch
            {
                CombatTextType.Damage => _prefab,
                CombatTextType.Crit => _critPrefab,
                CombatTextType.Dodge => _dodgePrefab,
                _ => _prefab
            };

            CombatText worldText = LeanPool.Spawn(prefab);
            worldText.Setup(text, worldPosition, true);
            worldText.SetColor(textType == CombatTextType.Crit ? Color.red : Color.white);

            return worldText;
        }
    }
}

