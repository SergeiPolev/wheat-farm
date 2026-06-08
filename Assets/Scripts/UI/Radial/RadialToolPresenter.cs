using System;
using UnityEngine;
using VContainer.Unity;
using WheatFarm.Player.Tools;

namespace WheatFarm.UI
{
    /// <summary>
    /// Hold Tab to open the radial tool menu; move the cursor toward a slice to highlight it;
    /// release Tab to equip the highlighted tool.
    /// </summary>
    public class RadialToolPresenter : ITickable, IDisposable
    {
        private static readonly (ToolId id, string name)[] Tools =
        {
            (ToolId.Placement, "Plant"),
            (ToolId.WateringCan, "Water"),
            (ToolId.Sickle, "Sickle"),
            (ToolId.Fertilizer, "Fertilizer"),
            (ToolId.Dye, "Dye"),
            (ToolId.Uproot, "Uproot"),
            (ToolId.Bulldoze, "Bulldoze"),
        };

        private readonly RadialMenuView _view;
        private readonly IToolService _toolService;

        private bool _built;
        private int _highlighted = -1;

        public RadialToolPresenter(RadialMenuView view, IToolService toolService)
        {
            _view = view;
            _toolService = toolService;
        }

        public void Tick()
        {
            if (!_built)
            {
                _built = true;
                var names = new string[Tools.Length];
                for (int i = 0; i < Tools.Length; i++) names[i] = Tools[i].name;
                _view.SetItems(names);
            }

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                _highlighted = -1;
                _view.SetHighlight(-1);
                _view.Show();
            }

            if (_view.IsOpen && Input.GetKey(KeyCode.Tab))
            {
                _highlighted = _view.IndexFromScreenPoint(Input.mousePosition);
                _view.SetHighlight(_highlighted);
            }

            if (Input.GetKeyUp(KeyCode.Tab))
            {
                _view.Hide();
                if (_highlighted >= 0 && _highlighted < Tools.Length)
                    _toolService.EquipTool(Tools[_highlighted].id);
                _highlighted = -1;
            }
        }

        public void Dispose() { }
    }
}
