using System;
using SaveLoadSystem.Core.Attributes;
using UnityEngine;

namespace SaveLoadCore
{
    public class GameObjectReference : MonoBehaviour
    {
        [Savable] public GameObject Test { get; private set; }

        private void Awake()
        {
            GameObject newTest;
            if (Test == null)
            {
                newTest = gameObject;
            }
            else
            {
                return;
            }
            
            foreach (GameObjectReference savableTest in GetComponents<GameObjectReference>())
            {
                savableTest.Test = newTest;
            }
        }

        [ContextMenu("Add")]
        public void Add()
        {
            //TODO: needs gameobject serialization
            Test.name = Guid.NewGuid().ToString();
        }

        [ContextMenu("Remove")]
        public void Remove()
        {
            Test = null;
        }
    }
}
