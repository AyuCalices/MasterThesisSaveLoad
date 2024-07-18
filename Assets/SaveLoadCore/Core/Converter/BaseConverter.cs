using System;

namespace SaveLoadCore.Core.Converter
{
    public abstract class BaseConverter<T> : IConvertable
    {
        public virtual bool TryGetConverter(Type type, out IConvertable convertable)
        {
            if (typeof(T).IsAssignableFrom(type) || type is T)
            {
                convertable = this;
                return true;
            }
            
            convertable = default;
            return false;
        }
        
        public void OnSave(object data, SaveDataHandler saveDataHandler)
        {
            OnSave((T)data, saveDataHandler);
        }

        protected abstract void OnSave(T data, SaveDataHandler saveDataHandler);

        public abstract void OnLoad(LoadDataHandler loadDataHandler);
    }
}
