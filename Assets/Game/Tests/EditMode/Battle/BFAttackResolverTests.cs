using BF.Game.Runtime.Battle.Commands;
using NUnit.Framework;
using System.Reflection;

namespace BF.Game.Tests.EditMode.Battle
{
    /// <summary>
    /// 验证运行时攻击结算使用明确的成功/失败结果合同。
    /// </summary>
    public sealed class BFAttackResolverTests
    {
        [Test]
        public void ResolveResultExposesExplicitSuccessAndFailureContract()
        {
            var resultType = typeof(BFAttackResolveResult);

            var succeeded = resultType.GetProperty(
                "Succeeded",
                BindingFlags.Instance | BindingFlags.Public);
            var failureReason = resultType.GetProperty(
                "FailureReason",
                BindingFlags.Instance | BindingFlags.Public);

            Assert.That(succeeded, Is.Not.Null);
            Assert.That(succeeded.PropertyType, Is.EqualTo(typeof(bool)));
            Assert.That(failureReason, Is.Not.Null);
            Assert.That(failureReason.PropertyType, Is.EqualTo(typeof(string)));
        }
    }
}
