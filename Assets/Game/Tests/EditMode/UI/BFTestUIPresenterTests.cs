using BF.Game.Runtime.UI.Test;
using NUnit.Framework;
using UnityEngine;

namespace BF.Game.Tests.EditMode.UI
{
    public sealed class BFTestUIPresenterTests
    {
        [Test]
        public void Presenter_AppliesStringContextToViewTitle()
        {
            var go = new GameObject("BFTestScreenView");
            var view = go.AddComponent<BFTestScreenView>();
            var presenter = new BFTestScreenPresenter();
            presenter.Bind(view);

            presenter.ApplyContext("Framework Ready");

            Assert.That(view.LastTitle, Is.EqualTo("Framework Ready"));
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Presenter_NonStringContext_ReturnsDefaultTitle()
        {
            var go = new GameObject("BFTestScreenView");
            var view = go.AddComponent<BFTestScreenView>();
            var presenter = new BFTestScreenPresenter();
            presenter.Bind(view);

            presenter.ApplyContext(123);

            Assert.That(view.LastTitle, Is.EqualTo("BattleFlag UI Test"));
            Object.DestroyImmediate(go);
        }
    }
}
