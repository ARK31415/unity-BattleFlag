using UnityEngine;

namespace Wit.Framework.UI
{
    /// <summary>
    /// View 内部可复用 UI 子组件基类。Widget 由所属 View 管理，不进入全局 UIManager。
    /// </summary>
    [DisallowMultipleComponent]
    public class WitUIWidget : MonoBehaviour
    {
        /// <summary>绑定所属 View 或父 Widget 时调用。</summary>
        public virtual void Bind(Object owner) { }

        /// <summary>所属 View 关闭或释放子组件时调用。</summary>
        public virtual void Unbind() { }

        /// <summary>设置 Widget 显隐状态。</summary>
        public virtual void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }
    }

    /// <summary>
    /// 带强类型数据入口的 Widget 基类，统一使用 SetData 命名表达外部数据刷新。
    /// </summary>
    public class WitUIWidget<TData> : WitUIWidget
    {
        public TData Data { get; private set; }

        /// <summary>刷新 Widget 展示数据。</summary>
        public void SetData(TData data)
        {
            Data = data;
            OnDataChanged(data);
        }

        protected virtual void OnDataChanged(TData data) { }
    }
}
