using NUnit.Framework;
using UnityEngine;
using Wit.Framework.UI;

namespace BF.Game.Tests.EditMode.UI
{
    public sealed class WitUIWidgetTests
    {
        [Test]
        public void SetData_StoresTypedWidgetData()
        {
            var go = new GameObject("UnitInfoWidget");
            var widget = go.AddComponent<TestWidget>();
            var data = new TestWidgetData("Knight");

            widget.SetData(data);

            Assert.That(widget.Data, Is.SameAs(data));
            Assert.That(widget.LastName, Is.EqualTo("Knight"));
            Object.DestroyImmediate(go);
        }

        [Test]
        public void SetVisible_ChangesGameObjectActiveState()
        {
            var go = new GameObject("Widget");
            var widget = go.AddComponent<WitUIWidget>();

            widget.SetVisible(false);

            Assert.That(go.activeSelf, Is.False);
            Object.DestroyImmediate(go);
        }

        private sealed class TestWidgetData
        {
            public TestWidgetData(string name)
            {
                Name = name;
            }

            public string Name { get; }
        }

        private sealed class TestWidget : WitUIWidget<TestWidgetData>
        {
            public string LastName { get; private set; }

            protected override void OnDataChanged(TestWidgetData data)
            {
                LastName = data.Name;
            }
        }
    }
}
