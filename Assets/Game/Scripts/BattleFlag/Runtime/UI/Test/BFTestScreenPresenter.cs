namespace BF.Game.Runtime.UI.Test
{
    /// <summary>
    /// 测试用 Screen Presenter，验证项目层 MVP 接入框架的方式。
    /// 将 context 对象转换为 View 的标题或默认值。
    /// </summary>
    public sealed class BFTestScreenPresenter
    {
        private BFTestScreenView _view;

        /// <summary>绑定目标 View。</summary>
        public void Bind(BFTestScreenView view)
        {
            _view = view;
            ApplyContext(_view.Context);
        }

        /// <summary>将 context 应用到 View 标题。</summary>
        public void ApplyContext(object context)
        {
            if (_view == null) return;

            if (context is string title)
                _view.SetTitle(title);
            else
                _view.SetTitle("BattleFlag UI Test");
        }
    }
}
