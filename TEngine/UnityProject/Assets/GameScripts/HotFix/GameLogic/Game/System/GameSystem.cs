using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameLogic
{
    public class GameSystem : MonoSingleton<GameSystem>
    {
        private Dictionary<Type, int> _baseGameSystemIndexes = new Dictionary<Type, int>();
        private List<BaseGameSystem> _baseGameSystems = new List<BaseGameSystem>();
        
        public static T GetSystem<T>() where T : BaseGameSystem
        {
            if (Instance._baseGameSystemIndexes.TryGetValue(typeof(T), out var systemIndex) && systemIndex >= 0 && systemIndex < Instance._baseGameSystems.Count)
            {
                var system = Instance._baseGameSystems[systemIndex];
                if (system is T gameSystem)
                {
                    return gameSystem;
                }
            }
            return null;
        }

        protected override void OnInit()
        {
            base.OnInit();
            AddSystem<CommonGameSystem>();
        }

        private void Update()
        {
            for (int i = _baseGameSystems.Count - 1; i >= 0; i--)
            {
                _baseGameSystems[i].Update(Time.deltaTime);
            }
        }

        protected override void OnDestroy()
        {
            for (int i = _baseGameSystems.Count - 1; i >= 0; i--)
            {
                _baseGameSystems[i].Destroy();
            }
            base.OnDestroy();
        }
        
        private void AddSystem<T>() where T :BaseGameSystem , new()
        {
            T gameSystem = new T();
            _baseGameSystemIndexes[typeof(T)] = _baseGameSystems.Count;
            _baseGameSystems.Add(gameSystem);
            gameSystem.Init();
        }
    }
}