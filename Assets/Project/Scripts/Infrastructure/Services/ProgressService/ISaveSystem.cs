using Data;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ISaveSystem
{
    public void Save(PlayerProgress data);
    public void RemoveData();
    public bool HasData();
    public PlayerProgress Load();
}
